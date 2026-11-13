#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VAP.Editor
{
    [CustomEditor(typeof(VAPDescriptorExpressions))]
    public class VAPDescriptorExpressionsEditor : VAPExpressionsEditor
    {
        static readonly GUIContent FoundLabel = new GUIContent("Avatar Descriptor found",
            "The menu and parameters come from the avatar's descriptor. The files are never changed; values set below are applied to copies at build time.");

        void DrawDescriptor()
        {
            var c = target as VAPDescriptorExpressions;
            if (c == null)
                return;
            if (c.Descriptor == null)
            {
                EditorGUILayout.HelpBox("Avatar Descriptor not found. Put this on the avatar, under its VRC Avatar Descriptor.", MessageType.Error);
                return;
            }
            EditorGUILayout.LabelField(FoundLabel, EditorStyles.miniLabel);
            var other = c.Other();
            if (other != null)
            {
                EditorGUILayout.HelpBox(VAPEditorGUI.OnlyOne("VRC Avatar Descriptor Expressions", other), MessageType.Error);
                return;
            }
            if (c.RootMenus().Count == 0 && c.ParameterAssets().Count == 0)
            {
                EditorGUILayout.HelpBox("The descriptor has no Expressions Menu or Parameters.", MessageType.Info);
                return;
            }
            DrawContents(c);
        }

        public override VisualElement CreateInspectorGUI() => VAPInspectorHeader.Create("VRC Avatar Descriptor Expressions", DrawDescriptor);
    }
}
#endif
