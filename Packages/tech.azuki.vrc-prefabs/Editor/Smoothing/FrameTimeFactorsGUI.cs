#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace VAP.Editor {
    internal class FrameTimeFactorsGUI {
        static readonly GUIContent DefaultsLabel = new GUIContent("Defaults", "Values every avatar with frame time gets.");
        static readonly GUIContent FactorsLabel = new GUIContent("Custom Factors",
            "Smoothing gives a smoothing factor for the given speed. Linear gives the value multiplied by the frame time, like StepSizeOne. " +
            "Use the parameter name your animators use. At build time it is renamed by its value (like VAP/Smooth/Speed2.5 or VAP/Smooth/Linear2) " +
            "in the animators here and made global, so equal values share one parameter and different values never clash.");
        const float KindWidth = 80f;
        const float ValueWidth = 50f;

        readonly SerializedObject serializedObject;
        readonly string property;
        ReorderableList list;

        internal FrameTimeFactorsGUI(SerializedObject serializedObject, string property) {
            this.serializedObject = serializedObject;
            this.property = property;
        }

        internal void Draw(List<VAPFrameTime.Factor> factors, bool locked) {
            EditorGUILayout.LabelField(DefaultsLabel, EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true)) {
                foreach (var d in ContactSmoothingBuild.Defaults()) {
                    Row(EditorGUILayout.GetControlRect(), d.parameter, d.kind, d.value);
                }
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(locked)) {
                list ??= MakeList();
                list.draggable = list.displayAdd = list.displayRemove = !locked;
                list.DoLayoutList();
            }

            var seen = new HashSet<string>();
            foreach (var factor in factors) {
                string problem = ContactSmoothingBuild.FactorProblem(factor);
                if (problem == null && !seen.Add(factor.parameter)) {
                    problem = $"\"{factor.parameter}\" is already in the list; only the first one is used.";
                }

                if (problem != null) {
                    EditorGUILayout.HelpBox(problem, MessageType.Warning);
                }
            }
        }

        ReorderableList MakeList() {
            var prop = serializedObject.FindProperty(property);
            var l = new ReorderableList(serializedObject, prop, true, true, true, true);
            l.drawHeaderCallback = rect => EditorGUI.LabelField(rect, FactorsLabel);
            l.elementHeight = EditorGUIUtility.singleLineHeight + 4f;
            l.drawElementCallback = (rect, index, active, focused) => {
                var e = prop.GetArrayElementAtIndex(index);
                rect.y += 2f;
                rect.height = EditorGUIUtility.singleLineHeight;
                var (name, kind, value) = Split(rect);
                var parameter = e.FindPropertyRelative(nameof(VAPFrameTime.Factor.parameter));
                EditorGUI.PropertyField(name, parameter, GUIContent.none);
                EditorGUI.PropertyField(kind, e.FindPropertyRelative(nameof(VAPFrameTime.Factor.kind)), GUIContent.none);
                EditorGUI.PropertyField(value, e.FindPropertyRelative(nameof(VAPFrameTime.Factor.value)), GUIContent.none);
            };
            l.onAddCallback = x => {
                prop.arraySize++;
                var e = prop.GetArrayElementAtIndex(prop.arraySize - 1);
                e.FindPropertyRelative(nameof(VAPFrameTime.Factor.parameter)).stringValue = "";
                e.FindPropertyRelative(nameof(VAPFrameTime.Factor.kind)).enumValueIndex = (int)VAPFrameTime.Kind.Smoothing;
                e.FindPropertyRelative(nameof(VAPFrameTime.Factor.value)).floatValue = 8f;
                x.index = prop.arraySize - 1;
            };
            return l;
        }

        static void Row(Rect rect, string parameter, VAPFrameTime.Kind kind, float value) {
            rect.xMin += 20f;
            rect.xMax -= 6f;
            var (name, kindRect, valueRect) = Split(rect);
            EditorGUI.TextField(name, parameter);
            EditorGUI.EnumPopup(kindRect, kind);
            EditorGUI.TextField(valueRect, value.ToString(CultureInfo.InvariantCulture));
        }

        static (Rect, Rect, Rect) Split(Rect rect) {
            var value = new Rect(rect.xMax - ValueWidth, rect.y, ValueWidth, rect.height);
            var kind = new Rect(value.x - KindWidth - 4f, rect.y, KindWidth, rect.height);
            var name = new Rect(rect.x, rect.y, kind.x - rect.x - 4f, rect.height);
            return (name, kind, value);
        }
    }
}
#endif
