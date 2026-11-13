#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
#if VAP_NDMF
using nadena.dev.ndmf;
#endif

#if VAP_NDMF
[assembly: ExportsPlugin(typeof(VAP.Editor.ParameterBehaviorsPlugin))]
#endif

namespace VAP.Editor
{
    internal static class ParameterBehaviorsBuild
    {
        const int MaxParameterBehaviors = 128;
        const string Log = "[VAP Parameter Behaviors] ";

        internal static void Process(GameObject avatarRoot)
        {
            var components = avatarRoot.GetComponentsInChildren<VAPParameterBehaviors>(true);
            if (components.Length == 0)
                return;

            var descriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null)
            {
                Debug.LogWarning(Log + "No VRC Avatar Descriptor on the avatar root; skipped.", avatarRoot);
            }
            else
            {
                if (descriptor.faceTrackingParameterBehaviors == null)
                    descriptor.faceTrackingParameterBehaviors = new List<VRCAvatarDescriptor.FaceTrackingParameterBehavior>();
                var target = descriptor.faceTrackingParameterBehaviors;
                var sets = ReadSets(descriptor);

                var ftShapes = FaceTrackingBlendshapes(descriptor);
                int added = 0, dropped = 0, fromCorrectives = 0;
                void Add(VRCAvatarDescriptor.FaceTrackingParameterBehavior behavior)
                {
                    if (target.Count >= MaxParameterBehaviors)
                    {
                        dropped++;
                        return;
                    }
                    target.Add(behavior);
                    added++;
                }

                var own = new List<(VAPParameterBehaviors c, List<VRCAvatarDescriptor.FaceTrackingParameterBehavior> behaviors)>();
                foreach (var c in components)
                {
                    var behaviors = Behaviors(c, sets, true);
                    own.Add((c, behaviors));
                    foreach (var behavior in behaviors)
                        Add(behavior);
                }

                var applied = AppliedBlendshapes(target);
                foreach (var (c, behaviors) in own)
                    foreach (var behavior in Correctives(c, sets, ftShapes, applied, Limits(behaviors)))
                    {
                        Add(behavior);
                        fromCorrectives++;
                    }

                Debug.Log(Log + $"Added {added} Parameter Behaviors to {avatarRoot.name} ({fromCorrectives} correctives, {target.Count} total).", avatarRoot);
                if (dropped > 0)
                    Debug.LogWarning(Log + $"The descriptor is limited to {MaxParameterBehaviors} Parameter Behaviors; {dropped} were not added.", avatarRoot);
                if (!descriptor.enableFaceTracking)
                    Debug.LogWarning(Log + "Face Tracking is off on the avatar descriptor, so these behaviors have no effect.", avatarRoot);
            }

            foreach (var c in components)
                Object.DestroyImmediate(c);
        }

        [System.Serializable]
        class WeightPatch
        {
            public string weightBlendSetName;
        }

        [System.Serializable]
        class ModeProbe
        {
            public int targetMode;
        }

        internal static HashSet<string> FaceTrackingBlendshapes(VRCAvatarDescriptor descriptor)
        {
            HashSet<string> result = null;
            using (var so = new SerializedObject(descriptor))
            {
                var meshes = so.FindProperty("faceTrackingMeshes");
                if (meshes == null)
                    return null;
                for (int i = 0; i < meshes.arraySize; i++)
                {
                    var mesh = (meshes.GetArrayElementAtIndex(i).objectReferenceValue as SkinnedMeshRenderer)?.sharedMesh;
                    if (mesh == null)
                        continue;
                    result ??= new HashSet<string>();
                    result.UnionWith(Blendshapes.Names(mesh));
                }
            }
            return result;
        }

        internal static List<(string shape, string expression)> FindCorrectives(string[] setNames, HashSet<string> shapes,
            Dictionary<string, List<(bool max, string expression)>> limits = null)
        {
            var result = new List<(string, string)>();
            if (setNames == null || shapes == null)
                return result;
            var keysOf = new Dictionary<string, List<int>>();
            var order = new List<string>();
            for (int k = 0; k < setNames.Length && k < TrackingKeyGroups.Keys.Length; k++)
            {
                string name = setNames[k];
                if (string.IsNullOrEmpty(name))
                    continue;
                if (!keysOf.TryGetValue(name, out var keys))
                {
                    keysOf[name] = keys = new List<int>();
                    order.Add(name);
                }
                keys.Add(k);
            }
            var found = new HashSet<string>();
            foreach (string a in order)
                foreach (string b in order)
                {
                    if (a == b)
                        continue;
                    string corrective = CorrectiveNames(a, b).FirstOrDefault(shapes.Contains);
                    if (corrective == null || !found.Add(corrective))
                        continue;
                    var products = new List<string>();
                    foreach (int ka in keysOf[a])
                        foreach (int kb in keysOf[b])
                            products.Add($"{Limited(TrackingKeyGroups.Keys[ka], a, limits)} * {Limited(TrackingKeyGroups.Keys[kb], b, limits)}");
                    result.Add((corrective, MaxOf(products)));
                }
            return result;
        }

        static IEnumerable<string> CorrectiveNames(string a, string b)
        {
            yield return a + b + "Corrective";
            yield return a + b + "Morph";
            int n = 0;
            while (n < a.Length && n < b.Length && a[n] == b[n])
                n++;
            while (n > 0 && !(n < b.Length && char.IsUpper(b[n])))
                n--;
            if (n > 0)
                yield return a + b.Substring(n) + "Morph";
        }

        static string Limited(string key, string shape, Dictionary<string, List<(bool max, string expression)>> limits)
        {
            if (limits == null || !limits.TryGetValue(shape, out var list))
                return key;
            foreach (var (max, expression) in list)
                key = $"{(max ? "min" : "max")}({key}, {expression})";
            return key;
        }

        internal static Dictionary<string, List<(bool max, string expression)>> Limits(
            IEnumerable<VRCAvatarDescriptor.FaceTrackingParameterBehavior> behaviors)
        {
            var result = new Dictionary<string, List<(bool, string)>>();
            foreach (var b in behaviors)
            {
                if (b == null)
                    continue;
                var probe = JsonUtility.FromJson<TargetProbe>(JsonUtility.ToJson(b));
                bool max = probe.targetMode == (int)VRCAvatarDescriptor.ParameterBehaviorTargetMode.LimitBlendshapeMax;
                if (!max && probe.targetMode != (int)VRCAvatarDescriptor.ParameterBehaviorTargetMode.LimitBlendshapeMin)
                    continue;
                string expression = InputExpression(probe);
                if (string.IsNullOrEmpty(probe.targetBlendshapeName) || string.IsNullOrWhiteSpace(expression))
                    continue;
                if (!result.TryGetValue(probe.targetBlendshapeName, out var list))
                    result[probe.targetBlendshapeName] = list = new List<(bool, string)>();
                list.Add((max, expression.Trim()));
            }
            return result;
        }

        internal static List<VRCAvatarDescriptor.FaceTrackingParameterBehavior> Behaviors(
            VAPParameterBehaviors c, Dictionary<string, string[]> sets, bool log)
        {
            var result = new List<VRCAvatarDescriptor.FaceTrackingParameterBehavior>();
            if (c.parameterBehaviors != null)
                foreach (var behavior in c.parameterBehaviors)
                    if (behavior != null)
                        result.Add(WithSharedSet(Copy(behavior), c));
            if (c.trackingKeyBehaviors != null)
                foreach (var entry in c.trackingKeyBehaviors)
                    result.AddRange(Expand(entry, sets, c, log));
            return result;
        }

        static string InputExpression(TargetProbe b)
        {
            var mode = (VRCAvatarDescriptor.ParameterBehaviorCombineMode)b.combineMode;
            if (mode == VRCAvatarDescriptor.ParameterBehaviorCombineMode.Custom)
                return b.customExpression;
            var inputs = new List<string>();
            if (b.inputTrackingKeyNames != null)
                for (int i = 0; i < b.inputTrackingKeyNames.Length; i++)
                {
                    string key = b.inputTrackingKeyNames[i];
                    if (string.IsNullOrEmpty(key))
                        continue;
                    bool inverted = b.inputInverted != null && i < b.inputInverted.Length && b.inputInverted[i];
                    inputs.Add(inverted ? $"(1 - {key})" : key);
                }
            if (inputs.Count == 0)
                return null;
            switch (mode)
            {
                case VRCAvatarDescriptor.ParameterBehaviorCombineMode.Add:
                    return string.Join(" + ", inputs);
                case VRCAvatarDescriptor.ParameterBehaviorCombineMode.Multiply:
                    return string.Join(" * ", inputs);
                default:
                    string fn = mode == VRCAvatarDescriptor.ParameterBehaviorCombineMode.Min ? "min" : "max";
                    string expression = inputs[0];
                    for (int i = 1; i < inputs.Count; i++)
                        expression = $"{fn}({expression}, {inputs[i]})";
                    return expression;
            }
        }

        static string MaxOf(List<string> terms)
        {
            string expression = terms[0];
            for (int i = 1; i < terms.Count; i++)
                expression = $"max({expression}, {terms[i]})";
            return expression;
        }

        internal static HashSet<string> AppliedOnAvatar(VRCAvatarDescriptor descriptor)
        {
            var result = AppliedBlendshapes(descriptor.faceTrackingParameterBehaviors);
            foreach (var c in descriptor.GetComponentsInChildren<VAPParameterBehaviors>(true))
                result.UnionWith(AppliedBlendshapes(c.parameterBehaviors));
            return result;
        }

        internal static HashSet<string> AppliedBlendshapes(IEnumerable<VRCAvatarDescriptor.FaceTrackingParameterBehavior> behaviors)
        {
            var result = new HashSet<string>();
            if (behaviors != null)
                foreach (var b in behaviors)
                {
                    if (b == null)
                        continue;
                    var probe = JsonUtility.FromJson<TargetProbe>(JsonUtility.ToJson(b));
                    if (probe.targetMode == 2 && !string.IsNullOrEmpty(probe.targetBlendshapeName))
                        result.Add(probe.targetBlendshapeName);
                }
            return result;
        }

        [System.Serializable]
        class TargetProbe
        {
            public int targetMode;
            public string targetBlendshapeName;
            public int combineMode;
            public string customExpression;
            public string[] inputTrackingKeyNames;
            public bool[] inputInverted;
        }

        [System.Serializable]
        class CorrectivePatch
        {
            public int targetMode = 2;   // Apply to Blendshape
            public string targetBlendshapeName;
            public string weightBlendSetName;
            public int combineMode = 4;   // Custom
            public string customExpression;
        }

        static IEnumerable<VRCAvatarDescriptor.FaceTrackingParameterBehavior> Correctives(
            VAPParameterBehaviors c, Dictionary<string, string[]> sets, HashSet<string> shapes, HashSet<string> applied,
            Dictionary<string, List<(bool max, string expression)>> limits)
        {
            string set = c.CorrectivesSet;
            if (string.IsNullOrEmpty(set))
            {
                yield break;
            }
            if (!sets.TryGetValue(set, out var setNames))
            {
                Debug.LogWarning(Log + $"{c.name}: Auto Correctives: the avatar has no Blendshapes Set named \"{set}\"; skipped.", c);
                yield break;
            }
            foreach (var (shape, expression) in FindCorrectives(setNames, shapes, limits))
            {
                if (applied.Contains(shape))
                    continue;
                if (c.disabledCorrectives != null && c.disabledCorrectives.Contains(shape))
                    continue;
                applied.Add(shape);
                var b = new VRCAvatarDescriptor.FaceTrackingParameterBehavior();
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new CorrectivePatch
                {
                    targetBlendshapeName = shape,
                    weightBlendSetName = set,
                    customExpression = expression,
                }), b);
                yield return b;
            }
        }

        internal static VRCAvatarDescriptor.FaceTrackingParameterBehavior WithSharedSet(
            VRCAvatarDescriptor.FaceTrackingParameterBehavior b, VAPParameterBehaviors c)
        {
            int mode = JsonUtility.FromJson<ModeProbe>(JsonUtility.ToJson(b)).targetMode;
            bool blendshapeMode = mode >= 2 && mode <= 4;
            if (blendshapeMode && !c.perBehaviorSet && !string.IsNullOrEmpty(c.blendshapesSet))
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new WeightPatch { weightBlendSetName = c.blendshapesSet }), b);
            return b;
        }

        internal static VRCAvatarDescriptor.FaceTrackingParameterBehavior Copy(VRCAvatarDescriptor.FaceTrackingParameterBehavior b) =>
            JsonUtility.FromJson<VRCAvatarDescriptor.FaceTrackingParameterBehavior>(JsonUtility.ToJson(b));

        internal static Dictionary<string, string[]> ReadSets(VRCAvatarDescriptor descriptor)
        {
            var result = new Dictionary<string, string[]>();
            using (var so = new SerializedObject(descriptor))
            {
                var list = so.FindProperty("faceTrackingBlendshapesSetList");
                if (list != null)
                    for (int i = 0; i < list.arraySize; i++)
                        AddSet(result, list.GetArrayElementAtIndex(i));
            }
            return result;
        }

        internal static void AddSet(Dictionary<string, string[]> sets, SerializedProperty set)
        {
            string name = set.FindPropertyRelative("setName")?.stringValue;
            var names = set.FindPropertyRelative("faceTrackingBlendshapeNames");
            if (string.IsNullOrEmpty(name) || names == null)
                return;
            var array = new string[names.arraySize];
            for (int k = 0; k < array.Length; k++)
                array[k] = names.GetArrayElementAtIndex(k).stringValue;
            sets[name] = array;
        }

        internal static List<string> BlendshapesFor(string[] setNames, int[] keys)
        {
            var result = new List<string>();
            foreach (int k in keys)
            {
                string shape = k < setNames.Length ? setNames[k] : null;
                if (!string.IsNullOrEmpty(shape) && !result.Contains(shape))
                    result.Add(shape);
            }
            return result;
        }

        [System.Serializable]
        class TargetPatch
        {
            public int targetMode;
            public string outputParameterName = "";
            public string targetBlendSetName = "";
            public string targetBlendshapeName;
            public string weightBlendSetName;
        }

        static IEnumerable<VRCAvatarDescriptor.FaceTrackingParameterBehavior> Expand(
            VAPParameterBehaviors.TrackingKeyBehavior entry, Dictionary<string, string[]> sets, Object context, bool log = true)
        {
            if (entry == null || entry.behavior == null)
                yield break;
            string setName = context is VAPParameterBehaviors owner ? owner.SetFor(entry.blendshapesSet) : entry.blendshapesSet;
            var keys = TrackingKeyGroups.Resolve(entry.trackingKey);
            if (keys == null)
            {
                if (log)
                    Debug.LogWarning(Log + $"Tracking Key behavior has an unknown tracking key \"{entry.trackingKey}\"; skipped.", context);
                yield break;
            }
            if (!sets.TryGetValue(setName ?? "", out var setNames))
            {
                if (log)
                    Debug.LogWarning(Log + $"Tracking Key behavior {entry.trackingKey}: the avatar has no Blendshapes Set named \"{setName}\"; skipped.", context);
                yield break;
            }
            var shapes = BlendshapesFor(setNames, keys);
            if (shapes.Count == 0 && log)
                Debug.LogWarning(Log + $"Tracking Key behavior {entry.trackingKey}: \"{setName}\" has no blendshapes for these keys; skipped.", context);
            var mode = entry.mode == VRCAvatarDescriptor.ParameterBehaviorTargetMode.LimitBlendshapeMin
                ? VRCAvatarDescriptor.ParameterBehaviorTargetMode.LimitBlendshapeMin
                : VRCAvatarDescriptor.ParameterBehaviorTargetMode.LimitBlendshapeMax;
            foreach (string shape in shapes)
            {
                var b = Copy(entry.behavior);
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new TargetPatch
                {
                    targetMode = (int)mode,
                    targetBlendshapeName = shape,
                    weightBlendSetName = setName,
                }), b);
                yield return b;
            }
        }
    }

#if VAP_NDMF
    public class ParameterBehaviorsPlugin : Plugin<ParameterBehaviorsPlugin>
    {
        public override string QualifiedName => "tech.azuki.vrc-prefabs.parameter-behaviors";
        public override string DisplayName => "VAP Parameter Behaviors";

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .AfterPlugin("tech.azuki.vrc-prefabs.face-tracking-debug")
                .AfterPlugin("tech.azuki.vrc-prefabs.blendshape-set-hook")
                .Run("Add face tracking parameter behaviors", ctx => ParameterBehaviorsBuild.Process(ctx.AvatarRootObject));
        }
    }
#endif
}
#endif
