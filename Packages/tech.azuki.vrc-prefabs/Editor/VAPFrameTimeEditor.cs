#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace VAP.Editor
{
    [CustomEditor(typeof(VAPFrameTime))]
    public class VAPFrameTimeEditor : UnityEditor.Editor
    {
        static readonly GUIContent DefaultsLabel = new GUIContent("Defaults", "Values every avatar with frame time gets.");
        static readonly GUIContent FactorsLabel = new GUIContent("Custom Factors",
            "Smoothing gives a smoothing factor for the given speed. Linear gives the value multiplied by the frame time, like StepSizeOne.");
        const float KindWidth = 80f;
        const float ValueWidth = 50f;

        ReorderableList list;

        public override VisualElement CreateInspectorGUI() => VAPInspectorHeader.Create("Frame Time", DrawFields);

        void DrawFields()
        {
            if (target == null)
                return;
            serializedObject.Update();
            var f = (VAPFrameTime)target;
            if (VAPAvatar.Descriptor(f) == null)
                EditorGUILayout.HelpBox(VAPEditorGUI.NotInAvatar, MessageType.Warning);

            bool locked = VAPPrefabLock.IsFromPrefab(f);
            EditorGUILayout.LabelField(DefaultsLabel, EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
                foreach (var d in ContactSmoothing.Defaults())
                    Row(EditorGUILayout.GetControlRect(), d.parameter, d.kind, d.value);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(locked))
            {
                list ??= MakeList();
                list.DoLayoutList();
            }
            serializedObject.ApplyModifiedProperties();

            var seen = new Dictionary<string, VAPFrameTime.Factor>();
            foreach (var factor in f.factors)
            {
                string problem = ContactSmoothing.FactorProblem(factor);
                if (problem == null && seen.TryGetValue(factor.parameter, out var first) && !ContactSmoothing.Same(first, factor))
                    problem = $"\"{factor.parameter}\" is set twice with different values; the first one is used.";
                else if (problem == null)
                    seen.TryAdd(factor.parameter, factor);
                if (problem != null)
                    EditorGUILayout.HelpBox(problem, MessageType.Warning);
            }
        }

        ReorderableList MakeList()
        {
            var prop = serializedObject.FindProperty(nameof(VAPFrameTime.factors));
            var l = new ReorderableList(serializedObject, prop, true, true, true, true);
            l.drawHeaderCallback = rect => EditorGUI.LabelField(rect, FactorsLabel);
            l.elementHeight = EditorGUIUtility.singleLineHeight + 4f;
            l.drawElementCallback = (rect, index, active, focused) =>
            {
                var e = prop.GetArrayElementAtIndex(index);
                rect.y += 2f;
                rect.height = EditorGUIUtility.singleLineHeight;
                var (name, kind, value) = Split(rect);
                VAPEditorGUI.StringField(name, e.FindPropertyRelative(nameof(VAPFrameTime.Factor.parameter)));
                EditorGUI.PropertyField(kind, e.FindPropertyRelative(nameof(VAPFrameTime.Factor.kind)), GUIContent.none);
                EditorGUI.PropertyField(value, e.FindPropertyRelative(nameof(VAPFrameTime.Factor.value)), GUIContent.none);
            };
            l.onAddCallback = x =>
            {
                prop.arraySize++;
                var e = prop.GetArrayElementAtIndex(prop.arraySize - 1);
                e.FindPropertyRelative(nameof(VAPFrameTime.Factor.parameter)).stringValue = "";
                e.FindPropertyRelative(nameof(VAPFrameTime.Factor.kind)).enumValueIndex = (int)VAPFrameTime.Kind.Smoothing;
                e.FindPropertyRelative(nameof(VAPFrameTime.Factor.value)).floatValue = 8f;
                x.index = prop.arraySize - 1;
            };
            return l;
        }

        static void Row(Rect rect, string parameter, VAPFrameTime.Kind kind, float value)
        {
            rect.xMin += 20f;
            rect.xMax -= 6f;
            var (name, kindRect, valueRect) = Split(rect);
            EditorGUI.TextField(name, parameter);
            EditorGUI.EnumPopup(kindRect, kind);
            EditorGUI.TextField(valueRect, value.ToString(CultureInfo.InvariantCulture));
        }

        static (Rect, Rect, Rect) Split(Rect rect)
        {
            var value = new Rect(rect.xMax - ValueWidth, rect.y, ValueWidth, rect.height);
            var kind = new Rect(value.x - KindWidth - 4f, rect.y, KindWidth, rect.height);
            var name = new Rect(rect.x, rect.y, kind.x - rect.x - 4f, rect.height);
            return (name, kind, value);
        }
    }
}
#endif
