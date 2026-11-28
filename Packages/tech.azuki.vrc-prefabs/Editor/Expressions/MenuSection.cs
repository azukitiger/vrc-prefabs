#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using ControlType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType;

namespace VAP.Editor {
    internal class MenuSection {
        static readonly string[] RadialParams = { "Rotation" };
        static readonly Color RowTint = new Color(1f, 1f, 1f, 0.035f);
        static readonly Color Divider = new Color(0f, 0f, 0f, 0.45f);
        static readonly Color LightRowFill = new Color(0.6f, 0.6f, 0.6f);
        static readonly Color LightRowTint = new Color(0f, 0f, 0f, 0.05f);
        const float RowHeight = 26f, IconSize = 24f, IconPad = 4f, DefaultWidth = 200f, CheckWidth = 16f, Indent = 20f, Pad = 6f;

        static float Line => EditorGUIUtility.singleLineHeight;
        static float Step => Line + EditorGUIUtility.standardVerticalSpacing;

        readonly VAPExpressions owner;
        readonly VRCExpressionsMenu subMenu;
        SerializedObject assetSO;
        int count;
        internal GenericMenu.MenuFunction RestoreSection;
        bool SectionRemoved => RestoreSection != null;
        static GUIStyle nameStyle;
        HashSet<string> unsynced = new HashSet<string>();
        static GUIStyle listBox;

        internal MenuSection(VAPExpressions owner, VRCExpressionsMenu subMenu = null) {
            this.owner = owner;
            this.subMenu = subMenu;
        }

        VRCExpressionsMenu Asset => !ReferenceEquals(subMenu, null) ? subMenu : owner.menuAsset;

        internal void Draw() {
            var asset = Asset;
            if (asset == null) {
                return;
            }

            if (assetSO == null || assetSO.targetObject != asset) {
                assetSO = new SerializedObject(asset);
            }

            assetSO.Update();
            unsynced = owner.UnsyncedParameters();
            var prop = assetSO.FindProperty(nameof(VRCExpressionsMenu.controls));
            count = prop.arraySize;
            DrawList(count, i => Height(prop.GetArrayElementAtIndex(i), i) + 4f,
                (row, content, i) => {
                    DrawRow(row, i, i < count - 1);
                    DrawControl(content, row, prop.GetArrayElementAtIndex(i), i);
                });
            if (Kept() > VRCExpressionsMenu.MAX_CONTROLS) {
                EditorGUILayout.HelpBox($"VRChat menus show at most {VRCExpressionsMenu.MAX_CONTROLS} controls.", MessageType.Warning);
            }
        }

        internal static void DrawList(int count, System.Func<int, float> height, System.Action<Rect, Rect, int> draw) {
            var heights = new float[count];
            float total = 0f;
            for (int i = 0; i < count; i++) {
                total += heights[i] = height(i);
            }

            var box = GUILayoutUtility.GetRect(10f, total + 2f, GUILayout.ExpandWidth(true));
            listBox ??= new GUIStyle("RL Background");
            if (Event.current.type == EventType.Repaint) {
                listBox.Draw(box, false, false, false, false);
            }

            float y = box.y + 1f;
            for (int i = 0; i < count; i++) {
                var row = new Rect(box.x + 1f, y, box.width - 2f, heights[i]);
                draw(row, new Rect(box.x + Pad, y, box.width - Pad * 2f, heights[i]), i);
                y += heights[i];
            }
        }

        int Kept() {
            int n = 0;
            for (int i = 0; i < count; i++) {
                if (!owner.IsRemoved(Asset, i)) {
                    n++;
                }
            }

            return n;
        }

        internal static void DrawRow(Rect rect, int index, bool divider) {
            if (Event.current.type != EventType.Repaint) {
                return;
            }

            if (!EditorGUIUtility.isProSkin) {
                EditorGUI.DrawRect(rect, LightRowFill);
            }

            if (index % 2 == 1) {
                EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? RowTint : LightRowTint);
            }

            if (divider) {
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Divider);
            }
        }

        static ControlType TypeOf(SerializedProperty control) =>
            (ControlType)control.FindPropertyRelative("type").intValue;

        static string[] SubParamNames(ControlType type) =>
            type == ControlType.RadialPuppet ? RadialParams : new string[0];

        static bool IsToggleLike(ControlType type) => type == ControlType.Toggle || type == ControlType.Button;

        static string ParamName(SerializedProperty control) =>
            control.FindPropertyRelative("parameter").FindPropertyRelative("name").stringValue;

        VRCExpressionParameters.Parameter Param(string name) => owner.FindParameter(name);

        List<(string axis, VRCExpressionParameters.Parameter param)> Axes(SerializedProperty control, ControlType type) {
            var result = new List<(string, VRCExpressionParameters.Parameter)>();
            var names = SubParamNames(type);
            var subs = control.FindPropertyRelative("subParameters");
            for (int i = 0; i < names.Length && i < subs.arraySize; i++) {
                var p = Param(subs.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue);
                if (p != null) {
                    result.Add((names[i], p));
                }
            }
            return result;
        }

        float Height(SerializedProperty control, int index) {
            int axes = Axes(control, TypeOf(control)).Count;
            return RowHeight + (axes > 1 ? axes * Step : 0f);
        }

        void DrawControl(Rect rect, Rect edges, SerializedProperty control, int index) {
            var menu = Asset;
            var type = TypeOf(control);
            var axes = Axes(control, type);
            var related = new List<string>();
            bool hasValue = IsToggleLike(type) && Param(ParamName(control)) != null;
            if (hasValue) {
                related.Add(ParamName(control));
            }

            foreach (var (_, p) in axes) {
                related.Add(p.name);
            }

            bool removed = owner.IsRemoved(menu, index);
            bool removedChanged = ExpressionsOverrides.RemovedChanged(owner, menu, index);
            bool defaultChanged = related.Exists(n => ExpressionsOverrides.DefaultChanged(owner, n));
            if (removedChanged) {
                ExpressionsOverrides.DrawEdgeBar(edges, removed);
            }

            if (defaultChanged) {
                ExpressionsOverrides.DrawRightEdgeBar(edges);
            }

            if (ExpressionsOverrides.ContextClick(rect)) {
                ShowContext(menu, index, type == ControlType.SubMenu, removed, defaultChanged, related);
            }

            var full = new Rect(rect.x, rect.y + 2f, rect.width, RowHeight);
            var row = new Rect(rect.x, full.y + (RowHeight - Line) * 0.5f, rect.width, Line);
            var check = new Rect(row.xMax - CheckWidth, row.y, CheckWidth, Line);
            var value = new Rect(row.xMax - DefaultWidth, row.y, DefaultWidth, Line);
            var name = new Rect(row.x, row.y, check.x - row.x - 8f, Line);
            bool inline = axes.Count == 1;
            if (inline) {
                name.width = value.x - row.x - 8f;
            }

            using (new EditorGUI.DisabledScope(removed || SectionRemoved)) {
                DrawName(new Rect(name.x, full.y, name.width, RowHeight), control, string.Join("\n", related.ConvertAll(n => ParamInfo(Param(n), hasValue && n == ParamName(control) ? control : null))));
                if (type == ControlType.SubMenu) {
                    DrawSubMenuIcon(check);
                }
            }

            if (IsToggleLike(type) && Param(ParamName(control)) is VRCExpressionParameters.Parameter p0) {
                DrawToggleDefault(check, control, p0);
            }

            if (inline) {
                DrawSliderDefault(value, axes[0].param, type == ControlType.RadialPuppet);
            } else {
                foreach (var (axis, p) in axes) {
                    row.y += Step;
                    var axisValue = new Rect(value.x, row.y, row.xMax - value.x, Line);
                    EditorGUI.LabelField(new Rect(row.x + Indent, row.y, axisValue.x - row.x - Indent - 8f, Line), new GUIContent(axis, ParamInfo(p)), EditorStyles.miniLabel);
                    DrawSliderDefault(axisValue, p, type == ControlType.RadialPuppet);
                }
            }
        }

        void ShowContext(VRCExpressionsMenu menu, int index, bool subMenu, bool removed, bool defaultChanged, List<string> related) {
            var context = new GenericMenu();
            if (!removed && SectionRemoved) {
                context.AddItem(new GUIContent("Restore Sub Menu"), false, RestoreSection);
            } else {
                ExpressionsOverrides.AddRemoveItems(context, owner, menu, index, subMenu);
            }

            if (defaultChanged) {
                context.AddSeparator("");
                context.AddItem(new GUIContent("Revert Menu Item Default Value"), false, () => {
                    foreach (var n in related) {
                        ExpressionsOverrides.RevertDefault(owner, n);
                    }
                });
            }
            context.ShowAsContext();
            Event.current.Use();
        }

        internal static string CleanName(string name) =>
            Regex.Replace(Regex.Replace(name ?? "", "<[^>]*>", ""), @"\s+", " ").Trim();

        static void DrawName(Rect rect, SerializedProperty control, string tooltip) {
            DrawName(rect, control.FindPropertyRelative("icon").objectReferenceValue as Texture2D,
                new GUIContent(CleanName(control.FindPropertyRelative("name").stringValue), tooltip));
        }

        static void DrawSubMenuIcon(Rect rect) {
            if (Event.current.type != EventType.Repaint) {
                return;
            }

            float px = 1f / EditorGUIUtility.pixelsPerPoint;
            float Snap(float v) => Mathf.Round(v / px) * px;
            const float width = 12f, height = 10f, dot = 2f, gap = 2f, step = 4f;
            float x = Snap(rect.x + (rect.width - width) * 0.5f);
            float y = Snap(rect.y + (rect.height - height) * 0.5f);
            var text = EditorStyles.label.normal.textColor;
            var color = new Color(text.r, text.g, text.b, GUI.enabled ? 0.6f : 0.3f);
            for (int i = 0; i < 3; i++) {
                float row = y + i * step;
                EditorGUI.DrawRect(new Rect(x, row, dot, dot), color);
                EditorGUI.DrawRect(new Rect(x + dot + gap, row, width - dot - gap, dot), color);
            }
        }

        string ParamInfo(VRCExpressionParameters.Parameter p, SerializedProperty control = null) {
            bool synced = p.networkSynced && !unsynced.Contains(p.name);
            string name = p.name;
            if (control != null && p.valueType != VRCExpressionParameters.ValueType.Bool) {
                float v = control.FindPropertyRelative("value").floatValue;
                name += " = " + (p.valueType == VRCExpressionParameters.ValueType.Int ? Mathf.RoundToInt(v).ToString() : v.ToString("0.###", CultureInfo.InvariantCulture));
            }
            return ParamInfo(name, p.valueType.ToString(), synced ? VAPExpressions.SyncedBits(p) : 0, synced);
        }

        internal static string ParamInfo(string name, string type, int bits, bool synced) =>
            $"{name} | {type} | {bits} {(bits == 1 ? "bit" : "bits")} | {(synced ? "Synced" : "Unsynced")}";

        internal static void DrawName(Rect rect, Texture2D icon, GUIContent text) {
            nameStyle ??= new GUIStyle(EditorStyles.label) {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Clip,
                padding = new RectOffset(0, 0, 0, 0),
            };
            float x = rect.x;
            if (icon != null) {
                var iconRect = new Rect(rect.x + IconPad, rect.y + (rect.height - IconSize) * 0.5f, IconSize, IconSize);
                if (Event.current.type == EventType.Repaint) {
                    var color = GUI.color;
                    if (!GUI.enabled) {
                        GUI.color = new Color(color.r, color.g, color.b, color.a * 0.5f);
                    }

                    GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
                    GUI.color = color;
                }
                x = iconRect.xMax + IconPad;
            }
            EditorGUI.LabelField(new Rect(x, rect.y, rect.xMax - x, rect.height), text, nameStyle);
        }

        void DrawToggleDefault(Rect rect, SerializedProperty control, VRCExpressionParameters.Parameter p) {
            float value = control.FindPropertyRelative("value").floatValue;
            float current = owner.FindDefault(p.name)?.value ?? p.defaultValue;
            bool isBool = p.valueType == VRCExpressionParameters.ValueType.Bool;
            bool on = isBool ? current != 0f : Mathf.Approximately(current, value);
            EditorGUI.BeginChangeCheck();
            on = GUI.Toggle(rect, on, GUIContent.none, EditorStyles.toggle);
            if (!EditorGUI.EndChangeCheck()) {
                return;
            }

            float v = isBool ? (on ? 1f : 0f) : (on ? value : 0f);
            ExpressionsOverrides.SetDefault(owner, p.name, v, p.defaultValue);
        }

        void DrawSliderDefault(Rect rect, VRCExpressionParameters.Parameter p, bool radial) {
            float current = owner.FindDefault(p.name)?.value ?? p.defaultValue;
            EditorGUI.BeginChangeCheck();
            float v;
            VAPExpressions.Range(p, out float min, out float max);
            if (p.valueType == VRCExpressionParameters.ValueType.Int) {
                v = EditorGUI.IntSlider(rect, Mathf.RoundToInt(current), (int)min, (int)max);
            } else if (p.valueType == VRCExpressionParameters.ValueType.Bool) {
                v = EditorGUI.Toggle(rect, current != 0f) ? 1f : 0f;
            } else {
                v = EditorGUI.Slider(rect, current, radial ? Mathf.Max(min, 0f) : min, max);
            }

            if (EditorGUI.EndChangeCheck()) {
                ExpressionsOverrides.SetDefault(owner, p.name, v, p.defaultValue);
            }
        }
    }
}
#endif
