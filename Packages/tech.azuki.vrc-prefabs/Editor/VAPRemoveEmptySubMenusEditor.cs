#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VAP.Editor
{
    [CustomEditor(typeof(VAPRemoveEmptySubMenus))]
    public class VAPRemoveEmptySubMenusEditor : UnityEditor.Editor
    {
        static readonly GUIContent Status = new GUIContent("Empty sub menus are removed at build time",
            "Runs after VRCFury. Sub menu items whose menu is missing, has no items, or only leads to other empty menus (including menus that only link to each other) are removed. Menu files are never changed.");

        void Draw()
        {
            var c = target as VAPRemoveEmptySubMenus;
            if (c == null)
                return;
            if (VAPAvatar.Descriptor(c) == null)
            {
                EditorGUILayout.HelpBox(VAPEditorGUI.NotInAvatar, MessageType.Warning);
                return;
            }
            var other = VAPAvatar.Other(c);
            if (other != null)
            {
                EditorGUILayout.HelpBox(VAPEditorGUI.OnlyOne("Remove Empty Sub Menus", other), MessageType.Error);
                return;
            }
            EditorGUILayout.LabelField(Status, EditorStyles.miniLabel);
        }

        public override VisualElement CreateInspectorGUI() => VAPInspectorHeader.Create("Remove Empty Sub Menus", Draw);
    }
}
#endif
