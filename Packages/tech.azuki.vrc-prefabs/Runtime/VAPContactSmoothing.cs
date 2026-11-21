using UnityEngine;
using VRC.SDKBase;

namespace VAP
{
    [AddComponentMenu("VAP/Contact Smoothing")]
    [Icon(VAPFaceTrackingDebug.IconPath)]
    public class VAPContactSmoothing : MonoBehaviour, IEditorOnly
    {
        public const string None = "None";

        public enum PassMode
        {
            Double,
            Single,
        }

        [Tooltip("Parameter of a VRC Contact Receiver on this object or its children. If empty, the first one found is used.")]
        public string contactParameter = "";

        [Tooltip("Animator parameter that receives the smoothed value. If empty, VAP/ followed by the contact parameter.")]
        public string outputParameter = "";

        [Tooltip("How fast the output follows the contact. Custom sets your own speed, None follows the contact directly.")]
        public string smoothing = "Touch";

        [Tooltip("Higher follows faster. Touch is 8, Gesture is 20.")]
        public float customSpeed = 12f;

        [Tooltip("Use a different smoothing when the value goes down than when it goes up.")]
        public bool splitSmoothing = false;

        [Tooltip("Smoothing used when the value goes down, when Split is on.")]
        public string decreaseSmoothing = "Touch";

        [Tooltip("Higher follows faster. Touch is 8, Gesture is 20.")]
        public float decreaseCustomSpeed = 12f;

        [Tooltip("Double smooths through an extra hidden step for a softer curve. Single moves the output straight toward the contact.")]
        public PassMode passes = PassMode.Double;

        [Tooltip("Also count, in seconds, how long the contact is touched. Goes back to 0 when it is released.")]
        public bool timer = false;

        [Tooltip("Animator parameter that receives the touch time. If empty, the output parameter followed by Timer.")]
        public string timerParameter = "";
    }
}
