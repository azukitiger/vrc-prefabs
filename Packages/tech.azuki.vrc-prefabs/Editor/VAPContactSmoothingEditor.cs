#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VAP.Editor
{
    [CustomEditor(typeof(VAPSmoothing), true)]
    [CanEditMultipleObjects]
    public class VAPContactSmoothingEditor : UnityEditor.Editor
    {
        static readonly GUIContent ContactLabel = new GUIContent("Contact Parameter",
            "The VRC Contact Receiver parameter to smooth, from this object or its children.");
        static readonly GUIContent PhysBoneLabel = new GUIContent("PhysBone Parameter",
            "The VRC PhysBone IsGrabbed, IsPosed, Angle, Stretch or Squish parameter to smooth, from this object or its children.");
        static readonly GUIContent OutputLabel = new GUIContent("Output Parameter",
            "Animator parameter that receives the smoothed value. If empty, VAP/ followed by the input parameter.");
        static readonly GUIContent SmoothingLabel = new GUIContent("Smoothing",
            "How fast the output follows the input. Custom sets your own speed.");
        static readonly GUIContent IncreaseLabel = new GUIContent("Increase Smoothing",
            "Smoothing used when the value goes up. None follows the input directly.");
        static readonly GUIContent DecreaseLabel = new GUIContent("Decrease Smoothing",
            "Smoothing used when the value goes down. None follows the input directly.");
        static readonly GUIContent SplitLabel = new GUIContent("Split Increase / Decrease",
            "Use a different smoothing when the value goes down than when it goes up.");
        static readonly GUIContent PassesLabel = new GUIContent("Passes",
            "Double smooths through an extra hidden step for a softer curve. Single moves the output straight toward the input.");
        static readonly GUIContent SpeedLabel = new GUIContent("Speed", "Higher follows faster. Touch is 8, Gesture is 20.");
        static readonly GUIContent TimerLabel = new GUIContent("Timer",
            "Also count, in seconds, how long the input is above 0. Goes back to 0 when it is.");
        static readonly GUIContent TimerParameterLabel = new GUIContent("Timer Parameter",
            "Animator parameter that receives the touch time. If empty, the output parameter followed by Timer.");

        static GUIStyle placeholderStyle;
        static GUIStyle PlaceholderStyle => placeholderStyle ??= new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = new Color(0.5f, 0.5f, 0.5f) },
            padding = new RectOffset(3, 3, 0, 0),
        };

        bool PhysBone => target is VAPPhysBoneSmoothing;

        public override VisualElement CreateInspectorGUI() =>
            VAPInspectorHeader.Create(PhysBone ? "PhysBone Smoothing" : "Contact Smoothing", DrawFields);

        void DrawFields()
        {
            if (target == null)
                return;
            serializedObject.Update();
            var c = (VAPSmoothing)target;
            bool single = targets.Length == 1;
            if (VAPAvatar.Descriptor(c) == null)
                EditorGUILayout.HelpBox(VAPEditorGUI.NotInAvatar, MessageType.Warning);
            else if (single && ContactSmoothing.SmoothedBy(c) is VAPSmoothing owner)
            {
                EditorGUILayout.HelpBox($"This does nothing because {ContactSmoothing.Input(c)} is already smoothed on {owner.name}.", MessageType.Error);
                return;
            }

            using (new EditorGUI.DisabledScope(targets.Any(VAPPrefabLock.IsFromPrefab)))
            {
                if (single)
                    DrawInput(c);
                var split = serializedObject.FindProperty(nameof(VAPSmoothing.splitSmoothing));
                bool splitOn = split.boolValue && !split.hasMultipleDifferentValues;
                DrawSmoothing(nameof(VAPSmoothing.smoothing), nameof(VAPSmoothing.customSpeed), splitOn ? IncreaseLabel : SmoothingLabel, splitOn);
                EditorGUILayout.PropertyField(split, SplitLabel);
                if (splitOn)
                    DrawSmoothing(nameof(VAPSmoothing.decreaseSmoothing), nameof(VAPSmoothing.decreaseCustomSpeed), DecreaseLabel, true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(VAPSmoothing.passes)), PassesLabel);
                string output = single ? ContactSmoothing.Output(c, ContactSmoothing.Input(c)) : null;
                Field(nameof(VAPSmoothing.outputParameter), OutputLabel, output);
                var timer = serializedObject.FindProperty(nameof(VAPSmoothing.timer));
                EditorGUILayout.PropertyField(timer, TimerLabel);
                if (timer.boolValue || timer.hasMultipleDifferentValues)
                    using (new EditorGUI.IndentLevelScope())
                        Field(nameof(VAPSmoothing.timerParameter), TimerParameterLabel, output == null ? null : output + "Timer");
            }
            serializedObject.ApplyModifiedProperties();

            if (!single)
                return;
            if (ContactSmoothing.Problem(c, ContactSmoothing.Resolve(c)) is string problem)
                EditorGUILayout.HelpBox(problem, MessageType.Warning);
        }

        void DrawInput(VAPSmoothing c)
        {
            var label = PhysBone ? PhysBoneLabel : ContactLabel;
            var prop = serializedObject.FindProperty(PhysBone ? nameof(VAPPhysBoneSmoothing.physBoneParameter) : nameof(VAPContactSmoothing.contactParameter));
            var found = ContactSmoothing.Parameters(c);
            string current = ContactSmoothing.Input(c);
            var options = found.Select(p => new GUIContent(p.Replace("/", "\u2215"))).ToList();
            int index = current == null ? -1 : found.IndexOf(current);
            if (current != null && index < 0)
            {
                index = options.Count;
                options.Add(new GUIContent(current.Replace("/", "\u2215") + " (missing)"));
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

        void DrawSmoothing(string property, string customProperty, GUIContent label, bool allowNone)
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
            EditorGUI.showMixedValue = smoothing.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int picked = EditorGUILayout.Popup(label, index, options.ToArray());
            if (EditorGUI.EndChangeCheck() && picked < values.Count)
                smoothing.stringValue = values[picked];
            EditorGUI.showMixedValue = false;
            if (!smoothing.hasMultipleDifferentValues && string.IsNullOrEmpty(smoothing.stringValue))
                using (new EditorGUI.IndentLevelScope())
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(customProperty), SpeedLabel);
        }

        static string Display(string value)
        {
            if (value == "")
                return "Custom";
            var entry = SmoothingSpeeds.Find(value);
            return entry == null ? value : $"{entry.name} ({entry.speed.ToString(System.Globalization.CultureInfo.InvariantCulture)})";
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
