#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VAP.Editor
{
    // Instance values stored on the component when the data lives in an asset.
    internal static class ExpressionsOverrides
    {
        internal static readonly Color Bar = new Color(0.004f, 0.6f, 0.85f);

        internal static void DrawBar(Rect row)
        {
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(new Rect(row.x - 4f, row.y, 2f, row.height), Bar);
        }

        static void Changed(VAPExpressions c)
        {
            c.defaultOverrides.RemoveAll(o => o == null);
            c.controlOverrides.RemoveAll(o => o == null || o.IsEmpty);
            EditorUtility.SetDirty(c);
            PrefabUtility.RecordPrefabInstancePropertyModifications(c);
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

        internal static void RevertDefault(VAPExpressions c, string name)
        {
            Undo.RecordObject(c, "Revert Default");
            c.defaultOverrides.RemoveAll(o => o != null && o.name == name);
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

        internal static void SetName(VAPExpressions c, VRCExpressionsMenu menu, int control, int label, string value, string assetValue)
        {
            Undo.RecordObject(c, "Change Name");
            var o = Get(c, menu, control, label);
            o.overrideName = value != assetValue;
            o.name = o.overrideName ? value : "";
            Changed(c);
        }

        internal static void SetIcon(VAPExpressions c, VRCExpressionsMenu menu, int control, int label, Texture2D value, Texture2D assetValue)
        {
            Undo.RecordObject(c, "Change Icon");
            var o = Get(c, menu, control, label);
            o.overrideIcon = value != assetValue;
            o.icon = o.overrideIcon ? value : null;
            Changed(c);
        }

        internal static bool HasControl(VAPExpressions c, VRCExpressionsMenu menu, int control) =>
            c.controlOverrides.Exists(o => o != null && o.menu == menu && o.control == control && !o.IsEmpty);

        internal static void RevertControl(VAPExpressions c, VRCExpressionsMenu menu, int control)
        {
            Undo.RecordObject(c, "Revert Menu Item");
            c.controlOverrides.RemoveAll(o => o != null && o.menu == menu && o.control == control);
            Changed(c);
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
