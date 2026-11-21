#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace VAP.Editor
{
    internal class CorrectivesSection
    {
        static readonly GUIContent HeaderLabel = new GUIContent("Corrective Behaviors",
            "At build time, adds an Apply to Blendshape behavior for every blendshape named [A][B]Corrective or [A][B]Morph, where " +
            "A and B are blendshapes the Blendshapes Set uses (e.g. MouthClosedLipPuckerCorrective). A Morph may leave out the start B shares " +
            "with A (TongueDown + TongueLeft = TongueDownLeftMorph). Its expression is KeyA * KeyB; when keys " +
            "share a blendshape, it is max() of every KeyA * KeyB pair. A key whose blendshape has a Limit Blendshape behavior " +
            "on this component is limited the same way. The set's weight scales them. Blendshapes another " +
            "Apply to Blendshape behavior already drives are skipped.");
        static readonly GUIContent SetLabel = new GUIContent("Blendshapes Set", "The Blendshapes Set whose blendshapes the correctives combine.");
        static readonly GUIContent ExpressionLabel = new GUIContent("Expression",
            "Made from the tracking keys of the two blendshapes, with this component's Limit Blendshape behaviors applied.");
        static readonly GUIContent[] PreviewMode = { new GUIContent("Apply to Blendshape") };
        static readonly GUIContent[] PreviewCombine = { new GUIContent("Custom") };

        static float Line => VAPEditorGUI.Line;
        static float HeaderHeight => VAPEditorGUI.BoxHeaderHeight;
        const float LabelWidth = 90f;
        static GUIStyle wrapStyle, listStyle;
        static GUIStyle WrapStyle => wrapStyle ??= new GUIStyle(EditorStyles.textArea) { wordWrap = true };
        static GUIStyle ListStyle => listStyle ??= new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };

        readonly SerializedObject serializedObject;
        readonly Func<VRCAvatarDescriptor> descriptor;
        readonly Cached<Dictionary<string, string[]>> sets;
        readonly Cached<(string note, List<(string shape, string expression)> made, List<string> skipped)> preview;

        internal CorrectivesSection(SerializedObject serializedObject, Func<VRCAvatarDescriptor> descriptor, Cached<Dictionary<string, string[]>> sets)
        {
            this.serializedObject = serializedObject;
            this.descriptor = descriptor;
            this.sets = sets;
            preview = new Cached<(string, List<(string, string)>, List<string>)>(0.5, MakePreview);
        }

        internal int Count => preview.Value.made is List<(string shape, string expression)> made
            ? made.Count(m => !Disabled(m.shape)) : 0;

        internal void Draw(bool locked)
        {
            if (serializedObject.FindProperty(nameof(VAPParameterBehaviors.perBehaviorSet)).boolValue)
            {
                using (new EditorGUI.DisabledScope(locked))
                    VAPEditorGUI.SetFieldWithPicker(serializedObject.FindProperty(nameof(VAPParameterBehaviors.correctivesSet)),
                        SetLabel, () => sets.Value.Keys);
                EditorGUILayout.Space(2f);
            }
            DrawPreview();
        }

        (string note, List<(string shape, string expression)> made, List<string> skipped) MakePreview()
        {
            var d = descriptor();
            if (d == null)
                return (VAPEditorGUI.NotInAvatar, null, null);
            var component = (VAPParameterBehaviors)serializedObject.targetObject;
            string set = component.CorrectivesSet;
            if (string.IsNullOrEmpty(set))
                return ("Pick the Blendshapes Set whose blendshapes the correctives combine.", null, null);
            if (!sets.Value.TryGetValue(set, out var names))
                return ($"No Blendshapes Set named \"{set}\" on this avatar.", null, null);
            if (!(ParameterBehaviorsBuild.FaceTrackingBlendshapes(d) is HashSet<string> shapes))
                return ("The avatar descriptor has no Face Tracking Meshes.", null, null);
            var limits = ParameterBehaviorsBuild.Limits(ParameterBehaviorsBuild.Behaviors(component, sets.Value, false));
            var found = ParameterBehaviorsBuild.FindCorrectives(names, shapes, limits);
            if (found.Count == 0)
                return ($"No correctives found for \"{set}\" (blendshapes named like [A][B]Corrective or [A][B]Morph).", null, null);
            var applied = ParameterBehaviorsBuild.AppliedOnAvatar(d);
            return (null,
                found.Where(f => !applied.Contains(f.shape)).ToList(),
                found.Select(f => f.shape).Where(applied.Contains).Distinct().ToList());
        }

        void DrawPreview()
        {
            var (note, made, skipped) = preview.Value;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (note != null)
                {
                    EditorGUILayout.LabelField(note, ListStyle);
                    return;
                }
                int on = made.Count(m => !Disabled(m.shape));
                EditorGUILayout.LabelField(new GUIContent($"Creates {on} behavior{(on == 1 ? "" : "s")} at build:", HeaderLabel.tooltip),
                    EditorStyles.miniBoldLabel);
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

        SerializedProperty DisabledList => serializedObject.FindProperty(nameof(VAPParameterBehaviors.disabledCorrectives));

        bool Disabled(string shape)
        {
            var list = DisabledList;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).stringValue == shape)
                    return true;
            return false;
        }

        void SetDisabled(string shape, bool disabled)
        {
            var list = DisabledList;
            for (int i = list.arraySize - 1; i >= 0; i--)
                if (list.GetArrayElementAtIndex(i).stringValue == shape)
                    list.DeleteArrayElementAtIndex(i);
            if (disabled)
            {
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = shape;
            }
        }
    }
}
#endif
