#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VAP.Editor
{
    public static class FaceTrackingDebugSetFiller
    {
        public const string DefaultSetName = "Debug";
        public static bool HasFormat(string format) => !string.IsNullOrWhiteSpace(format);

        public static string FormatName(string format, string key)
        {
            return Normalize(format).Replace("*", key);
        }

        public static string Normalize(string format)
        {
            if (!HasFormat(format))
                return "";
            format = format.Trim();
            return format.Contains("*") ? format : "*" + format;
        }

        public static bool Matches(string format, string blendshape)
        {
            format = Normalize(format);
            if (format.Length == 0)
                return false;
            int star = format.IndexOf('*');
            string before = format.Substring(0, star);
            string after = format.Substring(star + 1).Replace("*", "");
            return blendshape.Length > before.Length + after.Length &&
                   blendshape.StartsWith(before, StringComparison.Ordinal) &&
                   blendshape.EndsWith(after, StringComparison.Ordinal);
        }

        const int MaxFaceTrackingMeshes = 16;

        public struct Result
        {
            public bool created;
            public int mapped;
            public int keyCount;
            public List<string> missing;
            public List<string> warnings;
        }

        public static HashSet<string> BlendshapeNames(IEnumerable<SkinnedMeshRenderer> meshes)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            if (meshes == null)
                return names;
            foreach (var smr in meshes)
            {
                if (smr == null || smr.sharedMesh == null)
                    continue;
                var mesh = smr.sharedMesh;
                for (int b = 0; b < mesh.blendShapeCount; b++)
                    names.Add(mesh.GetBlendShapeName(b));
            }
            return names;
        }

        public static void EnsureArrays(SerializedProperty set)
        {
            int keyCount = (int)VRCAvatarDescriptor.FaceTrackingKey.Max;
            int visemeCount = (int)VRC_AvatarDescriptor.Viseme.Count;
            Resize(set.FindPropertyRelative("faceTrackingBlendshapeNames"), keyCount, p => p.stringValue = "");
            Resize(set.FindPropertyRelative("faceTrackingBlendshapeMultipliers"), keyCount, p => p.floatValue = 1f);
            Resize(set.FindPropertyRelative("visemeBlendshapeNames"), visemeCount, p => p.stringValue = "");
            Resize(set.FindPropertyRelative("visemeBlendshapeMultipliers"), visemeCount, p => p.floatValue = 1f);
        }

        public static int AutoFill(SerializedProperty set, string format, HashSet<string> available, List<string> missing = null)
        {
            EnsureArrays(set);
            if (!HasFormat(format))
                return 0;
            var names = set.FindPropertyRelative("faceTrackingBlendshapeNames");
            var mults = set.FindPropertyRelative("faceTrackingBlendshapeMultipliers");
            int filled = 0;
            for (int i = 0; i < names.arraySize; i++)
            {
                var name = names.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(name.stringValue))
                    continue;
                string shape = FormatName(format, ((VRCAvatarDescriptor.FaceTrackingKey)i).ToString());
                if (!available.Contains(shape))
                {
                    missing?.Add(shape);
                    continue;
                }
                name.stringValue = shape;
                mults.GetArrayElementAtIndex(i).floatValue = 1f;
                filled++;
            }
            return filled;
        }

        public static int FillEmpty(SerializedProperty target, SerializedProperty source)
        {
            EnsureArrays(target);
            EnsureArrays(source);
            int filled = 0;
            foreach (var (namesPath, multsPath) in new[] {
                         ("faceTrackingBlendshapeNames", "faceTrackingBlendshapeMultipliers"),
                         ("visemeBlendshapeNames", "visemeBlendshapeMultipliers") })
            {
                var tNames = target.FindPropertyRelative(namesPath);
                var tMults = target.FindPropertyRelative(multsPath);
                var sNames = source.FindPropertyRelative(namesPath);
                var sMults = source.FindPropertyRelative(multsPath);
                if (tNames == null || sNames == null)
                    continue;
                int n = Math.Min(tNames.arraySize, sNames.arraySize);
                for (int i = 0; i < n; i++)
                {
                    var t = tNames.GetArrayElementAtIndex(i);
                    string s = sNames.GetArrayElementAtIndex(i).stringValue;
                    if (!string.IsNullOrEmpty(t.stringValue) || string.IsNullOrEmpty(s))
                        continue;
                    t.stringValue = s;
                    if (tMults != null && sMults != null && i < tMults.arraySize && i < sMults.arraySize)
                        tMults.GetArrayElementAtIndex(i).floatValue = sMults.GetArrayElementAtIndex(i).floatValue;
                    filled++;
                }
            }
            return filled;
        }

        public static void Clear(SerializedProperty set)
        {
            EnsureArrays(set);
            foreach (var prop in new[] { "faceTrackingBlendshapeNames", "visemeBlendshapeNames" })
            {
                var names = set.FindPropertyRelative(prop);
                for (int i = 0; i < names.arraySize; i++)
                    names.GetArrayElementAtIndex(i).stringValue = "";
            }
        }

        public static Result Apply(VRCAvatarDescriptor descriptor, IEnumerable<SkinnedMeshRenderer> addMeshes,
            VRCAvatarDescriptor.FaceTrackingBlendshapesSet template, string format, bool recordUndo)
        {
            var result = new Result { missing = new List<string>(), warnings = new List<string>() };
            var so = new SerializedObject(descriptor);

            // Face tracking meshes
            var meshList = so.FindProperty("faceTrackingMeshes");
            var allMeshes = new List<SkinnedMeshRenderer>();
            if (meshList != null)
            {
                var present = new HashSet<UnityEngine.Object>();
                for (int i = 0; i < meshList.arraySize; i++)
                {
                    var obj = meshList.GetArrayElementAtIndex(i).objectReferenceValue;
                    present.Add(obj);
                    if (obj is SkinnedMeshRenderer existing)
                        allMeshes.Add(existing);
                }
                foreach (var smr in addMeshes ?? Array.Empty<SkinnedMeshRenderer>())
                {
                    if (smr == null || present.Contains(smr))
                        continue;
                    if (meshList.arraySize >= MaxFaceTrackingMeshes)
                    {
                        result.warnings.Add($"Face Tracking Meshes is full ({MaxFaceTrackingMeshes}); {smr.name} was not added.");
                        continue;
                    }
                    int at = meshList.arraySize++;
                    meshList.GetArrayElementAtIndex(at).objectReferenceValue = smr;
                    present.Add(smr);
                    allMeshes.Add(smr);
                }
            }

            var sets = so.FindProperty("faceTrackingBlendshapesSetList");
            if (sets == null)
            {
                result.warnings.Add("This VRChat SDK has no Blendshapes Sets on the avatar descriptor.");
                return result;
            }

            // Replace the set with the same name, or add it at the end.
            string setName = template.setName ?? "";
            SerializedProperty set = null;
            for (int i = 0; i < sets.arraySize; i++)
            {
                var s = sets.GetArrayElementAtIndex(i);
                if (s.FindPropertyRelative("setName").stringValue == setName)
                {
                    set = s;
                    break;
                }
            }
            // Copy, so the build never shares data with the scene component.
            var copy = JsonUtility.FromJson<VRCAvatarDescriptor.FaceTrackingBlendshapesSet>(JsonUtility.ToJson(template));
            if (set == null)
            {
                set = sets.GetArrayElementAtIndex(sets.arraySize++);
                set.boxedValue = copy;
                result.created = true;
            }
            else
            {
                set.boxedValue = copy;
            }

            AutoFill(set, format, BlendshapeNames(allMeshes), result.missing);
            var names = set.FindPropertyRelative("faceTrackingBlendshapeNames");
            result.keyCount = names.arraySize;
            for (int i = 0; i < names.arraySize; i++)
                if (!string.IsNullOrEmpty(names.GetArrayElementAtIndex(i).stringValue))
                    result.mapped++;

            var ftEnabled = so.FindProperty("enableFaceTracking");
            var allKeys = so.FindProperty("useAllTrackingKeys");
            if (ftEnabled != null && !ftEnabled.boolValue)
                result.warnings.Add("Face Tracking is off on the avatar descriptor, so the set has no effect.");
            else if (allKeys != null && !allKeys.boolValue)
                result.warnings.Add("\"Use All Tracking Keys\" is off, so only the basic keys are used in game.");

            if (recordUndo)
                so.ApplyModifiedProperties();
            else
                so.ApplyModifiedPropertiesWithoutUndo();
            return result;
        }

        public static string Describe(Result r, string setName, string format, string avatarName)
        {
            string msg = (r.created ? $"Created Blendshapes Set \"{setName}\" on {avatarName}: "
                             : $"Replaced the existing Blendshapes Set \"{setName}\" on {avatarName}: ") +
                         $"{r.mapped} of {r.keyCount} tracking keys mapped.";
            if (r.mapped == 0 && !HasFormat(format))
                msg += "\n\nThe set has no mappings and no Auto-Fill Format is set.";
            else if (r.mapped == 0)
                msg += $"\n\nNo mappings, and no {Normalize(format)} blendshapes were found on the Face Tracking Meshes.";
            else if (r.missing.Count > 0)
                msg += $"\n\nLeft empty, not found on any face tracking mesh ({r.missing.Count}): " +
                       string.Join(", ", r.missing.Take(10)) + (r.missing.Count > 10 ? ", ..." : "");
            foreach (var w in r.warnings)
                msg += "\n\n" + w;
            return msg;
        }

        static void Resize(SerializedProperty array, int size, Action<SerializedProperty> init)
        {
            if (array == null)
                return;
            int old = array.arraySize;
            if (old != size)
                array.arraySize = size;
            for (int i = old; i < size; i++)
                init(array.GetArrayElementAtIndex(i));
        }
    }
}
#endif
