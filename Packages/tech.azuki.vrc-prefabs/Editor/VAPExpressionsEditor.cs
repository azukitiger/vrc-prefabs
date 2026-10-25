#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VAP.Editor
{
    // Shared drawing for the editors of components built on VAPExpressions.
    public abstract class VAPExpressionsEditor : UnityEditor.Editor
    {
        static readonly GUIContent MenuHeader = new GUIContent("Menu");
        static readonly GUIContent MenuHint = new GUIContent("Right-click an item to remove, restore or revert it");
        static GUIStyle hintStyle;

        SubMenusSection subMenusSection;

        static string SyncedBits(VAPExpressions component, System.Collections.Generic.HashSet<string> dropped)
        {
            int cost = 0;
            var counted = new System.Collections.Generic.HashSet<string>();
            foreach (var asset in component.ParameterAssets())
                if (asset.parameters != null)
                    foreach (var p in asset.parameters)
                        if (p != null && p.networkSynced && !dropped.Contains(p.name) && counted.Add(p.name))
                            cost += p.valueType == VRCExpressionParameters.ValueType.Bool ? 1 : 8;
            return $"{cost} synced bits";
        }

        // Synced bits and the menu, for any component built on VAPExpressions.
        protected void DrawContents(VAPExpressions component)
        {
            subMenusSection ??= new SubMenusSection(component);
            if (component.ParameterAssets().Count > 0)
                EditorGUILayout.LabelField(SyncedBits(component, component.DroppedParameters()), EditorStyles.miniLabel);
            if (component.RootMenus().Count > 0)
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
