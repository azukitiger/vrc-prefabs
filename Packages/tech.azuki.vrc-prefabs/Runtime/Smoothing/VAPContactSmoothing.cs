using UnityEngine;

namespace VAP
{
    [HelpURL(VAPAvatar.Docs + "#vrc-contact-smoothing--vrc-physbone-smoothing")]
    [AddComponentMenu("VAP/VRC Contact Smoothing")]
    [Icon(VAPFaceTrackingDebug.IconPath)]
    public class VAPContactSmoothing : VAPSmoothing
    {
        [Tooltip("Parameter of a VRC Contact Receiver on this object. If empty, the first one found is used.")]
        public string contactParameter = "";

        public override string InputParameter => contactParameter;
    }
}
