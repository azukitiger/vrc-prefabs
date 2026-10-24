#if UNITY_EDITOR
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VAP.Editor
{
    public static class VAPInspectorHeader
    {
        const string LogoPath = "Packages/tech.azuki.vrc-prefabs/Editor/Icons/VAP Header.png";
        const string LogoPath2x = "Packages/tech.azuki.vrc-prefabs/Editor/Icons/VAP Header@2x.png";
        const float LogoHeight = 10f;
        static readonly Color BadgeBack = new Color32(34, 34, 34, 255);
        static readonly Color BadgeText = new Color32(205, 65, 48, 255);

        static Texture2D logo, logo2x;
        static Texture2D Logo
        {
            get
            {
                if (EditorGUIUtility.pixelsPerPoint > 1.5f)
                {
                    if (logo2x == null)
                        logo2x = AssetDatabase.LoadAssetAtPath<Texture2D>(LogoPath2x);
                    if (logo2x != null)
                        return logo2x;
                }
                if (logo == null)
                    logo = AssetDatabase.LoadAssetAtPath<Texture2D>(LogoPath);
                return logo;
            }
        }

        // Our draw handler for each title bar, to tell whether Unity has replaced it.
        static readonly ConditionalWeakTable<IMGUIContainer, System.Action> Wrapped = new ConditionalWeakTable<IMGUIContainer, System.Action>();

        public static void Attach(VisualElement inspectorRoot, string title)
        {
#if !VAP_NDMF
            inspectorRoot.Clear();
            inspectorRoot.Add(new IMGUIContainer(() => EditorGUILayout.HelpBox(
                "NDMF is not installed, so this component does nothing when the avatar is built. " +
                "Install NDMF (nadena.dev.ndmf) from the VRChat Creator Companion.", MessageType.Error)));
#endif
            IVisualElementScheduledItem watch = null;
            inspectorRoot.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                // Wait a frame so Unity has built the title bar.
                inspectorRoot.schedule.Execute(() =>
                {
                    if (!WrapTitlebar(inspectorRoot, title))
                        AddFallback(inspectorRoot, title);
                });
                watch?.Pause();
                watch = inspectorRoot.schedule.Execute(() => WrapTitlebar(inspectorRoot, title)).Every(150);
            });
            inspectorRoot.RegisterCallback<DetachFromPanelEvent>(_ => watch?.Pause());
        }

        // Finds Unity's title bar for this inspector and draws over its title text.
        static bool WrapTitlebar(VisualElement root, string title)
        {
            VisualElement editorElement = root;
            while (editorElement != null && editorElement.GetType().Name != "EditorElement")
                editorElement = editorElement.parent;
            if (editorElement == null)
                return false;

            IMGUIContainer titlebar = null;
            foreach (var child in editorElement.Children())
            {
                if (child is IMGUIContainer c && c.name != null && c.name.EndsWith("Header"))
                {
                    titlebar = c;
                    break;
                }
            }
            if (titlebar == null)
                return false;
            if (Wrapped.TryGetValue(titlebar, out var ours))
            {
                if (titlebar.onGUIHandler == ours)
                    return true;
                Wrapped.Remove(titlebar);
            }

            var original = titlebar.onGUIHandler;
            System.Action handler = () =>
            {
                original?.Invoke();
                if (Event.current.type != EventType.Repaint)
                    return;
                var r = titlebar.contentRect;
                // Cover the icon and name, leave the foldout arrow and the right-side buttons.
                var area = new Rect(18, 0, Mathf.Max(0, r.width - 18 - 64), r.height);
                EditorGUI.DrawRect(area, EditorGUIUtility.isProSkin ? new Color32(62, 62, 62, 255) : new Color32(203, 203, 203, 255));
                Draw(area, title);
            };
            Wrapped.Add(titlebar, handler);
            titlebar.onGUIHandler = handler;
            titlebar.MarkDirtyRepaint();
            return true;
        }

        // Used if Unity's title bar can't be found: draw the bar at the top of the inspector.
        static void AddFallback(VisualElement root, string title)
        {
            if (root.Q<IMGUIContainer>("VAPHeaderFallback") != null)
                return;
            var bar = new IMGUIContainer(() =>
            {
                var rect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
                if (Event.current.type == EventType.Repaint)
                    Draw(rect, title);
            }) { name = "VAPHeaderFallback" };
            bar.style.marginBottom = 4;
            root.Insert(0, bar);
        }

        static void Draw(Rect area, string title)
        {
            float h = area.height;
            var tex = Logo;
            float logoH = LogoHeight;
            float logoW = tex != null ? Mathf.Round(logoH * tex.width / tex.height) : 30;
            var box = new Rect(area.x + 2, area.y, logoW + 14, h);
            EditorGUI.DrawRect(box, BadgeBack);
            var logoRect = new Rect(Mathf.Round(box.x + 7), Mathf.Round(box.y + (box.height - logoH) / 2), logoW, logoH);
            if (tex != null)
                GUI.DrawTexture(logoRect, tex, ScaleMode.ScaleToFit, true, 0, BadgeText, 0, 0);
            else
                GUI.Label(box, "VAP", new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = BadgeText } });

            var labelRect = new Rect(box.xMax + 6, area.y, Mathf.Max(0, area.xMax - box.xMax - 6), h);
            GUI.Label(labelRect, title, new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleLeft });
        }
    }
}
#endif
