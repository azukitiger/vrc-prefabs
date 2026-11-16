using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace VAP
{
    [AddComponentMenu("VAP/Blendshape Normals")]
    [Icon(VAPFaceTrackingDebug.IconPath)]
    public class VAPBlendshapeNormals : MonoBehaviour, IEditorOnly
    {
        [Tooltip("Blendshapes whose normals and tangents are recalculated at build time the way Blender shades them, on every mesh in the avatar that has them.")]
        public List<string> blendShapes = new List<string>();
    }
}
