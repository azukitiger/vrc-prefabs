#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using System;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
#if VAP_NDMF
using nadena.dev.ndmf;
#endif
using Object = UnityEngine.Object;

#if VAP_NDMF
[assembly: ExportsPlugin(typeof(VAP.Editor.FloorAdjusterPlugin))]
#endif

namespace VAP.Editor
{
    internal static class FloorAdjusterBuild
    {
        const string Log = "[VAP Floor Adjuster] ";

        internal static Transform FindAvatarRoot(Transform t)
        {
            var descriptor = t.GetComponentInParent<VRCAvatarDescriptor>(true);
            return descriptor != null ? descriptor.transform : null;
        }

        internal static void Process(GameObject avatarRoot, Action<Object> saveAsset)
        {
            var all = avatarRoot.GetComponentsInChildren<VAPFloorAdjuster>(true);
            if (all.Length == 0)
                return;
            var active = avatarRoot.GetComponentsInChildren<VAPFloorAdjuster>(false);
            if (active.Length > 1)
                Debug.LogWarning(Log + "More than one active Floor Adjuster; none were applied.", avatarRoot);
            else if (active.Length == 1)
                Apply(avatarRoot, active[0], saveAsset);

            foreach (var c in all)
                Object.DestroyImmediate(c);
        }

        static void Apply(GameObject avatarRoot, VAPFloorAdjuster adjuster, Action<Object> saveAsset)
        {
            var root = avatarRoot.transform;
            var animator = avatarRoot.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                Debug.LogWarning(Log + "The avatar needs a humanoid Animator; skipped.", avatarRoot);
                return;
            }

            Transform hips = null;
            try { hips = animator.GetBoneTransform(HumanBodyBones.Hips); }
            catch (Exception) { /* thrown when the Avatar is broken */ }
            var armature = hips != null ? hips.parent : null;
            if (armature == null || armature == root)
            {
                Debug.LogWarning(Log + "The hips need a parent object (the armature) under the avatar root; skipped.", avatarRoot);
                return;
            }

            float height = adjuster.height;
            if (Mathf.Abs(height) < 0.00001f)
                return;

            armature.position += root.TransformVector(new Vector3(0f, height, 0f));

            var descriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            if (descriptor != null)
                descriptor.ViewPosition.y += height;

            // The humanoid Avatar stores the rig's rest positions, so rebuild it with the new armature height.
            var oldAvatar = animator.avatar;
            var human = oldAvatar.humanDescription;
            var skeleton = human.skeleton;
            if (skeleton != null && skeleton.Length > 0)
            {
                skeleton[0].name = avatarRoot.name; // the build copy may be renamed, e.g. "(Clone)"
                for (int i = 0; i < skeleton.Length; i++)
                {
                    if (skeleton[i].name == armature.name)
                        skeleton[i].position = armature.localPosition;
                }
                human.skeleton = skeleton;
            }

            var armaturePosition = armature.localPosition;
            var rebuilt = BuildOnCleanCopy(root, armature, human, out string problem);
            if (rebuilt == null || !rebuilt.isValid || !rebuilt.isHuman)
            {
                Debug.LogError(Log + "Couldn't rebuild the humanoid Avatar, so the rig still uses the original one" +
                               (problem != null ? $" ({problem})" : "") + ". Only the armature and view position were moved.", avatarRoot);
                return;
            }
            rebuilt.name = oldAvatar.name + " (Floor Adjusted)";
            saveAsset?.Invoke(rebuilt);
            animator.avatar = rebuilt;
            // Rebinding the Avatar can reset the pose, so set the armature again.
            armature.localPosition = armaturePosition;

            Debug.Log(Log + $"Moved the armature {(height > 0 ? "up" : "down")} {Mathf.Abs(height) * 100f:0.0} cm and rebuilt the rig as \"{rebuilt.name}\".", avatarRoot);
        }

        static Avatar BuildOnCleanCopy(Transform root, Transform armature, HumanDescription human, out string problem)
        {
            problem = null;
            var temp = new GameObject(root.name) { hideFlags = HideFlags.HideAndDontSave };
            temp.SetActive(false);   // the copied components (PhysBones etc.) never wake up
            try
            {
                // Same path from the root as on the avatar (the armature may sit deeper than one level).
                Transform parent = temp.transform;
                string path = UnityEditor.AnimationUtility.CalculateTransformPath(armature.parent, root);
                if (!string.IsNullOrEmpty(path))
                    foreach (string part in path.Split('/'))
                    {
                        var go = new GameObject(part);
                        go.transform.SetParent(parent, false);
                        parent = go.transform;
                    }
                var copy = Object.Instantiate(armature.gameObject, parent, false);
                copy.name = armature.name;
                copy.transform.localPosition = armature.localPosition;
                copy.transform.localRotation = armature.localRotation;
                copy.transform.localScale = armature.localScale;

                // Only skeleton entries that exist in the copy; the root entry is the copy's root.
                var names = new System.Collections.Generic.HashSet<string> { temp.name };
                foreach (var t in temp.GetComponentsInChildren<Transform>(true))
                    names.Add(t.name);
                var skeleton = new System.Collections.Generic.List<SkeletonBone>();
                for (int i = 0; i < human.skeleton.Length; i++)
                {
                    var bone = human.skeleton[i];
                    if (i == 0)
                        bone.name = temp.name;
                    if (names.Contains(bone.name))
                        skeleton.Add(bone);
                }
                human.skeleton = skeleton.ToArray();

                var missing = System.Linq.Enumerable.FirstOrDefault(human.human, h => !names.Contains(h.boneName));
                if (!string.IsNullOrEmpty(missing.boneName))
                {
                    problem = $"bone \"{missing.boneName}\" ({missing.humanName}) isn't under the armature";
                    return null;
                }
                var avatar = AvatarBuilder.BuildHumanAvatar(temp, human);
                if (avatar == null || !avatar.isValid || !avatar.isHuman)
                    problem = "Unity's AvatarBuilder rejected it; see the Console for its message";
                return avatar;
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }
    }

#if VAP_NDMF
    public class FloorAdjusterPlugin : Plugin<FloorAdjusterPlugin>
    {
        public override string QualifiedName => "tech.azuki.vrc-prefabs.floor-adjuster";
        public override string DisplayName => "VAP Floor Adjuster";

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .Run("Adjust floor height", ctx => FloorAdjusterBuild.Process(ctx.AvatarRootObject, o => ctx.AssetSaver.SaveAsset(o)));
        }
    }
#endif
}
#endif
