#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VAP.Editor
{
    [CustomEditor(typeof(VAPExpressions))]
    public class VAPExpressionsEditor : UnityEditor.Editor
    {
        static readonly GUIContent MenuAssetLabel = new GUIContent("Menu Asset",
            "The menu below is this asset; editing it edits the asset. Without one, the menu is kept on the component. " +
            "At build time, references to it use a copy with this instance's names and icons.");
        static readonly GUIContent ParametersAssetLabel = new GUIContent("Parameters Asset",
            "The parameters below are this asset; editing them edits the asset. Without one, they are kept on the component. " +
            "At build time, references to it use a copy with this instance's start values.");
        static readonly GUIContent ParametersHeader = new GUIContent("Parameters");
        static readonly GUIContent MenuHeader = new GUIContent("Menu");

        ParametersSection parametersSection;
        MenuSection menuSection;
        SubMenusSection subMenusSection;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new IMGUIContainer(DrawFields));
            VAPInspectorHeader.Attach(root, "VRC Menu & Parameters");
            return root;
        }

        void DrawFields()
        {
            if (target == null)
                return;
            VAPPrefabLock.Draw(targets);
            serializedObject.Update();
            var component = (VAPExpressions)target;
            parametersSection ??= new ParametersSection(serializedObject, component);
            menuSection ??= new MenuSection(serializedObject, component);
            subMenusSection ??= new SubMenusSection(serializedObject, component);
            bool locked = PrefabUtility.IsPartOfPrefabInstance(component);

            var menuProp = serializedObject.FindProperty(nameof(VAPExpressions.menuAsset));
            var paramsProp = serializedObject.FindProperty(nameof(VAPExpressions.parametersAsset));
            VAPPrefabAsset.AutoLink<VRCExpressionsMenu>(menuProp, component, locked, "Menu");
            VAPPrefabAsset.AutoLink<VRCExpressionParameters>(paramsProp, component, locked, "Parameters");
            VAPPrefabAsset.Field(menuProp, MenuAssetLabel, locked);
            VAPPrefabAsset.Field(paramsProp, ParametersAssetLabel, locked);
            VAPPrefabAsset.CreateButton(component, locked,
                (menuProp, "Menu", () =>
                {
                    var a = CreateInstance<VRCExpressionsMenu>();
                    a.controls = component.controls.Select(VAPExpressions.Copy).ToList();
                    Undo.RecordObject(component, "Create Menu");
                    component.controls.Clear();
                    return a;
                }),
                (paramsProp, "Parameters", () =>
                {
                    var a = CreateInstance<VRCExpressionParameters>();
                    a.parameters = component.parameters.Where(p => p != null).Select(ParametersSection.Copy).ToArray();
                    Undo.RecordObject(component, "Create Parameters");
                    component.parameters.Clear();
                    return a;
                }));
            if (!locked)
                LinkParameters(component);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(ParametersHeader, EditorStyles.boldLabel);
            parametersSection.Draw(locked);
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(MenuHeader, EditorStyles.boldLabel);
            menuSection.Draw(locked);
            subMenusSection.Draw(locked);
            serializedObject.ApplyModifiedProperties();
        }

        // The menu asset uses this component's parameters asset.
        static void LinkParameters(VAPExpressions component)
        {
            var menu = component.menuAsset;
            if (component.parametersAsset == null || menu == null || menu.Parameters == component.parametersAsset)
                return;
            Undo.RecordObject(menu, "Link Parameters");
            menu.Parameters = component.parametersAsset;
            EditorUtility.SetDirty(menu);
        }
    }
}
#endif
