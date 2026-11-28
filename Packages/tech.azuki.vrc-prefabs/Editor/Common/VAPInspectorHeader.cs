#if UNITY_EDITOR
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VAP.Editor {
    public static class VAPInspectorHeader {
        const string LogoPath = "Packages/tech.azuki.vrc-prefabs/Editor/Icons/VAP Header.png";
        const string LogoPath2x = "Packages/tech.azuki.vrc-prefabs/Editor/Icons/VAP Header@2x.png";
        const float LogoHeight = 14f;
        static readonly Color BadgeBack = new Color32(34, 34, 34, 255);
        static readonly Color BadgeText = new Color32(205, 65, 48, 255);

        static Texture2D logo, logo2x;
        static Texture2D Logo {
            get {
                if (EditorGUIUtility.pixelsPerPoint > 1.5f) {
                    if (logo2x == null) {
                        logo2x = AssetDatabase.LoadAssetAtPath<Texture2D>(LogoPath2x);
                    }

                    if (logo2x != null) {
                        return logo2x;
                    }
                }
                if (logo == null) {
                    logo = AssetDatabase.LoadAssetAtPath<Texture2D>(LogoPath);
                }

                return logo;
            }
        }

        static readonly ConditionalWeakTable<IMGUIContainer, System.Action> Wrapped = new ConditionalWeakTable<IMGUIContainer, System.Action>();

        static readonly ConditionalWeakTable<VisualElement, UnityEditor.Editor> EditButtons = new ConditionalWeakTable<VisualElement, UnityEditor.Editor>();
        static readonly GUIContent EditLabel = new GUIContent("Edit in Prefab", "Open the prefab this component comes from, where it can be changed.");

        internal static void AddEditButton(VisualElement root, UnityEditor.Editor editor) {
            EditButtons.Remove(root);
            EditButtons.Add(root, editor);
        }

        static UnityEditor.Editor EditFor(VisualElement root) =>
            root != null && EditButtons.TryGetValue(root, out var editor) && editor != null && editor.targets.Any(VAPPrefabLock.IsFromPrefab) ? editor : null;

        static Rect EditRect(Rect area) {
            var size = EditorStyles.miniButton.CalcSize(EditLabel);
            float height = Mathf.Min(18f, area.height - 2f);
            return new Rect(area.xMax - size.x - 4f, Mathf.Round(area.y + (area.height - height) / 2), size.x, height);
        }

        static bool HandleEdit(Rect area, VisualElement root) {
            var e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0) {
                return false;
            }

            var editor = EditFor(root);
            if (editor == null || !EditRect(area).Contains(e.mousePosition)) {
                return false;
            }

            var target = editor.targets.FirstOrDefault(VAPPrefabLock.IsFromPrefab);
            e.Use();
            if (target != null) {
                EditorApplication.delayCall += () => VAPPrefabLock.Open(target);
            }

            return true;
        }

        static readonly Color WarningBorder = new Color32(200, 40, 40, 255);

        public class Toggle {
            public System.Func<bool> Get;
            public System.Action<bool> Set;
        }

        public static VisualElement Create(string title, System.Action onGUI, UnityEditor.Editor editor = null, Toggle toggle = null) {
            var root = new VisualElement();
            root.Add(new IMGUIContainer(onGUI));
            Attach(root, title, editor, toggle);
            return root;
        }

        static void RepaintOnHover(VisualElement root) {
            root.RegisterCallback<MouseMoveEvent>(e => RepaintContainer(e.target), TrickleDown.TrickleDown);
            root.RegisterCallback<MouseOutEvent>(e => RepaintContainer(e.target), TrickleDown.TrickleDown);
            root.RegisterCallback<MouseLeaveEvent>(e => root.Query<IMGUIContainer>().ForEach(c => c.MarkDirtyRepaint()));
        }

        static void RepaintContainer(IEventHandler target) {
            for (var v = target as VisualElement; v != null; v = v.parent) {
                if (v is IMGUIContainer container) {
                    container.MarkDirtyRepaint();
                    return;
                }
            }
        }

        public static void Attach(VisualElement inspectorRoot, string title, UnityEditor.Editor editor = null, Toggle toggle = null) {
#if !VAP_NDMF
            inspectorRoot.Clear();
            inspectorRoot.Add(new IMGUIContainer(() => EditorGUILayout.HelpBox(
                "NDMF is not installed, so this component does nothing when the avatar is built. " +
                "Install NDMF (nadena.dev.ndmf) from the VRChat Creator Companion.", MessageType.Error)));
#endif
            if (editor != null) {
                inspectorRoot.Add(NotInAvatarWarning(editor));
            }

            RepaintOnHover(inspectorRoot);
            IVisualElementScheduledItem watch = null;
            inspectorRoot.RegisterCallback<AttachToPanelEvent>(_ => {
                inspectorRoot.schedule.Execute(() => {
                    if (!WrapTitlebar(inspectorRoot, title, toggle)) {
                        AddFallback(inspectorRoot, title, toggle);
                    }
                });
                watch?.Pause();
                watch = inspectorRoot.schedule.Execute(() => WrapTitlebar(inspectorRoot, title, toggle)).Every(150);
            });
            inspectorRoot.RegisterCallback<DetachFromPanelEvent>(_ => watch?.Pause());
        }

        static VisualElement NotInAvatarWarning(UnityEditor.Editor editor) {
            var box = new VisualElement();
            var style = box.style;
            style.marginTop = 8f;
            style.paddingLeft = style.paddingRight = 6f;
            style.paddingTop = style.paddingBottom = 4f;
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 2f;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = WarningBorder;
            style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = 4f;
            var label = new Label(VAPEditorGUI.NotInAvatar);
            label.style.whiteSpace = WhiteSpace.Normal;
            box.Add(label);
            void Refresh() => style.display = editor.targets.Any(t => t is Component c && VAPAvatar.Descriptor(c) == null)
                ? DisplayStyle.Flex : DisplayStyle.None;
            Refresh();
            box.schedule.Execute(Refresh).Every(500);
            return box;
        }

        static bool WrapTitlebar(VisualElement root, string title, Toggle toggle) {
            VisualElement editorElement = root;
            while (editorElement != null && editorElement.GetType().Name != "EditorElement") {
                editorElement = editorElement.parent;
            }

            if (editorElement == null) {
                return false;
            }

            IMGUIContainer titlebar = null;
            foreach (var child in editorElement.Children()) {
                if (child is IMGUIContainer c && c.name != null && c.name.EndsWith("Header")) {
                    titlebar = c;
                    break;
                }
            }
            if (titlebar == null) {
                return false;
            }

            if (Wrapped.TryGetValue(titlebar, out var ours)) {
                if (titlebar.onGUIHandler == ours) {
                    return true;
                }

                Wrapped.Remove(titlebar);
            }

            var original = titlebar.onGUIHandler;
            System.Action handler = () => {
                var r = titlebar.contentRect;
                var area = new Rect(18, 1, Mathf.Max(0, r.width - 18 - 64), Mathf.Max(0, r.height - 2));
                if (HandleToggle(area, toggle) || HandleEdit(area, root)) {
                    titlebar.MarkDirtyRepaint();
                    return;
                }
                original?.Invoke();
                if (Event.current.type != EventType.Repaint) {
                    return;
                }

                EditorGUI.DrawRect(area, EditorGUIUtility.isProSkin ? new Color32(62, 62, 62, 255) : new Color32(203, 203, 203, 255));
                Draw(area, title, toggle, root);
            };
            Wrapped.Add(titlebar, handler);
            titlebar.onGUIHandler = handler;
            titlebar.MarkDirtyRepaint();
            return true;
        }

        static void AddFallback(VisualElement root, string title, Toggle toggle) {
            if (root.Q<IMGUIContainer>("VAPHeaderFallback") != null) {
                return;
            }

            var bar = new IMGUIContainer(() => {
                var rect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
                if (HandleToggle(rect, toggle) || HandleEdit(rect, root)) {
                    return;
                }

                if (Event.current.type == EventType.Repaint) {
                    Draw(rect, title, toggle, root);
                }
            }) { name = "VAPHeaderFallback" };
            bar.style.marginBottom = 4;
            root.Insert(0, bar);
        }

        const float ToggleSize = 14f;

        static Rect Badge(Rect area) {
            var tex = Logo;
            float logoW = tex != null ? Mathf.Round(LogoHeight * tex.width / tex.height) : 30;
            return new Rect(area.x + 2, area.y, logoW + 14, area.height);
        }

        static Rect ToggleRect(Rect area) {
            var box = Badge(area);
            return new Rect(box.xMax + 6, Mathf.Round(area.y + (area.height - ToggleSize) / 2), ToggleSize, ToggleSize);
        }

        static bool HandleToggle(Rect area, Toggle toggle) {
            var e = Event.current;
            if (toggle == null || e.type != EventType.MouseDown || e.button != 0 || !ToggleRect(area).Contains(e.mousePosition)) {
                return false;
            }

            toggle.Set(!toggle.Get());
            e.Use();
            return true;
        }

        static void Draw(Rect area, string title, Toggle toggle, VisualElement root) {
            float h = area.height;
            var tex = Logo;
            float logoH = LogoHeight;
            float logoW = tex != null ? Mathf.Round(logoH * tex.width / tex.height) : 30;
            var box = Badge(area);
            EditorGUI.DrawRect(box, BadgeBack);
            var logoRect = new Rect(Mathf.Round(box.x + 7), Mathf.Round(box.y + (box.height - logoH) / 2), logoW, logoH);
            if (tex != null) {
                GUI.DrawTexture(logoRect, tex, ScaleMode.ScaleToFit, true, 0, BadgeText, 0, 0);
            } else {
                GUI.Label(box, "VAP", new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = BadgeText } });
            }

            float labelX = box.xMax + 6;
            if (toggle != null) {
                var t = ToggleRect(area);
                EditorStyles.toggle.Draw(t, GUIContent.none, false, false, toggle.Get(), false);
                labelX = t.xMax + 6;
            }
            float labelEnd = area.xMax;
            if (EditFor(root) != null) {
                var edit = EditRect(area);
                EditorStyles.miniButton.Draw(edit, EditLabel, edit.Contains(Event.current.mousePosition), false, false, false);
                labelEnd = edit.x - 4f;
            }
            var labelRect = new Rect(labelX, area.y, Mathf.Max(0, labelEnd - labelX), h);
            GUI.Label(labelRect, title, new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleLeft });
        }
    }
}
#endif
