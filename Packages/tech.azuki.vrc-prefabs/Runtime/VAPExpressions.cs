using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;

namespace VAP
{
    // Base of the VRCFury Full Controller and VRC Avatar Descriptor Expressions components.
    [Icon(VAPFaceTrackingDebug.IconPath)]
    public abstract class VAPExpressions : MonoBehaviour, IEditorOnly
    {
        [Tooltip("The Expressions Menu asset. It is never changed; values set here are applied to a copy at build time.")]
        [FormerlySerializedAs("asset")]
        public VRCExpressionsMenu menuAsset;

        [Tooltip("The Expression Parameters asset. It is never changed; values set here are applied to a copy at build time.")]
        public VRCExpressionParameters parametersAsset;

        // Values set on this component, applied over the assets at build time.
        public List<DefaultOverride> defaultOverrides = new List<DefaultOverride>();
        public List<ControlOverride> controlOverrides = new List<ControlOverride>();

        [Serializable]
        public class DefaultOverride
        {
            public string name = "";
            public float value;
        }

        [Serializable]
        public class ControlOverride
        {
            public VRCExpressionsMenu menu;
            public int control;
            public int label = -1;
            public bool removed;

            public bool IsEmpty => !removed;
        }

        // The menus and parameter files this component reads.
        public virtual List<VRCExpressionsMenu> RootMenus() =>
            menuAsset != null ? new List<VRCExpressionsMenu> { menuAsset } : new List<VRCExpressionsMenu>();

        public virtual List<VRCExpressionParameters> ParameterAssets() =>
            parametersAsset != null ? new List<VRCExpressionParameters> { parametersAsset } : new List<VRCExpressionParameters>();

        // Where a root menu shows up in the avatar's menu, or null when it is the main menu.
        public virtual string RootPath(VRCExpressionsMenu root) => null;

        public VRCExpressionParameters.Parameter FindParameter(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            foreach (var asset in ParameterAssets())
                if (asset.parameters != null)
                    foreach (var p in asset.parameters)
                        if (p != null && p.name == name)
                            return p;
            return null;
        }

        // Every menu reachable through Sub Menu controls, each once, in menu order. Root menus are not included.
        public List<VRCExpressionsMenu> SubMenus()
        {
            var result = new List<VRCExpressionsMenu>();
            var roots = RootMenus();
            var seen = new HashSet<VRCExpressionsMenu>(roots);
            foreach (var root in roots)
                Collect(root, seen, result, true);
            return result;
        }

        void Collect(VRCExpressionsMenu menu, HashSet<VRCExpressionsMenu> seen, List<VRCExpressionsMenu> result, bool skipRemoved)
        {
            if (menu.controls == null)
                return;
            for (int i = 0; i < menu.controls.Count; i++)
            {
                var sub = menu.controls[i]?.subMenu;
                if (sub == null || (skipRemoved && IsRemoved(menu, i)) || !seen.Add(sub))
                    continue;
                result.Add(sub);
                Collect(sub, seen, result, skipRemoved);
            }
        }

        public bool IsRemoved(VRCExpressionsMenu menu, int control) => FindControl(menu, control, -1)?.removed ?? false;

        // Parameter names used by menu items, with or without the removed ones.
        HashSet<string> UsedParameters(bool skipRemoved)
        {
            var result = new HashSet<string>();
            var roots = RootMenus();
            var menus = new List<VRCExpressionsMenu>(roots);
            var seen = new HashSet<VRCExpressionsMenu>(roots);
            foreach (var root in roots)
                Collect(root, seen, menus, skipRemoved);
            foreach (var menu in menus)
            {
                if (menu.controls == null)
                    continue;
                for (int i = 0; i < menu.controls.Count; i++)
                {
                    var c = menu.controls[i];
                    if (c == null || (skipRemoved && IsRemoved(menu, i)))
                        continue;
                    if (!string.IsNullOrEmpty(c.parameter?.name))
                        result.Add(c.parameter.name);
                    if (c.subParameters != null)
                        foreach (var p in c.subParameters)
                            if (!string.IsNullOrEmpty(p?.name))
                                result.Add(p.name);
                }
            }
            return result;
        }

        // Parameters only removed menu items used, which are left out at build time.
        public HashSet<string> DroppedParameters()
        {
            var before = UsedParameters(false);
            before.ExceptWith(UsedParameters(true));
            return before;
        }

        public DefaultOverride FindDefault(string name) =>
            defaultOverrides.FirstOrDefault(o => o != null && o.name == name);

        public ControlOverride FindControl(VRCExpressionsMenu menu, int control, int label) =>
            controlOverrides.FirstOrDefault(o => o != null && o.menu == menu && o.control == control && o.label == label);

        public VRCExpressionParameters.Parameter[] BuildParameters(VRCExpressionParameters asset)
        {
            var source = asset != null && asset.parameters != null
                ? asset.parameters : new VRCExpressionParameters.Parameter[0];
            var dropped = DroppedParameters();
            return source.Where(p => p != null && !dropped.Contains(p.name)).Select(p => new VRCExpressionParameters.Parameter
            {
                name = p.name,
                valueType = p.valueType,
                defaultValue = FindDefault(p.name)?.value ?? p.defaultValue,
                saved = p.saved,
                networkSynced = p.networkSynced,
            }).ToArray();
        }

        public List<VRCExpressionsMenu.Control> BuildControls(VRCExpressionsMenu menu)
        {
            var result = new List<VRCExpressionsMenu.Control>();
            if (menu == null || menu.controls == null)
                return result;
            for (int i = 0; i < menu.controls.Count; i++)
            {
                if (IsRemoved(menu, i))
                    continue;
                result.Add(Copy(menu.controls[i]));
            }
            return result;
        }

        public static VRCExpressionsMenu.Control Copy(VRCExpressionsMenu.Control c)
        {
            if (c == null)
                return new VRCExpressionsMenu.Control
                {
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = "" },
                    subParameters = new VRCExpressionsMenu.Control.Parameter[0],
                    labels = new VRCExpressionsMenu.Control.Label[0],
                };
            return new VRCExpressionsMenu.Control
            {
                name = c.name,
                icon = c.icon,
                type = c.type,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = c.parameter?.name ?? "" },
                value = c.value,
                style = c.style,
                subMenu = c.subMenu,
                subParameters = (c.subParameters ?? new VRCExpressionsMenu.Control.Parameter[0])
                    .Select(p => new VRCExpressionsMenu.Control.Parameter { name = p?.name ?? "" }).ToArray(),
                labels = (c.labels ?? new VRCExpressionsMenu.Control.Label[0])
                    .Select(l => new VRCExpressionsMenu.Control.Label { name = l.name, icon = l.icon }).ToArray(),
            };
        }
    }
}
