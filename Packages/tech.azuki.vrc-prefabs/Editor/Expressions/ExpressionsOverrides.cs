#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace VAP.Editor
{
    internal static class ExpressionsOverrides
    {
        internal static readonly Color Bar = new Color(0.004f, 0.6f, 0.85f);

        static readonly Color RemovedBar = new Color(0.9f, 0.3f, 0.25f);

        internal static void DrawBar(Rect row, bool removed = false)
        {
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(new Rect(row.x - 4f, row.y, 2f, row.height), removed ? RemovedBar : Bar);
        }

        internal static void DrawEdgeBar(Rect row, bool removed = false)
        {
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(new Rect(row.x, row.y, 2f, row.height), removed ? RemovedBar : Bar);
        }

        internal static void DrawRightEdgeBar(Rect row)
        {
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(new Rect(row.xMax - 2f, row.y, 2f, row.height), Bar);
        }

        static void Changed(VAPExpressions c)
        {
            c.defaultOverrides.RemoveAll(o => o == null);
            c.controlOverrides.RemoveAll(o => o == null || o.IsEmpty);
            EditorUtility.SetDirty(c);
            PrefabUtility.RecordPrefabInstancePropertyModifications(c);
            ClearUnchanged(c, "defaultOverrides", "controlOverrides");
        }

        internal static void ClearUnchanged(Object c, params string[] names)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(c);
            if (source == null)
                return;
            using var mine = new SerializedObject(c);
            using var theirs = new SerializedObject(source);
            bool cleared = false;
            foreach (var name in names)
            {
                var a = mine.FindProperty(name);
                var b = theirs.FindProperty(name);
                if (a == null || b == null || !a.prefabOverride || !SerializedProperty.DataEquals(a, b))
                    continue;
                PrefabUtility.RevertPropertyOverride(a, InteractionMode.AutomatedAction);
                cleared = true;
            }
#if UNITY_2022_2_OR_NEWER
            if (cleared && c is Component component)
            {
                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(component);
                if (root != null)
                    PrefabUtility.RemoveUnusedOverrides(new[] { root }, InteractionMode.AutomatedAction);
            }
#endif
            if (cleared && c is Component owner)
            {
                var top = PrefabUtility.GetOutermostPrefabInstanceRoot(owner);
                var target = top != null ? top : owner.gameObject;
                WrappedToggles.Hide(target);
                EditorApplication.delayCall += () => WrappedToggles.Hide(target);
            }
        }

        internal static void SetDefault(VAPExpressions c, string name, float value, float assetValue)
        {
            Undo.RecordObject(c, "Change Default");
            var o = c.FindDefault(name);
            if (Mathf.Approximately(value, assetValue))
                c.defaultOverrides.Remove(o);
            else if (o != null)
                o.value = value;
            else
                c.defaultOverrides.Add(new VAPExpressions.DefaultOverride { name = name, value = value });
            Changed(c);
        }

        static VAPExpressions Source(VAPExpressions c) => PrefabUtility.GetCorrespondingObjectFromSource(c);

        internal static bool DefaultChanged(VAPExpressions c, string name)
        {
            var mine = c.FindDefault(name);
            var source = Source(c);
            if (source == null)
                return mine != null;
            var theirs = source.FindDefault(name);
            if (mine == null || theirs == null)
                return (mine == null) != (theirs == null);
            return !Mathf.Approximately(mine.value, theirs.value);
        }

        internal static bool RemovedChanged(VAPExpressions c, VRCExpressionsMenu menu, int control)
        {
            var source = Source(c);
            return c.IsRemoved(menu, control) != (source != null && source.IsRemoved(menu, control));
        }

        internal static bool MenuChanged(VAPExpressions c, VRCExpressionsMenu menu)
        {
            if (menu == null || menu.controls == null)
                return false;
            for (int i = 0; i < menu.controls.Count; i++)
            {
                if (RemovedChanged(c, menu, i))
                    return true;
                var control = menu.controls[i];
                if (control == null || c.IsRemoved(menu, i))
                    continue;
                if (control.parameter != null && !string.IsNullOrEmpty(control.parameter.name) && DefaultChanged(c, control.parameter.name))
                    return true;
                if (control.subParameters != null)
                    foreach (var sub in control.subParameters)
                        if (sub != null && !string.IsNullOrEmpty(sub.name) && DefaultChanged(c, sub.name))
                            return true;
            }
            return false;
        }

        internal static void RevertDefault(VAPExpressions c, string name)
        {
            Undo.RecordObject(c, "Revert Default");
            var theirs = Source(c)?.FindDefault(name);
            c.defaultOverrides.RemoveAll(o => o != null && o.name == name);
            if (theirs != null)
                c.defaultOverrides.Add(new VAPExpressions.DefaultOverride { name = name, value = theirs.value });
            Changed(c);
        }

        static VAPExpressions.ControlOverride Get(VAPExpressions c, VRCExpressionsMenu menu, int control, int label)
        {
            var o = c.FindControl(menu, control, label);
            if (o == null)
            {
                o = new VAPExpressions.ControlOverride { menu = menu, control = control, label = label };
                c.controlOverrides.Add(o);
            }
            return o;
        }

        internal static void SetRemoved(VAPExpressions c, VRCExpressionsMenu menu, int control, bool removed)
        {
            Undo.RecordObject(c, removed ? "Remove Menu Item" : "Restore Menu Item");
            Get(c, menu, control, -1).removed = removed;
            Changed(c);
        }

        static HashSet<string> RelatedNames(VAPExpressions c, VRCExpressionsMenu menu, int control)
        {
            var names = new HashSet<string>();
            var item = menu != null && menu.controls != null && control >= 0 && control < menu.controls.Count ? menu.controls[control] : null;
            if (item == null)
                return names;
            names.UnionWith(VAPExpressions.ControlParameters(item));
            if (item.subMenu == null)
                return names;
            var menus = new List<VRCExpressionsMenu> { item.subMenu };
            var seen = new HashSet<VRCExpressionsMenu> { item.subMenu };
            for (int m = 0; m < menus.Count; m++)
                if (menus[m].controls != null)
                    foreach (var sub in menus[m].controls)
                    {
                        names.UnionWith(VAPExpressions.ControlParameters(sub));
                        if (sub?.subMenu != null && seen.Add(sub.subMenu))
                            menus.Add(sub.subMenu);
                    }
            return names;
        }

        static List<(VRCExpressionsMenu menu, int control)> RelatedItems(VAPExpressions c, VRCExpressionsMenu menu, int control)
        {
            var names = RelatedNames(c, menu, control);
            var result = new List<(VRCExpressionsMenu, int)> { (menu, control) };
            if (names.Count == 0)
                return result;
            foreach (var m in c.AllMenus())
                if (m.controls != null)
                    for (int i = 0; i < m.controls.Count; i++)
                        if ((m != menu || i != control) && VAPExpressions.ControlParameters(m.controls[i]).Any(names.Contains))
                            result.Add((m, i));
            return result;
        }

        static void SetRelatedRemoved(VAPExpressions c, VRCExpressionsMenu menu, int control, bool removed)
        {
            Undo.RecordObject(c, removed ? "Remove Menu Item" : "Restore Menu Item");
            foreach (var (m, i) in RelatedItems(c, menu, control))
                Get(c, m, i, -1).removed = removed;
            Changed(c);
        }

        internal static void AddRemoveItems(GenericMenu context, VAPExpressions c, VRCExpressionsMenu menu, int control, bool subMenu)
        {
            string item = subMenu ? "Sub Menu" : "Menu Item";
            if (c.IsRemoved(menu, control))
            {
                context.AddItem(new GUIContent($"Restore {item}"), false, () => SetRemoved(c, menu, control, false));
                if (!subMenu)
                    context.AddItem(new GUIContent("Restore Related Menu Items"), false, () => SetRelatedRemoved(c, menu, control, false));
                return;
            }
            context.AddItem(new GUIContent($"Remove {item}"), false, () => SetRemoved(c, menu, control, true));
            if (!subMenu)
                context.AddItem(new GUIContent("Remove Related Menu Items"), false, () => SetRelatedRemoved(c, menu, control, true));
        }

        internal static bool ContextClick(Rect rect)
        {
            var e = Event.current;
            return (e.type == EventType.ContextClick || (e.type == EventType.MouseDown && e.button == 1)) && rect.Contains(e.mousePosition);
        }

        internal static void ShowRevert(string label, GenericMenu.MenuFunction revert)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(label), false, revert);
            menu.ShowAsContext();
            Event.current.Use();
        }
    }
}
#endif
