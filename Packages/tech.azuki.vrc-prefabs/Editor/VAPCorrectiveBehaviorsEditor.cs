#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;

namespace VAP.Editor
{
    [CustomEditor(typeof(VAPCorrectiveBehaviors))]
    public class VAPCorrectiveBehaviorsEditor : UnityEditor.Editor
    {
        static readonly GUIContent SetLabel = new GUIContent("Blendshapes Set",
            "At build time, adds an Apply to Blendshape behavior for every blendshape named [A][B]Corrective or [A][B]Morph, where " +
            "A and B are blendshapes this set uses (e.g. MouthClosedLipPuckerCorrective). A Morph may leave out the start B shares " +
            "with A (TongueDown + TongueLeft = TongueDownLeftMorph). Its expression is KeyA * KeyB; when keys " +
            "share a blendshape, it is max() of every KeyA * KeyB pair. This set's weight scales them. Blendshapes another " +
            "Apply to Blendshape behavior already drives are skipped.");
        static readonly GUIContent PickContent = new GUIContent("", "Pick a Blendshapes Set from this avatar.");
        static readonly GUIContent ExpressionLabel = new GUIContent("Expression", "Made from the tracking keys of the two blendshapes.");
        static readonly GUIContent[] PreviewMode = { new GUIContent("Apply to Blendshape") };
        static readonly GUIContent[] PreviewCombine = { new GUIContent("Custom") };

        static float Line => EditorGUIUtility.singleLineHeight;
        static float HeaderHeight => Line + EditorStyles.helpBox.padding.vertical;
        const float LabelWidth = 90f;
        static GUIStyle wrapStyle, listStyle;
        static GUIStyle WrapStyle => wrapStyle ??= new GUIStyle(EditorStyles.textArea) { wordWrap = true };
        static GUIStyle ListStyle => listStyle ??= new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new IMGUIContainer(DrawFields));
            VAPInspectorHeader.Attach(root, "Face Tracking Auto Corrective Behaviors");
            return root;
        }

        void DrawFields()
        {
            if (target == null)
                return;
            bool locked = VAPPrefabLock.Draw(targets);
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(locked))
            {
                DrawSetField(serializedObject.FindProperty(nameof(VAPCorrectiveBehaviors.blendshapesSet)));
                EditorGUILayout.Space(4f);
                DrawPreview();
            }
            serializedObject.ApplyModifiedProperties();
        }

        void DrawSetField(SerializedProperty prop)
        {
            const float pickWidth = 20f;
            var rect = EditorGUILayout.GetControlRect();
            var field = new Rect(rect.x, rect.y, rect.width - pickWidth - 2f, rect.height);
            EditorGUI.BeginChangeCheck();
            string value = EditorGUI.TextField(field, SetLabel, prop.stringValue);
            if (EditorGUI.EndChangeCheck())
                prop.stringValue = value;
            var pick = new Rect(rect.xMax - pickWidth, rect.y, pickWidth, rect.height);
            if (!EditorGUI.DropdownButton(pick, PickContent, FocusType.Passive))
                return;
            var menu = new GenericMenu();
            var names = Sets().Keys.OrderBy(n => n).ToList();
            if (names.Count == 0)
                menu.AddDisabledItem(new GUIContent("No Blendshapes Sets on this avatar"));
            var so = prop.serializedObject;
            string path = prop.propertyPath;
            foreach (var n in names)
            {
                string name = n;
                menu.AddItem(new GUIContent(name.Replace("/", "\u2215")), prop.stringValue == name, () =>
                {
                    so.Update();
                    so.FindProperty(path).stringValue = name;
                    so.ApplyModifiedProperties();
                });
            }
            menu.DropDown(pick);
        }

        (string note, List<(string shape, string expression)> made, List<string> skipped) cache;
        double cacheTime = -10;

        (string note, List<(string shape, string expression)> made, List<string> skipped) Preview()
        {
            if (EditorApplication.timeSinceStartup - cacheTime < 0.5)
                return cache;
            cacheTime = EditorApplication.timeSinceStartup;
            var descriptor = Descriptor;
            if (descriptor == null)
                return cache = ("Put this inside an avatar (under its VRC Avatar Descriptor).", null, null);
            string set = ((VAPCorrectiveBehaviors)target).blendshapesSet;
            if (string.IsNullOrEmpty(set))
                return cache = ("Pick the Blendshapes Set whose blendshapes the correctives combine.", null, null);
            if (!Sets().TryGetValue(set, out var names))
                return cache = ($"No Blendshapes Set named \"{set}\" on this avatar.", null, null);
            if (!(ParameterBehaviorsBuild.FaceTrackingBlendshapes(descriptor) is HashSet<string> shapes))
                return cache = ("The avatar descriptor has no Face Tracking Meshes.", null, null);
            var found = ParameterBehaviorsBuild.FindCorrectives(names, shapes);
            if (found.Count == 0)
                return cache = ($"No correctives found for \"{set}\" (blendshapes named like [A][B]Corrective or [A][B]Morph).", null, null);
            var applied = ParameterBehaviorsBuild.AppliedOnAvatar(descriptor);
            return cache = (null,
                found.Where(f => !applied.Contains(f.shape)).ToList(),
                found.Select(f => f.shape).Where(applied.Contains).Distinct().ToList());
        }

        void DrawPreview()
        {
            var (note, made, skipped) = Preview();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (note != null)
                {
                    EditorGUILayout.LabelField(note, ListStyle);
                    return;
                }
                int on = made.Count(m => !Disabled(m.shape));
                EditorGUILayout.LabelField($"Creates {on} behavior{(on == 1 ? "" : "s")} at build:", EditorStyles.miniBoldLabel);
                foreach (var (shape, expression) in made)
                    DrawBehavior(shape, expression);
                if (skipped.Count > 0)
                    EditorGUILayout.LabelField(new GUIContent(
                        $"Already driven by another Apply to Blendshape behavior, not created: {string.Join(", ", skipped)}",
                        string.Join("\n", skipped)), ListStyle);
            }
        }

        void DrawBehavior(string shape, string expression)
        {
            float width = EditorGUIUtility.currentViewWidth - LabelWidth - 60f;
            float exprH = Mathf.Max(Line, WrapStyle.CalcHeight(new GUIContent(expression), width));
            var rect = EditorGUILayout.GetControlRect(false, HeaderHeight + exprH + 6f);
            var header = new Rect(rect.x, rect.y, rect.width, HeaderHeight);
            if (Event.current.type == EventType.Repaint)
                EditorStyles.helpBox.Draw(header, false, false, false, false);
            var row = EditorStyles.helpBox.padding.Remove(header);
            row.height = Line;

            // Unchecked correctives are not created (their names are kept in disabledCorrectives).
            bool enabled = !Disabled(shape);
            EditorGUI.BeginChangeCheck();
            bool next = EditorGUI.Toggle(new Rect(row.x, row.y, 16f, Line),
                new GUIContent("", enabled ? "Uncheck to not create this corrective." : "Check to create this corrective."), enabled);
            if (EditorGUI.EndChangeCheck())
                SetDisabled(shape, !next);

            const float modeWidth = 150f, combineWidth = 70f;
            float modeX = row.x + 20f;
            float combineX = row.xMax - combineWidth;
            using var dim = new EditorGUI.DisabledScope(!next);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.Popup(new Rect(modeX, row.y, modeWidth, Line), 0, PreviewMode);
                EditorGUI.Popup(new Rect(combineX, row.y, combineWidth, Line), 0, PreviewCombine);
            }
            float nameX = modeX + modeWidth + 4f;
            EditorGUI.SelectableLabel(new Rect(nameX, row.y, combineX - 4f - nameX, Line), shape, EditorStyles.textField);

            float y = rect.y + HeaderHeight + 3f;
            EditorGUI.LabelField(new Rect(rect.x + 15f, y, LabelWidth - 15f, Line), ExpressionLabel);
            EditorGUI.SelectableLabel(new Rect(rect.x + LabelWidth, y, rect.width - LabelWidth, exprH), expression, WrapStyle);
        }

        bool Disabled(string shape)
        {
            var list = serializedObject.FindProperty(nameof(VAPCorrectiveBehaviors.disabledCorrectives));
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).stringValue == shape)
                    return true;
            return false;
        }

        void SetDisabled(string shape, bool disabled)
        {
            var list = serializedObject.FindProperty(nameof(VAPCorrectiveBehaviors.disabledCorrectives));
            for (int i = list.arraySize - 1; i >= 0; i--)
                if (list.GetArrayElementAtIndex(i).stringValue == shape)
                    list.DeleteArrayElementAtIndex(i);
            if (disabled)
            {
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = shape;
            }
        }

        VRCAvatarDescriptor Descriptor => ((Component)target).GetComponentInParent<VRCAvatarDescriptor>(true);

        Dictionary<string, string[]> sets;
        double setsTime = -10;

        Dictionary<string, string[]> Sets()
        {
            if (sets != null && EditorApplication.timeSinceStartup - setsTime < 1.0)
                return sets;
            setsTime = EditorApplication.timeSinceStartup;
            var descriptor = Descriptor;
            if (descriptor == null)
                return sets = new Dictionary<string, string[]>();
            sets = ParameterBehaviorsBuild.ReadSets(descriptor);
            foreach (var c in descriptor.GetComponentsInChildren<VAPFaceTrackingDebug>(true))
                using (var so = new SerializedObject(c))
                {
                    var set = so.FindProperty("blendshapesSet");
                    if (set != null)
                        ParameterBehaviorsBuild.AddSet(sets, set);
                }
            return sets;
        }
    }
}
#endif
