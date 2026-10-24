using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace VAP
{
    [AddComponentMenu("VAP/Face Tracking BlendShape Set")]
    [Icon(IconPath)]
    public class VAPFaceTrackingDebug : VAPBlendshapeSetBase
    {
        // Older versions stored the set name and weight as separate fields.
        [SerializeField, HideInInspector] string setName;
        [SerializeField, HideInInspector] float setWeight = -1f;

        void Reset()
        {
            blendshapesSet = new VRCAvatarDescriptor.FaceTrackingBlendshapesSet { setName = "Debug", setWeight = 1f };
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (blendshapesSet == null)
                blendshapesSet = new VRCAvatarDescriptor.FaceTrackingBlendshapesSet { setName = "Debug", setWeight = 1f };
            if (!string.IsNullOrEmpty(setName))
            {
                blendshapesSet.setName = setName;
                setName = null;
            }
            if (setWeight >= 0f)
            {
                blendshapesSet.setWeight = setWeight;
                setWeight = -1f;
            }
        }
#endif
    }
}
