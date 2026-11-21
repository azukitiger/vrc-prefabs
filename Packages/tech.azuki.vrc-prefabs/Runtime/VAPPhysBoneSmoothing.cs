using UnityEngine;

namespace VAP
{
    [AddComponentMenu("VAP/PhysBone Smoothing")]
    [Icon(VAPFaceTrackingDebug.IconPath)]
    public class VAPPhysBoneSmoothing : VAPSmoothing
    {
        [Tooltip("IsGrabbed, IsPosed, Angle, Stretch or Squish parameter of a VRC PhysBone on this object or its children. If empty, the first one found is used.")]
        public string physBoneParameter = "";

        public override string InputParameter => physBoneParameter;
    }
}
