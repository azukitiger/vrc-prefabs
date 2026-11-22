#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace VAP.Editor
{
    [InitializeOnLoad]
    public static class PhysBoneGizmos
    {
        enum Mode { Selected, Pinned, All }

        const string Log = "[VAP PhysBone Gizmos] ";
        const string ModeKey = "Azuki.PhysBoneGizmos.Mode";
        static readonly string PinsKey = "Azuki.PhysBoneGizmos.PathPins." + Application.dataPath.GetHashCode();

        static Mode mode;
        static System.Action modeChanged;
        static System.Action pinsChanged;
        static readonly HashSet<string> pins = new HashSet<string>();
        static VRCPhysBone[] cache = new VRCPhysBone[0];
        static readonly HashSet<VRCPhysBone> pinnedCache = new HashSet<VRCPhysBone>();
        static readonly HashSet<VRCPhysBone> selectedCache = new HashSet<VRCPhysBone>();

        static PhysBoneGizmos()
        {
            mode = (Mode)EditorPrefs.GetInt(ModeKey, (int)Mode.Pinned);
            LoadPins();
            RefreshCache();
            EditorApplication.hierarchyChanged += RefreshCache;
            Selection.selectionChanged += RefreshSelected;
            EditorSceneManager.sceneOpened += (s, m) => RefreshCache();
            PrefabStage.prefabStageOpened += s => RefreshCache();
            PrefabStage.prefabStageClosing += s => EditorApplication.delayCall += RefreshCache;
            EditorApplication.delayCall += () =>
            {
                sdkDraw = FindSdkDraw();
                SceneView.RepaintAll();
            };
        }

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

        static void ClearPins()
        {
            pins.Clear();
            pinnedCache.Clear();
            AfterPinChange();
        }

        static void SetMode(Mode m)
        {
            mode = m;
            EditorPrefs.SetInt(ModeKey, (int)m);
            modeChanged?.Invoke();
            SceneView.RepaintAll();
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
            pinsChanged?.Invoke();
            RefreshCache();
            SceneView.RepaintAll();
        }

        static void RefreshCache()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            cache = stage != null && stage.prefabContentsRoot != null
                ? stage.prefabContentsRoot.GetComponentsInChildren<VRCPhysBone>(true)
                : Object.FindObjectsByType<VRCPhysBone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            RefreshSelected();
            pinnedCache.RemoveWhere(p => p == null);
            if (pins.Count == 0)
                return;
            foreach (var pb in cache)
                if (pb != null && !pinnedCache.Contains(pb) && pins.Contains(Id(pb)))
                    pinnedCache.Add(pb);
        }

        static System.Reflection.MethodInfo sdkDraw;

        static System.Reflection.MethodInfo FindSdkDraw()
        {
            foreach (var method in TypeCache.GetMethodsWithAttribute<DrawGizmo>())
            {
                var p = method.GetParameters();
                if (method.IsStatic && method.DeclaringType != typeof(PhysBoneGizmos) && p.Length == 2 &&
                    p[0].ParameterType != typeof(Object) && p[0].ParameterType != typeof(Component) &&
                    p[0].ParameterType.IsAssignableFrom(typeof(VRCPhysBone)))
                    return method;
            }
            Debug.LogWarning(Log + "VRChat SDK gizmos not found.");
            return null;
        }

        static void RefreshSelected()
        {
            selectedCache.Clear();
            var selected = Selection.transforms;
            if (selected.Length > 0)
                foreach (var pb in cache)
                {
                    if (pb == null)
                        continue;
                    var root = pb.rootTransform != null ? pb.rootTransform : pb.transform;
                    foreach (var t in selected)
                        if (t.IsChildOf(root))
                        {
                            selectedCache.Add(pb);
                            break;
                        }
                }
            SceneView.RepaintAll();
        }

        [DrawGizmo(GizmoType.NotInSelectionHierarchy)]
        static void DrawPinned(VRCPhysBone pb, GizmoType type)
        {
            if (sdkDraw == null || !pb.isActiveAndEnabled)
                return;
            if (mode != Mode.All && !selectedCache.Contains(pb) && (mode != Mode.Pinned || !pinnedCache.Contains(pb)))
                return;
            try
            {
                sdkDraw.Invoke(null, new object[] { pb, GizmoType.Selected | GizmoType.Active });
            }
            catch (System.Exception e)
            {
                sdkDraw = null;
                Debug.LogWarning(Log + "Couldn't draw the VRChat SDK gizmos. " + (e.InnerException ?? e).Message);
            }
        }

        const string ToolbarId = "VAP/PhysBoneGizmos/";

        [Overlay(typeof(SceneView), "VAP PhysBone Gizmos", "PhysBone Gizmos", true)]
        class GizmosOverlay : ToolbarOverlay
        {
            public GizmosOverlay() : base(
                ToolbarId + "Selected", ToolbarId + "Pinned", ToolbarId + "All",
                ToolbarId + "Pin", ToolbarId + "Unpin", ToolbarId + "Clear") { }
        }

        abstract class ModeToggle : EditorToolbarToggle
        {
            readonly Mode target;

            protected ModeToggle(Mode target, string label, string tip)
            {
                this.target = target;
                text = label;
                tooltip = tip;
                this.RegisterValueChangedCallback(e =>
                {
                    if (e.newValue)
                        SetMode(target);
                    else
                        SetValueWithoutNotify(mode == target);
                });
                RegisterCallback<AttachToPanelEvent>(_ => modeChanged += Refresh);
                RegisterCallback<DetachFromPanelEvent>(_ => modeChanged -= Refresh);
                Refresh();
            }

            void Refresh() => SetValueWithoutNotify(mode == target);
        }

        [EditorToolbarElement(ToolbarId + "Selected", typeof(SceneView))]
        class SelectedToggle : ModeToggle
        {
            public SelectedToggle() : base(Mode.Selected, "Selected", "Draw PhysBones whose root or bones are selected") { }
        }

        [EditorToolbarElement(ToolbarId + "Pinned", typeof(SceneView))]
        class PinnedToggle : ModeToggle
        {
            public PinnedToggle() : base(Mode.Pinned, "Pinned", "Draw pinned and selected PhysBones") { }
        }

        [EditorToolbarElement(ToolbarId + "All", typeof(SceneView))]
        class AllToggle : ModeToggle
        {
            public AllToggle() : base(Mode.All, "All", "Draw every PhysBone") { }
        }

        abstract class SelectionButton : EditorToolbarButton
        {
            readonly System.Func<bool> enabled;

            protected SelectionButton(string label, string tip, System.Action action, System.Func<bool> enabled)
            {
                this.enabled = enabled;
                text = label;
                tooltip = tip;
                clicked += action;
                RegisterCallback<AttachToPanelEvent>(_ =>
                {
                    Selection.selectionChanged += Refresh;
                    pinsChanged += Refresh;
                    Refresh();
                });
                RegisterCallback<DetachFromPanelEvent>(_ =>
                {
                    Selection.selectionChanged -= Refresh;
                    pinsChanged -= Refresh;
                });
            }

            void Refresh() => SetEnabled(enabled());
        }

        static bool AnySelected() => SelectedPhysBones().GetEnumerator().MoveNext();

        [EditorToolbarElement(ToolbarId + "Pin", typeof(SceneView))]
        class PinButton : SelectionButton
        {
            public PinButton() : base("Pin", "Pin the selected PhysBones", PinSelected, AnySelected) { }
        }

        [EditorToolbarElement(ToolbarId + "Unpin", typeof(SceneView))]
        class UnpinButton : SelectionButton
        {
            public UnpinButton() : base("Unpin", "Unpin the selected PhysBones", UnpinSelected, AnySelected) { }
        }

        [EditorToolbarElement(ToolbarId + "Clear", typeof(SceneView))]
        class ClearButton : SelectionButton
        {
            public ClearButton() : base("Clear", "Clear all pins", ClearPins, () => pins.Count > 0) { }
        }
    }
}
#endif
