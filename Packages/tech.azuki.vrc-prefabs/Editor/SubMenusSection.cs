#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VAP.Editor
{
    // The sub menus the menu links to, each drawn once.
    internal class SubMenusSection
    {
        static readonly GUIContent Header = new GUIContent("Sub Menus", "Every menu reachable from the menu above through Sub Menu controls.");

        readonly SerializedObject serializedObject;
        readonly VAPExpressions owner;
        readonly Dictionary<VRCExpressionsMenu, MenuSection> sections = new Dictionary<VRCExpressionsMenu, MenuSection>();

        internal SubMenusSection(SerializedObject so, VAPExpressions owner)
        {
            serializedObject = so;
            this.owner = owner;
        }

        internal void Draw(bool locked)
        {
            var menus = owner.SubMenus();
            if (menus.Count == 0)
                return;
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(Header, EditorStyles.boldLabel);
            foreach (var menu in menus)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                string key = "VAP.SubMenu." + menu.GetInstanceID();
                bool open = SessionState.GetBool(key, false);
                var header = EditorGUILayout.GetControlRect();
                if (locked && owner.controlOverrides.Any(o => o != null && o.menu == menu && !o.IsEmpty))
                    ExpressionsOverrides.DrawBar(header);
                bool now = EditorGUI.Foldout(new Rect(header.x + 12f, header.y, header.width - 12f, header.height), open, menu.name, true);
                if (now != open)
                    SessionState.SetBool(key, now);
                if (now)
                {
                    if (!sections.TryGetValue(menu, out var section))
                        sections[menu] = section = new MenuSection(serializedObject, owner, menu);
                    section.Draw(locked);
                }
                EditorGUILayout.EndVertical();
            }
        }
    }
}
#endif
