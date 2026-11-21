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
                return;
            var other = VAPAvatar.Other(c);
            if (other != null)
            {
                EditorGUILayout.HelpBox(VAPEditorGUI.OnlyOne("VRC Remove Empty SubMenus", other), MessageType.Error);
                return;
            }
            EditorGUILayout.LabelField(Status, EditorStyles.miniLabel);
        }

        public override VisualElement CreateInspectorGUI() => VAPInspectorHeader.Create("VRC Remove Empty SubMenus", Draw, this);
    }
}
#endif
