#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
#if VAP_NDMF
using nadena.dev.ndmf;
#endif
using Object = UnityEngine.Object;

#if VAP_NDMF
[assembly: ExportsPlugin(typeof(VAP.Editor.AnimationCombinerPlugin))]
#endif

namespace VAP.Editor
{
    internal static class AnimationCombinerBuild
    {
        const string Log = "[VAP Animation Combiner] ";

        internal static void Process(GameObject avatarRoot, Action<Object> saveAsset)
        {
            var combiners = avatarRoot.GetComponentsInChildren<VAPAnimationCombiner>(true);
            if (combiners.Length == 0)
                return;
            var pairs = new Dictionary<AnimationClip, List<(AnimationClip clip, VAPAnimationCombiner.Mode mode)>>();
            foreach (var c in combiners)
                foreach (var e in c.entries)
                {
                    if (e == null || e.animation == null || e.combine == null)
                    {
                        Debug.LogWarning(Log + $"{c.name}: an entry is missing an animation; skipped.", c);
                        continue;
                    }
                    if (e.animation == e.combine)
                        continue;
                    string problem = AnimationProblem(e.animation) ?? CombineProblem(e.combine);
                    if (problem != null)
                    {
                        Debug.LogWarning(Log + $"{c.name}: {problem} Skipped.", c);
                        continue;
                    }
                    if (!pairs.TryGetValue(e.animation, out var list))
                        pairs[e.animation] = list = new List<(AnimationClip, VAPAnimationCombiner.Mode)>();
                    list.Add((e.combine, e.mode));
                }
            foreach (var c in combiners)
                Object.DestroyImmediate(c);
            if (pairs.Count == 0)
                return;

            var made = new Dictionary<AnimationClip, AnimationClip>();
            AnimationClip Combined(AnimationClip clip)
            {
                if (clip == null || !pairs.TryGetValue(clip, out var adds))
                    return null;
                if (!made.TryGetValue(clip, out var result))
                {
                    made[clip] = result = Combine(clip, adds);
                    saveAsset?.Invoke(result);
                }
                return result;
            }

            var copies = new Dictionary<AnimatorController, AnimatorController>();
            AnimatorController Swap(AnimatorController controller)
            {
                if (!copies.TryGetValue(controller, out var copy))
                    copies[controller] = copy = Replace(controller, Combined, saveAsset);
                return copy;
            }

            ControllerCopy.SwapDescriptor(avatarRoot.GetComponent<VRCAvatarDescriptor>(), Swap);
            foreach (var f in avatarRoot.GetComponentsInChildren<VAPFullController>(true))
                ControllerCopy.SwapFullController(f.wrapped, Swap);

            foreach (var pair in pairs)
            {
                string with = string.Join(", ", pair.Value.Select(a => $"{(a.mode == VAPAnimationCombiner.Mode.Replace ? "replaced by" : "combined with")} \"{a.clip.name}\""));
                if (made.ContainsKey(pair.Key))
                    Debug.Log(Log + $"\"{pair.Key.name}\" {with}.", avatarRoot);
            }
        }

        internal static AnimationClip Combine(AnimationClip animation, List<(AnimationClip clip, VAPAnimationCombiner.Mode mode)> adds)
        {
            var clip = Object.Instantiate(animation);
            clip.name = animation.name;
            float length = animation.length;
            foreach (var (add, mode) in adds)
            {
                if (mode == VAPAnimationCombiner.Mode.Replace)
                    clip.ClearCurves();
                bool single = length <= 0f && add.length > 0f;
                float scale = Compressed(animation, add) && !single ? length / add.length : 1f;
                var bindings = AnimationUtility.GetCurveBindings(add);
                AnimationUtility.SetEditorCurves(clip, bindings, bindings.Select(b =>
                {
                    var curve = AnimationUtility.GetEditorCurve(add, b);
                    return single ? new AnimationCurve(new Keyframe(0f, curve.Evaluate(0f))) : Scaled(curve, scale);
                }).ToArray());
                foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(add))
                {
                    var keys = AnimationUtility.GetObjectReferenceCurve(add, b);
                    AnimationUtility.SetObjectReferenceCurve(clip, b, single
                        ? keys.Take(1).Select(k => new ObjectReferenceKeyframe { time = 0f, value = k.value }).ToArray()
                        : keys.Select(k => new ObjectReferenceKeyframe { time = k.time * scale, value = k.value }).ToArray());
                }
            }
            if (length > 0f && clip.length < length)
                Hold(clip, length);
            return clip;
        }

        static void Hold(AnimationClip clip, float length)
        {
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, b);
                var keys = curve.keys;
                if (keys.Length == 0 || keys[keys.Length - 1].time >= length)
                    continue;
                var last = keys[keys.Length - 1];
                last.outTangent = 0f;
                keys[keys.Length - 1] = last;
                curve.keys = keys.Append(new Keyframe(length, last.value, 0f, 0f)).ToArray();
                AnimationUtility.SetEditorCurve(clip, b, curve);
            }
            foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, b);
                if (keys.Length == 0 || keys[keys.Length - 1].time >= length)
                    continue;
                AnimationUtility.SetObjectReferenceCurve(clip, b, keys.Append(new ObjectReferenceKeyframe { time = length, value = keys[keys.Length - 1].value }).ToArray());
            }
        }

        internal static string AnimationProblem(AnimationClip clip)
        {
            if (clip == null)
                return null;
            var bindings = AnimationUtility.GetCurveBindings(clip).Concat(AnimationUtility.GetObjectReferenceCurveBindings(clip)).ToList();
            if (bindings.Count > 0 && bindings.All(IsParameter))
                return $"{clip.name} only animates animator parameters and can't be combined into.";
            if (bindings.Count == 1 && bindings[0].path.StartsWith("_"))
                return $"{clip.name} is a placeholder clip and can't be combined into.";
            return null;
        }

        internal static string CombineProblem(AnimationClip clip) =>
            clip != null && HasParameters(clip) ? $"{clip.name} animates animator parameters and can't be combined." : null;

        static bool HasParameters(AnimationClip clip) => AnimationUtility.GetCurveBindings(clip).Any(IsParameter);

        static bool IsParameter(EditorCurveBinding b) =>
            !b.isPPtrCurve && b.type == typeof(Animator) && b.path == "" && !HumanCurves.Contains(b.propertyName) && !b.propertyName.Contains("TDOF.");

        static HashSet<string> humanCurves;

        static HashSet<string> HumanCurves
        {
            get
            {
                if (humanCurves != null)
                    return humanCurves;
                humanCurves = new HashSet<string>();
                string[] fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
                foreach (string muscle in HumanTrait.MuscleName)
                {
                    humanCurves.Add(muscle);
                    foreach (string side in new[] { "Left", "Right" })
                        foreach (string finger in fingers)
                            if (muscle.StartsWith(side + " " + finger + " "))
                            {
                                string rest = muscle.Substring(side.Length + finger.Length + 2);
                                int space = rest.IndexOf(' ');
                                humanCurves.Add(space > 0 && char.IsDigit(rest[0])
                                    ? $"{side}Hand.{finger}.{rest.Substring(0, space)} {rest.Substring(space + 1)}"
                                    : $"{side}Hand.{finger}.{rest}");
                            }
                }
                foreach (string goal in new[] { "Root", "Motion", "LeftFoot", "RightFoot", "LeftHand", "RightHand" })
                {
                    foreach (string axis in new[] { "x", "y", "z" })
                        humanCurves.Add($"{goal}T.{axis}");
                    foreach (string axis in new[] { "x", "y", "z", "w" })
                        humanCurves.Add($"{goal}Q.{axis}");
                }
                return humanCurves;
            }
        }

        internal static bool Compressed(AnimationClip animation, AnimationClip add) =>
            animation != null && add != null && add.length > animation.length;

        static AnimationCurve Scaled(AnimationCurve curve, float scale)
        {
            if (curve == null || scale == 1f)
                return curve;
            curve.keys = curve.keys.Select(k =>
            {
                k.time *= scale;
                k.inTangent /= scale;
                k.outTangent /= scale;
                return k;
            }).ToArray();
            return curve;
        }

        static AnimatorController Replace(AnimatorController controller, Func<AnimationClip, AnimationClip> combined, Action<Object> saveAsset)
        {
            var used = Clips(controller);
            if (!used.Any(c => combined(c) != null))
                return null;
            var map = new Dictionary<Object, Object>();
            var copy = ControllerCopy.Clone(controller, map);
            foreach (var o in map.Values)
            {
                if (o is AnimatorState state)
                    state.motion = Swap(state.motion, combined);
                else if (o is BlendTree tree)
                    tree.children = tree.children.Select(c =>
                    {
                        c.motion = Swap(c.motion, combined);
                        return c;
                    }).ToArray();
            }
            foreach (var o in map.Values)
                saveAsset?.Invoke(o);
            return copy;
        }

        static Motion Swap(Motion motion, Func<AnimationClip, AnimationClip> combined) =>
            motion is AnimationClip clip && combined(clip) is AnimationClip result ? result : motion;

        internal static HashSet<AnimationClip> Clips(AnimatorController controller)
        {
            var clips = new HashSet<AnimationClip>();
            var seen = new HashSet<Object>();
            if (controller != null)
                foreach (var layer in controller.layers)
                    Walk(layer.stateMachine, clips, seen);
            return clips;
        }

        static void Walk(AnimatorStateMachine machine, HashSet<AnimationClip> clips, HashSet<Object> seen)
        {
            if (machine == null || !seen.Add(machine))
                return;
            foreach (var child in machine.states)
                if (child.state != null)
                    Walk(child.state.motion, clips, seen);
            foreach (var child in machine.stateMachines)
                Walk(child.stateMachine, clips, seen);
        }

        static void Walk(Motion motion, HashSet<AnimationClip> clips, HashSet<Object> seen)
        {
            if (motion is AnimationClip clip)
                clips.Add(clip);
            else if (motion is BlendTree tree && seen.Add(tree))
                foreach (var child in tree.children)
                    Walk(child.motion, clips, seen);
        }
    }

#if VAP_NDMF
    public class AnimationCombinerPlugin : Plugin<AnimationCombinerPlugin>
    {
        public override string QualifiedName => "tech.azuki.vrc-prefabs.animation-combiner";
        public override string DisplayName => "VAP Animation Combiner";

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .BeforePlugin("nadena.dev.modular-avatar")
                .AfterPlugin("tech.azuki.vrc-prefabs.contact-smoothing")
                .BeforePlugin("tech.azuki.vrc-prefabs.expressions")
                .Run("Combine animations", ctx =>
                    AnimationCombinerBuild.Process(ctx.AvatarRootObject, o => ctx.AssetSaver.SaveAsset(o)));
        }
    }
#endif
}
#endif
