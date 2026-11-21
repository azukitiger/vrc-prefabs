using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace VAP
{
    [AddComponentMenu("VAP/Frame Time")]
    [Icon(VAPFaceTrackingDebug.IconPath)]
    public class VAPFrameTime : MonoBehaviour, IEditorOnly
    {
        public enum Kind
        {
            Smoothing,
            Linear,
        }

        [Serializable]
        public class Factor
        {
            [Tooltip("Animator parameter that receives the value every frame.")]
            public string parameter = "";

            [Tooltip("Smoothing gives a smoothing factor for the given speed. Linear gives the value multiplied by the frame time.")]
            public Kind kind = Kind.Smoothing;

            [Tooltip("Speed for Smoothing, or the amount per second for Linear.")]
            public float value = 8f;
        }

        [Tooltip("Extra values added to the frame time.")]
        public List<Factor> factors = new List<Factor>();
    }
}
