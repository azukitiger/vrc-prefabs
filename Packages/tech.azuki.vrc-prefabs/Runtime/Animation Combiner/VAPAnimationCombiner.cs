using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace VAP
{
    [HelpURL(VAPAvatar.Docs + "#-animation-combiner")]
    [AddComponentMenu("VAP/Animation Combiner")]
    [Icon(VAPFaceTrackingDebug.IconPath)]
    public class VAPAnimationCombiner : MonoBehaviour, IEditorOnly
    {
        public enum Mode
        {
            Combine,
            Replace,
        }

        [Serializable]
        public class Entry
        {
            public AnimationClip animation;
            public Mode mode;
            public AnimationClip combine;
        }

        public Transform startingPoint;

        public List<Entry> entries = new List<Entry>();
    }
}
