#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace VAP.Editor
{
    // Draws PhysBone chains in the Scene view even when they aren't selected.
    [InitializeOnLoad]
    public static class PhysBoneGizmos
    {
        enum Mode { Off, Pinned, All }

        const string MenuRoot = "Tools/VAP/PhysBone Gizmos/";
        const string Log = "[VAP PhysBone Gizmos] ";
        const string ModeKey = "Azuki.PhysBoneGizmos.Mode";
        const string DrawSelectedKey = "Azuki.PhysBoneGizmos.DrawWhenSelected";
        static readonly string PinsKey = "Azuki.PhysBoneGizmos.PathPins." + Application.dataPath.GetHashCode();

        static readonly Color LineColor = new Color(1f, 0.85f, 0.2f, 0.9f);
        static readonly Color RadiusColor = new Color(0.3f, 0.9f, 1f, 0.6f);
        static readonly Color EndColor = new Color(1f, 0.5f, 0.2f, 0.9f);

        static Mode mode;
        static bool drawWhenSelected;
        static readonly HashSet<string> pins = new HashSet<string>();
        static VRCPhysBone[] cache = new VRCPhysBone[0];
        static readonly HashSet<VRCPhysBone> pinnedCache = new HashSet<VRCPhysBone>();
        static readonly List<(Transform t, int depth, bool leaf)> nodes = new List<(Transform, int, bool)>();

        static PhysBoneGizmos()
        {
            mode = (Mode)EditorPrefs.GetInt(ModeKey, (int)Mode.Pinned);
            drawWhenSelected = EditorPrefs.GetBool(DrawSelectedKey, true);
            LoadPins();
            RefreshCache();
            EditorApplication.hierarchyChanged += RefreshCache;
            EditorSceneManager.sceneOpened += (s, m) => RefreshCache();
            PrefabStage.prefabStageOpened += s => RefreshCache();
            PrefabStage.prefabStageClosing += s => EditorApplication.delayCall += RefreshCache;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        [MenuItem(MenuRoot + "Off", false, 0)] static void SetOff() => SetMode(Mode.Off);
        [MenuItem(MenuRoot + "Pinned Only", false, 1)] static void SetPinned() => SetMode(Mode.Pinned);
        [MenuItem(MenuRoot + "All", false, 2)] static void SetAll() => SetMode(Mode.All);

        [MenuItem(MenuRoot + "Off", true)] static bool ValidateOff() => Checks();
        [MenuItem(MenuRoot + "Pinned Only", true)] static bool ValidatePinned() => Checks();
        [MenuItem(MenuRoot + "All", true)] static bool ValidateAll() => Checks();

        [MenuItem(MenuRoot + "Pin Selected", false, 20)]
        static void PinSelected()
        {
            int n = 0;
            foreach (var pb in SelectedPhysBones())
            {
                pins.Add(Id(pb));
                pinnedCache.Add(pb);
                n++;
            }
            AfterPinChange();
            Debug.Log(Log + $"Pinned {n} PhysBone(s). Total pinned: {pins.Count}.");
        }

        [MenuItem(MenuRoot + "Unpin Selected", false, 21)]
        static void UnpinSelected()
        {
            int n = 0;
            foreach (var pb in SelectedPhysBones())
            {
                pinnedCache.Remove(pb);
                if (pins.Remove(Id(pb)))
                    n++;
            }
            AfterPinChange();
            Debug.Log(Log + $"Unpinned {n} PhysBone(s). Total pinned: {pins.Count}.");
        }

        [MenuItem(MenuRoot + "Clear Pins", false, 22)]
        static void ClearPins()
        {
            pins.Clear();
            pinnedCache.Clear();
            AfterPinChange();
        }

        const string ObjectMenu = "GameObject/VAP/PhysBone Gizmos/";

        [MenuItem(ObjectMenu + "Pin", false, 20)]
        static void ObjectPin() => PinSelected();

        [MenuItem(ObjectMenu + "Unpin", false, 21)]
        static void ObjectUnpin() => UnpinSelected();

        [MenuItem(ObjectMenu + "Clear Pins", false, 22)]
        static void ObjectClear() => ClearPins();

        [MenuItem(ObjectMenu + "Pin", true)]
        [MenuItem(ObjectMenu + "Unpin", true)]
        static bool ValidateObject()
        {
            foreach (var go in Selection.gameObjects)
                if (go.GetComponentInChildren<VRCPhysBone>(true) != null)
                    return true;
            return false;
        }

        [MenuItem(MenuRoot + "Also Draw When Selected", false, 30)]
        static void ToggleDrawSelected()
        {
            drawWhenSelected = !drawWhenSelected;
            EditorPrefs.SetBool(DrawSelectedKey, drawWhenSelected);
            SceneView.RepaintAll();
        }

        [MenuItem(MenuRoot + "Also Draw When Selected", true)]
        static bool ValidateDrawSelected()
        {
            Menu.SetChecked(MenuRoot + "Also Draw When Selected", drawWhenSelected);
            return true;
        }

        [MenuItem("CONTEXT/VRCPhysBone/Toggle Always-On Gizmo")]
        static void TogglePin(MenuCommand cmd)
        {
            var pb = (VRCPhysBone)cmd.context;
            string id = Id(pb);
            bool pinned = !pins.Remove(id);
            if (pinned)
            {
                pins.Add(id);
                pinnedCache.Add(pb);
            }
            else
                pinnedCache.Remove(pb);
            AfterPinChange();
        }

        static void SetMode(Mode m)
        {
            mode = m;
            EditorPrefs.SetInt(ModeKey, (int)m);
            SceneView.RepaintAll();
        }

        static bool Checks()
        {
            Menu.SetChecked(MenuRoot + "Off", mode == Mode.Off);
            Menu.SetChecked(MenuRoot + "Pinned Only", mode == Mode.Pinned);
            Menu.SetChecked(MenuRoot + "All", mode == Mode.All);
            return true;
        }

        static IEnumerable<VRCPhysBone> SelectedPhysBones()
        {
            var seen = new HashSet<VRCPhysBone>();
            foreach (var go in Selection.gameObjects)
                foreach (var pb in go.GetComponentsInChildren<VRCPhysBone>(true))
                    if (seen.Add(pb))
                        yield return pb;
        }

        static string Path(Transform t)
        {
            var s = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                s = t.name + "/" + s;
            }
            return s;
        }

        // Pins are saved by scene, hierarchy path and component index, so they survive reloads.
        static string Id(VRCPhysBone pb)
        {
            int index = System.Array.IndexOf(pb.GetComponents<VRCPhysBone>(), pb);
            return pb.gameObject.scene.name + ":" + Path(pb.transform) + "#" + index;
        }

        static void LoadPins()
        {
            pins.Clear();
            foreach (var s in EditorPrefs.GetString(PinsKey, "").Split('\n'))
                if (!string.IsNullOrEmpty(s))
                    pins.Add(s);
        }

        static void AfterPinChange()
        {
            EditorPrefs.SetString(PinsKey, string.Join("\n", pins));
            if (mode == Mode.Off)
                SetMode(Mode.Pinned);
            RefreshCache();
            SceneView.RepaintAll();
        }

        static void RefreshCache()
        {
            // Prefab Mode objects live in their own stage, which FindObjectsByType doesn't search.
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            cache = stage != null && stage.prefabContentsRoot != null
                ? stage.prefabContentsRoot.GetComponentsInChildren<VRCPhysBone>(true)
                : Object.FindObjectsByType<VRCPhysBone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            pinnedCache.RemoveWhere(p => p == null);
            if (pins.Count == 0)
                return;
            foreach (var pb in cache)
                if (pb != null && !pinnedCache.Contains(pb) && pins.Contains(Id(pb)))
                    pinnedCache.Add(pb);
        }

        static void OnSceneGUI(SceneView view)
        {
            if (mode == Mode.Off || Event.current.type != EventType.Repaint || view.camera == null)
                return;
            var camForward = view.camera.transform.forward;
            var oldZTest = Handles.zTest;
            var oldColor = Handles.color;
            Handles.zTest = CompareFunction.Always;
            foreach (var pb in cache)
            {
                if (pb == null || !pb.isActiveAndEnabled)
                    continue;
                if (mode == Mode.Pinned && !pinnedCache.Contains(pb))
                    continue;
                // VRChat's own gizmo already shows the selected one.
                if (!drawWhenSelected && Selection.Contains(pb.gameObject))
                    continue;
                Draw(pb, camForward);
            }
            Handles.zTest = oldZTest;
            Handles.color = oldColor;
        }

        static Transform Root(VRCPhysBone pb) => pb.rootTransform != null ? pb.rootTransform : pb.transform;

        static bool HasEndpoint(VRCPhysBone pb) => pb.endpointPosition != Vector3.zero;

        // Fills nodes with the chain and returns its depth, counting the endpoint.
        static int CollectChain(VRCPhysBone pb)
        {
            nodes.Clear();
            int maxDepth = Collect(Root(pb), 0, pb.ignoreTransforms);
            return HasEndpoint(pb) ? maxDepth + 1 : maxDepth;
        }

        static void Draw(VRCPhysBone pb, Vector3 camForward)
        {
            var root = Root(pb);
            int maxDepth = CollectChain(pb);
            bool hasEnd = HasEndpoint(pb);
            Handles.color = LineColor;
            Handles.Label(root.position, pb.name);

            foreach (var (t, depth, leaf) in nodes)
            {
                if (t != root && t.parent != null)
                {
                    Handles.color = LineColor;
                    Handles.DrawLine(t.parent.position, t.position);
                }
                DrawJoint(t.position, Radius(pb, depth, maxDepth, Scale(t)), camForward);

                if (hasEnd && leaf)
                {
                    var end = t.TransformPoint(pb.endpointPosition);
                    Handles.color = EndColor;
                    Handles.DrawLine(t.position, end);
                    DrawJoint(end, Radius(pb, depth + 1, maxDepth, Scale(t)), camForward);
                }
            }
        }

        static int Collect(Transform t, int depth, List<Transform> ignore)
        {
            int index = nodes.Count;
            nodes.Add((t, depth, true));
            int max = depth;
            foreach (Transform child in t)
            {
                if (ignore != null && ignore.Contains(child))
                    continue;
                nodes[index] = (t, depth, false);
                max = Mathf.Max(max, Collect(child, depth + 1, ignore));
            }
            return max;
        }

        // Close to VRChat's own gizmo: radius * curve * bone scale.
        static float Radius(VRCPhysBone pb, int depth, int maxDepth, float scale)
        {
            float r = pb.radius;
            if (pb.radiusCurve != null && pb.radiusCurve.length > 0)
                r *= pb.radiusCurve.Evaluate(maxDepth > 0 ? (float)depth / maxDepth : 0f);
            return r * scale;
        }

        static void DrawJoint(Vector3 pos, float radius, Vector3 camForward)
        {
            // The dot shows the joint even when its radius is zero.
            Handles.color = RadiusColor;
            float size = HandleUtility.GetHandleSize(pos) * 0.025f;
            Handles.DotHandleCap(0, pos, Quaternion.LookRotation(camForward), size, EventType.Repaint);
            if (radius > 0.0001f)
                Handles.DrawWireDisc(pos, camForward, radius);
        }

        static float Scale(Transform t)
        {
            var s = t.lossyScale;
            return Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        }
    }
}
#endif
