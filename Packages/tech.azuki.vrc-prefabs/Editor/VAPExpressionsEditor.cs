#if UNITY_EDITOR && VRC_SDK_VRCSDK3
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
            "The asset is never changed. Names and icons set below are applied to a copy at build time, and references to the asset use that copy.");
        static readonly GUIContent ParametersAssetLabel = new GUIContent("Parameters Asset",
            "The asset is never changed. Start values set in the menu below are applied to a copy at build time, and references to the asset use that copy.");
        static readonly GUIContent MenuHeader = new GUIContent("Menu");
        static readonly GUIContent MenuHint = new GUIContent("Right-click an item to remove, restore or revert it");
        static GUIStyle hintStyle;

        SubMenusSection subMenusSection;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new IMGUIContainer(DrawFields));
            VAPInspectorHeader.Attach(root, "VRC Menu & Parameters");
            return root;
        }

        static string SyncedBits(VRCExpressionParameters asset, System.Collections.Generic.HashSet<string> dropped)
        {
            int cost = 0;
            if (asset.parameters != null)
                foreach (var p in asset.parameters)
                    if (p != null && p.networkSynced && !dropped.Contains(p.name))
                        cost += p.valueType == VRCExpressionParameters.ValueType.Bool ? 1 : 8;
            return $"{cost} synced bits";
        }

        void DrawFields()
        {
            if (target == null)
                return;
            serializedObject.Update();
            var component = (VAPExpressions)target;
            subMenusSection ??= new SubMenusSection(component);
            bool locked = PrefabUtility.IsPartOfPrefabInstance(component);

            var menuProp = serializedObject.FindProperty(nameof(VAPExpressions.menuAsset));
            var paramsProp = serializedObject.FindProperty(nameof(VAPExpressions.parametersAsset));
            ExpressionsAssets.LinkCreated(component, menuProp, paramsProp);
            using (new EditorGUI.DisabledScope(locked))
            {
                EditorGUILayout.PropertyField(menuProp, MenuAssetLabel);
                EditorGUILayout.PropertyField(paramsProp, ParametersAssetLabel);
            }
            serializedObject.ApplyModifiedProperties();

            if (component.parametersAsset != null)
            {
                var dropped = component.DroppedParameters();
                EditorGUILayout.LabelField(SyncedBits(component.parametersAsset, dropped), EditorStyles.miniLabel);
            }
            if (component.menuAsset != null)
            {
                EditorGUILayout.Space(6f);
                var header = EditorGUILayout.GetControlRect();
                EditorGUI.LabelField(header, MenuHeader, EditorStyles.boldLabel);
                hintStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
                EditorGUI.LabelField(header, MenuHint, hintStyle);
                subMenusSection.Draw();
            }
        }
    }
}
#endif
