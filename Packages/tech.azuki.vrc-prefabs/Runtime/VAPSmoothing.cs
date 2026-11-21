using UnityEngine;
using VRC.SDKBase;

namespace VAP
{
    public abstract class VAPSmoothing : MonoBehaviour, IEditorOnly
    {
        public const string None = "None";

        public enum PassMode
        {
            Double,
            Single,
        }

        [Tooltip("Animator parameter that receives the smoothed value. If empty, VAP/ followed by the input parameter.")]
        public string outputParameter = "";

        [Tooltip("How fast the output follows the input. Custom sets your own speed, None follows the input directly.")]
        public string smoothing = "Touch";

        [Tooltip("Higher follows faster. Touch is 8, Gesture is 20.")]
        public float customSpeed = 12f;

        [Tooltip("Use a different smoothing when the value goes down than when it goes up.")]
        public bool splitSmoothing = false;

        [Tooltip("Smoothing used when the value goes down, when Split is on.")]
        public string decreaseSmoothing = "Touch";

        [Tooltip("Higher follows faster. Touch is 8, Gesture is 20.")]
        public float decreaseCustomSpeed = 12f;

        [Tooltip("Double smooths through an extra hidden step for a softer curve. Single moves the output straight toward the input.")]
        public PassMode passes = PassMode.Double;

        [Tooltip("Also count, in seconds, how long the input is above 0. Goes back to 0 when it is.")]
        public bool timer = false;

        [Tooltip("Animator parameter that receives the time. If empty, the output parameter followed by Timer.")]
        public string timerParameter = "";

        public abstract string InputParameter { get; }
    }
}
