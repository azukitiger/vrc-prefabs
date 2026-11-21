using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace VAP
{
    [AddComponentMenu("VAP/Blendshape Normals")]
    [Icon(VAPFaceTrackingDebug.IconPath)]
    public class VAPBlendshapeNormals : MonoBehaviour, IEditorOnly
    {
        [Tooltip("Mesh to recalculate. Leave empty to use every mesh in the avatar that has the listed blendshapes.")]
        public SkinnedMeshRenderer targetRenderer;

        [Tooltip("Blendshapes whose normals and tangents are recalculated at build time the way Blender shades them.")]
        public List<string> blendShapes = new List<string>();
    }
}
