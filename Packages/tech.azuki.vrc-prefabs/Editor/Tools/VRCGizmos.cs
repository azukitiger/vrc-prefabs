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
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Object = UnityEngine.Object;

namespace VAP.Editor {
    [InitializeOnLoad]
    public static class VRCGizmos {
        const string Log = "[VAP VRC Gizmos] ";
        const string SelectedKey = "VAP.VRCGizmos.SelectedOnly";
        const string PinnedKey = "VAP.VRCGizmos.ShowPinned";
        const string AvatarCollidersKey = "VAP.VRCGizmos.AvatarColliders";
        static readonly Color AvatarColliderColor = new Color(1f, 0.6f, 0.2f, 0.9f);
        static bool avatarColliders;
        static Action avatarCollidersChanged;
        static VRCAvatarDescriptor[] descriptors = new VRCAvatarDescriptor[0];
        static readonly string PinsKey = "VAP.VRCGizmos.Pins." + Application.dataPath.GetHashCode();
        static readonly Type[] Types = { typeof(VRCPhysBone), typeof(VRCPhysBoneCollider), typeof(VRCContactReceiver), typeof(VRCContactSender) };

        static bool selectedOnly;
        static bool showPinned;
        static Action modeChanged;
        static Action pinsChanged;
        static Action targetsChanged;
        static readonly List<Component> targets = new List<Component>();
        static readonly HashSet<string> pins = new HashSet<string>();
        static Component[] cache = new Component[0];
        static readonly HashSet<Component> pinnedCache = new HashSet<Component>();
        static readonly HashSet<Component> selectedCache = new HashSet<Component>();
        static readonly Dictionary<Type, MethodInfo> sdkDraw = new Dictionary<Type, MethodInfo>();
        static readonly Dictionary<Type, MethodInfo> selfDraw = new Dictionary<Type, MethodInfo>();

        static VRCGizmos() {
            selectedOnly = EditorPrefs.GetBool(SelectedKey, true);
            showPinned = EditorPrefs.GetBool(PinnedKey, true);
            avatarColliders = EditorPrefs.GetBool(AvatarCollidersKey, false);
            SceneView.duringSceneGui += DrawAvatarColliders;
            EditorApplication.update += InstallHeaderItem;
            EditorApplication.update += WatchSkin;
            LoadPins();
            RefreshCache();
            EditorApplication.hierarchyChanged += RefreshCache;
            Selection.selectionChanged += RefreshSelected;
            EditorSceneManager.sceneOpened += (s, m) => RefreshCache();
            PrefabStage.prefabStageOpened += s => RefreshCache();
            PrefabStage.prefabStageClosing += s => EditorApplication.delayCall += RefreshCache;
            EditorApplication.delayCall += () => {
                FindSdkDraw();
                SceneView.RepaintAll();
            };
        }

        static void ClearPins() {
            pins.Clear();
            pinnedCache.Clear();
            AfterPinChange();
        }

        static void SetSelectedOnly(bool on) {
            selectedOnly = on;
            EditorPrefs.SetBool(SelectedKey, on);
            modeChanged?.Invoke();
            SceneView.RepaintAll();
        }

        static void SetShowPinned(bool on) {
            showPinned = on;
            EditorPrefs.SetBool(PinnedKey, on);
            modeChanged?.Invoke();
            SceneView.RepaintAll();
        }

        static IEnumerable<Component> Find(GameObject go) =>
            Types.SelectMany(t => go.GetComponentsInChildren(t, true));

        static string Path(Transform t) {
            var s = t.name;
            while (t.parent != null) {
                t = t.parent;
                s = t.name + "/" + s;
            }
            return s;
        }

        static string Id(Component c) {
            int index = Array.IndexOf(c.GetComponents(c.GetType()), c);
            string id = c.gameObject.scene.name + ":" + Path(c.transform) + "#" + index;
            return c is VRCPhysBone ? id : c.GetType().Name + "|" + id;
        }

        static Transform Root(Component c) {
            var root = c is VRCPhysBoneBase pb ? pb.rootTransform
                : c is VRCPhysBoneColliderBase collider ? collider.rootTransform
                : c is ContactBase contact ? contact.rootTransform : null;
            return root != null ? root : c.transform;
        }

        static void LoadPins() {
            pins.Clear();
            foreach (var s in EditorPrefs.GetString(PinsKey, "").Split('\n')) {
                if (!string.IsNullOrEmpty(s)) {
                    pins.Add(s);
                }
            }
        }

        static void AfterPinChange() {
            EditorPrefs.SetString(PinsKey, string.Join("\n", pins));
            pinsChanged?.Invoke();
            RefreshCache();
            SceneView.RepaintAll();
        }

        static void RefreshCache() {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            bool inStage = stage != null && stage.prefabContentsRoot != null;
            cache = inStage
                ? Find(stage.prefabContentsRoot).ToArray()
                : Types.SelectMany(t => Object.FindObjectsByType(t, FindObjectsInactive.Include, FindObjectsSortMode.None)).Cast<Component>().ToArray();
            descriptors = inStage
                ? stage.prefabContentsRoot.GetComponentsInChildren<VRCAvatarDescriptor>(true)
                : Object.FindObjectsByType<VRCAvatarDescriptor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            RefreshSelected();
            pinnedCache.RemoveWhere(c => c == null);
            if (pins.Count == 0) {
                return;
            }

            foreach (var c in cache) {
                if (c != null && !pinnedCache.Contains(c) && pins.Contains(Id(c))) {
                    pinnedCache.Add(c);
                }
            }
        }

        static void RefreshSelected() {
            selectedCache.Clear();
            var selected = Selection.transforms;
            if (selected.Length > 0) {
                foreach (var c in cache) {
                    if (c == null) {
                        continue;
                    }

                    var root = Root(c);
                    if (selected.Any(t => t == c.transform || (c is VRCPhysBone ? t.IsChildOf(root) : t == root))) {
                        selectedCache.Add(c);
                    }
                }
            }

            targets.Clear();
            targets.AddRange(cache.Where(selectedCache.Contains).OrderBy(c => Array.IndexOf(Types, c.GetType())).ThenBy(c => c.name));
            targetsChanged?.Invoke();
            SceneView.RepaintAll();
        }

        static void FindSdkDraw() {
            sdkDraw.Clear();
            selfDraw.Clear();
            var methods = TypeCache.GetMethodsWithAttribute<DrawGizmo>();
            foreach (var type in Types) {
                foreach (var method in methods) {
                    var p = method.GetParameters();
                    if (method.IsStatic && method.DeclaringType != typeof(VRCGizmos) && p.Length == 2 &&
                        p[0].ParameterType != typeof(Object) && p[0].ParameterType != typeof(Component) &&
                        p[0].ParameterType != typeof(Behaviour) && p[0].ParameterType != typeof(MonoBehaviour) &&
                        p[0].ParameterType.IsAssignableFrom(type)) {
                        sdkDraw[type] = method;
                        break;
                    }
                }
                if (sdkDraw.ContainsKey(type)) {
                    continue;
                }

                for (var t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType) {
                    var self = t.GetMethod("OnDrawGizmosSelected", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                    if (self != null) {
                        selfDraw[type] = self;
                        break;
                    }
                }
                if (!selfDraw.ContainsKey(type)) {
                    Debug.LogWarning(Log + $"VRChat SDK gizmos for {type.Name} not found.");
                }
            }
        }

        static void Draw(Component c) {
            if (!(c is Behaviour b) || !b.isActiveAndEnabled) {
                return;
            }

            bool isStatic = sdkDraw.TryGetValue(c.GetType(), out var method);
            if (!isStatic && !selfDraw.TryGetValue(c.GetType(), out method)) {
                return;
            }

            if (selectedOnly && !selectedCache.Contains(c) && (!showPinned || !pinnedCache.Contains(c))) {
                return;
            }

            try {
                if (isStatic) {
                    method.Invoke(null, new object[] { c, GizmoType.Selected | GizmoType.Active });
                } else {
                    var matrix = Gizmos.matrix;
                    var color = Gizmos.color;
                    method.Invoke(c, null);
                    Gizmos.matrix = matrix;
                    Gizmos.color = color;
                }
            } catch (Exception e) {
                sdkDraw.Remove(c.GetType());
                selfDraw.Remove(c.GetType());
                Debug.LogWarning(Log + $"Couldn't draw the VRChat SDK gizmos for {c.GetType().Name}. " + (e.InnerException ?? e).Message);
            }
        }

        [DrawGizmo(GizmoType.NotInSelectionHierarchy)]
        static void DrawPhysBone(VRCPhysBone c, GizmoType type) => Draw(c);

        [DrawGizmo(GizmoType.NotInSelectionHierarchy)]
        static void DrawCollider(VRCPhysBoneCollider c, GizmoType type) => Draw(c);

        [DrawGizmo(GizmoType.NotInSelectionHierarchy)]
        static void DrawReceiver(VRCContactReceiver c, GizmoType type) => Draw(c);

        [DrawGizmo(GizmoType.NotInSelectionHierarchy)]
        static void DrawSender(VRCContactSender c, GizmoType type) => Draw(c);

        static void SetAvatarColliders(bool on) {
            avatarColliders = on;
            EditorPrefs.SetBool(AvatarCollidersKey, on);
            avatarCollidersChanged?.Invoke();
            SceneView.RepaintAll();
        }

        static void DrawAvatarColliders(SceneView view) {
            if (!avatarColliders || Event.current.type != EventType.Repaint) {
                return;
            }

            var matrix = Handles.matrix;
            var color = Handles.color;
            Handles.color = AvatarColliderColor;
            foreach (var d in descriptors) {
                if (d == null || !d.isActiveAndEnabled) {
                    continue;
                }

                DrawAvatarCollider(d.collider_head, false);
                DrawAvatarCollider(d.collider_torso, false);
                DrawAvatarCollider(d.collider_handL, false);
                DrawAvatarCollider(d.collider_handR, false);
                DrawAvatarCollider(d.collider_footL, false);
                DrawAvatarCollider(d.collider_footR, false);
                DrawAvatarCollider(d.collider_fingerIndexL, true);
                DrawAvatarCollider(d.collider_fingerIndexR, true);
                DrawAvatarCollider(d.collider_fingerMiddleL, true);
                DrawAvatarCollider(d.collider_fingerMiddleR, true);
                DrawAvatarCollider(d.collider_fingerRingL, true);
                DrawAvatarCollider(d.collider_fingerRingR, true);
                DrawAvatarCollider(d.collider_fingerLittleL, true);
                DrawAvatarCollider(d.collider_fingerLittleR, true);
            }
            Handles.matrix = matrix;
            Handles.color = color;
        }

        static void DrawAvatarCollider(VRCAvatarDescriptor.ColliderConfig c, bool finger) {
            var t = c.transform;
            if (t == null || c.state.ToString() == "Disabled" || c.radius <= 0f) {
                return;
            }

            if (finger) {
                var start = t.parent != null ? t.parent.position : t.position;
                var dir = t.position - start;
                if (c.height <= c.radius * 2f || dir.sqrMagnitude < 1e-10f) {
                    Handles.matrix = Matrix4x4.TRS(t.position, Quaternion.identity, Vector3.one);
                    WireCapsule(c.radius, 0f);
                    return;
                }
                dir.Normalize();
                Handles.matrix = Matrix4x4.TRS(start + dir * (c.height * 0.5f), Quaternion.FromToRotation(Vector3.up, dir), Vector3.one);
                WireCapsule(c.radius, c.height);
                return;
            }
            Handles.matrix = Matrix4x4.TRS(t.position + t.rotation * c.position, t.rotation * c.rotation, Vector3.one);
            WireCapsule(c.radius, c.height);
        }

        static void WireCapsule(float radius, float height) {
            float half = Mathf.Max(0f, height * 0.5f - radius);
            var top = Vector3.up * half;
            var bottom = -top;
            if (half <= 0f) {
                Handles.DrawWireDisc(Vector3.zero, Vector3.up, radius);
                Handles.DrawWireDisc(Vector3.zero, Vector3.right, radius);
                Handles.DrawWireDisc(Vector3.zero, Vector3.forward, radius);
                return;
            }
            Handles.DrawWireDisc(top, Vector3.up, radius);
            Handles.DrawWireDisc(bottom, Vector3.up, radius);
            Handles.DrawLine(top + Vector3.right * radius, bottom + Vector3.right * radius);
            Handles.DrawLine(top - Vector3.right * radius, bottom - Vector3.right * radius);
            Handles.DrawLine(top + Vector3.forward * radius, bottom + Vector3.forward * radius);
            Handles.DrawLine(top - Vector3.forward * radius, bottom - Vector3.forward * radius);
            Handles.DrawWireArc(top, Vector3.forward, Vector3.right, 180f, radius);
            Handles.DrawWireArc(bottom, Vector3.forward, Vector3.left, 180f, radius);
            Handles.DrawWireArc(top, Vector3.right, Vector3.back, 180f, radius);
            Handles.DrawWireArc(bottom, Vector3.right, Vector3.forward, 180f, radius);
        }

        static string TypeLabel(Component c) =>
            c is VRCPhysBone ? "PhysBone" : c is VRCPhysBoneCollider ? "Collider" : c is VRCContactReceiver ? "Receiver" : "Sender";

        static bool IsPinned(Component c) => pinnedCache.Contains(c);

        static readonly Dictionary<Type, Texture2D> typeIcons = new Dictionary<Type, Texture2D>();

        static Texture2D TypeIcon(Component c) {
            var type = c.GetType();
            if (typeIcons.TryGetValue(type, out var icon) && icon != null) {
                return icon;
            }

            icon = c is VRCPhysBone ? MakeIcon(InBone)
                : c is VRCPhysBoneCollider ? MakeIcon(InCollider)
                : c is VRCContactReceiver ? MakeIcon(InReceiver)
                : MakeIcon(InSender);
            typeIcons[type] = icon;
            return icon;
        }

        static bool InBone(float px, float py) {
            var p = new Vector2(px, py);
            var a = new Vector2(10f, 10f);
            var b = new Vector2(22f, 22f);
            var d = (b - a).normalized;
            var n = new Vector2(-d.y, d.x) * 3.2f;
            const float knob = 3.8f;
            return Segment(p, a, b) <= 2.6f
                || Vector2.Distance(p, a - d + n) <= knob || Vector2.Distance(p, a - d - n) <= knob
                || Vector2.Distance(p, b + d + n) <= knob || Vector2.Distance(p, b + d - n) <= knob;
        }

        static bool InContact(float px, float py, float ringX, float gapAngle, float fromX, float toX) {
            const float w = 1.6f;
            var p = new Vector2(px, py);
            float r = Vector2.Distance(p, new Vector2(ringX, 16f));
            float angle = Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(py - 16f, px - ringX) * Mathf.Rad2Deg, gapAngle));
            if (Mathf.Abs(r - 11f) <= w && angle > 42f) {
                return true;
            }

            var tip = new Vector2(toX, 16f);
            float back = toX > fromX ? -5.5f : 5.5f;
            return Segment(p, new Vector2(fromX, 16f), tip) <= w
                || Segment(p, tip, new Vector2(toX + back, 10.5f)) <= w
                || Segment(p, tip, new Vector2(toX + back, 21.5f)) <= w;
        }

        static bool InSender(float px, float py) => InContact(px, py, 14f, 0f, 13f, 29f);

        static bool InReceiver(float px, float py) => InContact(px, py, 18f, 180f, 3f, 19f);

        static void InstallHeaderItem() {
            try {
                var field = typeof(EditorGUIUtility).GetField("s_EditorHeaderItemsMethods", BindingFlags.NonPublic | BindingFlags.Static);
                var type = typeof(EditorGUIUtility).GetNestedType("HeaderItemDelegate", BindingFlags.NonPublic);
                if (field != null && type != null && !(field.GetValue(null) is System.Collections.IList)) {
                    return;
                }

                EditorApplication.update -= InstallHeaderItem;
                if (field == null || type == null) {
                    return;
                }

                var method = typeof(VRCGizmos).GetMethod(nameof(DrawHeaderPin), BindingFlags.NonPublic | BindingFlags.Static);
                ((System.Collections.IList)field.GetValue(null)).Insert(0, Delegate.CreateDelegate(type, method));
            } catch {
                EditorApplication.update -= InstallHeaderItem;
            }
        }

        static GUIContent headerPin;
        static GUIStyle iconButton;

        static bool DrawHeaderPin(Rect rect, Object[] targets) {
            if (targets == null || targets.Length == 0 || !(targets[0] is Component first) || !Types.Contains(first.GetType())) {
                return false;
            }

            bool pinned = IsPinned(first);
            headerPin ??= new GUIContent();
            headerPin.image = PinIcon();
            headerPin.tooltip = pinned ? "Unpin from VRC Gizmos" : "Pin to VRC Gizmos";
            var color = GUI.color;
            GUI.color = pinned ? PinnedColor : UnpinnedColor;
            iconButton ??= GUI.skin.FindStyle("IconButton") ?? GUIStyle.none;
            if (GUI.Button(rect, headerPin, iconButton)) {
                foreach (var t in targets) {
                    if (t is Component c && IsPinned(c) == pinned) {
                        TogglePin(c);
                    }
                }

                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
            GUI.color = color;
            return true;
        }

        static void TogglePin(Component c) {
            if (c == null) {
                return;
            }

            if (pinnedCache.Remove(c)) {
                pins.Remove(Id(c));
            } else {
                pinnedCache.Add(c);
                pins.Add(Id(c));
            }
            AfterPinChange();
        }

        static Texture2D Icon(string name) {
            var icon = EditorGUIUtility.isProSkin ? EditorGUIUtility.FindTexture("d_" + name) : null;
            return icon != null ? icon : EditorGUIUtility.FindTexture(name);
        }

        static readonly Color LightIcon = new Color(0.333f, 0.333f, 0.333f);
        static Color IconOn => EditorGUIUtility.isProSkin ? Color.white : LightIcon;
        static Color IconOff => EditorGUIUtility.isProSkin ? new Color(0.77f, 0.77f, 0.77f) : LightIcon;

        static bool darkSkin = EditorGUIUtility.isProSkin;

        static void WatchSkin() {
            if (darkSkin == EditorGUIUtility.isProSkin) {
                return;
            }

            darkSkin = EditorGUIUtility.isProSkin;
            modeChanged?.Invoke();
        }

        static void StyleIcon(VisualElement e, bool on) {
            e.Query<Image>().ForEach(i => {
                i.tintColor = on ? IconOn : IconOff;
            });
        }

        static Button IconButton(Texture2D icon, string fallback, string tip, Action action, Color? tint = null) {
            var button = new Button(action) { tooltip = tip, style = { width = 22, height = 18, paddingLeft = 1, paddingRight = 1 } };
            if (icon != null) {
                button.Add(new Image { image = icon, scaleMode = ScaleMode.ScaleToFit, tintColor = tint ?? Color.white, style = { flexGrow = 1 } });
            } else {
                button.text = fallback;
            }

            return button;
        }

        static readonly Color PinnedColor = new Color(0.3f, 0.6f, 1f);
        static readonly Color UnpinnedColor = new Color(0.55f, 0.55f, 0.55f);
        static Texture2D pinIcon;

        static Texture2D cursorIcon, avatarIcon;

        static Texture2D PinIcon() => pinIcon != null ? pinIcon : pinIcon = MakeIcon(InPin);

        static Texture2D CursorIcon() => cursorIcon != null ? cursorIcon : cursorIcon = MakeIcon(WithCollider(InCursor, -2f, 2f));

        static Texture2D AvatarIcon() => avatarIcon != null ? avatarIcon : avatarIcon = MakeIcon(WithCollider(InAvatar, -3f, 2f));

        static readonly Vector2[] CursorShape =
        {
            new Vector2(9f, 3f), new Vector2(9f, 26f), new Vector2(14.5f, 20.5f), new Vector2(18.5f, 29f),
            new Vector2(22f, 27.5f), new Vector2(18f, 19.5f), new Vector2(25f, 19.5f),
        };

        static bool InCursor(float px, float py) {
            float x = px, y = 32f - py;
            bool inside = false;
            for (int i = 0, j = CursorShape.Length - 1; i < CursorShape.Length; j = i++) {
                var a = CursorShape[i];
                var b = CursorShape[j];
                if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x) {
                    inside = !inside;
                }
            }
            return inside;
        }

        static bool InAvatar(float px, float py) {
            var p = new Vector2(px, 32f - py);
            const float w = 1.25f;
            return (p - new Vector2(16f, 6.5f)).sqrMagnitude <= 3.6f * 3.6f
                || Segment(p, new Vector2(16f, 11f), new Vector2(16f, 20f)) <= w
                || Segment(p, new Vector2(16f, 13.5f), new Vector2(8.5f, 18f)) <= w
                || Segment(p, new Vector2(16f, 13.5f), new Vector2(23.5f, 18f)) <= w
                || Segment(p, new Vector2(16f, 20f), new Vector2(10f, 29.5f)) <= w
                || Segment(p, new Vector2(16f, 20f), new Vector2(22f, 29.5f)) <= w;
        }

        static float Segment(Vector2 p, Vector2 a, Vector2 b) {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        static bool InCollider(float px, float py) {
            const float w = 1.3f, a2 = 144f, b2 = 20f;
            float dx = px - 16f, dy = py - 16f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            if (Mathf.Abs(r - 12f) <= w) {
                return true;
            }

            if (r > 12f || Mathf.Abs(dx) <= w) {
                return r <= 12f;
            }

            float e = Mathf.Sqrt(dx * dx / a2 + dy * dy / b2);
            float g = Mathf.Sqrt(dx * dx / (a2 * a2) + dy * dy / (b2 * b2)) / Mathf.Max(e, 0.0001f);
            return Mathf.Abs(e - 1f) / g <= w;
        }

        static Func<float, float, bool> WithCollider(Func<float, float, bool> main, float shiftX, float shiftY) {
            const float bx = 23.5f, by = 8.5f, br = 8.5f;
            return (px, py) => {
                float d = Mathf.Sqrt((px - bx) * (px - bx) + (py - by) * (py - by));
                if (d <= br) {
                    float s = br / 13.6f;
                    return InCollider(16f + (px - bx) / s, 16f + (py - by) / s);
                }
                return d > br + 2f && main(px - shiftX, py - shiftY);
            };
        }

        static Texture2D MakeIcon(Func<float, float, bool> inside) {
            const int size = 32;
            const int samples = 4;
            var icon = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    int hits = 0;
                    for (int sy = 0; sy < samples; sy++) {
                        for (int sx = 0; sx < samples; sx++) {
                            if (inside(x + (sx + 0.5f) / samples, y + (sy + 0.5f) / samples)) {
                                hits++;
                            }
                        }
                    }

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(hits * 255 / (samples * samples)));
                }
            }

            icon.SetPixels32(pixels);
            icon.Apply();
            return icon;
        }

        static bool InPin(float px, float py) {
            const float c = 0.7071f;
            float dx = px - 16f, dy = py - 16f;
            float x = 16f + dx * c + dy * c;
            float y = 16f - dx * c + dy * c;
            if ((x - 16f) * (x - 16f) + (y - 23f) * (y - 23f) <= 6f * 6f) {
                return true;
            }

            if (x >= 12.5f && x <= 19.5f && y >= 14f && y <= 23f) {
                return true;
            }

            if (x >= 9f && x <= 23f && y >= 12f && y <= 15f) {
                return true;
            }

            return Mathf.Abs(x - 16f) <= 1f && y >= 2f && y <= 12f;
        }

        const string ToolbarId = "VAP/VRCGizmos/";

        static List<Component> Listed() {
            targets.RemoveAll(c => c == null);
            var items = new List<Component>(targets);
            items.AddRange(pinnedCache.Where(c => c != null && !targets.Contains(c))
                .OrderBy(c => Array.IndexOf(Types, c.GetType())).ThenBy(c => c.name));
            return items;
        }

        static VisualElement ListView() {
            var list = new VisualElement { style = { marginTop = 4 } };

            void Rebuild() {
                list.Clear();
                var properties = Icon("UnityEditor.InspectorWindow");
                var items = Listed();
                if (items.Count == 0) {
                    list.Add(new Label("Nothing selected or pinned") { style = { marginLeft = 4, marginTop = 2 } });
                }

                foreach (var c in items) {
                    var component = c;
                    bool isPinned = IsPinned(c);
                    var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                    var pin = IconButton(PinIcon(), "Pin", isPinned ? "Unpin" : "Pin", () => TogglePin(component),
                        isPinned ? Color.white : UnpinnedColor);
                    if (isPinned) {
                        pin.style.backgroundColor = PinnedColor;
                    }

                    row.Add(pin);
                    row.Add(new Image {
                        image = TypeIcon(c),
                        scaleMode = ScaleMode.ScaleToFit,
                        tintColor = IconOn,
                        tooltip = TypeLabel(c),
                        style = { width = 16, height = 16, marginLeft = 4, flexShrink = 0 },
                    });
                    row.Add(new Label(c.name) {
                        tooltip = $"{TypeLabel(c)}: {Path(c.transform)}",
                        style =
                        {
                            flexGrow = 1, height = 18, marginLeft = 4, marginTop = 0, marginBottom = 0,
                            paddingTop = 0, paddingBottom = 0, unityTextAlign = TextAnchor.MiddleLeft,
                        },
                    });
                    row.Add(IconButton(properties, "...", "Open in a Properties window", () => EditorUtility.OpenPropertyEditor(component)));
                    list.Add(row);
                }
                var clear = new Button(ClearPins) { text = "Clear Pins", tooltip = "Unpin everything", style = { marginTop = 4, height = 20 } };
                clear.SetEnabled(pinnedCache.Any(c => c != null));
                list.Add(clear);
            }

            list.RegisterCallback<AttachToPanelEvent>(_ => {
                targetsChanged += Rebuild;
                pinsChanged += Rebuild;
                modeChanged += Rebuild;
                Rebuild();
            });
            list.RegisterCallback<DetachFromPanelEvent>(_ => {
                targetsChanged -= Rebuild;
                pinsChanged -= Rebuild;
                modeChanged -= Rebuild;
            });
            return list;
        }

        class ListPopup : PopupWindowContent {
            public override Vector2 GetWindowSize() {
                var items = Listed();
                float label = items.Count == 0 ? 180f : items.Max(c => EditorStyles.label.CalcSize(new GUIContent(c.name)).x + 20f);
                return new Vector2(Mathf.Clamp(label + 90f, 240f, 700f), Mathf.Clamp(items.Count, 1, 20) * 20f + 38f);
            }

            public override void OnGUI(Rect rect) { }

            public override void OnOpen() {
                var root = editorWindow.rootVisualElement;
                root.style.paddingLeft = 4f;
                root.style.paddingRight = 4f;
                root.style.paddingTop = 4f;
                root.style.paddingBottom = 4f;
                var scroll = new ScrollView(ScrollViewMode.Vertical);
                scroll.Add(ListView());
                root.Add(scroll);
            }
        }

        [Overlay(typeof(SceneView), "VAP VRC Gizmos", "VRC Gizmos", true, defaultLayout = Layout.VerticalToolbar)]
        class GizmosOverlay : ToolbarOverlay {
            GizmosOverlay() : base(ToolbarId + "Buttons") { }
        }

        const float ButtonWidth = 37f;

        [EditorToolbarElement(ToolbarId + "Buttons", typeof(SceneView))]
        class GizmoButtons : VisualElement {
            public GizmoButtons() {
                foreach (var button in new VisualElement[] { new SelectedToggle(), new PinnedToggle(), new AvatarCollidersToggle() }) {
                    button.style.width = ButtonWidth;
                    Add(button);
                }
                EditorToolbarUtility.SetupChildrenAsButtonStrip(this);
                RegisterCallback<AttachToPanelEvent>(_ => {
                    for (var p = parent; p != null; p = p.parent) {
                        if (p.ClassListContains("unity-overlay")) {
                            p.Query<Label>(className: "overlay-header__title").ForEach(l => l.style.display = DisplayStyle.None);
                            break;
                        }
                    }
                });
            }
        }

        abstract class IconToggle : EditorToolbarToggle {
            readonly Func<bool> get;

            protected IconToggle(Texture2D image, string tip, Func<bool> get, Action<bool> set) {
                this.get = get;
                icon = image;
                tooltip = tip;
                this.RegisterValueChangedCallback(e => set(e.newValue));
                RegisterCallback<AttachToPanelEvent>(_ => {
                    modeChanged += Refresh;
                    avatarCollidersChanged += Refresh;
                    Refresh();
                });
                RegisterCallback<DetachFromPanelEvent>(_ => {
                    modeChanged -= Refresh;
                    avatarCollidersChanged -= Refresh;
                });
                Refresh();
            }

            protected virtual void Refresh() {
                bool on = get();
                SetValueWithoutNotify(on);
                StyleIcon(this, on);
            }
        }

        [EditorToolbarElement(ToolbarId + "Selected", typeof(SceneView))]
        class SelectedToggle : IconToggle {
            public SelectedToggle() : base(CursorIcon(),
                "Only draw what is selected: PhysBones whose root or bones are selected, and components on or targeting the selection. Off draws every PhysBone, collider and contact",
                () => selectedOnly, SetSelectedOnly) { }
        }

        [EditorToolbarElement(ToolbarId + "Pinned", typeof(SceneView))]
        class PinnedToggle : IconToggle {
            const float ArrowWidth = 14f;

            public PinnedToggle() : base(PinIcon(), "Also draw pinned PhysBones, colliders and contacts. The arrow lists the selected and pinned ones",
                () => showPinned, on => {
                    if (selectedOnly) {
                        SetShowPinned(on);
                    } else {
                        modeChanged?.Invoke();
                    }
                }) {
                style.paddingRight = ArrowWidth;
                var arrow = new VisualElement {
                    pickingMode = PickingMode.Ignore,
                    style =
                    {
                        position = Position.Absolute, right = 0, top = 0, bottom = 0, width = ArrowWidth,
                        borderLeftWidth = 1f, borderLeftColor = new Color(0f, 0f, 0f, 0.3f),
                        justifyContent = Justify.Center, alignItems = Align.Center,
                    },
                };
                arrow.Add(new Image { image = ArrowIcon(), scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore, style = { width = ArrowWidth, height = ArrowWidth } });
                Add(arrow);
                RegisterCallback<PointerDownEvent>(e => {
                    if (e.button != 0 || e.localPosition.x < layout.width - ArrowWidth) {
                        return;
                    }

                    e.StopImmediatePropagation();
#if !UNITY_2023_2_OR_NEWER
                    e.PreventDefault();
#endif
                    UnityEditor.PopupWindow.Show(worldBound, new ListPopup());
                }, TrickleDown.TrickleDown);
                RegisterCallback<MouseDownEvent>(e => {
                    if (e.button == 0 && e.localMousePosition.x >= layout.width - ArrowWidth) {
                        e.StopImmediatePropagation();
                    }
                }, TrickleDown.TrickleDown);
            }

            protected override void Refresh() {
                base.Refresh();
                if (!selectedOnly) {
                    this.Query<Image>().ForEach(i => i.tintColor = new Color(IconOff.r, IconOff.g, IconOff.b, 0.4f));
                }
            }
        }

        static Texture2D arrowIcon;

        static Texture2D ArrowIcon() => arrowIcon != null ? arrowIcon : arrowIcon = MakeIcon((px, py) => {
            float y = 32f - py;
            return y >= 12f && y <= 20f && Mathf.Abs(px - 16f) <= 20f - y;
        });

        [EditorToolbarElement(ToolbarId + "AvatarColliders", typeof(SceneView))]
        class AvatarCollidersToggle : IconToggle {
            public AvatarCollidersToggle() : base(AvatarIcon(), "Also draw the colliders set on the avatar descriptor", () => avatarColliders, SetAvatarColliders) { }
        }
    }
}
#endif
