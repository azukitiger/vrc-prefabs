using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VAP
{
    [AddComponentMenu("VAP/VRCFury Full Controller")]
    [Icon(VAPFaceTrackingDebug.IconPath)]
    public class VAPFullController : VAPExpressions, IVRCFuryWrapper
    {
        [HideInInspector] public Component wrapped;

        [NonSerialized] public List<VRCExpressionsMenu> menus = new List<VRCExpressionsMenu>();
        [NonSerialized] public List<string> menuPrefixes = new List<string>();
        [NonSerialized] public List<VRCExpressionParameters> parameterFiles = new List<VRCExpressionParameters>();

        public Component Wrapped
        {
            get => wrapped;
            set => wrapped = value;
        }

        public override List<VRCExpressionsMenu> RootMenus() => menus.Where(m => m != null).Distinct().ToList();

        public override List<VRCExpressionParameters> ParameterAssets() => parameterFiles.Where(p => p != null).Distinct().ToList();

        public override string RootPath(VRCExpressionsMenu root)
        {
            int i = menus.IndexOf(root);
            string prefix = i >= 0 && i < menuPrefixes.Count ? menuPrefixes[i] : null;
            return string.IsNullOrWhiteSpace(prefix) ? null : prefix.Trim('/');
        }
    }
}
