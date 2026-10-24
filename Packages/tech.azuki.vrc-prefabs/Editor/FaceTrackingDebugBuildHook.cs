#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
#if VAP_NDMF
using nadena.dev.ndmf;
#endif

#if VAP_NDMF
[assembly: ExportsPlugin(typeof(VAP.Editor.FaceTrackingDebugPlugin))]
#endif

namespace VAP.Editor
{
    internal static class FaceTrackingDebugBuild
    {
        internal const string ErrorTitle = "VAP BlendShape Set";

        internal static string Process(GameObject avatarRoot)
        {
            var components = avatarRoot.GetComponentsInChildren<VAPFaceTrackingDebug>(true);
            if (components.Length == 0)
                return null;

            var descriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            // Names must be unique on this avatar; checked before any set is added.
            var conflicts = components.Select(c => BlendshapeSetNameCheck.Conflict(c, descriptor))
                .Where(m => m != null).Distinct().ToList();
            if (conflicts.Count > 0)
            {
                string error = $"{avatarRoot.name} can't be built: a BlendShape Set component uses a Blendshapes Set name " +
                               "that is already taken. Rename it.\n\n" + string.Join("\n", conflicts);
                Debug.LogError("[VAP Face Tracking Debug] " + error, avatarRoot);
                return error;
            }

            if (descriptor == null)
            {
                Debug.LogWarning("[VAP Face Tracking Debug] No VRC Avatar Descriptor on the avatar root; skipped.", avatarRoot);
            }
            else
            {
                foreach (var c in components)
                {
                    string format = FaceTrackingDebugSetFiller.Normalize(c.nameFormat);
                    var set = c.blendshapesSet ?? new VRCAvatarDescriptor.FaceTrackingBlendshapesSet { setWeight = 1f };
                    if (string.IsNullOrEmpty(set.setName))
                        set.setName = FaceTrackingDebugSetFiller.DefaultSetName;
                    string setName = set.setName;
                    var meshes = MeshesFor(c, format);
                    var r = FaceTrackingDebugSetFiller.Apply(descriptor, meshes, set, format, false);
                    string msg = FaceTrackingDebugSetFiller.Describe(r, setName, format, avatarRoot.name).Replace("\n\n", " ");
                    if (r.mapped == 0)
                        Debug.LogWarning("[VAP Face Tracking Debug] " + msg, avatarRoot);
                    else
                        Debug.Log("[VAP Face Tracking Debug] " + msg, avatarRoot);
                }
            }

            foreach (var c in components)
                Object.DestroyImmediate(c);
            return null;
        }

        internal static IEnumerable<SkinnedMeshRenderer> MeshesFor(VAPBlendshapeSetBase c, string format)
        {
            var listed = (c.meshes ?? new SkinnedMeshRenderer[0]).Where(m => m != null).ToList();
            if (listed.Count > 0)
                return listed;
            // Blendshapes this set already maps, so meshes are found even without an auto-fill format.
            var used = new HashSet<string>();
            if (c.blendshapesSet?.faceTrackingBlendshapeNames != null)
                used.UnionWith(c.blendshapesSet.faceTrackingBlendshapeNames.Where(n => !string.IsNullOrEmpty(n)));
            return c.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(smr =>
            {
                var mesh = smr.sharedMesh;
                if (mesh == null)
                    return false;
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    string name = mesh.GetBlendShapeName(i);
                    if (used.Contains(name) || FaceTrackingDebugSetFiller.Matches(format, name))
                        return true;
                }
                return false;
            }).ToList();
        }
    }

#if VAP_NDMF
    public class FaceTrackingDebugPlugin : Plugin<FaceTrackingDebugPlugin>
    {
        public override string QualifiedName => "tech.azuki.vrc-prefabs.face-tracking-debug";
        public override string DisplayName => "VAP Face Tracking Debug";

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .Run("Add face tracking debug set", ctx =>
                {
                    string error = FaceTrackingDebugBuild.Process(ctx.AvatarRootObject);
                    if (error != null)
                        throw new System.InvalidOperationException(error);   // stops the build; NDMF shows the message
                });
        }
    }
#endif
}
#endif
