using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VAP {
    [HelpURL(VAPAvatar.Docs + "#-vrc-face-tracking-parameter-behaviors")]
    [AddComponentMenu("VAP/VRC Face Tracking Parameter Behaviors")]
    [Icon(VAPFaceTrackingDebug.IconPath)]
    public class VAPParameterBehaviors : MonoBehaviour, IEditorOnly {
        [Tooltip("The Blendshapes Set every Tracking Key and Blendshape behavior here uses. " +
                 "Ignored when Per Behavior is on. If empty, each behavior's own set is used.")]
        [NotKeyable]
        public string blendshapesSet = "";

        [Tooltip("Give each behavior its own Blendshapes Set instead of the shared one.")]
        [NotKeyable]
        public bool perBehaviorSet = false;

        public string SetFor(string own) =>
            !perBehaviorSet && !string.IsNullOrEmpty(blendshapesSet) ? blendshapesSet : own;

        [Tooltip("Added to the avatar descriptor's face tracking Parameter Behaviors at build time.")]
        public List<VRCAvatarDescriptor.FaceTrackingParameterBehavior> parameterBehaviors =
            new List<VRCAvatarDescriptor.FaceTrackingParameterBehavior>();

        [Tooltip("Limits the blendshapes a Blendshapes Set uses for a tracking key or group of keys. " +
                 "Turned into Limit Blendshape behaviors at build time.")]
        [NotKeyable]
        public List<TrackingKeyBehavior> trackingKeyBehaviors = new List<TrackingKeyBehavior>();

        [Tooltip("Add Apply to Blendshape behaviors for corrective blendshapes at build time.")]
        [NotKeyable]
        public bool generateCorrectives = true;

        [Tooltip("The Blendshapes Set the correctives combine when Per Behavior is on.")]
        [NotKeyable]
        public string correctivesSet = "";

        [Tooltip("Corrective blendshapes left out (unchecked in the list), by blendshape name.")]
        [NotKeyable]
        public List<string> disabledCorrectives = new List<string>();

        public string CorrectivesSet => SetFor(correctivesSet);

        [Serializable]
        public class TrackingKeyBehavior {
            [Tooltip("A tracking key (LipFunnelUpperLeft) or a group of keys (LipFunnel, LipFunnelUpper).")]
            [NotKeyable]
            public string trackingKey = "";

            [Tooltip("Limit Blendshape (Max) or Limit Blendshape (Min).")]
            public VRCAvatarDescriptor.ParameterBehaviorTargetMode mode =
                VRCAvatarDescriptor.ParameterBehaviorTargetMode.LimitBlendshapeMax;

            [Tooltip("The Blendshapes Set whose blendshapes for these keys are limited. It also scales how strongly the limit applies.")]
            [NotKeyable]
            public string blendshapesSet = "";

            [Tooltip("Inputs and combine mode. Its target is ignored.")]
            public VRCAvatarDescriptor.FaceTrackingParameterBehavior behavior =
                new VRCAvatarDescriptor.FaceTrackingParameterBehavior();
        }
    }
}
