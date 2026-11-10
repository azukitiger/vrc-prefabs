using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VAP
{
    [AddComponentMenu("VAP/VRC Avatar Descriptor Expressions")]
    [DisallowMultipleComponent]
    [Icon(VAPFaceTrackingDebug.IconPath)]
    public class VAPDescriptorExpressions : VAPExpressions
    {
        public VRCAvatarDescriptor Descriptor => GetComponentInParent<VRCAvatarDescriptor>(true);

        public VAPDescriptorExpressions Other()
        {
            var descriptor = Descriptor;
            if (descriptor == null)
                return null;
            foreach (var c in descriptor.GetComponentsInChildren<VAPDescriptorExpressions>(true))
                if (c != this)
                    return c;
            return null;
        }

        public override List<VRCExpressionsMenu> RootMenus()
        {
            var menu = Descriptor != null ? Descriptor.expressionsMenu : null;
            return menu != null ? new List<VRCExpressionsMenu> { menu } : new List<VRCExpressionsMenu>();
        }

        public override List<VRCExpressionParameters> ParameterAssets()
        {
            var parameters = Descriptor != null ? Descriptor.expressionParameters : null;
            return parameters != null ? new List<VRCExpressionParameters> { parameters } : new List<VRCExpressionParameters>();
        }
    }
}
