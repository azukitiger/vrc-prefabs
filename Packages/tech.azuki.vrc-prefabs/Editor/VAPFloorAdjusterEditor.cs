#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VAP.Editor
{
    [CustomEditor(typeof(VAPFloorAdjuster))]
    [CanEditMultipleObjects]
    public class VAPFloorAdjusterEditor : UnityEditor.Editor
    {
        static readonly GUIContent HeightLabel = new GUIContent("Armature Height Offset (m)",
            "How far to move the armature, in meters. Positive moves the avatar up, negative moves it down. The view position moves by the same amount.");

        SerializedProperty height;

        void OnEnable() => height = serializedObject.FindProperty(nameof(VAPFloorAdjuster.height));

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new IMGUIContainer(DrawFields));
            VAPInspectorHeader.Attach(root, "Floor Adjuster");
            return root;
        }

        void DrawFields()
        {
            if (target == null)
                return;
            bool locked = VAPPrefabLock.Draw(targets);
            serializedObject.Update();

            var adjuster = (VAPFloorAdjuster)target;
            if (FloorAdjusterBuild.FindAvatarRoot(adjuster.transform) == null)
                EditorGUILayout.HelpBox("Put this inside an avatar (under its VRC Avatar Descriptor).", MessageType.Warning);

            using (new EditorGUI.DisabledScope(locked))
            {
                float old = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 180f;
                EditorGUILayout.PropertyField(height, HeightLabel);
                EditorGUIUtility.labelWidth = old;
            }
            serializedObject.ApplyModifiedProperties();

            if (!height.hasMultipleDifferentValues)
            {
                float h = height.floatValue;
                string status = Mathf.Abs(h) < 0.0005f ? "No change."
                    : $"The armature moves {(h > 0 ? "up" : "down")} {Mathf.Abs(h) * 100f:0.0} cm at build time.";
                EditorGUILayout.LabelField(status, EditorStyles.wordWrappedLabel);
            }
        }
    }
}
#endif
