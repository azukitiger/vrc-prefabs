using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace VAP
{
    [AddComponentMenu("VAP/Face Tracking Auto Corrective Behaviors")]
    [Icon(VAPFaceTrackingDebug.IconPath)]
    public class VAPCorrectiveBehaviors : MonoBehaviour, IEditorOnly
    {
        [Tooltip("The Blendshapes Set whose blendshapes the correctives combine. Its weight also scales the correctives.")]
        public string blendshapesSet = "";

        [Tooltip("Corrective blendshapes left out (unchecked in the list), by blendshape name.")]
        public List<string> disabledCorrectives = new List<string>();
    }
}
