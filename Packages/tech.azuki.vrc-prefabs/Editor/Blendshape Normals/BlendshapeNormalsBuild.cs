#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if VAP_NDMF
using nadena.dev.ndmf;
#endif
using Object = UnityEngine.Object;

#if VAP_NDMF
[assembly: ExportsPlugin(typeof(VAP.Editor.BlendshapeNormalsPlugin))]
#endif

namespace VAP.Editor
{
    internal static class BlendshapeNormalsBuild
    {
        const string Log = "[VAP Blendshape Normals] ";

        internal static void Process(GameObject avatarRoot, Action<Object> saveAsset)
        {
            var all = avatarRoot.GetComponentsInChildren<VAPBlendshapeNormals>(true);
            if (all.Length == 0)
                return;

            var wanted = new Dictionary<SkinnedMeshRenderer, HashSet<string>>();
            var renderers = avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var c in all)
            {
                var names = c.blendShapes.Where(n => !string.IsNullOrEmpty(n)).ToList();
                foreach (var smr in renderers)
                {
                    if (smr == null || smr.sharedMesh == null || (c.targetRenderer != null && c.targetRenderer != smr))
                        continue;
                    var has = new HashSet<string>(Blendshapes.Names(smr.sharedMesh));
                    var found = names.Where(has.Contains).ToList();
                    if (found.Count == 0)
                        continue;
                    if (!wanted.TryGetValue(smr, out var set))
                        wanted[smr] = set = new HashSet<string>();
                    set.UnionWith(found);
                }
            }

            var done = new Dictionary<(Mesh, string), Mesh>();
            foreach (var pair in wanted)
            {
                var smr = pair.Key;
                var key = (smr.sharedMesh, string.Join("\n", pair.Value.OrderBy(n => n)));
                if (!done.TryGetValue(key, out var mesh))
                {
                    mesh = Recalculate(smr.sharedMesh, pair.Value);
                    saveAsset?.Invoke(mesh);
                    done[key] = mesh;
                }
                smr.sharedMesh = mesh;
            }

            foreach (var c in all)
                Object.DestroyImmediate(c);
        }

        internal static Mesh Recalculate(Mesh source, HashSet<string> names)
        {
            int count = source.vertexCount;
            var positions = source.vertices;
            var normals = source.normals;
            var triangles = source.triangles;
            var group = Groups(positions, normals, out int groupCount);
            var baseNormals = Shade(positions, triangles, group, groupCount);

            var temp = new Mesh { indexFormat = source.indexFormat, vertices = positions, normals = normals, uv = source.uv };
            temp.triangles = triangles;
            temp.RecalculateTangents();
            var baseTangents = temp.tangents;
            var finalNormals = new Vector3[count];

            var mesh = Object.Instantiate(source);
            mesh.name = source.name;
            mesh.ClearBlendShapes();

            var dv = new Vector3[count];
            var dn = new Vector3[count];
            var dt = new Vector3[count];
            var shaped = new Vector3[count];
            for (int s = 0; s < source.blendShapeCount; s++)
            {
                string name = source.GetBlendShapeName(s);
                bool redo = names.Contains(name);
                for (int f = 0; f < source.GetBlendShapeFrameCount(s); f++)
                {
                    source.GetBlendShapeFrameVertices(s, f, dv, dn, dt);
                    if (redo)
                    {
                        for (int i = 0; i < count; i++)
                            shaped[i] = positions[i] + dv[i];
                        var n = Shade(shaped, triangles, group, groupCount);
                        for (int i = 0; i < count; i++)
                        {
                            dn[i] = n[i] - baseNormals[i];
                            finalNormals[i] = (normals[i] + dn[i]).normalized;
                        }
                        temp.vertices = shaped;
                        temp.normals = finalNormals;
                        temp.RecalculateTangents();
                        var t = temp.tangents;
                        for (int i = 0; i < count; i++)
                            dt[i] = (Vector3)(t[i] - baseTangents[i]);
                    }
                    mesh.AddBlendShapeFrame(name, source.GetBlendShapeFrameWeight(s, f), dv, dn, dt);
                }
            }
            Object.DestroyImmediate(temp);
            return mesh;
        }

        static int[] Groups(Vector3[] positions, Vector3[] normals, out int groupCount)
        {
            const float sameNormal = 0.985f;
            var group = new int[positions.Length];
            var byPosition = new Dictionary<Vector3, List<(Vector3, int)>>();
            bool hasNormals = normals != null && normals.Length == positions.Length;
            groupCount = 0;
            for (int i = 0; i < positions.Length; i++)
            {
                var n = hasNormals ? normals[i] : Vector3.zero;
                if (!byPosition.TryGetValue(positions[i], out var found))
                    byPosition[positions[i]] = found = new List<(Vector3, int)>();
                int id = -1;
                foreach (var (other, otherId) in found)
                    if (!hasNormals || Vector3.Dot(n, other) >= sameNormal)
                    {
                        id = otherId;
                        break;
                    }
                if (id < 0)
                {
                    id = groupCount++;
                    found.Add((n, id));
                }
                group[i] = id;
            }
            return group;
        }

        static Vector3[] Shade(Vector3[] positions, int[] triangles, int[] group, int groupCount)
        {
            var sum = new Vector3[groupCount];
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                Vector3 pa = positions[a], pb = positions[b], pc = positions[c];
                var face = Vector3.Cross(pb - pa, pc - pa);
                float area = face.magnitude;
                if (area < 1e-12f)
                    continue;
                face /= area;
                sum[group[a]] += face * Corner(pb - pa, pc - pa);
                sum[group[b]] += face * Corner(pc - pb, pa - pb);
                sum[group[c]] += face * Corner(pa - pc, pb - pc);
            }
            var result = new Vector3[positions.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = sum[group[i]].normalized;
            return result;
        }

        static float Corner(Vector3 u, Vector3 v) => Vector3.Angle(u, v) * Mathf.Deg2Rad;
    }

#if VAP_NDMF
    public class BlendshapeNormalsPlugin : Plugin<BlendshapeNormalsPlugin>
    {
        public override string QualifiedName => "tech.azuki.vrc-prefabs.blendshape-normals";
        public override string DisplayName => "VAP Blendshape Normals";

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .Run("Recalculate blendshape normals", ctx => BlendshapeNormalsBuild.Process(ctx.AvatarRootObject, o => ctx.AssetSaver.SaveAsset(o)));
        }
    }
#endif
}
#endif
