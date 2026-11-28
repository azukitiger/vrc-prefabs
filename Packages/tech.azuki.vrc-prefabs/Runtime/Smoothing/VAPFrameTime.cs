using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDKBase;

namespace VAP {
    [HelpURL(VAPAvatar.Docs + "#frame-time")]
    [AddComponentMenu("")]
    [Icon(VAPFaceTrackingDebug.IconPath)]
    public class VAPFrameTime : MonoBehaviour, IEditorOnly {
        public enum Kind {
            Smoothing,
            Linear,
        }

        [Serializable]
        public class Factor {
            [Tooltip("Animator parameter that receives the value every frame.")]
            [NotKeyable]
            public string parameter = "";

            [Tooltip("Smoothing gives a smoothing factor for the given speed. Linear gives the value multiplied by the frame time.")]
            [NotKeyable]
            public Kind kind = Kind.Smoothing;

            [Tooltip("Speed for Smoothing, or the amount per second for Linear.")]
            [NotKeyable]
            public float value = 8f;
        }

        [Tooltip("Extra values added to the frame time.")]
        [NotKeyable]
        public List<Factor> factors = new List<Factor>();
    }
}
