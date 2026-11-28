using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
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
            [NotKeyable]
            public AnimationClip animation;
            [NotKeyable]
            public Mode mode;
            [NotKeyable]
            public AnimationClip combine;
        }

        [NotKeyable]
        public Transform startingPoint;

        [NotKeyable]
        public List<Entry> entries = new List<Entry>();
    }
}
