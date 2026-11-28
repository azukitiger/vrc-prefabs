using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDKBase;

namespace VAP
{
    [HelpURL(VAPAvatar.Docs + "#-skinned-mesh-renderer")]
    [AddComponentMenu("VAP/Skinned Mesh Renderer")]
    [Icon(VAPFaceTrackingDebug.IconPath)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    [ExecuteAlways]
    public class VAPSkinnedMeshRenderer : MonoBehaviour, IEditorOnly, IVRCFuryWrapper
    {
        [NotKeyable]
        [HideInInspector] public SkinnedMeshRenderer wrapped;
        [NotKeyable]
        public bool blendShapeNormals;
        [NotKeyable]
        public List<string> normalBlendShapes = new List<string>();

        public Component Wrapped
        {
            get => wrapped;
            set => wrapped = value as SkinnedMeshRenderer;
        }

#if UNITY_EDITOR
        void Reset() => wrapped = GetComponent<SkinnedMeshRenderer>();

        void OnDestroy()
        {
            if (wrapped != null)
                wrapped.hideFlags &= ~HideFlags.HideInInspector;
        }
#endif
    }
}
