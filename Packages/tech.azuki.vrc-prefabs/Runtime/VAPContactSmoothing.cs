using UnityEngine;

namespace VAP
{
    [AddComponentMenu("VAP/Contact Smoothing")]
    [Icon(VAPFaceTrackingDebug.IconPath)]
    public class VAPContactSmoothing : VAPSmoothing
    {
        [Tooltip("Parameter of a VRC Contact Receiver on this object or its children. If empty, the first one found is used.")]
        public string contactParameter = "";

        public override string InputParameter => contactParameter;
    }
}
