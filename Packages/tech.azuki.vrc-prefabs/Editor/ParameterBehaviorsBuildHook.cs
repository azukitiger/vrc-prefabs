#if UNITY_EDITOR && VRC_SDK_VRCSDK3
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
        // Same limit the SDK's inspector uses.
        const int MaxParameterBehaviors = 128;
        const string Log = "[VAP Parameter Behaviors] ";

        internal static void Process(GameObject avatarRoot)
        {
            var components = avatarRoot.GetComponentsInChildren<VAPParameterBehaviors>(true);
            var correctiveComponents = avatarRoot.GetComponentsInChildren<VAPCorrectiveBehaviors>(true);
            if (components.Length == 0 && correctiveComponents.Length == 0)
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
                int added = 0, dropped = 0, fromKeys = 0, fromCorrectives = 0;
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

                foreach (var c in components)
                {
                    if (c.parameterBehaviors != null)
                        foreach (var behavior in c.parameterBehaviors)
                            if (behavior != null)
                                Add(WithSharedSet(Copy(behavior), c));   // never share data with the scene component

                    if (c.trackingKeyBehaviors != null)
                        foreach (var entry in c.trackingKeyBehaviors)
                        {
                            foreach (var behavior in Expand(entry, sets, c))
                            {
                                Add(behavior);
                                fromKeys++;
                            }
                        }
                }

                var applied = AppliedBlendshapes(target);
                foreach (var c in correctiveComponents)
                    foreach (var behavior in Correctives(c, sets, ftShapes, applied))
                    {
                        Add(behavior);
                        fromCorrectives++;
                    }

                Debug.Log(Log + $"Added {added} Parameter Behaviors to {avatarRoot.name} ({fromKeys} from Tracking Keys, {fromCorrectives} correctives, {target.Count} total).", avatarRoot);
                if (dropped > 0)
                    Debug.LogWarning(Log + $"The descriptor is limited to {MaxParameterBehaviors} Parameter Behaviors; {dropped} were not added.", avatarRoot);
                if (!descriptor.enableFaceTracking)
                    Debug.LogWarning(Log + "Face Tracking is off on the avatar descriptor, so these behaviors have no effect.", avatarRoot);
            }

            foreach (var c in components)
                Object.DestroyImmediate(c);
            foreach (var c in correctiveComponents)
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
                    for (int k = 0; k < mesh.blendShapeCount; k++)
                        result.Add(mesh.GetBlendShapeName(k));
                }
            }
            return result;
        }

        internal static List<(string shape, string expression)> FindCorrectives(string[] setNames, HashSet<string> shapes)
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
                            products.Add($"{TrackingKeyGroups.Keys[ka]} * {TrackingKeyGroups.Keys[kb]}");
                    result.Add((corrective, MaxOf(products)));
                }
            return result;
        }

        // AB + "Corrective" or "Morph"; a Morph may drop the start B shares with A (TongueDown + TongueLeft = TongueDownLeftMorph).
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
            VAPCorrectiveBehaviors c, Dictionary<string, string[]> sets, HashSet<string> shapes, HashSet<string> applied)
        {
            string set = c.blendshapesSet;
            if (string.IsNullOrEmpty(set))
            {
                Debug.LogWarning(Log + $"{c.name}: Corrective Behaviors has no Blendshapes Set; skipped.", c);
                yield break;
            }
            if (!sets.TryGetValue(set, out var setNames))
            {
                Debug.LogWarning(Log + $"{c.name}: Corrective Behaviors: the avatar has no Blendshapes Set named \"{set}\"; skipped.", c);
                yield break;
            }
            foreach (var (shape, expression) in FindCorrectives(setNames, shapes))
            {
                if (applied.Contains(shape))
                    continue;   // already driven by another behavior
                if (c.disabledCorrectives != null && c.disabledCorrectives.Contains(shape))
                    continue;   // unchecked in the list
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

        // Blendshape behaviors use the component's shared Blendshapes Set as their weight set.
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
            VAPParameterBehaviors.TrackingKeyBehavior entry, Dictionary<string, string[]> sets, Object context)
        {
            if (entry == null || entry.behavior == null)
                yield break;
            string setName = context is VAPParameterBehaviors owner ? owner.SetFor(entry.blendshapesSet) : entry.blendshapesSet;
            var keys = TrackingKeyGroups.Resolve(entry.trackingKey);
            if (keys == null)
            {
                Debug.LogWarning(Log + $"Tracking Key behavior has an unknown tracking key \"{entry.trackingKey}\"; skipped.", context);
                yield break;
            }
            if (!sets.TryGetValue(setName ?? "", out var setNames))
            {
                Debug.LogWarning(Log + $"Tracking Key behavior {entry.trackingKey}: the avatar has no Blendshapes Set named \"{setName}\"; skipped.", context);
                yield break;
            }
            var shapes = BlendshapesFor(setNames, keys);
            if (shapes.Count == 0)
                Debug.LogWarning(Log + $"Tracking Key behavior {entry.trackingKey}: \"{setName}\" has no blendshapes for these keys; skipped.", context);
            var mode = entry.mode == VRCAvatarDescriptor.ParameterBehaviorTargetMode.LimitBlendshapeMin
                ? VRCAvatarDescriptor.ParameterBehaviorTargetMode.LimitBlendshapeMin
                : VRCAvatarDescriptor.ParameterBehaviorTargetMode.LimitBlendshapeMax;
            foreach (string shape in shapes)
            {
                var b = Copy(entry.behavior);
                // Patched through JSON so it works whatever the SDK's field access is.
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
            // Tracking Key behaviors read the Blendshapes Sets, so the VAP sets and hooks go first.
            InPhase(BuildPhase.Transforming)
                .AfterPlugin("tech.azuki.vrc-prefabs.face-tracking-debug")
                .AfterPlugin("tech.azuki.vrc-prefabs.blendshape-set-hook")
                .Run("Add face tracking parameter behaviors", ctx => ParameterBehaviorsBuild.Process(ctx.AvatarRootObject));
        }
    }
#endif
}
#endif
