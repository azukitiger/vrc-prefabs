#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VAP.Editor
{
    public abstract class VAPExpressionsEditor : UnityEditor.Editor
    {
        static readonly GUIContent MenuHeader = new GUIContent("Menu");
        static readonly GUIContent MenuHint = new GUIContent("Right-click an item to remove, restore or revert it");

        SubMenusSection subMenusSection;

        static string SyncedBits(VAPExpressions component, System.Collections.Generic.HashSet<string> dropped)
        {
            int cost = 0;
            var counted = new System.Collections.Generic.HashSet<string>();
            foreach (var asset in component.ParameterAssets())
                if (asset.parameters != null)
                    foreach (var p in asset.parameters)
                        if (p != null && p.networkSynced && !dropped.Contains(p.name) && counted.Add(p.name))
                            cost += VAPExpressions.SyncedBits(p);
            return $"{cost} synced bits";
        }

        protected void DrawContents(VAPExpressions component)
        {
            subMenusSection ??= new SubMenusSection(component);
            string bits = component.ParameterAssets().Count > 0 ? SyncedBits(component, component.DroppedParameters()) : null;
            if (component.RootMenus().Count > 0)
            {
                EditorGUILayout.Space(6f);
                VAPEditorGUI.BeginMenuBox();
                var header = EditorGUILayout.GetControlRect();
                SubMenusSection.DrawHeader(header, subMenusSection.SingleSteps(), MenuHeader, bits);
                subMenusSection.Draw();
                EditorGUILayout.LabelField(MenuHint, VAPEditorGUI.RightMini);
                VAPEditorGUI.EndMenuBox();
            }
            else if (bits != null)
                EditorGUILayout.LabelField(bits, EditorStyles.miniLabel);
        }
    }
}
#endif
