#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using ControlType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType;

namespace VAP.Editor
{
    // Shows a menu asset, one row per item. Start values and removed items are stored on the component.
    internal class MenuSection
    {
        static readonly string[] RadialParams = { "Rotation" };
        static readonly Color RowTint = new Color(1f, 1f, 1f, 0.035f);
        static readonly Color Divider = new Color(0f, 0f, 0f, 0.45f);
        const float RowHeight = 26f, IconSize = 24f, IconPad = 4f, DefaultWidth = 200f, CheckWidth = 16f, Indent = 20f;

        static float Line => EditorGUIUtility.singleLineHeight;
        static float Step => Line + EditorGUIUtility.standardVerticalSpacing;

        readonly VAPExpressions owner;
        readonly VRCExpressionsMenu subMenu;
        SerializedObject assetSO;
        ReorderableList list;
        readonly System.Func<VRCExpressionsMenu, string> pathOf;
        static GUIStyle nameStyle, pathStyle;

        // subMenu is null for the main menu.
        internal MenuSection(VAPExpressions owner, VRCExpressionsMenu subMenu = null, System.Func<VRCExpressionsMenu, string> pathOf = null)
        {
            this.owner = owner;
            this.subMenu = subMenu;
            this.pathOf = pathOf;
        }

        VRCExpressionsMenu Asset => !ReferenceEquals(subMenu, null) ? subMenu : owner.menuAsset;

        internal void Draw()
        {
            var asset = Asset;
            if (asset == null)
                return;
            if (assetSO == null || assetSO.targetObject != asset)
                assetSO = new SerializedObject(asset);
            assetSO.Update();
            var prop = assetSO.FindProperty(nameof(VRCExpressionsMenu.controls));
            GetList(prop).DoLayoutList();
            if (Kept() > VRCExpressionsMenu.MAX_CONTROLS)
                EditorGUILayout.HelpBox($"VRChat menus show at most {VRCExpressionsMenu.MAX_CONTROLS} controls.", MessageType.Warning);
        }

        ReorderableList GetList(SerializedProperty prop)
        {
            if (list == null || list.serializedProperty == null || list.serializedProperty.serializedObject != prop.serializedObject)
            {
                list = new ReorderableList(prop.serializedObject, prop, false, false, false, false)
                {
                    headerHeight = 0f,
                    elementHeightCallback = i => Height(list.serializedProperty.GetArrayElementAtIndex(i), i) + 4f,
                    drawElementCallback = (rect, i, active, focused) => DrawControl(rect, list.serializedProperty.GetArrayElementAtIndex(i), i),
                    drawElementBackgroundCallback = (rect, i, active, focused) => DrawBackground(rect, i),
                    footerHeight = 0f,
                    drawFooterCallback = rect => { },
                };
            }
            list.serializedProperty = prop;
            return list;
        }

        int Kept()
        {
            int n = 0;
            for (int i = 0; i < list.count; i++)
                if (!owner.IsRemoved(Asset, i))
                    n++;
            return n;
        }

        void DrawBackground(Rect rect, int index)
        {
            if (Event.current.type != EventType.Repaint || index < 0)
                return;
            if (index % 2 == 1)
                EditorGUI.DrawRect(rect, RowTint);
            if (index < list.count - 1)
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Divider);
        }

        static ControlType TypeOf(SerializedProperty control) =>
            (ControlType)control.FindPropertyRelative("type").intValue;

        // Axis puppets spring back to the center when let go, so only radial puppets get a start value.
        static string[] SubParamNames(ControlType type) =>
            type == ControlType.RadialPuppet ? RadialParams : new string[0];

        static bool IsToggleLike(ControlType type) => type == ControlType.Toggle || type == ControlType.Button;

        static string ParamName(SerializedProperty control) =>
            control.FindPropertyRelative("parameter").FindPropertyRelative("name").stringValue;

        VRCExpressionParameters.Parameter Param(string name)
        {
            var asset = owner.parametersAsset;
            if (asset == null || asset.parameters == null || string.IsNullOrEmpty(name))
                return null;
            foreach (var p in asset.parameters)
                if (p != null && p.name == name)
                    return p;
            return null;
        }

        // Puppet axes with a parameter in the parameters asset.
        List<(string axis, VRCExpressionParameters.Parameter param)> Axes(SerializedProperty control, ControlType type)
        {
            var result = new List<(string, VRCExpressionParameters.Parameter)>();
            var names = SubParamNames(type);
            var subs = control.FindPropertyRelative("subParameters");
            for (int i = 0; i < names.Length && i < subs.arraySize; i++)
            {
                var p = Param(subs.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue);
                if (p != null)
                    result.Add((names[i], p));
            }
            return result;
        }

        float Height(SerializedProperty control, int index)
        {
            if (owner.IsRemoved(Asset, index))
                return RowHeight;
            int axes = Axes(control, TypeOf(control)).Count;
            return RowHeight + (axes > 1 ? axes * Step : 0f);
        }

        void DrawControl(Rect rect, SerializedProperty control, int index)
        {
            var menu = Asset;
            var type = TypeOf(control);
            var axes = Axes(control, type);
            var related = new List<string>();
            if (IsToggleLike(type) && Param(ParamName(control)) != null)
                related.Add(ParamName(control));
            foreach (var (_, p) in axes)
                related.Add(p.name);
            bool removed = owner.IsRemoved(menu, index);
            bool changed = ExpressionsOverrides.RemovedChanged(owner, menu, index) ||
                           related.Exists(n => ExpressionsOverrides.DefaultChanged(owner, n));
            if (changed)
                ExpressionsOverrides.DrawBar(rect);
            if (ExpressionsOverrides.ContextClick(rect))
                ShowContext(menu, index, removed, changed, related);

            var full = new Rect(rect.x, rect.y + 2f, rect.width, RowHeight);
            var row = new Rect(rect.x, full.y + (RowHeight - Line) * 0.5f, rect.width, Line);
            var check = new Rect(row.xMax - CheckWidth, row.y, CheckWidth, Line);
            var value = new Rect(row.xMax - DefaultWidth, row.y, DefaultWidth, Line);
            var name = new Rect(row.x, row.y, check.x - row.x - 8f, Line);
            bool inline = axes.Count == 1;
            if (inline)
                name.width = value.x - row.x - 8f;

            string path = type == ControlType.SubMenu && !removed && pathOf != null
                ? pathOf(control.FindPropertyRelative("subMenu").objectReferenceValue as VRCExpressionsMenu)
                : null;
            if (path != null)
                name.width = Mathf.Min(name.width, row.width * 0.45f);

            using (new EditorGUI.DisabledScope(removed))
                DrawName(new Rect(name.x, full.y, name.width, RowHeight), control);
            if (removed)
                return;

            if (path != null)
            {
                pathStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, clipping = TextClipping.Clip };
                var target = new Rect(name.xMax + 8f, row.y, row.xMax - name.xMax - 8f, Line);
                EditorGUI.LabelField(target, new GUIContent(path, path), pathStyle);
            }

            if (IsToggleLike(type) && Param(ParamName(control)) is VRCExpressionParameters.Parameter p0)
                DrawToggleDefault(check, control, p0);

            if (inline)
            {
                DrawSliderDefault(value, axes[0].param, type == ControlType.RadialPuppet);
                return;
            }
            foreach (var (axis, p) in axes)
            {
                row.y += Step;
                var axisValue = new Rect(value.x, row.y, row.xMax - value.x, Line);
                EditorGUI.LabelField(new Rect(row.x + Indent, row.y, axisValue.x - row.x - Indent - 8f, Line), axis, EditorStyles.miniLabel);
                DrawSliderDefault(axisValue, p, type == ControlType.RadialPuppet);
            }
        }

        void ShowContext(VRCExpressionsMenu menu, int index, bool removed, bool changed, List<string> related)
        {
            var context = new GenericMenu();
            if (changed)
                context.AddItem(new GUIContent("Revert Menu Item"), false, () =>
                {
                    ExpressionsOverrides.RevertControl(owner, menu, index);
                    foreach (var n in related)
                        ExpressionsOverrides.RevertDefault(owner, n);
                });
            if (removed)
                context.AddItem(new GUIContent("Restore Menu Item"), false, () => ExpressionsOverrides.SetRemoved(owner, menu, index, false));
            else
                context.AddItem(new GUIContent("Remove Menu Item"), false, () => ExpressionsOverrides.SetRemoved(owner, menu, index, true));
            context.ShowAsContext();
            Event.current.Use();
        }

        // Menu item names as one plain line: rich text tags and line breaks removed.
        internal static string CleanName(string name) =>
            Regex.Replace(Regex.Replace(name ?? "", "<[^>]*>", ""), @"\s+", " ").Trim();

        static void DrawName(Rect rect, SerializedProperty control)
        {
            nameStyle ??= new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Clip, padding = new RectOffset(0, 0, 0, 0),
            };
            var icon = control.FindPropertyRelative("icon").objectReferenceValue as Texture2D;
            string text = CleanName(control.FindPropertyRelative("name").stringValue);
            float x = rect.x;
            if (icon != null)
            {
                var iconRect = new Rect(rect.x + IconPad, rect.y + (rect.height - IconSize) * 0.5f, IconSize, IconSize);
                if (Event.current.type == EventType.Repaint)
                {
                    var color = GUI.color;
                    if (!GUI.enabled)
                        GUI.color = new Color(color.r, color.g, color.b, color.a * 0.5f);
                    GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
                    GUI.color = color;
                }
                x = iconRect.xMax + IconPad;
            }
            EditorGUI.LabelField(new Rect(x, rect.y, rect.xMax - x, rect.height), text, nameStyle);
        }

        static readonly GUIContent DefaultOnLabel = new GUIContent("", "Starts on.");
        static readonly GUIContent DefaultChoiceLabel = new GUIContent("", "This is the item that's on at the start. Only one item using the same parameter can be.");

        void DrawToggleDefault(Rect rect, SerializedProperty control, VRCExpressionParameters.Parameter p)
        {
            float value = control.FindPropertyRelative("value").floatValue;
            float current = owner.FindDefault(p.name)?.value ?? p.defaultValue;
            bool isBool = p.valueType == VRCExpressionParameters.ValueType.Bool;
            bool on = isBool ? current != 0f : Mathf.Approximately(current, value);
            EditorGUI.BeginChangeCheck();
            on = GUI.Toggle(rect, on, isBool ? DefaultOnLabel : DefaultChoiceLabel, EditorStyles.toggle);
            if (!EditorGUI.EndChangeCheck())
                return;
            float v = isBool ? (on ? 1f : 0f) : (on ? value : 0f);
            ExpressionsOverrides.SetDefault(owner, p.name, v, p.defaultValue);
        }

        void DrawSliderDefault(Rect rect, VRCExpressionParameters.Parameter p, bool radial)
        {
            float current = owner.FindDefault(p.name)?.value ?? p.defaultValue;
            EditorGUI.BeginChangeCheck();
            float v;
            if (p.valueType == VRCExpressionParameters.ValueType.Int)
                v = EditorGUI.IntSlider(rect, Mathf.RoundToInt(current), 0, 255);
            else if (p.valueType == VRCExpressionParameters.ValueType.Bool)
                v = EditorGUI.Toggle(rect, current != 0f) ? 1f : 0f;
            else
                v = EditorGUI.Slider(rect, current, radial ? 0f : -1f, 1f);
            if (EditorGUI.EndChangeCheck())
                ExpressionsOverrides.SetDefault(owner, p.name, v, p.defaultValue);
        }
    }
}
#endif
