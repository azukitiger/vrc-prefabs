#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VAP.Editor
{
    // The main menu and every menu it reaches, each drawn once under its path.
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
        }

        readonly VAPExpressions owner;
        readonly Dictionary<VRCExpressionsMenu, MenuSection> sections = new Dictionary<VRCExpressionsMenu, MenuSection>();
        readonly Dictionary<VRCExpressionsMenu, string> paths = new Dictionary<VRCExpressionsMenu, string>();
        static GUIStyle stepStyle, slashStyle;

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
                // A root placed under a path in the avatar's menu shows that path; one merged into the main menu doesn't.
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
                if (sub == null || owner.IsRemoved(menu, i) || !seen.Add(sub))
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
                result.Add(entry);
                Walk(sub, entry, seen, result);
            }
        }

        MenuSection Section(VRCExpressionsMenu menu)
        {
            if (!sections.TryGetValue(menu, out var section))
                sections[menu] = section = new MenuSection(owner, menu, PathOf);
            return section;
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
                if (ExpressionsOverrides.MenuChanged(owner, menu))
                    ExpressionsOverrides.DrawBar(header);
                if (e.parent != null && ExpressionsOverrides.ContextClick(header))
                {
                    var context = new GenericMenu();
                    var parent = e.parent;
                    int control = e.control;
                    context.AddItem(new GUIContent("Remove Menu Item"), false, () => ExpressionsOverrides.SetRemoved(owner, parent, control, true));
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
                DrawSteps(new Rect(foldout.x + 16f, header.y, foldout.width - 16f, header.height), e.steps);
                if (now)
                {
                    var section = Section(menu);
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

        static void DrawSteps(Rect rect, List<string> steps)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            stepStyle ??= new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
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
                float w = stepStyle.CalcSize(new GUIContent(name)).x;
                GUI.Label(new Rect(x, rect.y, Mathf.Min(w, rect.xMax - x), rect.height), name, stepStyle);
                x += w + Gap;
            }
        }
    }
}
#endif
