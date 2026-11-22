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
        static Action targetsChanged;
        static readonly List<Component> targets = new List<Component>();
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
            targets.Clear();
            targets.AddRange(cache.Where(selectedCache.Contains).OrderBy(c => Array.IndexOf(Types, c.GetType())).ThenBy(c => c.name));
            targetsChanged?.Invoke();
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

        static string TypeLabel(Component c) =>
            c is VRCPhysBone ? "PhysBone" : c is VRCContactReceiver ? "Receiver" : "Sender";

        static bool IsPinned(Component c) => pinnedCache.Contains(c);

        static void TogglePin(Component c)
        {
            if (c == null)
                return;
            if (pinnedCache.Remove(c))
                pins.Remove(Id(c));
            else
            {
                pinnedCache.Add(c);
                pins.Add(Id(c));
            }
            AfterPinChange();
        }

        static Texture2D Icon(params string[] names)
        {
            foreach (var name in names)
            {
                var icon = EditorGUIUtility.FindTexture(name);
                if (icon != null)
                    return icon;
            }
            return null;
        }

        static void SizeLikeToolbar(VisualElement e)
        {
            e.style.height = 20f;
            e.style.minWidth = 30f;
            e.style.paddingLeft = 8f;
            e.style.paddingRight = 8f;
            e.style.justifyContent = Justify.Center;
            e.style.alignItems = Align.Center;
        }

        static Button IconButton(Texture2D icon, string fallback, string tip, Action action, Color? tint = null)
        {
            var button = new Button(action) { tooltip = tip, style = { width = 22, height = 18, paddingLeft = 1, paddingRight = 1 } };
            if (icon != null)
                button.Add(new Image { image = icon, scaleMode = ScaleMode.ScaleToFit, tintColor = tint ?? Color.white, style = { flexGrow = 1 } });
            else
                button.text = fallback;
            return button;
        }

        static readonly Color PinnedColor = new Color(0.3f, 0.6f, 1f);
        static readonly Color UnpinnedColor = new Color(0.55f, 0.55f, 0.55f);
        static Texture2D pinIcon;

        static Texture2D PinIcon()
        {
            if (pinIcon != null)
                return pinIcon;
            const int size = 32;
            const int samples = 4;
            pinIcon = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < samples; sy++)
                        for (int sx = 0; sx < samples; sx++)
                            if (InPin(x + (sx + 0.5f) / samples, y + (sy + 0.5f) / samples))
                                hits++;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(hits * 255 / (samples * samples)));
                }
            pinIcon.SetPixels32(pixels);
            pinIcon.Apply();
            return pinIcon;
        }

        static bool InPin(float px, float py)
        {
            const float c = 0.7071f;
            float dx = px - 16f, dy = py - 16f;
            float x = 16f + dx * c + dy * c;
            float y = 16f - dx * c + dy * c;
            if ((x - 16f) * (x - 16f) + (y - 23f) * (y - 23f) <= 6f * 6f)
                return true;
            if (x >= 12.5f && x <= 19.5f && y >= 14f && y <= 23f)
                return true;
            if (x >= 9f && x <= 23f && y >= 12f && y <= 15f)
                return true;
            return Mathf.Abs(x - 16f) <= 1f && y >= 2f && y <= 12f;
        }

        const string ToolbarId = "VAP/PhysBoneGizmos/";

        [Overlay(typeof(SceneView), "VAP PhysBone Gizmos", "PhysBone & Contact Gizmos", true)]
        class GizmosOverlay : Overlay, ICreateToolbar
        {
            public IEnumerable<string> toolbarElements => new[]
            {
                ToolbarId + "Selected", ToolbarId + "Pinned", ToolbarId + "All", ToolbarId + "Clear",
            };

            public override VisualElement CreatePanelContent()
            {
                var root = new VisualElement { style = { minWidth = 240 } };
                var modes = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                var group = new VisualElement[] { new SelectedToggle(), new PinnedToggle(), new AllToggle() };
                for (int i = 0; i < group.Length; i++)
                {
                    var st = group[i].style;
                    SizeLikeToolbar(group[i]);
                    float left = i == 0 ? 3f : 0f;
                    float right = i == group.Length - 1 ? 3f : 0f;
                    st.borderTopLeftRadius = left;
                    st.borderBottomLeftRadius = left;
                    st.borderTopRightRadius = right;
                    st.borderBottomRightRadius = right;
                    if (i > 0)
                    {
                        st.marginLeft = 0f;
                        st.borderLeftWidth = 0f;
                    }
                    if (i < group.Length - 1)
                        st.marginRight = 0f;
                    modes.Add(group[i]);
                }
                modes.Add(new VisualElement { style = { flexGrow = 1 } });
                var clear = new ClearButton();
                SizeLikeToolbar(clear);
                clear.style.borderTopLeftRadius = 3f;
                clear.style.borderBottomLeftRadius = 3f;
                clear.style.borderTopRightRadius = 3f;
                clear.style.borderBottomRightRadius = 3f;
                modes.Add(clear);
                root.Add(modes);
                var list = new VisualElement { style = { marginTop = 4 } };
                root.Add(list);

                var properties = Icon("d_UnityEditor.InspectorWindow", "UnityEditor.InspectorWindow");

                void Rebuild()
                {
                    list.Clear();
                    targets.RemoveAll(c => c == null);
                    foreach (var c in targets)
                    {
                        var component = c;
                        bool isPinned = IsPinned(c);
                        var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                        var pin = IconButton(PinIcon(), "Pin", isPinned ? "Unpin" : "Pin", () => TogglePin(component),
                            isPinned ? Color.white : UnpinnedColor);
                        if (isPinned)
                            pin.style.backgroundColor = PinnedColor;
                        row.Add(pin);
                        row.Add(new Label($"{TypeLabel(c)}: {c.name}")
                        {
                            tooltip = Path(c.transform),
                            style =
                            {
                                flexGrow = 1, height = 18, marginLeft = 4, marginTop = 0, marginBottom = 0,
                                paddingTop = 0, paddingBottom = 0, unityTextAlign = TextAnchor.MiddleLeft,
                            },
                        });
                        row.Add(IconButton(properties, "...", "Open in a Properties window", () => EditorUtility.OpenPropertyEditor(component)));
                        list.Add(row);
                    }
                }

                root.RegisterCallback<AttachToPanelEvent>(_ =>
                {
                    targetsChanged += Rebuild;
                    pinsChanged += Rebuild;
                    Rebuild();
                });
                root.RegisterCallback<DetachFromPanelEvent>(_ =>
                {
                    targetsChanged -= Rebuild;
                    pinsChanged -= Rebuild;
                });
                return root;
            }
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

            void Refresh()
            {
                bool on = mode == target;
                SetValueWithoutNotify(on);
                var color = on ? new StyleColor(Color.white) : new StyleColor(StyleKeyword.Null);
                style.color = color;
                this.Query<TextElement>().ForEach(t => t.style.color = color);
            }
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

        [EditorToolbarElement(ToolbarId + "Clear", typeof(SceneView))]
        class ClearButton : EditorToolbarButton
        {
            public ClearButton()
            {
                text = "Clear";
                tooltip = "Clear all pins";
                clicked += ClearPins;
                RegisterCallback<AttachToPanelEvent>(_ =>
                {
                    pinsChanged += Refresh;
                    Refresh();
                });
                RegisterCallback<DetachFromPanelEvent>(_ => pinsChanged -= Refresh);
            }

            void Refresh() => SetEnabled(pins.Count > 0);
        }
    }
}
#endif
