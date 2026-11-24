#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VAP.Editor
{
    internal class SubMenusSection
    {
        const string RootLabel = "Main Menu";
        const float ChildIndent = 16f, HeaderHeight = 22f, Gap = 4f;

        class Entry
        {
            public VRCExpressionsMenu menu;
            public string path;
            public List<string> steps;
            public VRCExpressionsMenu parent;
            public int control = -1;
            public List<(VRCExpressionsMenu menu, int control)> removedLinks = new List<(VRCExpressionsMenu, int)>();
            public bool Removed => removedLinks.Count > 0;
        }

        readonly VAPExpressions owner;
        readonly Dictionary<VRCExpressionsMenu, MenuSection> sections = new Dictionary<VRCExpressionsMenu, MenuSection>();
        readonly Dictionary<VRCExpressionsMenu, string> paths = new Dictionary<VRCExpressionsMenu, string>();
        static GUIStyle stepStyle, stepBoldStyle, slashStyle;

        internal SubMenusSection(VAPExpressions owner)
        {
            this.owner = owner;
        }

        List<Entry> Entries()
        {
            var result = new List<Entry>();
            var roots = owner.RootMenus();
            var seen = new HashSet<VRCExpressionsMenu>(roots);
            foreach (var root in roots)
            {
                string at = owner.RootPath(root);
                string label = at ?? (roots.Count == 1 ? RootLabel : $"{RootLabel} ({root.name})");
                var entry = new Entry
                {
                    menu = root,
                    path = label,
                    steps = at != null ? at.Split('/').ToList() : new List<string> { label },
                };
                result.Add(entry);
                Walk(root, at != null ? entry : null, seen, result);
            }
            return result;
        }

        void Walk(VRCExpressionsMenu menu, Entry parent, HashSet<VRCExpressionsMenu> seen, List<Entry> result)
        {
            if (menu.controls == null)
                return;
            for (int i = 0; i < menu.controls.Count; i++)
            {
                var c = menu.controls[i];
                var sub = c?.subMenu;
                if (sub == null || !seen.Add(sub))
                    continue;
                string name = MenuSection.CleanName(c.name);
                if (name.Length == 0)
                    name = sub.name;
                var entry = new Entry
                {
                    menu = sub,
                    parent = menu,
                    control = i,
                    path = parent == null ? name : parent.path + "/" + name,
                    steps = parent == null ? new List<string>() : new List<string>(parent.steps),
                };
                entry.steps.Add(name);
                if (parent != null)
                    entry.removedLinks.AddRange(parent.removedLinks);
                if (owner.IsRemoved(menu, i))
                    entry.removedLinks.Add((menu, i));
                result.Add(entry);
                Walk(sub, entry, seen, result);
            }
        }

        GenericMenu.MenuFunction Restore(Entry e)
        {
            var links = e.removedLinks;
            return () =>
            {
                foreach (var (m, i) in links)
                    ExpressionsOverrides.SetRemoved(owner, m, i, false);
            };
        }

        MenuSection Section(VRCExpressionsMenu menu)
        {
            if (!sections.TryGetValue(menu, out var section))
                sections[menu] = section = new MenuSection(owner, menu, PathOf);
            return section;
        }

        internal List<string> SingleSteps()
        {
            var entries = Entries();
            return entries.Count == 1 ? entries[0].steps : null;
        }

        string PathOf(VRCExpressionsMenu menu) =>
            menu != null && paths.TryGetValue(menu, out var path) ? path : null;

        internal void Draw()
        {
            var entries = Entries();
            paths.Clear();
            foreach (var e in entries)
                paths[e.menu] = e.path;
            if (entries.Count == 1)
            {
                Section(entries[0].menu).Draw();
                return;
            }
            foreach (var e in entries)
            {
                var menu = e.menu;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                string key = "VAP.SubMenu." + menu.GetInstanceID();
                bool open = SessionState.GetBool(key, false);
                var header = EditorGUILayout.GetControlRect(false, HeaderHeight);
                bool changed = ExpressionsOverrides.MenuChanged(owner, menu);
                if (changed)
                    ExpressionsOverrides.DrawBar(header, e.parent != null && owner.IsRemoved(e.parent, e.control));
                if (e.parent != null && ExpressionsOverrides.ContextClick(header))
                {
                    var context = new GenericMenu();
                    var parent = e.parent;
                    int control = e.control;
                    if (e.Removed && !owner.IsRemoved(parent, control))
                        context.AddItem(new GUIContent("Restore Sub Menu"), false, Restore(e));
                    else
                        ExpressionsOverrides.AddRemoveItems(context, owner, parent, control, "Sub Menu");
                    context.ShowAsContext();
                    Event.current.Use();
                }
                float line = EditorGUIUtility.singleLineHeight;
                var foldout = new Rect(header.x + 2f, header.y + (header.height - line) * 0.5f, header.width - 2f, line);
                bool now = EditorGUI.Foldout(foldout, open, new GUIContent("", menu.name), true);
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && header.Contains(Event.current.mousePosition))
                {
                    now = !open;
                    Event.current.Use();
                }
                if (now != open)
                    SessionState.SetBool(key, now);
                using (new EditorGUI.DisabledScope(e.Removed))
                    DrawSteps(new Rect(foldout.x + 16f, header.y, foldout.width - 16f, header.height), e.steps, changed);
                if (now)
                {
                    var section = Section(menu);
                    section.RestoreSection = e.Removed ? Restore(e) : null;
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(ChildIndent);
                    EditorGUILayout.BeginVertical();
                    section.Draw();
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }
        }

        internal static void DrawHeader(Rect header, List<string> steps, GUIContent title, string bits)
        {
            float bitsWidth = bits != null ? VAPEditorGUI.RightMini.CalcSize(new GUIContent(bits)).x + 8f : 0f;
            var left = new Rect(header.x, header.y, header.width - bitsWidth, header.height);
            if (steps != null)
                DrawSteps(left, steps, true);
            else
                EditorGUI.LabelField(left, title, EditorStyles.boldLabel);
            if (bits != null)
                EditorGUI.LabelField(header, bits, VAPEditorGUI.RightMini);
        }

        internal static float DrawSteps(Rect rect, List<string> steps, bool bold)
        {
            stepStyle ??= new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            stepBoldStyle ??= new GUIStyle(stepStyle) { fontStyle = FontStyle.Bold };
            var style = bold ? stepBoldStyle : stepStyle;
            slashStyle ??= new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };
            float x = rect.x;
            for (int i = 0; i < steps.Count && x < rect.xMax; i++)
            {
                if (i > 0)
                {
                    GUI.Label(new Rect(x, rect.y, 10f, rect.height), "/", slashStyle);
                    x += 10f + Gap;
                }
                string name = steps[i];
                float w = style.CalcSize(new GUIContent(name)).x;
                if (Event.current.type == EventType.Repaint)
                    GUI.Label(new Rect(x, rect.y, Mathf.Min(w, rect.xMax - x), rect.height), name, style);
                x += w + Gap;
            }
            return x - Gap;
        }
    }
}
#endif
