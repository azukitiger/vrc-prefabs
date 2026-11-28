#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace VAP.Editor {
    [CustomEditor(typeof(VAPAnimationCombiner))]
    public class VAPAnimationCombinerEditor : UnityEditor.Editor {
        static readonly GUIContent AnimationLabel = new GUIContent("Target Animation",
            "At build time, every animator controller on the avatar that uses this animation uses the new animation instead.");
        static readonly GUIContent ModeLabel = new GUIContent("Mode",
            "Combine adds the curves of the animation being combined; where both animate the same property, the animation being combined is used. Replace uses only the curves of the animation being combined. Either way the result keeps the animation's settings and length.");
        static readonly GUIContent CombineLabel = new GUIContent("With Source Animation",
            "The animation being combined. If it is longer than the animation, it is compressed to fit.");
        static readonly GUIContent StartLabel = new GUIContent("Source Animations Root",
            "The object the animations being combined were recorded from. At build time their paths are fixed to work from the avatar root. Leave empty if they were recorded from the avatar root.");
        const float ModeWidth = 70f;

        SerializedProperty entries;
        SerializedProperty startingPoint;
        ReorderableList list;
        readonly Dictionary<(AnimationClip, Transform, Transform), List<string>> missing = new Dictionary<(AnimationClip, Transform, Transform), List<string>>();

        void OnEnable() {
            entries = serializedObject.FindProperty(nameof(VAPAnimationCombiner.entries));
            startingPoint = serializedObject.FindProperty(nameof(VAPAnimationCombiner.startingPoint));
            EditorApplication.hierarchyChanged += ClearMissing;
            Undo.undoRedoPerformed += ClearMissing;
            AnimationUtility.onCurveWasModified += CurveModified;
        }

        void OnDisable() {
            EditorApplication.hierarchyChanged -= ClearMissing;
            Undo.undoRedoPerformed -= ClearMissing;
            AnimationUtility.onCurveWasModified -= CurveModified;
        }

        void ClearMissing() => missing.Clear();

        void CurveModified(AnimationClip clip, EditorCurveBinding binding, AnimationUtility.CurveModifiedType type) => ClearMissing();

        public override VisualElement CreateInspectorGUI() => VAPPrefabLock.AddEditButton(VAPInspectorHeader.Create("Animation Combiner", DrawFields, this), this);

        void DrawFields() {
            if (target == null) {
                return;
            }

            bool locked = targets.Any(VAPPrefabLock.IsFromPrefab);
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(locked)) {
                EditorGUILayout.PropertyField(startingPoint, StartLabel);
                list ??= MakeList();
                list.draggable = list.displayAdd = list.displayRemove = !locked;
                list.DoLayoutList();
            }
            serializedObject.ApplyModifiedProperties();
        }

        ReorderableList MakeList() {
            var l = new ReorderableList(serializedObject, entries, true, true, true, true);
            l.elementHeight = EditorGUIUtility.singleLineHeight + 4f;
            l.drawHeaderCallback = rect => {
                var (left, mode, right) = Split(new Rect(rect.x + 14f, rect.y, rect.width - 14f, rect.height));
                EditorGUI.LabelField(left, AnimationLabel);
                EditorGUI.LabelField(mode, ModeLabel);
                EditorGUI.LabelField(right, CombineLabel);
            };
            l.drawElementCallback = (rect, index, active, focused) => {
                var e = entries.GetArrayElementAtIndex(index);
                rect.y += 2f;
                rect.height = EditorGUIUtility.singleLineHeight;
                var (left, modeRect, right) = Split(rect);
                var animation = e.FindPropertyRelative(nameof(VAPAnimationCombiner.Entry.animation));
                var combine = e.FindPropertyRelative(nameof(VAPAnimationCombiner.Entry.combine));
                string animationProblem = ClipField(left, animation, AnimationCombinerBuild.AnimationProblem);
                string combineProblem;
                using (new EditorGUI.DisabledScope(animationProblem != null)) {
                    EditorGUI.PropertyField(modeRect, e.FindPropertyRelative(nameof(VAPAnimationCombiner.Entry.mode)), GUIContent.none);
                    combineProblem = ClipField(right, combine, AnimationCombinerBuild.CombineProblem);
                }
                var leftIcon = new Rect(left.x, left.y, left.width - 18f, left.height);
                var rightIcon = new Rect(right.x, right.y, right.width - 18f, right.height);
                var from = animation.objectReferenceValue as AnimationClip;
                var add = combine.objectReferenceValue as AnimationClip;
                if (animationProblem != null) {
                    VAPEditorGUI.ErrorIcon(leftIcon, animationProblem);
                    return;
                }
                if (combineProblem != null) {
                    VAPEditorGUI.ErrorIcon(rightIcon, combineProblem);
                    return;
                }
                var warnings = new List<string>();
                if (AnimationCombinerBuild.Compressed(from, add)) {
                    warnings.Add($"{add.name} is longer than {from.name} and will be compressed to {(from.length <= 0f ? "1 frame" : "fit")}.");
                }

                var paths = Missing(add);
                if (paths.Count > 0) {
                    warnings.Add($"{add.name} has paths that don't match anything:\n" + string.Join("\n", paths.Take(5)) +
                                 (paths.Count > 5 ? $"\nand {paths.Count - 5} more" : ""));
                }

                if (warnings.Count > 0) {
                    VAPEditorGUI.WarningIcon(rightIcon, string.Join("\n", warnings));
                }
            };
            l.onAddCallback = x => {
                entries.arraySize++;
                var e = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                e.FindPropertyRelative(nameof(VAPAnimationCombiner.Entry.animation)).objectReferenceValue = null;
                e.FindPropertyRelative(nameof(VAPAnimationCombiner.Entry.combine)).objectReferenceValue = null;
                e.FindPropertyRelative(nameof(VAPAnimationCombiner.Entry.mode)).enumValueIndex = (int)VAPAnimationCombiner.Mode.Combine;
                x.index = entries.arraySize - 1;
            };
            return l;
        }

        List<string> Missing(AnimationClip clip) {
            var combiner = (VAPAnimationCombiner)target;
            var descriptor = VAPAvatar.Descriptor(combiner);
            if (clip == null || descriptor == null) {
                return new List<string>();
            }

            var root = descriptor.transform;
            var start = combiner.startingPoint != null && combiner.startingPoint.IsChildOf(root) ? combiner.startingPoint : root;
            var key = (clip, start, root);
            if (!missing.TryGetValue(key, out var paths)) {
                missing[key] = paths = AnimationCombinerBuild.MissingPaths(clip, root, start);
            }

            return paths;
        }

        static string ClipField(Rect rect, SerializedProperty prop, System.Func<AnimationClip, string> problem) {
            EditorGUI.PropertyField(rect, prop, GUIContent.none);
            return problem(prop.objectReferenceValue as AnimationClip);
        }

        static (Rect, Rect, Rect) Split(Rect rect) {
            float half = (rect.width - ModeWidth - 8f) * 0.5f;
            return (new Rect(rect.x, rect.y, half, rect.height),
                new Rect(rect.x + half + 4f, rect.y, ModeWidth, rect.height),
                new Rect(rect.xMax - half, rect.y, half, rect.height));
        }
    }
}
#endif
