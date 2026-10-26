#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;

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
            if (c.GetComponentInParent<VRCAvatarDescriptor>(true) == null)
            {
                EditorGUILayout.HelpBox("Put this inside an avatar (under its VRC Avatar Descriptor).", MessageType.Warning);
                return;
            }
            var other = RemoveEmptySubMenusBuild.Other(c);
            if (other != null)
            {
                EditorGUILayout.HelpBox($"Only one Remove Empty Sub Menus is allowed per avatar. \"{other.name}\" has one too.", MessageType.Error);
                return;
            }
            EditorGUILayout.LabelField(Status, EditorStyles.miniLabel);
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new IMGUIContainer(Draw));
            VAPInspectorHeader.Attach(root, "Remove Empty Sub Menus");
            return root;
        }
    }
}
#endif
