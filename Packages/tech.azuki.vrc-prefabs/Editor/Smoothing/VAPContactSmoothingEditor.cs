#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace VAP.Editor
{
    [CustomEditor(typeof(VAPSmoothing), true)]
    [CanEditMultipleObjects]
    public class VAPContactSmoothingEditor : UnityEditor.Editor
    {
        static readonly GUIContent ContactLabel = new GUIContent("Contact Parameter",
            "The VRC Contact Receiver parameter to smooth, from a component on this object.");
        static readonly GUIContent PhysBoneLabel = new GUIContent("PhysBone Parameter",
            "The VRC PhysBone IsGrabbed, IsPosed, Angle, Stretch or Squish parameter to smooth, from a component on this object.");
        static readonly GUIContent OutputLabel = new GUIContent("Output Parameter",
            "Animator parameter that receives the smoothed value. If empty, VAP/ followed by the input parameter.");
        static readonly GUIContent SmoothingLabel = new GUIContent("Smoothing Factor",
            "How fast the output follows the input. Custom sets your own speed.");
        static readonly GUIContent IncreaseLabel = new GUIContent("Rise Factor",
            "Smoothing used when the value goes up. None follows the input directly.");
        static readonly GUIContent DecreaseLabel = new GUIContent("Drop Factor",
            "Smoothing used when the value goes down. None follows the input directly.");
        static GUIContent linkedIcon, unlinkedIcon;
        static GUIContent LinkedIcon => linkedIcon ??= new GUIContent(EditorGUIUtility.IconContent("Linked").image,
            "Rise and drop use the same factor. Click to set them separately.");
        static GUIContent UnlinkedIcon => unlinkedIcon ??= new GUIContent(EditorGUIUtility.IconContent("Unlinked").image,
            "Rise and drop use separate factors. Click to use one factor for both.");
        static readonly GUIContent PassesLabel = new GUIContent("Passes",
            "Double smooths through an extra hidden step for a softer curve. Single moves the output straight toward the input.");
        static readonly GUIContent SpeedLabel = new GUIContent("Speed", "Higher follows faster. Touch is 8, Gesture is 20.");
        static readonly GUIContent TimerLabel = new GUIContent("Timer",
            "Also count, in seconds, how long the input is above 0. Goes back to 0 when it is.");
        static readonly GUIContent TimerStartLabel = new GUIContent("Start Above Value (>)",
            "The timer counts while the input is above this value and resets when it is at or below it.");
        static readonly GUIContent TimerResetLabel = new GUIContent("Reset Below Value (<=)",
            "The timer resets when the input is at or below this value. In between, a timer that has started keeps counting.");
        static readonly GUIContent TimerParameterLabel = new GUIContent("Timer Parameter",
            "Animator parameter that receives the touch time. If empty, the output parameter followed by Timer.");

        static readonly GUIContent TransformLabel = new GUIContent("Transform Output",
            "Map the smoothed value and another parameter to the output with a 2D blend, like a PhysBone Stretch that fades in and out with a smoothed IsGrabbed.");
        static readonly GUIContent TransformByLabel = new GUIContent("Y Parameter",
            "Parameter used as the Y axis. It is not smoothed itself.");
        static readonly GUIContent BeforeLabel = new GUIContent("Apply Before Smoothing",
            "Transform the input before it is smoothed, so the smoothing follows the 2D blend result. Off transforms the smoothed value.");
        static readonly GUIContent PointsTooltip = new GUIContent("", "Drag points in the graph to move them, hold Shift to move freely.");

        readonly CartesianGraph graph = new CartesianGraph();
        ReorderableList pointList;

        static GUIStyle placeholderStyle;
        static GUIStyle PlaceholderStyle => placeholderStyle ??= new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = new Color(0.5f, 0.5f, 0.5f) },
            padding = new RectOffset(3, 3, 0, 0),
        };

        bool PhysBone => target is VAPPhysBoneSmoothing;

        public override VisualElement CreateInspectorGUI() =>
            VAPPrefabLock.AddEditButton(VAPInspectorHeader.Create(PhysBone ? "VRC PhysBone Smoothing" : "VRC Contact Smoothing", DrawFields, this), this);

        void DrawFields()
        {
            if (target == null)
                return;
            serializedObject.Update();
            var c = (VAPSmoothing)target;
            bool single = targets.Length == 1;
            using (new EditorGUI.DisabledScope(targets.Any(VAPPrefabLock.IsFromPrefab)))
            {
                if (single)
                    DrawInput(c);
                var split = serializedObject.FindProperty(nameof(VAPSmoothing.splitSmoothing));
                bool splitOn = split.boolValue && !split.hasMultipleDifferentValues;
                DrawSmoothing(nameof(VAPSmoothing.smoothing), nameof(VAPSmoothing.customSpeed), splitOn ? IncreaseLabel : SmoothingLabel, splitOn, split);
                if (splitOn)
                    DrawSmoothing(nameof(VAPSmoothing.decreaseSmoothing), nameof(VAPSmoothing.decreaseCustomSpeed), DecreaseLabel, true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(VAPSmoothing.passes)), PassesLabel);
                string output = single ? ContactSmoothing.Output(c, ContactSmoothing.Input(c)) : null;
                Field(nameof(VAPSmoothing.outputParameter), OutputLabel, output);
                EditorGUILayout.Space(4f);
                ToggleBox(nameof(VAPSmoothing.transformOutput), TransformLabel, () =>
                {
                    if (single)
                        DrawTransform(c);
                });
                ToggleBox(nameof(VAPSmoothing.timer), TimerLabel, () =>
                {
                    Field(nameof(VAPSmoothing.timerParameter), TimerParameterLabel, output == null ? null : output + "Timer");
                    DrawTimerValues();
                });
            }
            serializedObject.ApplyModifiedProperties();

            if (!single)
                return;
            if (VAPAvatar.Descriptor(c) != null && ContactSmoothing.SmoothedBy(c) is VAPSmoothing owner)
                EditorGUILayout.HelpBox($"This does nothing because {ContactSmoothing.Input(c)} is already smoothed on {owner.name}.", MessageType.Error);
            else if (ContactSmoothing.Problem(c, ContactSmoothing.Resolve(c)) is string problem)
                EditorGUILayout.HelpBox(problem, MessageType.Warning);
            else
                foreach (var (name, by) in ContactSmoothing.Repeats(c))
                    EditorGUILayout.HelpBox(ContactSmoothing.RepeatMessage(name, by), MessageType.Error);
        }

        void DrawTransform(VAPSmoothing c)
        {
            string input = ContactSmoothing.Input(c);
            string axis = ContactSmoothing.Axis(c, input);
            Pick(TransformByLabel, serializedObject.FindProperty(nameof(VAPSmoothing.transformParameter)), ContactSmoothing.Parameters(c), axis);
            var before = serializedObject.FindProperty(nameof(VAPSmoothing.transformBeforeSmoothing));
            EditorGUILayout.PropertyField(before, BeforeLabel);
            string xName = VAPEditorGUI.MenuName(input ?? "Input") + (before.boolValue ? "" : " (smoothed)");
            var points = serializedObject.FindProperty(nameof(VAPSmoothing.transformPoints));
            EditorGUILayout.Space(2f);
            int wasSelected = graph.selected = pointList?.index ?? -1;
            if (graph.Draw(points, nameof(VAPSmoothing.TransformPoint.position), nameof(VAPSmoothing.TransformPoint.value),
                xName, VAPEditorGUI.MenuName(axis ?? "None")) || graph.selected != wasSelected)
                Repaint();
            GUI.Label(GUILayoutUtility.GetLastRect(), PointsTooltip);
            EditorGUILayout.Space(2f);
            pointList ??= PointList(points);
            pointList.serializedProperty = points;
            if (graph.selected != pointList.index)
                pointList.index = graph.selected;
            pointList.DoLayoutList();
        }

        ReorderableList PointList(SerializedProperty points)
        {
            const float gap = 4f;
            var list = new ReorderableList(serializedObject, points, true, true, true, true);
            Rect[] Columns(Rect r)
            {
                float w = (r.width - gap * 2f) / 3f;
                return new[]
                {
                    new Rect(r.x, r.y, w, r.height),
                    new Rect(r.x + w + gap, r.y, w, r.height),
                    new Rect(r.x + (w + gap) * 2f, r.y, w, r.height),
                };
            }
            list.drawHeaderCallback = r =>
            {
                r.xMin += 14f;
                var cols = Columns(r);
                EditorGUI.LabelField(cols[0], "Pos X");
                EditorGUI.LabelField(cols[1], "Pos Y");
                EditorGUI.LabelField(cols[2], new GUIContent("Value", "Value at this point. With Apply Before Smoothing on it is what gets smoothed, otherwise it is the output."));
            };
            list.drawElementCallback = (r, i, active, focused) =>
            {
                var element = list.serializedProperty.GetArrayElementAtIndex(i);
                var position = element.FindPropertyRelative(nameof(VAPSmoothing.TransformPoint.position));
                var value = element.FindPropertyRelative(nameof(VAPSmoothing.TransformPoint.value));
                r.y += 1f;
                r.height = EditorGUIUtility.singleLineHeight;
                var cols = Columns(r);
                var p = position.vector2Value;
                EditorGUI.BeginChangeCheck();
                p.x = EditorGUI.FloatField(cols[0], p.x);
                p.y = EditorGUI.FloatField(cols[1], p.y);
                if (EditorGUI.EndChangeCheck())
                    position.vector2Value = p;
                EditorGUI.PropertyField(cols[2], value, GUIContent.none);
            };
            list.onAddCallback = l =>
            {
                var prop = l.serializedProperty;
                prop.arraySize++;
                var element = prop.GetArrayElementAtIndex(prop.arraySize - 1);
                element.FindPropertyRelative(nameof(VAPSmoothing.TransformPoint.position)).vector2Value = new Vector2(0.5f, 0.5f);
                element.FindPropertyRelative(nameof(VAPSmoothing.TransformPoint.value)).floatValue = 0f;
                l.index = prop.arraySize - 1;
            };
            return list;
        }

        void DrawInput(VAPSmoothing c)
        {
            var prop = serializedObject.FindProperty(PhysBone ? nameof(VAPPhysBoneSmoothing.physBoneParameter) : nameof(VAPContactSmoothing.contactParameter));
            Pick(PhysBone ? PhysBoneLabel : ContactLabel, prop, ContactSmoothing.Parameters(c), ContactSmoothing.Input(c));
        }

        void Pick(GUIContent label, SerializedProperty prop, List<string> found, string current)
        {
            var options = found.Select(p => new GUIContent(VAPEditorGUI.MenuName(p))).ToList();
            int index = current == null ? -1 : found.IndexOf(current);
            if (current != null && index < 0)
            {
                index = options.Count;
                options.Add(new GUIContent(VAPEditorGUI.MenuName(current) + " (missing)"));
            }
            if (options.Count == 0)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.Popup(label, 0, new[] { new GUIContent(PhysBone ? "No PhysBone parameters found" : "No contacts found") });
                return;
            }
            EditorGUI.BeginChangeCheck();
            int picked = EditorGUILayout.Popup(label, Mathf.Max(index, 0), options.ToArray());
            if (EditorGUI.EndChangeCheck() && picked < found.Count)
                prop.stringValue = found[picked];
        }

        void DrawSmoothing(string property, string customProperty, GUIContent label, bool allowNone, SerializedProperty link = null)
        {
            var smoothing = serializedObject.FindProperty(property);
            var values = SmoothingSpeeds.Entries.Select(e => e.name).Append("").ToList();
            if (allowNone)
                values.Add(VAPSmoothing.None);
            var options = values.Select(v => new GUIContent(Display(v))).ToList();
            string current = smoothing.stringValue;
            int index = values.IndexOf(current);
            if (index < 0)
            {
                index = options.Count;
                options.Add(new GUIContent(current + " (missing)"));
            }
            bool custom = !smoothing.hasMultipleDifferentValues && string.IsNullOrEmpty(smoothing.stringValue);
            var rect = EditorGUI.PrefixLabel(EditorGUILayout.GetControlRect(), label);
            if (link != null)
                LinkButton(rect, link);
            var popupRect = rect;
            if (custom)
                popupRect.width = (rect.width - 4f) * 0.5f;
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            EditorGUI.showMixedValue = smoothing.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int picked = EditorGUI.Popup(popupRect, index, options.ToArray());
            if (EditorGUI.EndChangeCheck() && picked < values.Count)
                smoothing.stringValue = values[picked];
            EditorGUI.showMixedValue = false;
            if (custom)
            {
                var speedRect = new Rect(popupRect.xMax + 4f, rect.y, rect.xMax - popupRect.xMax - 4f, rect.height);
                EditorGUI.PropertyField(speedRect, serializedObject.FindProperty(customProperty), new GUIContent("", SpeedLabel.tooltip));
            }
            EditorGUI.indentLevel = indent;
        }

        static readonly GUIContent TimerLinkedIcon = new GUIContent("", "The timer resets at the start value. Click to reset at a lower value.");
        static readonly GUIContent TimerUnlinkedIcon = new GUIContent("", "The timer resets at its own value. Click to reset at the start value.");

        static void LinkButton(Rect field, SerializedProperty link, GUIContent linkedTip = null, GUIContent unlinkedTip = null)
        {
            var icon = new Rect(field.x - 20f, field.y, 18f, field.height);
            bool linked = !link.boolValue || link.hasMultipleDifferentValues;
            var content = linked ? LinkedIcon : UnlinkedIcon;
            var tip = linked ? linkedTip : unlinkedTip;
            if (tip != null)
                content = new GUIContent(content.image, tip.tooltip);
            if (GUI.Button(icon, content, EditorStyles.iconButton))
                link.boolValue = linked;
        }

        void DrawTimerValues()
        {
            var split = serializedObject.FindProperty(nameof(VAPSmoothing.splitTimer));
            bool splitOn = split.boolValue && !split.hasMultipleDifferentValues;
            var rect = EditorGUI.PrefixLabel(EditorGUILayout.GetControlRect(), TimerStartLabel);
            LinkButton(rect, split, TimerLinkedIcon, TimerUnlinkedIcon);
            var start = serializedObject.FindProperty(nameof(VAPSmoothing.timerStart));
            var reset = serializedObject.FindProperty(nameof(VAPSmoothing.timerReset));
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(rect, start, GUIContent.none);
            bool startChanged = EditorGUI.EndChangeCheck();
            EditorGUI.indentLevel = indent;
            if (!splitOn)
                return;
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(reset, TimerResetLabel);
            bool resetChanged = EditorGUI.EndChangeCheck();
            if (start.hasMultipleDifferentValues || reset.hasMultipleDifferentValues || reset.floatValue <= start.floatValue)
                return;
            if (resetChanged)
                reset.floatValue = start.floatValue;
            else if (startChanged)
                start.floatValue = reset.floatValue;
        }

        static string Display(string value)
        {
            if (value == "")
                return "Custom";
            var entry = SmoothingSpeeds.Find(value);
            return entry == null ? value : $"{entry.name} ({entry.speed.ToString(System.Globalization.CultureInfo.InvariantCulture)})";
        }

        void ToggleBox(string property, GUIContent label, System.Action body)
        {
            var toggle = serializedObject.FindProperty(property);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.showMixedValue = toggle.hasMultipleDifferentValues;
                EditorGUI.BeginChangeCheck();
                bool on = EditorGUILayout.ToggleLeft(label, toggle.boolValue, EditorStyles.boldLabel);
                if (EditorGUI.EndChangeCheck())
                    toggle.boolValue = on;
                EditorGUI.showMixedValue = false;
                if (toggle.boolValue && !toggle.hasMultipleDifferentValues)
                    body();
            }
        }

        void Field(string property, GUIContent label, string placeholder)
        {
            var prop = serializedObject.FindProperty(property);
            var rect = EditorGUILayout.GetControlRect();
            VAPEditorGUI.StringField(rect, prop, label);
            if (!prop.hasMultipleDifferentValues && string.IsNullOrEmpty(prop.stringValue) && !string.IsNullOrEmpty(placeholder)
                && Event.current.type == EventType.Repaint)
            {
                var field = new Rect(rect.x + EditorGUIUtility.labelWidth + 2f, rect.y, rect.width - EditorGUIUtility.labelWidth - 2f, rect.height);
                PlaceholderStyle.Draw(field, new GUIContent(placeholder), false, false, false, false);
            }
        }
    }
}
#endif
