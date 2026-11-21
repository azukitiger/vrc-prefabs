using UnityEngine;
using VRC.SDKBase;

namespace VAP
{
    [HelpURL(VAPAvatar.Docs + "#rig-floor-adjuster")]
    [AddComponentMenu("VAP/Rig Floor Adjuster")]
    [Icon(VAPFaceTrackingDebug.IconPath)]
    public class VAPFloorAdjuster : MonoBehaviour, IEditorOnly
    {
        [Tooltip("How far to move the armature, in meters. Positive moves the avatar up, negative moves it down. The view position moves by the same amount.")]
        public float height;
    }
}
