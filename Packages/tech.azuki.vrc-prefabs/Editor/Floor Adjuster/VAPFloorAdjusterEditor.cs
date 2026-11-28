#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VAP.Editor {
    [CustomEditor(typeof(VAPFloorAdjuster))]
    [CanEditMultipleObjects]
    public class VAPFloorAdjusterEditor : UnityEditor.Editor {
        static readonly GUIContent HeightLabel = new GUIContent("Armature Height Offset (m)",
            "How far to move the armature, in meters. Positive moves the avatar up, negative moves it down. The view position moves by the same amount.");

        SerializedProperty height;

        void OnEnable() => height = serializedObject.FindProperty(nameof(VAPFloorAdjuster.height));

        public override VisualElement CreateInspectorGUI() => VAPInspectorHeader.Create("Rig Floor Adjuster", DrawFields, this);

        void DrawFields() {
            if (target == null) {
                return;
            }

            bool locked = VAPPrefabLock.Draw(targets);
            serializedObject.Update();

            var adjuster = (VAPFloorAdjuster)target;
            if (VAPAvatar.Descriptor(adjuster) != null && targets.Length == 1 && VAPAvatar.Other(adjuster, false) is VAPFloorAdjuster other) {
                EditorGUILayout.HelpBox(VAPEditorGUI.OnlyOne("Rig Floor Adjuster", other), MessageType.Error);
                return;
            }

            using (new EditorGUI.DisabledScope(locked)) {
                float old = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 180f;
                EditorGUILayout.PropertyField(height, HeightLabel);
                EditorGUIUtility.labelWidth = old;
            }
            serializedObject.ApplyModifiedProperties();

            if (!height.hasMultipleDifferentValues && Mathf.Abs(height.floatValue) >= 0.0005f) {
                float h = height.floatValue;
                EditorGUILayout.LabelField($"The armature moves {(h > 0 ? "up" : "down")} {Mathf.Abs(h) * 100f:0.0} cm at build time.",
                    EditorStyles.wordWrappedLabel);
            }
        }
    }
}
#endif
