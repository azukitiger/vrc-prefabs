using UnityEngine;
using VRC.SDKBase;

namespace VAP
{
    [AddComponentMenu("VAP/VRCFury Toggle")]
    [Icon(VAPFaceTrackingDebug.IconPath)]
    public class VAPToggle : MonoBehaviour, IEditorOnly, IVRCFuryWrapper
    {
        [HideInInspector] public Component toggle;

        public Component Wrapped
        {
            get => toggle;
            set => toggle = value;
        }

        public bool overrideDefault;
        public bool defaultOn;
        [Range(0, 1)] public float defaultValue;
    }
}
