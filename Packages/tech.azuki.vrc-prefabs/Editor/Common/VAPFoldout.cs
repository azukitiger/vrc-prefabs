#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VAP.Editor
{
    internal class VAPFoldout
    {
        const float HeaderHeight = 26f;

        readonly string key;
        readonly bool persistent;
        readonly bool openByDefault;
        const float BodyIndent = 6f;
        readonly VisualElement body = new VisualElement();

        internal VisualElement Root { get; } = new VisualElement();

        internal Func<SerializedProperty[]> Overrides { get; set; }

        internal VAPFoldout(string key, Action<Rect> drawHeader, Action drawBody, Func<bool> canOpen = null, bool persistent = false, bool openByDefault = false)
        {
            this.key = key;
            this.persistent = persistent;
            this.openByDefault = openByDefault;
            StyleBox(Root, body, canOpen == null || canOpen());

            Root.Add(new IMGUIContainer(() =>
            {
                bool open = IsOpen();
                var header = EditorGUILayout.GetControlRect(false, HeaderHeight);
                bool allowed = canOpen == null || canOpen();
                var content = GUI.contentColor;
                if (!allowed)
                    GUI.contentColor = DisabledText;
                drawHeader(new Rect(header.x + 20f, header.y, header.width - 24f, header.height));
                GUI.contentColor = content;
                var background = Background(allowed);
                if (Root.style.backgroundColor.value != background)
                    Root.style.backgroundColor = background;
                if (Overrides != null && Event.current.type == EventType.ContextClick && header.Contains(Event.current.mousePosition))
                {
                    ShowRevertMenu();
                    Event.current.Use();
                }
                if (!allowed && open)
                {
                    open = false;
                    SetOpen(false);
                    Show(false);
                }
                if (allowed && Event.current.type == EventType.MouseDown && Event.current.button == 0 && header.Contains(Event.current.mousePosition))
                {
                    open = !open;
                    SetOpen(open);
                    Show(open);
                    Event.current.Use();
                }
                if (Event.current.type != EventType.Repaint)
                    return;
                float line = EditorGUIUtility.singleLineHeight;
                using (new EditorGUI.DisabledScope(!allowed))
                    EditorStyles.foldout.Draw(new Rect(header.x + 4f, header.y + (header.height - line) * 0.5f, 14f, line), false, false, open, false);
            }));
            body.Add(new IMGUIContainer(() =>
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(BodyIndent);
                    using (new EditorGUILayout.VerticalScope())
                        drawBody();
                }
            }));
            Root.Add(body);
            Show(IsOpen());
        }

        void ShowRevertMenu()
        {
            var changed = (Overrides() ?? Array.Empty<SerializedProperty>())
                .Where(p => p != null && p.prefabOverride)
                .ToList();
            var menu = new GenericMenu();
            var label = new GUIContent("Revert");
            if (changed.Count == 0)
                menu.AddDisabledItem(label);
            else
                menu.AddItem(label, false, () =>
                {
                    foreach (var p in changed)
                    {
                        p.serializedObject.Update();
                        var current = p.serializedObject.FindProperty(p.propertyPath);
                        if (current != null && current.prefabOverride)
                            PrefabUtility.RevertPropertyOverride(current, InteractionMode.UserAction);
                    }
                });
            menu.ShowAsContext();
        }

        internal static void StyleBox(VisualElement root, VisualElement body, bool enabled)
        {
            var style = root.style;
            style.marginTop = 6f;
            style.backgroundColor = Background(enabled);
            var border = new Color(0f, 0f, 0f, 0.35f);
            style.borderTopColor = style.borderBottomColor = border;
            style.borderLeftColor = style.borderRightColor = border;
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1f;
            style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = 3f;
            body.style.paddingLeft = 4f;
            body.style.paddingRight = 4f;
            body.style.paddingBottom = 4f;
        }

        static Color DisabledText => EditorGUIUtility.isProSkin ? new Color(0.6f, 0.6f, 0.6f) : new Color(1f, 1f, 1f, 0.45f);

        static Color Background(bool enabled) =>
            !enabled ? (EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.11f) : new Color(1f, 1f, 1f, 0.6f))
            : EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.05f) : new Color(1f, 1f, 1f, 0.35f);

        bool IsOpen() => persistent ? EditorPrefs.GetBool(key, openByDefault) : SessionState.GetBool(key, openByDefault);

        void SetOpen(bool open)
        {
            if (persistent)
                EditorPrefs.SetBool(key, open);
            else
                SessionState.SetBool(key, open);
        }

        void Show(bool open) => body.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
#endif
