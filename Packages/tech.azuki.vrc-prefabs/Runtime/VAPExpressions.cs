using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;

namespace VAP
{
    [AddComponentMenu("VAP/VRC Menu & Parameters")]
    [Icon(VAPFaceTrackingDebug.IconPath)]
    public class VAPExpressions : MonoBehaviour, IEditorOnly
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

        // Every menu reachable through Sub Menu controls, each once, in menu order.
        public List<VRCExpressionsMenu> SubMenus()
        {
            var result = new List<VRCExpressionsMenu>();
            var seen = new HashSet<VRCExpressionsMenu>();
            if (menuAsset == null)
                return result;
            seen.Add(menuAsset);
            Collect(menuAsset, seen, result, true);
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
            if (menuAsset == null)
                return result;
            var menus = new List<VRCExpressionsMenu> { menuAsset };
            var seen = new HashSet<VRCExpressionsMenu> { menuAsset };
            Collect(menuAsset, seen, menus, skipRemoved);
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

        public VRCExpressionParameters.Parameter[] BuildParameters()
        {
            var source = parametersAsset != null && parametersAsset.parameters != null
                ? parametersAsset.parameters : new VRCExpressionParameters.Parameter[0];
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
