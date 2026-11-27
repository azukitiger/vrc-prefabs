#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace VAP.Editor
{
    [CustomEditor(typeof(VAPAnimationCombiner))]
    public class VAPAnimationCombinerEditor : UnityEditor.Editor
    {
        static readonly GUIContent AnimationLabel = new GUIContent("Animation",
            "At build time, every use of this animation in the avatar descriptor's controllers and in VAP VRCFury Full Controllers uses the new animation instead.");
        static readonly GUIContent ModeLabel = new GUIContent("Mode",
            "Combine adds the curves of With to the animation; where both animate the same property, With is used. Replace uses only the curves of With. Either way the result keeps the animation's settings and length.");
        static readonly GUIContent CombineLabel = new GUIContent("With",
            "The animation whose curves are used. If it is longer than the animation, it is compressed to fit.");
        const float ModeWidth = 70f;

        SerializedProperty entries;
        ReorderableList list;

        void OnEnable() => entries = serializedObject.FindProperty(nameof(VAPAnimationCombiner.entries));

        public override VisualElement CreateInspectorGUI() => VAPPrefabLock.AddEditButton(VAPInspectorHeader.Create("Animation Combiner", DrawFields, this), this);

        void DrawFields()
        {
            if (target == null)
                return;
            bool locked = targets.Any(VAPPrefabLock.IsFromPrefab);
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(locked))
            {
                list ??= MakeList();
                list.draggable = list.displayAdd = list.displayRemove = !locked;
                list.DoLayoutList();
            }
            serializedObject.ApplyModifiedProperties();
        }

        ReorderableList MakeList()
        {
            var l = new ReorderableList(serializedObject, entries, true, true, true, true);
            l.elementHeight = EditorGUIUtility.singleLineHeight + 4f;
            l.drawHeaderCallback = rect =>
            {
                var (left, mode, right) = Split(new Rect(rect.x + 14f, rect.y, rect.width - 14f, rect.height));
                EditorGUI.LabelField(left, AnimationLabel);
                EditorGUI.LabelField(mode, ModeLabel);
                EditorGUI.LabelField(right, CombineLabel);
            };
            l.drawElementCallback = (rect, index, active, focused) =>
            {
                var e = entries.GetArrayElementAtIndex(index);
                rect.y += 2f;
                rect.height = EditorGUIUtility.singleLineHeight;
                var (left, modeRect, right) = Split(rect);
                var animation = e.FindPropertyRelative(nameof(VAPAnimationCombiner.Entry.animation));
                var combine = e.FindPropertyRelative(nameof(VAPAnimationCombiner.Entry.combine));
                string animationProblem = ClipField(left, animation, AnimationCombinerBuild.AnimationProblem);
                string combineProblem;
                using (new EditorGUI.DisabledScope(animationProblem != null))
                {
                    EditorGUI.PropertyField(modeRect, e.FindPropertyRelative(nameof(VAPAnimationCombiner.Entry.mode)), GUIContent.none);
                    combineProblem = ClipField(right, combine, AnimationCombinerBuild.CombineProblem);
                }
                var leftIcon = new Rect(left.x, left.y, left.width - 18f, left.height);
                var rightIcon = new Rect(right.x, right.y, right.width - 18f, right.height);
                var from = animation.objectReferenceValue as AnimationClip;
                var add = combine.objectReferenceValue as AnimationClip;
                if (animationProblem != null)
                {
                    VAPEditorGUI.ErrorIcon(leftIcon, animationProblem);
                    return;
                }
                if (combineProblem != null)
                    VAPEditorGUI.ErrorIcon(rightIcon, combineProblem);
                else if (AnimationCombinerBuild.Compressed(from, add))
                    VAPEditorGUI.WarningIcon(rightIcon, $"{add.name} is longer than {from.name} and will be compressed to {(from.length <= 0f ? "1 frame" : "fit")}.");
            };
            l.onAddCallback = x =>
            {
                entries.arraySize++;
                var e = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                e.FindPropertyRelative(nameof(VAPAnimationCombiner.Entry.animation)).objectReferenceValue = null;
                e.FindPropertyRelative(nameof(VAPAnimationCombiner.Entry.combine)).objectReferenceValue = null;
                e.FindPropertyRelative(nameof(VAPAnimationCombiner.Entry.mode)).enumValueIndex = (int)VAPAnimationCombiner.Mode.Combine;
                x.index = entries.arraySize - 1;
            };
            return l;
        }

        static string ClipField(Rect rect, SerializedProperty prop, System.Func<AnimationClip, string> problem)
        {
            EditorGUI.PropertyField(rect, prop, GUIContent.none);
            return problem(prop.objectReferenceValue as AnimationClip);
        }

        static (Rect, Rect, Rect) Split(Rect rect)
        {
            float half = (rect.width - ModeWidth - 8f) * 0.5f;
            return (new Rect(rect.x, rect.y, half, rect.height),
                new Rect(rect.x + half + 4f, rect.y, ModeWidth, rect.height),
                new Rect(rect.xMax - half, rect.y, half, rect.height));
        }
    }
}
#endif
