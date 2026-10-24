using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VAP
{
    [AddComponentMenu("VAP/Face Tracking Parameter Behaviors")]
    [Icon(VAPFaceTrackingDebug.IconPath)]
    public class VAPParameterBehaviors : MonoBehaviour, IEditorOnly
    {
        [Tooltip("The Blendshapes Set every Tracking Key and Blendshape behavior here uses. " +
                 "Ignored when Per Behavior is on. If empty, each behavior's own set is used.")]
        public string blendshapesSet = "";

        [Tooltip("Give each behavior its own Blendshapes Set instead of the shared one.")]
        public bool perBehaviorSet = false;

        public string SetFor(string own) =>
            !perBehaviorSet && !string.IsNullOrEmpty(blendshapesSet) ? blendshapesSet : own;

        [Tooltip("Added to the avatar descriptor's face tracking Parameter Behaviors at build time.")]
        public List<VRCAvatarDescriptor.FaceTrackingParameterBehavior> parameterBehaviors =
            new List<VRCAvatarDescriptor.FaceTrackingParameterBehavior>();

        [Tooltip("Limits the blendshapes a Blendshapes Set uses for a tracking key or group of keys. " +
                 "Turned into Limit Blendshape behaviors at build time.")]
        public List<TrackingKeyBehavior> trackingKeyBehaviors = new List<TrackingKeyBehavior>();

        [Serializable]
        public class TrackingKeyBehavior
        {
            [Tooltip("A tracking key (LipFunnelUpperLeft) or a group of keys (LipFunnel, LipFunnelUpper).")]
            public string trackingKey = "";

            [Tooltip("Limit Blendshape (Max) or Limit Blendshape (Min).")]
            public VRCAvatarDescriptor.ParameterBehaviorTargetMode mode =
                VRCAvatarDescriptor.ParameterBehaviorTargetMode.LimitBlendshapeMax;

            [Tooltip("The Blendshapes Set whose blendshapes for these keys are limited. It also scales how strongly the limit applies.")]
            public string blendshapesSet = "";

            [Tooltip("Inputs and combine mode. Its target is ignored.")]
            public VRCAvatarDescriptor.FaceTrackingParameterBehavior behavior =
                new VRCAvatarDescriptor.FaceTrackingParameterBehavior();
        }
    }
}
