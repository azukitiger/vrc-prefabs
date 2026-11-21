#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
#if VAP_NDMF
using nadena.dev.ndmf;
#endif
using Object = UnityEngine.Object;

#if VAP_NDMF
[assembly: ExportsPlugin(typeof(VAP.Editor.RemoveEmptySubMenusPlugin))]
#endif

namespace VAP.Editor
{
    internal static class RemoveEmptySubMenusBuild
    {
        const string Log = "[VAP Remove Empty Sub Menus] ";

        internal static void Process(GameObject avatarRoot, Action<Object> saveAsset)
        {
            var all = avatarRoot.GetComponentsInChildren<VAPRemoveEmptySubMenus>(true);
            if (all.Length == 0)
                return;
            if (all.Length > 1)
                Debug.LogWarning(Log + "More than one on the avatar; only one is used.", avatarRoot);
            foreach (var c in all)
                Object.DestroyImmediate(c);

            var descriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null || descriptor.expressionsMenu == null)
            {
                Debug.Log(Log + "The avatar has no Expressions Menu.", avatarRoot);
                return;
            }

            var root = descriptor.expressionsMenu;
            var menus = Reachable(root);
            var full = Full(menus);
            if (!menus.Exists(m => m.controls.Exists(c => IsSubMenu(c) && !Leads(c, full))))
            {
                Debug.Log(Log + $"Checked {menus.Count} menu(s), none empty.", avatarRoot);
                return;
            }

            var copies = new Dictionary<VRCExpressionsMenu, VRCExpressionsMenu>();
            foreach (var m in menus)
            {
                var copy = Object.Instantiate(m);
                copy.name = m.name;
                copies[m] = copy;
                saveAsset(copy);
            }
            int removed = 0;
            foreach (var copy in copies.Values)
            {
                removed += copy.controls.RemoveAll(c => IsSubMenu(c) && !Leads(c, full));
                foreach (var c in copy.controls)
                    if (IsSubMenu(c))
                        c.subMenu = copies[c.subMenu];
            }
            descriptor.expressionsMenu = copies[root];
            Debug.Log(Log + $"Checked {menus.Count} menu(s), removed {removed} empty sub menu item(s).", avatarRoot);
        }

        static bool IsSubMenu(VRCExpressionsMenu.Control c) =>
            c != null && c.type == VRCExpressionsMenu.Control.ControlType.SubMenu;

        static bool Leads(VRCExpressionsMenu.Control c, HashSet<VRCExpressionsMenu> full) =>
            c.subMenu != null && full.Contains(c.subMenu);

        static List<VRCExpressionsMenu> Reachable(VRCExpressionsMenu root)
        {
            var seen = new HashSet<VRCExpressionsMenu> { root };
            var list = new List<VRCExpressionsMenu> { root };
            for (int i = 0; i < list.Count; i++)
                foreach (var c in list[i].controls)
                    if (IsSubMenu(c) && c.subMenu != null && seen.Add(c.subMenu))
                        list.Add(c.subMenu);
            return list;
        }

        static HashSet<VRCExpressionsMenu> Full(List<VRCExpressionsMenu> menus)
        {
            var full = new HashSet<VRCExpressionsMenu>();
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var m in menus)
                {
                    if (full.Contains(m))
                        continue;
                    if (m.controls.Exists(c => c != null && (!IsSubMenu(c) || Leads(c, full))))
                    {
                        full.Add(m);
                        changed = true;
                    }
                }
            }
            return full;
        }
    }

#if VAP_NDMF
    public class RemoveEmptySubMenusPlugin : Plugin<RemoveEmptySubMenusPlugin>
    {
        public override string QualifiedName => "tech.azuki.vrc-prefabs.remove-empty-sub-menus";
        public override string DisplayName => "VAP Remove Empty Sub Menus";

        protected override void Configure()
        {
            InPhase(BuildPhase.Optimizing)
                .Run("Remove empty sub menus", ctx =>
                    RemoveEmptySubMenusBuild.Process(ctx.AvatarRootObject, o => ctx.AssetSaver.SaveAsset(o)));
        }
    }
#endif
}
#endif
