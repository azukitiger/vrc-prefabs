#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Object = UnityEngine.Object;

namespace VAP.Editor
{
    [InitializeOnLoad]
    public static class PhysBoneGizmos
    {
        enum Mode { Selected, Pinned, All }

        const string Log = "[VAP PhysBone & Contact Gizmos] ";
        const string ModeKey = "Azuki.PhysBoneGizmos.Mode";
        static readonly string PinsKey = "Azuki.PhysBoneGizmos.PathPins." + Application.dataPath.GetHashCode();
        static readonly Type[] Types = { typeof(VRCPhysBone), typeof(VRCContactReceiver), typeof(VRCContactSender) };

        static Mode mode;
        static Action modeChanged;
        static Action pinsChanged;
        static readonly HashSet<string> pins = new HashSet<string>();
        static Component[] cache = new Component[0];
        static readonly HashSet<Component> pinnedCache = new HashSet<Component>();
        static readonly HashSet<Component> selectedCache = new HashSet<Component>();
        static readonly Dictionary<Type, MethodInfo> sdkDraw = new Dictionary<Type, MethodInfo>();

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
                FindSdkDraw();
                SceneView.RepaintAll();
            };
        }

        static void PinSelected()
        {
            int n = 0;
            foreach (var c in SelectedComponents())
            {
                pins.Add(Id(c));
                pinnedCache.Add(c);
                n++;
            }
            AfterPinChange();
            Debug.Log(Log + $"Pinned {n}. Total pinned: {pins.Count}.");
        }

        static void UnpinSelected()
        {
            int n = 0;
            foreach (var c in SelectedComponents())
            {
                pinnedCache.Remove(c);
                if (pins.Remove(Id(c)))
                    n++;
            }
            AfterPinChange();
            Debug.Log(Log + $"Unpinned {n}. Total pinned: {pins.Count}.");
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

        static IEnumerable<Component> Find(GameObject go) =>
            Types.SelectMany(t => go.GetComponentsInChildren(t, true));

        static IEnumerable<Component> SelectedComponents() =>
            Selection.gameObjects.SelectMany(Find).Distinct();

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

        static string Id(Component c)
        {
            int index = Array.IndexOf(c.GetComponents(c.GetType()), c);
            string id = c.gameObject.scene.name + ":" + Path(c.transform) + "#" + index;
            return c is VRCPhysBone ? id : c.GetType().Name + "|" + id;
        }

        static Transform Root(Component c)
        {
            var root = c is VRCPhysBoneBase pb ? pb.rootTransform : c is ContactBase contact ? contact.rootTransform : null;
            return root != null ? root : c.transform;
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
                ? Find(stage.prefabContentsRoot).ToArray()
                : Types.SelectMany(t => Object.FindObjectsByType(t, FindObjectsInactive.Include, FindObjectsSortMode.None)).Cast<Component>().ToArray();
            RefreshSelected();
            pinnedCache.RemoveWhere(c => c == null);
            if (pins.Count == 0)
                return;
            foreach (var c in cache)
                if (c != null && !pinnedCache.Contains(c) && pins.Contains(Id(c)))
                    pinnedCache.Add(c);
        }

        static void RefreshSelected()
        {
            selectedCache.Clear();
            var selected = Selection.transforms;
            if (selected.Length > 0)
                foreach (var c in cache)
                {
                    if (c == null)
                        continue;
                    var root = Root(c);
                    if (selected.Any(t => c is VRCPhysBone ? t.IsChildOf(root) : t == root))
                        selectedCache.Add(c);
                }
            SceneView.RepaintAll();
        }

        static void FindSdkDraw()
        {
            sdkDraw.Clear();
            var methods = TypeCache.GetMethodsWithAttribute<DrawGizmo>();
            foreach (var type in Types)
            {
                foreach (var method in methods)
                {
                    var p = method.GetParameters();
                    if (method.IsStatic && method.DeclaringType != typeof(PhysBoneGizmos) && p.Length == 2 &&
                        p[0].ParameterType != typeof(Object) && p[0].ParameterType != typeof(Component) &&
                        p[0].ParameterType != typeof(Behaviour) && p[0].ParameterType != typeof(MonoBehaviour) &&
                        p[0].ParameterType.IsAssignableFrom(type))
                    {
                        sdkDraw[type] = method;
                        break;
                    }
                }
                if (!sdkDraw.ContainsKey(type))
                    Debug.LogWarning(Log + $"VRChat SDK gizmos for {type.Name} not found.");
            }
        }

        static void Draw(Component c)
        {
            if (!(c is Behaviour b) || !b.isActiveAndEnabled || !sdkDraw.TryGetValue(c.GetType(), out var method))
                return;
            if (mode != Mode.All && !selectedCache.Contains(c) && (mode != Mode.Pinned || !pinnedCache.Contains(c)))
                return;
            try
            {
                method.Invoke(null, new object[] { c, GizmoType.Selected | GizmoType.Active });
            }
            catch (Exception e)
            {
                sdkDraw.Remove(c.GetType());
                Debug.LogWarning(Log + $"Couldn't draw the VRChat SDK gizmos for {c.GetType().Name}. " + (e.InnerException ?? e).Message);
            }
        }

        [DrawGizmo(GizmoType.NotInSelectionHierarchy)]
        static void DrawPhysBone(VRCPhysBone c, GizmoType type) => Draw(c);

        [DrawGizmo(GizmoType.NotInSelectionHierarchy)]
        static void DrawReceiver(VRCContactReceiver c, GizmoType type) => Draw(c);

        [DrawGizmo(GizmoType.NotInSelectionHierarchy)]
        static void DrawSender(VRCContactSender c, GizmoType type) => Draw(c);

        const string ToolbarId = "VAP/PhysBoneGizmos/";

        [Overlay(typeof(SceneView), "VAP PhysBone Gizmos", "PhysBone & Contact Gizmos", true)]
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
            public SelectedToggle() : base(Mode.Selected, "Selected", "Draw PhysBones whose root or bones are selected, and contacts whose root is selected") { }
        }

        [EditorToolbarElement(ToolbarId + "Pinned", typeof(SceneView))]
        class PinnedToggle : ModeToggle
        {
            public PinnedToggle() : base(Mode.Pinned, "Pinned", "Draw pinned and selected PhysBones and contacts") { }
        }

        [EditorToolbarElement(ToolbarId + "All", typeof(SceneView))]
        class AllToggle : ModeToggle
        {
            public AllToggle() : base(Mode.All, "All", "Draw every PhysBone and contact") { }
        }

        abstract class SelectionButton : EditorToolbarButton
        {
            readonly Func<bool> enabled;

            protected SelectionButton(string label, string tip, Action action, Func<bool> enabled)
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

        static bool AnySelected() => SelectedComponents().Any();

        [EditorToolbarElement(ToolbarId + "Pin", typeof(SceneView))]
        class PinButton : SelectionButton
        {
            public PinButton() : base("Pin", "Pin the PhysBones and contacts on the selected game objects and their children", PinSelected, AnySelected) { }
        }

        [EditorToolbarElement(ToolbarId + "Unpin", typeof(SceneView))]
        class UnpinButton : SelectionButton
        {
            public UnpinButton() : base("Unpin", "Unpin the PhysBones and contacts on the selected game objects and their children", UnpinSelected, AnySelected) { }
        }

        [EditorToolbarElement(ToolbarId + "Clear", typeof(SceneView))]
        class ClearButton : SelectionButton
        {
            public ClearButton() : base("Clear", "Clear all pins", ClearPins, () => pins.Count > 0) { }
        }
    }
}
#endif
