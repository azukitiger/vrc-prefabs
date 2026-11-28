#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.Rendering;
#if VAP_NDMF
using nadena.dev.ndmf;
#endif

#if VAP_NDMF
[assembly: ExportsPlugin(typeof(VAP.Editor.SkinnedMeshRendererPlugin))]
#endif

namespace VAP.Editor {
    internal static class SkinnedMeshRendererBuild {
        internal static void Enforce(SkinnedMeshRenderer smr) {
            smr.updateWhenOffscreen = false;
            smr.receiveShadows = true;
            smr.lightProbeUsage = LightProbeUsage.BlendProbes;
            smr.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            smr.quality = SkinQuality.Auto;
            smr.skinnedMotionVectors = false;
            smr.allowOcclusionWhenDynamic = true;
        }

        internal static void Process(GameObject avatarRoot) {
            foreach (var w in avatarRoot.GetComponentsInChildren<VAPSkinnedMeshRenderer>(true)) {
                var smr = w.wrapped != null ? w.wrapped : w.GetComponent<SkinnedMeshRenderer>();
                if (smr != null) {
                    Enforce(smr);
                }

                Object.DestroyImmediate(w);
            }
        }
    }

#if VAP_NDMF
    public class SkinnedMeshRendererPlugin : Plugin<SkinnedMeshRendererPlugin> {
        public override string QualifiedName => "tech.azuki.vrc-prefabs.skinned-mesh-renderer";
        public override string DisplayName => "VAP Skinned Mesh Renderer";

        protected override void Configure() {
            InPhase(BuildPhase.Transforming)
                .AfterPlugin("tech.azuki.vrc-prefabs.blendshape-normals")
                .Run("Apply renderer settings", ctx => SkinnedMeshRendererBuild.Process(ctx.AvatarRootObject));
        }
    }
#endif
}
#endif
