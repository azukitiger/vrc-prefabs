#if UNITY_EDITOR
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace VAP.Editor
{
    internal class CartesianGraph
    {
        static readonly Color Background = new Color(0.16f, 0.16f, 0.16f);
        static readonly Color Grid = new Color(1f, 1f, 1f, 0.06f);
        static readonly Color Axis = new Color(1f, 1f, 1f, 0.25f);
        static readonly Color Point = new Color(0.55f, 0.75f, 1f);
        static readonly Color Selected = new Color(1f, 0.6f, 0.2f);
        const float PointSize = 9f;
        const float Padding = 14f;

        static GUIStyle valueStyle;
        static GUIStyle ValueStyle => valueStyle ??= new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.85f, 0.85f, 0.85f) } };
        static GUIStyle axisStyle;
        static GUIStyle AxisStyle => axisStyle ??= new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };

        internal int selected = -1;
        int dragging = -1;
        Rect dragBounds;

        internal bool Draw(SerializedProperty points, string positionName, string valueName, string xLabel, string yLabel)
        {
            var rect = GUILayoutUtility.GetRect(10f, 4000f, 180f, 180f);
            rect = EditorGUI.IndentedRect(rect);
            int id = GUIUtility.GetControlID(FocusType.Passive, rect);
            var area = new Rect(rect.x + Padding, rect.y + Padding, rect.width - Padding * 2f, rect.height - Padding * 2f);
            var bounds = dragging >= 0 ? dragBounds : Bounds(points, positionName);
            Vector2 ToScreen(Vector2 p) => new Vector2(
                area.x + (p.x - bounds.xMin) / bounds.width * area.width,
                area.yMax - (p.y - bounds.yMin) / bounds.height * area.height);
            Vector2 ToGraph(Vector2 s) => new Vector2(
                bounds.xMin + (s.x - area.x) / area.width * bounds.width,
                bounds.yMin + (area.yMax - s.y) / area.height * bounds.height);

            bool changed = false;
            var e = Event.current;
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown when GUI.enabled && e.button == 0 && rect.Contains(e.mousePosition):
                    selected = Nearest(points, positionName, ToScreen, e.mousePosition);
                    if (selected >= 0)
                    {
                        dragging = selected;
                        dragBounds = bounds;
                        GUIUtility.hotControl = id;
                    }
                    e.Use();
                    break;
                case EventType.MouseDrag when GUIUtility.hotControl == id && dragging >= 0 && dragging < points.arraySize:
                    var p = ToGraph(e.mousePosition);
                    if (!e.shift)
                        p = new Vector2(Mathf.Round(p.x * 20f) / 20f, Mathf.Round(p.y * 20f) / 20f);
                    points.GetArrayElementAtIndex(dragging).FindPropertyRelative(positionName).vector2Value = p;
                    changed = true;
                    e.Use();
                    break;
                case EventType.MouseUp when GUIUtility.hotControl == id:
                    GUIUtility.hotControl = 0;
                    dragging = -1;
                    e.Use();
                    break;
                case EventType.Repaint:
                    Paint(rect, area, bounds, points, positionName, valueName, ToScreen, xLabel, yLabel);
                    break;
            }
            return changed;
        }

        void Paint(Rect rect, Rect area, Rect bounds, SerializedProperty points, string positionName, string valueName,
            System.Func<Vector2, Vector2> toScreen, string xLabel, string yLabel)
        {
            EditorGUI.DrawRect(rect, Background);
            float step = GridStep(Mathf.Max(bounds.width, bounds.height));
            for (float x = Mathf.Ceil(bounds.xMin / step) * step; x <= bounds.xMax; x += step)
            {
                float sx = toScreen(new Vector2(x, 0f)).x;
                EditorGUI.DrawRect(new Rect(sx, area.y, 1f, area.height), Mathf.Abs(x) < step * 0.01f ? Axis : Grid);
            }
            for (float y = Mathf.Ceil(bounds.yMin / step) * step; y <= bounds.yMax; y += step)
            {
                float sy = toScreen(new Vector2(0f, y)).y;
                EditorGUI.DrawRect(new Rect(area.x, sy, area.width, 1f), Mathf.Abs(y) < step * 0.01f ? Axis : Grid);
            }
            AxisStyle.alignment = TextAnchor.LowerRight;
            AxisStyle.Draw(new Rect(rect.x, rect.yMax - 14f, rect.width - 4f, 14f), new GUIContent("X: " + xLabel), false, false, false, false);
            AxisStyle.alignment = TextAnchor.UpperLeft;
            AxisStyle.Draw(new Rect(rect.x + 4f, rect.y, rect.width, 14f), new GUIContent("Y: " + yLabel), false, false, false, false);

            for (int i = 0; i < points.arraySize; i++)
            {
                var element = points.GetArrayElementAtIndex(i);
                var s = toScreen(element.FindPropertyRelative(positionName).vector2Value);
                EditorGUI.DrawRect(new Rect(s.x - PointSize / 2f, s.y - PointSize / 2f, PointSize, PointSize), i == selected ? Selected : Point);
                string value = element.FindPropertyRelative(valueName).floatValue.ToString("0.###", CultureInfo.InvariantCulture);
                ValueStyle.Draw(new Rect(s.x + PointSize / 2f + 2f, s.y - 8f, 60f, 16f), new GUIContent(value), false, false, false, false);
            }
        }

        static int Nearest(SerializedProperty points, string positionName, System.Func<Vector2, Vector2> toScreen, Vector2 mouse)
        {
            int best = -1;
            float bestDistance = PointSize * 1.5f;
            for (int i = 0; i < points.arraySize; i++)
            {
                float d = Vector2.Distance(toScreen(points.GetArrayElementAtIndex(i).FindPropertyRelative(positionName).vector2Value), mouse);
                if (d < bestDistance)
                {
                    best = i;
                    bestDistance = d;
                }
            }
            return best;
        }

        static Rect Bounds(SerializedProperty points, string positionName)
        {
            var min = Vector2.zero;
            var max = Vector2.one;
            for (int i = 0; i < points.arraySize; i++)
            {
                var p = points.GetArrayElementAtIndex(i).FindPropertyRelative(positionName).vector2Value;
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            var size = max - min;
            float pad = Mathf.Max(size.x, size.y) * 0.1f;
            return Rect.MinMaxRect(min.x - pad, min.y - pad, max.x + pad, max.y + pad);
        }

        static float GridStep(float span)
        {
            float step = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(span)));
            if (span / step < 4f)
                step /= 2f;
            return step / 2f;
        }
    }
}
#endif
