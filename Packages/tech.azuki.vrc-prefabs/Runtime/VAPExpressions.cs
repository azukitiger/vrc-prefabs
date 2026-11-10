using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;

namespace VAP
{
    [Icon(VAPFaceTrackingDebug.IconPath)]
    public abstract class VAPExpressions : MonoBehaviour, IEditorOnly
    {
        [Tooltip("The Expressions Menu asset. It is never changed; values set here are applied to a copy at build time.")]
        [FormerlySerializedAs("asset")]
        public VRCExpressionsMenu menuAsset;

        [Tooltip("The Expression Parameters asset. It is never changed; values set here are applied to a copy at build time.")]
        public VRCExpressionParameters parametersAsset;

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

        public virtual List<VRCExpressionsMenu> RootMenus() =>
            menuAsset != null ? new List<VRCExpressionsMenu> { menuAsset } : new List<VRCExpressionsMenu>();

        public virtual List<VRCExpressionParameters> ParameterAssets() =>
            parametersAsset != null ? new List<VRCExpressionParameters> { parametersAsset } : new List<VRCExpressionParameters>();

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
            return source.Where(p => p != null && !dropped.Contains(p.name)).Select(p =>
            {
                var copy = Copy(p);
                var o = FindDefault(p.name);
                if (o != null)
                {
                    Range(p, out float min, out float max);
                    copy.defaultValue = Mathf.Clamp(o.value, min, max);
                }
                return copy;
            }).ToArray();
        }

        public static VRCExpressionParameters.Parameter Copy(VRCExpressionParameters.Parameter p) =>
            JsonUtility.FromJson<VRCExpressionParameters.Parameter>(JsonUtility.ToJson(p));

        public static int SyncedBits(VRCExpressionParameters.Parameter p) =>
            p != null && p.networkSynced ? p.GetBitWidthByType() : 0;

        public static bool IsSigned(VRCExpressionParameters.Parameter p) =>
            p.GetRepresentationByType() == VRCExpressionParameters.Parameter.ValueRepresentation.Signed;

        public static void Range(VRCExpressionParameters.Parameter p, out float min, out float max)
        {
            bool signed = IsSigned(p);
            switch (p.valueType)
            {
                case VRCExpressionParameters.ValueType.Bool:
                    min = 0f;
                    max = 1f;
                    break;
                case VRCExpressionParameters.ValueType.Int:
                    int bits = p.GetBitWidthByType();
                    min = signed ? -(1 << (bits - 1)) : 0;
                    max = signed ? (1 << (bits - 1)) - 1 : (1 << bits) - 1;
                    break;
                default:
                    min = signed ? -1f : 0f;
                    max = 1f;
                    break;
            }
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
