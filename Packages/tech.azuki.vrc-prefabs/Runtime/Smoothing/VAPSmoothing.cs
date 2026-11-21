using System.Collections.Generic;
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

        [Tooltip("The timer counts while the input is above this value.")]
        public float timerStart = 0f;

        [Tooltip("Use a separate, lower value the input must drop under for the timer to reset.")]
        public bool splitTimer = false;

        [Tooltip("The timer resets when the input is at or below this value. Between this and the start value, a timer that has started keeps counting.")]
        public float timerReset = 0f;

        [System.Serializable]
        public struct TransformPoint
        {
            public Vector2 position;
            public float value;
        }

        [Tooltip("Map the smoothed value and another parameter to the output with a 2D blend, like a PhysBone Stretch that fades in and out with a smoothed IsGrabbed.")]
        public bool transformOutput = false;

        [Tooltip("Parameter used as the Y axis. It is not smoothed itself. If empty, the first other parameter found is used.")]
        public string transformParameter = "";

        [Tooltip("Transform the input before it is smoothed, so the smoothing follows the 2D blend result. Off transforms the smoothed value instead.")]
        public bool transformBeforeSmoothing = false;

        [Tooltip("Output value at each point. X is the smoothed value, Y is the transform parameter.")]
        public List<TransformPoint> transformPoints = DefaultPoints();

        public static List<TransformPoint> DefaultPoints() => new List<TransformPoint>
        {
            new TransformPoint { position = new Vector2(0f, 0f), value = 0f },
            new TransformPoint { position = new Vector2(1f, 0f), value = 0f },
            new TransformPoint { position = new Vector2(0f, 1f), value = 0f },
            new TransformPoint { position = new Vector2(1f, 1f), value = 1f },
        };

        public abstract string InputParameter { get; }
    }
}
