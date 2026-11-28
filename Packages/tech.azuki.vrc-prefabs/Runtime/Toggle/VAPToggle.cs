using UnityEngine;
using UnityEngine.Animations;
using VRC.SDKBase;

namespace VAP {
    [HelpURL(VAPAvatar.Docs + "#-vrcfury-toggle")]
    [AddComponentMenu("VAP/VRCFury Toggle")]
    [Icon(VAPFaceTrackingDebug.IconPath)]
    public class VAPToggle : MonoBehaviour, IEditorOnly, IVRCFuryWrapper {
        [NotKeyable]
        [HideInInspector] public Component toggle;
        [NotKeyable]
        public string menuPath = "";

        public Component Wrapped {
            get => toggle;
            set => toggle = value;
        }

        [NotKeyable]
        public bool overrideDefault;
        [NotKeyable]
        public bool defaultOn;
        [NotKeyable]
        [Range(0, 1)] public float defaultValue;
    }
}
