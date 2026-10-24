using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace VAP
{
    [AddComponentMenu("VAP/Face Tracking BlendShape Set Hook")]
    [Icon(IconPath)]
    public class VAPBlendshapeSetHook : VAPBlendshapeSetBase
    {
#if UNITY_EDITOR
        void OnValidate()
        {
            if (blendshapesSet == null)
                blendshapesSet = new VRCAvatarDescriptor.FaceTrackingBlendshapesSet { setName = "", setWeight = 1f };
        }
#endif
    }
}
