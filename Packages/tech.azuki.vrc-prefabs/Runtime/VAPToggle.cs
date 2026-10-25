using UnityEngine;
using VRC.SDKBase;

namespace VAP
{
    [AddComponentMenu("VAP/VRCFury Toggle")]
    [Icon(VAPFaceTrackingDebug.IconPath)]
    public class VAPToggle : MonoBehaviour, IEditorOnly, IVRCFuryWrapper
    {
        // The VRCFury Toggle this wraps. It lives on the same object, hidden from the inspector.
        [HideInInspector] public Component toggle;

        public Component Wrapped
        {
            get => toggle;
            set => toggle = value;
        }

        // Replaces the VRCFury Toggle's default at build time when set.
        public bool overrideDefault;
        public bool defaultOn;
        [Range(0, 1)] public float defaultValue;
    }
}
