using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDKBase;

namespace VAP
{
    [HelpURL(VAPAvatar.Docs + "#-vrc-avatar-descriptor-enhancements")]
    [AddComponentMenu("")]
    [Icon(VAPFaceTrackingDebug.IconPath)]
    public class VAPPlayAudioClips : MonoBehaviour, IEditorOnly
    {
        [Serializable]
        public class Entry
        {
            [NotKeyable]
            public string key;
            [NotKeyable]
            public List<AudioClip> clips = new List<AudioClip>();
        }

        [Tooltip("Animator controller whose VRC Animator Play Audio behaviours get the clips below at build time. The controller asset itself is never changed.")]
        [NotKeyable]
        public RuntimeAnimatorController controller;

        [NotKeyable]
        public List<Entry> entries = new List<Entry>();
    }
}
