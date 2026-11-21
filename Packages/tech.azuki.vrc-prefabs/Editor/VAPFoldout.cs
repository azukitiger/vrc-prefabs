#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VAP.Editor
{
    internal class VAPFoldout
    {
        const float HeaderHeight = 26f;

        readonly string key;
        readonly VisualElement body = new VisualElement();

        internal VisualElement Root { get; } = new VisualElement();

        internal VAPFoldout(string key, Action<Rect> drawHeader, Action drawBody, Func<bool> canOpen = null)
        {
            this.key = key;
            var style = Root.style;
            style.marginTop = 6f;
            style.backgroundColor = new Color(0f, 0f, 0f, 0.12f);
            var border = new Color(0f, 0f, 0f, 0.35f);
            style.borderTopColor = style.borderBottomColor = border;
            style.borderLeftColor = style.borderRightColor = border;
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1f;
            style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = 3f;
            body.style.paddingLeft = 4f;
            body.style.paddingRight = 4f;
            body.style.paddingBottom = 4f;

            Root.Add(new IMGUIContainer(() =>
            {
                bool open = SessionState.GetBool(key, false);
                var header = EditorGUILayout.GetControlRect(false, HeaderHeight);
                drawHeader(new Rect(header.x + 20f, header.y, header.width - 24f, header.height));
                bool allowed = canOpen == null || canOpen();
                if (!allowed && open)
                {
                    open = false;
                    SessionState.SetBool(key, false);
                    Show(false);
                }
                if (allowed && Event.current.type == EventType.MouseDown && Event.current.button == 0 && header.Contains(Event.current.mousePosition))
                {
                    open = !open;
                    SessionState.SetBool(key, open);
                    Show(open);
                    Event.current.Use();
                }
                if (Event.current.type != EventType.Repaint)
                    return;
                float line = EditorGUIUtility.singleLineHeight;
                using (new EditorGUI.DisabledScope(!allowed))
                    EditorStyles.foldout.Draw(new Rect(header.x + 4f, header.y + (header.height - line) * 0.5f, 14f, line), false, false, open, false);
            }));
            body.Add(new IMGUIContainer(drawBody));
            Root.Add(body);
            Show(SessionState.GetBool(key, false));
        }

        void Show(bool open) => body.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
#endif
