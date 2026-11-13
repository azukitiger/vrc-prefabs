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
        public VRCAvatarDescriptor Descriptor => VAPAvatar.Descriptor(this);

        public VAPDescriptorExpressions Other() => VAPAvatar.Other(this);

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
