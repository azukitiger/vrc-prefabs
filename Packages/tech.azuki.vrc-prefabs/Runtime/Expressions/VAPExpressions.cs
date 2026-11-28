using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;

namespace VAP {
    [Icon(VAPFaceTrackingDebug.IconPath)]
    public abstract class VAPExpressions : MonoBehaviour, IEditorOnly {
        [Tooltip("The Expressions Menu asset. It is never changed; values set here are applied to a copy at build time.")]
        [NotKeyable]
        public VRCExpressionsMenu menuAsset;

        [Tooltip("The Expression Parameters asset. It is never changed; values set here are applied to a copy at build time.")]
        [NotKeyable]
        public VRCExpressionParameters parametersAsset;

        [NotKeyable]
        public List<DefaultOverride> defaultOverrides = new List<DefaultOverride>();
        [NotKeyable]
        public List<ControlOverride> controlOverrides = new List<ControlOverride>();

        [Tooltip("Add frame time to the avatar and make its parameters available to the animators.")]
        [NotKeyable]
        public bool frameTime = false;

        [Tooltip("Extra values added to the frame time for the animators.")]
        [NotKeyable]
        public List<VAPFrameTime.Factor> frameTimeFactors = new List<VAPFrameTime.Factor>();

        [Tooltip("Clips for the VRC Animator Play Audio behaviours in the animators, applied at build time.")]
        [NotKeyable]
        public List<VAPPlayAudioClips.Entry> audioClips = new List<VAPPlayAudioClips.Entry>();

        [Serializable]
        public class DefaultOverride {
            [NotKeyable]
            public string name = "";
            [NotKeyable]
            public float value;
        }

        [Serializable]
        public class ControlOverride {
            [NotKeyable]
            public VRCExpressionsMenu menu;
            [NotKeyable]
            public int control;
            [NotKeyable]
            public int label = -1;
            [NotKeyable]
            public bool removed;

            public bool IsEmpty => !removed;
        }

        public virtual List<VRCExpressionsMenu> RootMenus() =>
            menuAsset != null ? new List<VRCExpressionsMenu> { menuAsset } : new List<VRCExpressionsMenu>();

        public virtual List<VRCExpressionParameters> ParameterAssets() =>
            parametersAsset != null ? new List<VRCExpressionParameters> { parametersAsset } : new List<VRCExpressionParameters>();

        public virtual string RootPath(VRCExpressionsMenu root) => null;

        public VRCExpressionParameters.Parameter FindParameter(string name) {
            if (string.IsNullOrEmpty(name)) {
                return null;
            }

            foreach (var asset in ParameterAssets()) {
                if (asset.parameters != null) {
                    foreach (var p in asset.parameters) {
                        if (p != null && p.name == name) {
                            return p;
                        }
                    }
                }
            }

            return null;
        }

        public List<VRCExpressionsMenu> SubMenus() {
            var result = new List<VRCExpressionsMenu>();
            var roots = RootMenus();
            var seen = new HashSet<VRCExpressionsMenu>(roots);
            foreach (var root in roots) {
                Collect(root, seen, result, IsRemoved);
            }

            return result;
        }

        public List<VRCExpressionsMenu> AllMenus() {
            var roots = RootMenus();
            var result = new List<VRCExpressionsMenu>(roots);
            var seen = new HashSet<VRCExpressionsMenu>(roots);
            foreach (var root in roots) {
                Collect(root, seen, result, null);
            }

            return result;
        }

        public static IEnumerable<string> ControlParameters(VRCExpressionsMenu.Control c) {
            if (c == null) {
                yield break;
            }

            if (!string.IsNullOrEmpty(c.parameter?.name)) {
                yield return c.parameter.name;
            }

            if (c.subParameters != null) {
                foreach (var p in c.subParameters) {
                    if (!string.IsNullOrEmpty(p?.name)) {
                        yield return p.name;
                    }
                }
            }
        }

        void Collect(VRCExpressionsMenu menu, HashSet<VRCExpressionsMenu> seen, List<VRCExpressionsMenu> result, Func<VRCExpressionsMenu, int, bool> skip) {
            if (menu.controls == null) {
                return;
            }

            for (int i = 0; i < menu.controls.Count; i++) {
                var sub = menu.controls[i]?.subMenu;
                if (sub == null || (skip != null && skip(menu, i)) || !seen.Add(sub)) {
                    continue;
                }

                result.Add(sub);
                Collect(sub, seen, result, skip);
            }
        }

        public bool IsRemoved(VRCExpressionsMenu menu, int control) => FindControl(menu, control, -1)?.removed ?? false;

        HashSet<string> UsedParameters(Func<VRCExpressionsMenu, int, bool> skip) {
            var result = new HashSet<string>();
            var roots = RootMenus();
            var menus = new List<VRCExpressionsMenu>(roots);
            var seen = new HashSet<VRCExpressionsMenu>(roots);
            foreach (var root in roots) {
                Collect(root, seen, menus, skip);
            }

            foreach (var menu in menus) {
                if (menu.controls == null) {
                    continue;
                }

                for (int i = 0; i < menu.controls.Count; i++) {
                    var c = menu.controls[i];
                    if (c == null || (skip != null && skip(menu, i))) {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(c.parameter?.name)) {
                        result.Add(c.parameter.name);
                    }

                    if (c.subParameters != null) {
                        foreach (var p in c.subParameters) {
                            if (!string.IsNullOrEmpty(p?.name)) {
                                result.Add(p.name);
                            }
                        }
                    }
                }
            }
            return result;
        }

        public HashSet<string> UnsyncedParameters() {
            var before = UsedParameters(null);
            before.ExceptWith(UsedParameters(IsRemoved));
            return before;
        }


        public DefaultOverride FindDefault(string name) =>
            defaultOverrides.FirstOrDefault(o => o != null && o.name == name);

        public ControlOverride FindControl(VRCExpressionsMenu menu, int control, int label) =>
            controlOverrides.FirstOrDefault(o => o != null && o.menu == menu && o.control == control && o.label == label);

        public VRCExpressionParameters.Parameter[] BuildParameters(VRCExpressionParameters asset) {
            var source = asset != null && asset.parameters != null
                ? asset.parameters : new VRCExpressionParameters.Parameter[0];
            var unsynced = UnsyncedParameters();
            return source.Where(p => p != null).Select(p => {
                var copy = Copy(p);
                if (unsynced.Contains(p.name)) {
                    copy.networkSynced = false;
                    copy.saved = false;
                }
                var o = FindDefault(p.name);
                if (o != null) {
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

        public static void Range(VRCExpressionParameters.Parameter p, out float min, out float max) {
            bool signed = IsSigned(p);
            switch (p.valueType) {
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

        public List<VRCExpressionsMenu.Control> BuildControls(VRCExpressionsMenu menu) {
            var result = new List<VRCExpressionsMenu.Control>();
            if (menu == null || menu.controls == null) {
                return result;
            }

            for (int i = 0; i < menu.controls.Count; i++) {
                if (IsRemoved(menu, i)) {
                    continue;
                }

                result.Add(Copy(menu.controls[i]));
            }
            return result;
        }

        public static VRCExpressionsMenu.Control Copy(VRCExpressionsMenu.Control c) {
            if (c == null) {
                return new VRCExpressionsMenu.Control {
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = "" },
                    subParameters = new VRCExpressionsMenu.Control.Parameter[0],
                    labels = new VRCExpressionsMenu.Control.Label[0],
                };
            }

            return new VRCExpressionsMenu.Control {
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
