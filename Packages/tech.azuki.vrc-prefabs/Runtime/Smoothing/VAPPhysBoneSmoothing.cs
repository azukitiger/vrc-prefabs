using UnityEngine;
using UnityEngine.Animations;

namespace VAP {
    [HelpURL(VAPAvatar.Docs + "#-vrc-contact-smoothing--vrc-physbone-smoothing")]
    [AddComponentMenu("VAP/VRC PhysBone Smoothing")]
    [Icon(VAPFaceTrackingDebug.IconPath)]
    public class VAPPhysBoneSmoothing : VAPSmoothing {
        [Tooltip("IsGrabbed, IsPosed, Angle, Stretch or Squish parameter of a VRC PhysBone on this object. If empty, the first one found is used.")]
        [NotKeyable]
        public string physBoneParameter = "";

        public override string InputParameter => physBoneParameter;
    }
}
