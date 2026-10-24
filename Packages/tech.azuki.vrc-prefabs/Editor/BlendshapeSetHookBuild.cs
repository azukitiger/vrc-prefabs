#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
#if VAP_NDMF
using nadena.dev.ndmf;
#endif
using Object = UnityEngine.Object;

#if VAP_NDMF
[assembly: ExportsPlugin(typeof(VAP.Editor.BlendshapeSetHookPlugin))]
#endif

namespace VAP.Editor
{
    internal static class BlendshapeSetHookBuild
    {
        const string Log = "[VAP Face Tracking BlendShape Set Hook] ";
        const int MaxFaceTrackingMeshes = 16;
        static readonly (string names, string mults)[] Arrays =
        {
            ("faceTrackingBlendshapeNames", "faceTrackingBlendshapeMultipliers"),
            ("visemeBlendshapeNames", "visemeBlendshapeMultipliers"),
        };

        internal static void Process(GameObject avatarRoot, Action<Object> saveAsset)
        {
            var hooks = avatarRoot.GetComponentsInChildren<VAPBlendshapeSetHook>(true);
            if (hooks.Length == 0)
                return;
            var descriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null)
                Debug.LogWarning(Log + "No VRC Avatar Descriptor on the avatar root; skipped.", avatarRoot);
            else
                foreach (var hook in hooks)
                    Apply(descriptor, hook, saveAsset);

            foreach (var hook in hooks)
                Object.DestroyImmediate(hook);
        }

        static void Apply(VRCAvatarDescriptor descriptor, VAPBlendshapeSetHook hook, Action<Object> saveAsset)
        {
            string target = hook.blendshapesSet?.setName ?? "";
            if (string.IsNullOrEmpty(target))
            {
                Debug.LogWarning(Log + $"{hook.name}: no Target Blendshapes Set; skipped.", hook);
                return;
            }

            using var descriptorSo = new SerializedObject(descriptor);
            var sets = descriptorSo.FindProperty("faceTrackingBlendshapesSetList");
            SerializedProperty targetSet = null;
            for (int i = 0; sets != null && i < sets.arraySize; i++)
            {
                var s = sets.GetArrayElementAtIndex(i);
                if (s.FindPropertyRelative("setName").stringValue == target)
                {
                    targetSet = s;
                    break;
                }
            }
            if (targetSet == null)
            {
                Debug.LogWarning(Log + $"{hook.name}: the avatar has no Blendshapes Set named \"{target}\"; skipped.", hook);
                return;
            }

            string format = FaceTrackingDebugSetFiller.Normalize(hook.nameFormat);
            if (format.Length == 0)
            {
                Debug.LogWarning(Log + $"{hook.name}: no Blendshape Format; skipped.", hook);
                return;
            }
            var meshes = HookMeshes(hook);
            using var hookSo = new SerializedObject(hook);
            var hookSet = hookSo.FindProperty("blendshapesSet");
            FaceTrackingDebugSetFiller.Clear(hookSet);
            FaceTrackingDebugSetFiller.AutoFill(hookSet, format, FaceTrackingDebugSetFiller.BlendshapeNames(meshes));
            hookSo.ApplyModifiedPropertiesWithoutUndo();

            var renames = new Dictionary<string, string>(StringComparer.Ordinal);
            var strips = new Dictionary<string, string>(StringComparer.Ordinal);
            var warnings = new List<string>();
            FaceTrackingDebugSetFiller.EnsureArrays(targetSet);
            foreach (var (namesPath, _) in Arrays)
            {
                var from = hookSet.FindPropertyRelative(namesPath);
                var to = targetSet.FindPropertyRelative(namesPath);
                if (from == null || to == null)
                    continue;
                int n = Math.Min(from.arraySize, to.arraySize);
                for (int i = 0; i < n; i++)
                {
                    string src = from.GetArrayElementAtIndex(i).stringValue;
                    var toName = to.GetArrayElementAtIndex(i);
                    string dst = toName.stringValue;
                    if (string.IsNullOrEmpty(src))
                        continue;
                    if (string.IsNullOrEmpty(dst))
                    {
                        if (StripFormat(format, src) is string plain && plain != src)
                            strips[src] = plain;
                        continue;
                    }
                    if (src == dst)
                        continue;
                    if (renames.TryGetValue(src, out var existing))
                    {
                        if (existing != dst)
                            warnings.Add($"{src} is mapped to more than one key; kept {existing}, not {dst}");
                        continue;
                    }
                    renames[src] = dst;
                }
            }

            // A blendshape mapped to a target name wins over stripping its affix.
            foreach (var src in strips.Keys.Where(renames.ContainsKey).ToList())
                strips.Remove(src);
            int stripCount = strips.Count;
            var all = new Dictionary<string, string>(renames, StringComparer.Ordinal);
            foreach (var kv in strips)
                all[kv.Key] = kv.Value;
            var hooked = new HashSet<string>(renames.Keys, StringComparer.Ordinal);

            int renamed = 0;
            var used = new List<SkinnedMeshRenderer>();
            foreach (var smr in meshes)
            {
                bool hasHooked = hooked.Count > 0 && HasAny(smr.sharedMesh, hooked);
                renamed += Rename(smr, all, warnings, saveAsset);
                if (hasHooked)
                    used.Add(smr);
            }
            AddFaceTrackingMeshes(descriptorSo, used, warnings);
            descriptorSo.ApplyModifiedPropertiesWithoutUndo();

            string msg = $"{hook.name}: renamed {renamed} blendshapes ({renames.Count} to match \"{target}\", " +
                         $"{stripCount} with the format removed where \"{target}\" is empty).";
            if (warnings.Count > 0)
                msg += " " + string.Join("; ", warnings.Take(10)) + (warnings.Count > 10 ? "; ..." : "") + ".";
            if (renamed == 0 || warnings.Count > 0)
                Debug.LogWarning(Log + msg, hook);
            else
                Debug.Log(Log + msg, hook);
        }

        internal static List<SkinnedMeshRenderer> HookMeshes(VAPBlendshapeSetHook hook) =>
            (hook.meshes ?? new SkinnedMeshRenderer[0]).Where(m => m != null && m.sharedMesh != null).Distinct().ToList();

        internal static string StripFormat(string format, string name)
        {
            if (string.IsNullOrEmpty(format) || !FaceTrackingDebugSetFiller.Matches(format, name))
                return null;
            int star = format.IndexOf('*');
            string before = format.Substring(0, star), after = format.Substring(star + 1).Replace("*", "");
            if (name.Length <= before.Length + after.Length)
                return null;
            return name.Substring(before.Length, name.Length - before.Length - after.Length);
        }

        static bool HasAny(Mesh mesh, HashSet<string> names)
        {
            for (int i = 0; i < mesh.blendShapeCount; i++)
                if (names.Contains(mesh.GetBlendShapeName(i)))
                    return true;
            return false;
        }

        static int Rename(SkinnedMeshRenderer smr, Dictionary<string, string> renames, List<string> warnings, Action<Object> saveAsset)
        {
            var mesh = smr.sharedMesh;
            int shapeCount = mesh.blendShapeCount;
            var names = new string[shapeCount];
            for (int i = 0; i < shapeCount; i++)
                names[i] = mesh.GetBlendShapeName(i);

            // New name -> the hooked blendshapes (indices) that become it.
            var groups = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (int i = 0; i < shapeCount; i++)
            {
                if (!renames.TryGetValue(names[i], out var dst))
                    continue;
                if (!groups.TryGetValue(dst, out var list))
                    groups[dst] = list = new List<int>();
                list.Add(i);
            }
            // Blendshapes that keep their name block a group from taking it.
            var kept = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < shapeCount; i++)
                if (!renames.ContainsKey(names[i]))
                    kept.Add(names[i]);
            foreach (var dst in groups.Keys.ToList())
            {
                if (!kept.Contains(dst))
                    continue;
                warnings.Add($"{smr.name} already has a blendshape named {dst}, so " +
                             string.Join(", ", groups[dst].Select(i => names[i])) + " kept their names");
                groups.Remove(dst);
            }
            if (groups.Count == 0)
                return 0;

            var groupOf = new Dictionary<int, string>();
            foreach (var g in groups)
                foreach (int i in g.Value)
                    groupOf[i] = g.Key;

            var copy = Object.Instantiate(mesh);
            copy.name = mesh.name + " (Hooked)";
            copy.ClearBlendShapes();
            int vertexCount = mesh.vertexCount;
            var dv = new Vector3[vertexCount];
            var dn = new Vector3[vertexCount];
            var dt = new Vector3[vertexCount];
            var oldWeights = new float[shapeCount];
            for (int i = 0; i < shapeCount; i++)
                oldWeights[i] = smr.GetBlendShapeWeight(i);
            var newWeights = new List<float>();
            int count = 0;
            for (int i = 0; i < shapeCount; i++)
            {
                if (!groupOf.TryGetValue(i, out var dst))
                {
                    CopyFrames(mesh, i, copy, names[i], dv, dn, dt);
                    newWeights.Add(oldWeights[i]);
                    continue;
                }
                var members = groups[dst];
                if (members[0] != i)
                    continue; // already merged into the first one
                if (members.Count == 1)
                    CopyFrames(mesh, i, copy, dst, dv, dn, dt);
                else
                    MergeFrames(mesh, members, copy, dst);
                newWeights.Add(members.Max(m => oldWeights[m]));
                count += members.Count;
            }
            saveAsset?.Invoke(copy);
            smr.sharedMesh = copy;
            for (int i = 0; i < newWeights.Count; i++)
                smr.SetBlendShapeWeight(i, newWeights[i]);
            return count;
        }

        static void CopyFrames(Mesh mesh, int shape, Mesh copy, string name, Vector3[] dv, Vector3[] dn, Vector3[] dt)
        {
            int frames = mesh.GetBlendShapeFrameCount(shape);
            for (int f = 0; f < frames; f++)
            {
                mesh.GetBlendShapeFrameVertices(shape, f, dv, dn, dt);
                copy.AddBlendShapeFrame(name, mesh.GetBlendShapeFrameWeight(shape, f), dv, dn, dt);
            }
        }

        static void MergeFrames(Mesh mesh, List<int> shapes, Mesh copy, string name)
        {
            int n = mesh.vertexCount;
            var dv = new Vector3[n];
            var dn = new Vector3[n];
            var dt = new Vector3[n];
            int frames = mesh.GetBlendShapeFrameCount(shapes[0]);
            bool sameFrames = shapes.All(s => mesh.GetBlendShapeFrameCount(s) == frames) &&
                              Enumerable.Range(0, frames).All(f => shapes.All(s =>
                                  Mathf.Approximately(mesh.GetBlendShapeFrameWeight(s, f), mesh.GetBlendShapeFrameWeight(shapes[0], f))));
            int outFrames = sameFrames ? frames : 1;
            for (int f = 0; f < outFrames; f++)
            {
                var sv = new Vector3[n];
                var sn = new Vector3[n];
                var st = new Vector3[n];
                foreach (int s in shapes)
                {
                    int frame = sameFrames ? f : mesh.GetBlendShapeFrameCount(s) - 1;
                    mesh.GetBlendShapeFrameVertices(s, frame, dv, dn, dt);
                    for (int v = 0; v < n; v++)
                    {
                        sv[v] += dv[v];
                        sn[v] += dn[v];
                        st[v] += dt[v];
                    }
                }
                float weight = sameFrames ? mesh.GetBlendShapeFrameWeight(shapes[0], f) : 100f;
                copy.AddBlendShapeFrame(name, weight, sv, sn, st);
            }
        }

        static void AddFaceTrackingMeshes(SerializedObject descriptorSo, List<SkinnedMeshRenderer> add, List<string> warnings)
        {
            var list = descriptorSo.FindProperty("faceTrackingMeshes");
            if (list == null)
                return;
            var present = new HashSet<Object>();
            for (int i = 0; i < list.arraySize; i++)
                present.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);
            foreach (var smr in add)
            {
                if (present.Contains(smr))
                    continue;
                if (list.arraySize >= MaxFaceTrackingMeshes)
                {
                    warnings.Add($"Face Tracking Meshes is full ({MaxFaceTrackingMeshes}); {smr.name} was not added");
                    continue;
                }
                int at = list.arraySize++;
                list.GetArrayElementAtIndex(at).objectReferenceValue = smr;
                present.Add(smr);
            }
        }
    }

#if VAP_NDMF
    public class BlendshapeSetHookPlugin : Plugin<BlendshapeSetHookPlugin>
    {
        public override string QualifiedName => "tech.azuki.vrc-prefabs.blendshape-set-hook";
        public override string DisplayName => "VAP Face Tracking BlendShape Set Hook";

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .AfterPlugin("tech.azuki.vrc-prefabs.face-tracking-debug")
                .Run("Hook blendshapes onto Blendshapes Sets",
                    ctx => BlendshapeSetHookBuild.Process(ctx.AvatarRootObject, o => ctx.AssetSaver.SaveAsset(o)));
        }
    }
#endif
}
#endif
