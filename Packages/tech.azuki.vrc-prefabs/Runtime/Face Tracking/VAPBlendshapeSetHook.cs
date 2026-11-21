using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace VAP
{
    [HelpURL(VAPAvatar.Docs + "#vrc-face-tracking-blendshape-set-hook")]
    [AddComponentMenu("VAP/VRC Face Tracking BlendShape Set Hook")]
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
