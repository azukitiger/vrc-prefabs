using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace VAP
{
    [AddComponentMenu("VAP/Face Tracking BlendShape Set")]
    [Icon(IconPath)]
    public class VAPFaceTrackingDebug : VAPBlendshapeSetBase
    {
        void Reset()
        {
            blendshapesSet = new VRCAvatarDescriptor.FaceTrackingBlendshapesSet { setName = "Debug", setWeight = 1f };
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (blendshapesSet == null)
                blendshapesSet = new VRCAvatarDescriptor.FaceTrackingBlendshapesSet { setName = "Debug", setWeight = 1f };
        }
#endif
    }
}
