#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VAP.Editor
{
    internal class BlendShapeRemap : AssetPostprocessor
    {
        const string WeightsPath = "m_BlendShapeWeights";
        const string SizePath = "m_BlendShapeWeights.Array.size";

        static readonly Dictionary<string, Dictionary<string, string[]>> before = new Dictionary<string, Dictionary<string, string[]>>();

        void OnPreprocessModel()
        {
            var names = new Dictionary<string, string[]>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                if (o is Mesh m && m.blendShapeCount > 0 && !names.ContainsKey(m.name))
                    names[m.name] = Blendshapes.Names(m);
            if (names.Count > 0)
                before[assetPath] = names;
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            var jobs = new Dictionary<string, Dictionary<string, string[]>>();
            foreach (var path in imported)
                if (before.TryGetValue(path, out var names))
                {
                    jobs[path] = names;
                    before.Remove(path);
                }
            if (jobs.Count > 0)
                EditorApplication.delayCall += () => Apply(jobs);
        }

        static void Apply(Dictionary<string, Dictionary<string, string[]>> jobs)
        {
            if (!jobs.Keys.Any(path => Changed(path, jobs[path])))
                return;

            var prefabs = AssetDatabase.FindAssets("t:Prefab")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => AssetDatabase.GetDependencies(p, true).Any(jobs.ContainsKey))
                .ToList();
            foreach (var p in prefabs)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (root != null)
                    root.GetComponentsInChildren<VAPSkinnedMeshRenderer>(true);
            }

            var targets = Resources.FindObjectsOfTypeAll<VAPSkinnedMeshRenderer>()
                .Where(w => w != null && w.wrapped != null && w.wrapped.sharedMesh != null)
                .OrderBy(w => Depth(w.wrapped))
                .ToList();
            var dirtyAssets = new HashSet<string>();
            int fixedCount = 0;
            foreach (var w in targets)
            {
                var smr = w.wrapped;
                string path = AssetDatabase.GetAssetPath(smr.sharedMesh);
                if (!jobs.TryGetValue(path, out var meshes) || !meshes.TryGetValue(smr.sharedMesh.name, out var old))
                    continue;
                if (!Remap(smr, old))
                    continue;
                fixedCount++;
                if (EditorUtility.IsPersistent(smr))
                    dirtyAssets.Add(AssetDatabase.GetAssetPath(smr));
            }
            foreach (var p in dirtyAssets)
                AssetDatabase.SaveAssetIfDirty(AssetDatabase.GUIDFromAssetPath(p));
            if (fixedCount > 0)
                Debug.Log($"[VAP Skinned Mesh Renderer] Kept blendshape values by name on {fixedCount} renderer(s) after the mesh changed.");
        }

        static bool Changed(string path, Dictionary<string, string[]> old)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Mesh m && old.TryGetValue(m.name, out var names) && !names.SequenceEqual(Blendshapes.Names(m)))
                    return true;
            return false;
        }

        static int Depth(Object o)
        {
            int depth = 0;
            for (var s = PrefabUtility.GetCorrespondingObjectFromSource(o); s != null; s = PrefabUtility.GetCorrespondingObjectFromSource(s))
                depth++;
            return depth;
        }

        static bool Remap(SkinnedMeshRenderer smr, string[] old)
        {
            var current = Blendshapes.Names(smr.sharedMesh);
            if (old.SequenceEqual(current))
                return false;
            var oldIndex = new Dictionary<string, int>();
            for (int i = 0; i < old.Length; i++)
                if (!oldIndex.ContainsKey(old[i]))
                    oldIndex[old[i]] = i;

            using var so = new SerializedObject(smr);
            var weights = so.FindProperty(WeightsPath);
            bool instance = PrefabUtility.IsPartOfPrefabInstance(smr);
            bool sizeOverridden = instance && so.FindProperty(SizePath).prefabOverride;
            int size = weights.arraySize;
            var values = new float[size];
            var overridden = new bool[size];
            for (int i = 0; i < size; i++)
            {
                var e = weights.GetArrayElementAtIndex(i);
                values[i] = e.floatValue;
                overridden[i] = instance && e.prefabOverride;
            }

            if (!EditorUtility.IsPersistent(smr))
                Undo.RecordObject(smr, "Keep BlendShape Values");
            weights.arraySize = Mathf.Max(size, current.Length);
            var revert = new List<int>();
            for (int j = 0; j < current.Length; j++)
            {
                bool had = oldIndex.TryGetValue(current[j], out int i) && i < size;
                if (instance && !(had && overridden[i]))
                {
                    revert.Add(j);
                    continue;
                }
                weights.GetArrayElementAtIndex(j).floatValue = had ? values[i] : 0f;
            }
            if (instance)
                for (int j = current.Length; j < weights.arraySize; j++)
                    revert.Add(j);
            if (EditorUtility.IsPersistent(smr))
                so.ApplyModifiedPropertiesWithoutUndo();
            else
                so.ApplyModifiedProperties();

            if (instance)
            {
                so.Update();
                foreach (int j in revert)
                {
                    var e = so.FindProperty(WeightsPath).GetArrayElementAtIndex(j);
                    if (e.prefabOverride)
                        PrefabUtility.RevertPropertyOverride(e, InteractionMode.AutomatedAction);
                }
                var sizeProp = so.FindProperty(SizePath);
                if (!sizeOverridden && sizeProp.prefabOverride)
                    PrefabUtility.RevertPropertyOverride(sizeProp, InteractionMode.AutomatedAction);
            }
            EditorUtility.SetDirty(smr);
            return true;
        }
    }
}
#endif
