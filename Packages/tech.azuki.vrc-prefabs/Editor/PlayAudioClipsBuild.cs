#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
#if VAP_NDMF
using nadena.dev.ndmf;
#endif
using Object = UnityEngine.Object;

#if VAP_NDMF
[assembly: ExportsPlugin(typeof(VAP.Editor.PlayAudioClipsPlugin))]
#endif

namespace VAP.Editor
{
    internal static class PlayAudioClips
    {
        const string Log = "[VAP VRC Animator Play Audio Clips] ";
        const string TypeName = "VRCAnimatorPlayAudio";
        const string DriverTypeName = "VRCAvatarParameterDriver";

        internal struct Found
        {
            internal string key;
            internal string path;
            internal StateMachineBehaviour behaviour;
            internal StateMachineBehaviour[] siblings;
            internal int max;
        }

        internal static List<Found> Find(AnimatorController controller)
        {
            var result = new List<Found>();
            if (controller == null)
                return result;
            var layers = controller.layers;
            var seen = new HashSet<AnimatorStateMachine>();
            for (int l = 0; l < layers.Length; l++)
                Walk(layers[l].stateMachine, l + "|", layers[l].name, result, seen);
            return result;
        }

        static void Walk(AnimatorStateMachine machine, string prefix, string path, List<Found> result, HashSet<AnimatorStateMachine> seen)
        {
            if (machine == null || !seen.Add(machine))
                return;
            Add(machine.behaviours, prefix, path, result);
            foreach (var child in machine.states)
                if (child.state != null)
                    Add(child.state.behaviours, prefix, path + "/" + child.state.name, result);
            foreach (var child in machine.stateMachines)
                if (child.stateMachine != null)
                    Walk(child.stateMachine, prefix, path + "/" + child.stateMachine.name, result, seen);
        }

        static void Add(StateMachineBehaviour[] behaviours, string prefix, string path, List<Found> result)
        {
            int n = 0;
            foreach (var b in behaviours)
                if (b != null && b.GetType().Name == TypeName)
                {
                    var found = new Found { key = prefix + path + "#" + n++, path = path, behaviour = b, siblings = behaviours };
                    found.max = MaxClips(found);
                    result.Add(found);
                }
        }

        internal static List<AudioClip> Clips(StateMachineBehaviour behaviour)
        {
            var list = new List<AudioClip>();
            using var so = new SerializedObject(behaviour);
            var clips = so.FindProperty("Clips");
            if (clips != null && clips.isArray)
                for (int i = 0; i < clips.arraySize; i++)
                    list.Add(clips.GetArrayElementAtIndex(i).objectReferenceValue as AudioClip);
            return list;
        }

        internal static string SourceName(StateMachineBehaviour behaviour)
        {
            using var so = new SerializedObject(behaviour);
            var source = so.FindProperty("Source")?.objectReferenceValue;
            if (source != null)
                return source.name;
            string path = so.FindProperty("SourcePath")?.stringValue;
            if (string.IsNullOrEmpty(path))
                return "No Audio Source";
            int slash = path.LastIndexOf('/');
            return slash < 0 ? path : path.Substring(slash + 1);
        }

        static string ParameterName(StateMachineBehaviour behaviour)
        {
            using var so = new SerializedObject(behaviour);
            return so.FindProperty("ParameterName")?.stringValue;
        }

        static int MaxClips(Found found)
        {
            string parameter = ParameterName(found.behaviour);
            int max = int.MaxValue;
            if (string.IsNullOrEmpty(parameter))
                return max;
            foreach (var sibling in found.siblings)
            {
                if (sibling == null || sibling.GetType().Name != DriverTypeName)
                    continue;
                using var so = new SerializedObject(sibling);
                var parameters = so.FindProperty("parameters");
                if (parameters == null || !parameters.isArray)
                    continue;
                for (int i = 0; i < parameters.arraySize; i++)
                {
                    var p = parameters.GetArrayElementAtIndex(i);
                    if (IsRangeCopy(p, parameter))
                        max = Math.Min(max, Math.Max(1, Mathf.RoundToInt(p.FindPropertyRelative("sourceMax").floatValue - p.FindPropertyRelative("sourceMin").floatValue)));
                }
            }
            return max;
        }

        static bool IsRangeCopy(SerializedProperty p, string parameter) =>
            p.FindPropertyRelative("type").intValue == 3   // Copy
            && p.FindPropertyRelative("name").stringValue == parameter
            && p.FindPropertyRelative("convertRange").boolValue;

        static void SetRange(StateMachineBehaviour driver, string parameter, int count)
        {
            using var so = new SerializedObject(driver);
            var parameters = so.FindProperty("parameters");
            if (parameters == null || !parameters.isArray)
                return;
            for (int i = 0; i < parameters.arraySize; i++)
            {
                var p = parameters.GetArrayElementAtIndex(i);
                if (!IsRangeCopy(p, parameter))
                    continue;
                p.FindPropertyRelative("destMax").floatValue = p.FindPropertyRelative("destMin").floatValue + count;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetClips(StateMachineBehaviour behaviour, List<AudioClip> list)
        {
            using var so = new SerializedObject(behaviour);
            var clips = so.FindProperty("Clips");
            if (clips == null || !clips.isArray)
                return;
            clips.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++)
                clips.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void Process(GameObject avatarRoot, Action<Object> saveAsset)
        {
            var all = avatarRoot.GetComponentsInChildren<VAPPlayAudioClips>(true);
            foreach (var c in all)
            {
                if (c.controller == null)
                {
                    Debug.LogWarning(Log + $"{c.name}: no animator controller selected; skipped.", c);
                    continue;
                }
                if (!(c.controller is AnimatorController controller))
                {
                    Debug.LogWarning(Log + $"{c.name}: \"{c.controller.name}\" isn't an Animator Controller; skipped.", c);
                    continue;
                }
                var copy = Apply(controller, c.entries, "", c, saveAsset);
                if (ExpressionsBuild.Replace(avatarRoot, controller, copy, c) == 0)
                    Debug.LogWarning(Log + $"{c.name}: nothing on the avatar uses \"{controller.name}\", so its clips weren't changed.", c);
            }
            foreach (var c in all)
                Object.DestroyImmediate(c);

            foreach (var f in avatarRoot.GetComponentsInChildren<VAPFullController>(true))
                ApplyFullController(f, saveAsset);
            foreach (var d in avatarRoot.GetComponentsInChildren<VAPDescriptorExpressions>(true))
                if (d.Other() == null)
                    ApplyDescriptor(d, saveAsset);
        }

        static void ApplyDescriptor(VAPDescriptorExpressions d, Action<Object> saveAsset)
        {
            if (d.audioClips.Count > 0)
                ControllerCopy.SwapDescriptor(d.Descriptor, controller => Patched(controller, d.audioClips, d, saveAsset));
        }

        static AnimatorController Patched(AnimatorController controller, List<VAPPlayAudioClips.Entry> entries, Component c, Action<Object> saveAsset)
        {
            string prefix = Prefix(controller);
            return entries.Any(e => e.key.StartsWith(prefix)) && Find(controller).Count > 0 ? Apply(controller, entries, prefix, c, saveAsset) : null;
        }

        internal static string Prefix(AnimatorController controller)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(controller, out string guid, out long _);
            return guid + "|";
        }

        static void ApplyFullController(VAPFullController f, Action<Object> saveAsset)
        {
            if (f.audioClips.Count > 0)
                ControllerCopy.SwapFullController(f.wrapped, controller => Patched(controller, f.audioClips, f, saveAsset));
        }

        static AnimatorController Apply(AnimatorController controller, List<VAPPlayAudioClips.Entry> entries, string prefix, Component c, Action<Object> saveAsset)
        {
            var map = new Dictionary<Object, Object>();
            var copy = ControllerCopy.Clone(controller, map);
            foreach (var found in Find(controller))
            {
                var entry = entries.FirstOrDefault(e => e.key == prefix + found.key);
                if (entry == null || !map.TryGetValue(found.behaviour, out var target))
                    continue;
                var clips = entry.clips.Take(found.max).ToList();
                if (clips.Count < entry.clips.Count)
                    Debug.LogWarning(Log + $"{c.name}: {found.path} only has room for {clips.Count} clips; the rest were left out.", c);
                SetClips((StateMachineBehaviour)target, clips);
                string parameter = ParameterName(found.behaviour);
                if (string.IsNullOrEmpty(parameter))
                    continue;
                foreach (var sibling in found.siblings)
                    if (sibling != null && sibling.GetType().Name == DriverTypeName && map.TryGetValue(sibling, out var driver))
                        SetRange((StateMachineBehaviour)driver, parameter, clips.Count);
            }
            foreach (var o in map.Values)
                saveAsset?.Invoke(o);
            return copy;
        }
    }

#if VAP_NDMF
    public class PlayAudioClipsPlugin : Plugin<PlayAudioClipsPlugin>
    {
        public override string QualifiedName => "tech.azuki.vrc-prefabs.play-audio-clips";
        public override string DisplayName => "VAP VRC Animator Play Audio Clips";

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .BeforePlugin("nadena.dev.modular-avatar")
                .BeforePlugin("tech.azuki.vrc-prefabs.expressions")
                .Run("Apply Play Audio clips", ctx =>
                    PlayAudioClips.Process(ctx.AvatarRootObject, o => ctx.AssetSaver.SaveAsset(o)));
        }
    }
#endif
}
#endif
