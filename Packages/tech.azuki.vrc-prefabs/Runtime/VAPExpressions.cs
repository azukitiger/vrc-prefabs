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
        [Tooltip("The Expressions Menu asset edited here (inside the prefab).")]
        [FormerlySerializedAs("asset")]
        public VRCExpressionsMenu menuAsset;

        [Tooltip("The Expression Parameters asset edited here (inside the prefab).")]
        public VRCExpressionParameters parametersAsset;

        // Used until an asset is created; then the asset holds them.
        public List<VRCExpressionParameters.Parameter> parameters = new List<VRCExpressionParameters.Parameter>();
        public List<VRCExpressionsMenu.Control> controls = new List<VRCExpressionsMenu.Control>();

        // Prefab instance values, applied over the assets at build time.
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
            public bool overrideName;
            public string name = "";
            public bool overrideIcon;
            public Texture2D icon;

            public bool IsEmpty => !overrideName && !overrideIcon;
        }

        // Every menu reachable through Sub Menu controls, each once, in menu order.
        public List<VRCExpressionsMenu> SubMenus()
        {
            var result = new List<VRCExpressionsMenu>();
            var seen = new HashSet<VRCExpressionsMenu>();
            if (menuAsset != null)
                seen.Add(menuAsset);
            Collect(menuAsset != null ? menuAsset.controls : controls, seen, result);
            return result;
        }

        static void Collect(List<VRCExpressionsMenu.Control> list, HashSet<VRCExpressionsMenu> seen, List<VRCExpressionsMenu> result)
        {
            if (list == null)
                return;
            foreach (var c in list)
            {
                var menu = c?.subMenu;
                if (menu == null || !seen.Add(menu))
                    continue;
                result.Add(menu);
                Collect(menu.controls, seen, result);
            }
        }

        public DefaultOverride FindDefault(string name) =>
            defaultOverrides.FirstOrDefault(o => o != null && o.name == name);

        public ControlOverride FindControl(VRCExpressionsMenu menu, int control, int label) =>
            controlOverrides.FirstOrDefault(o => o != null && o.menu == menu && o.control == control && o.label == label);

        public VRCExpressionParameters.Parameter[] BuildParameters()
        {
            IEnumerable<VRCExpressionParameters.Parameter> source = parametersAsset != null
                ? parametersAsset.parameters ?? new VRCExpressionParameters.Parameter[0]
                : (IEnumerable<VRCExpressionParameters.Parameter>)parameters;
            return source.Where(p => p != null).Select(p => new VRCExpressionParameters.Parameter
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
                var c = Copy(menu.controls[i]);
                Apply(FindControl(menu, i, -1), ref c.name, ref c.icon);
                for (int l = 0; l < c.labels.Length; l++)
                    Apply(FindControl(menu, i, l), ref c.labels[l].name, ref c.labels[l].icon);
                result.Add(c);
            }
            return result;
        }

        static void Apply(ControlOverride o, ref string name, ref Texture2D icon)
        {
            if (o == null)
                return;
            if (o.overrideName)
                name = o.name;
            if (o.overrideIcon)
                icon = o.icon;
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
