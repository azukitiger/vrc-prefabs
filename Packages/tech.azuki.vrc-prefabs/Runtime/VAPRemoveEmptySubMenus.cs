using UnityEngine;
using VRC.SDKBase;

namespace VAP
{
    // Removes sub menu items that lead to empty menus. Runs after VRCFury, on the final menu.
    [DisallowMultipleComponent]
    [AddComponentMenu("VAP/Remove Empty Sub Menus")]
    [Icon(VAPFaceTrackingDebug.IconPath)]
    public class VAPRemoveEmptySubMenus : MonoBehaviour, IEditorOnly
    {
    }
}
