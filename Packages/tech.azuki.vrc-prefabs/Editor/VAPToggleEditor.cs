#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VAP.Editor
{
    [CustomEditor(typeof(VAPToggle))]
    public class VAPToggleEditor : UnityEditor.Editor
    {
        internal const string FeatureType = "VF.Model.Feature.Toggle";
        const float IconSize = 24f, IconPad = 4f;
        readonly WrappedEditor wrap = new WrappedEditor(FeatureType, "VRCFury Toggle", "VAP.Toggle.Open.");

        VAPToggle Owner => (VAPToggle)wrap.Owner;
        Component Wrapped => wrap.Wrapped;

        void OnEnable() => wrap.Enable(this);

        void OnDisable() => wrap.Disable();

        static readonly GUIContent DefaultLabel = new GUIContent("Default",
            "Replaces the VRCFury Toggle's default when the avatar is built. Can be changed on prefab instances too.");

        void DrawDefault()
        {
            if (Owner == null || Wrapped == null)
                return;
            var (slider, ownOn, ownValue) = VRCFuryToggleDefaults.GetDefault(Wrapped);
            bool on = Owner.overrideDefault ? Owner.defaultOn : ownOn;
            float value = Owner.overrideDefault ? Owner.defaultValue : ownValue;

            var source = PrefabUtility.GetCorrespondingObjectFromSource(Owner);
            bool changed = source == null
                ? Owner.overrideDefault
                : Owner.overrideDefault != source.overrideDefault ||
                  (Owner.overrideDefault && (Owner.defaultOn != source.defaultOn || !Mathf.Approximately(Owner.defaultValue, source.defaultValue)));

            EditorGUILayout.Space(2f);
            var rect = EditorGUILayout.GetControlRect();
            if (changed)
                ExpressionsOverrides.DrawBar(rect);
            if (changed && ExpressionsOverrides.ContextClick(rect))
            {
                if (source == null)
                    ExpressionsOverrides.ShowRevert("Revert Default", () => SetOverride(false, ownOn, ownValue));
                else
                    ExpressionsOverrides.ShowRevert("Revert Default", () => SetOverride(source.overrideDefault, source.defaultOn, source.defaultValue));
            }

            EditorGUI.BeginChangeCheck();
            if (slider)
                value = EditorGUI.Slider(rect, DefaultLabel, Mathf.Round(value * 100f), 0f, 100f) / 100f;
            else
                on = EditorGUI.Toggle(rect, DefaultLabel, on);
            if (!EditorGUI.EndChangeCheck())
                return;
            bool same = slider ? Mathf.Approximately(value, ownValue) : on == ownOn;
            SetOverride(!same, on, value);
        }

        void SetOverride(bool enabled, bool on, float value)
        {
            Undo.RecordObject(Owner, enabled ? "Change Default" : "Revert Default");
            Owner.overrideDefault = enabled;
            Owner.defaultOn = on;
            Owner.defaultValue = Mathf.Clamp01(value);
            EditorUtility.SetDirty(Owner);
            PrefabUtility.RecordPrefabInstancePropertyModifications(Owner);
        }

        void DrawInfo(Rect rect)
        {
            using var so = new SerializedObject(Wrapped);
            var content = so.FindProperty("content");
            string path = content?.FindPropertyRelative("name")?.stringValue ?? "";
            var icon = (content?.FindPropertyRelative("enableIcon")?.boolValue ?? false)
                ? content.FindPropertyRelative("icon.objRef")?.objectReferenceValue as Texture2D : null;
            string param = (content?.FindPropertyRelative("useGlobalParam")?.boolValue ?? false)
                ? content.FindPropertyRelative("globalParam")?.stringValue : null;

            float x = rect.x;
            if (icon != null)
            {
                GUI.DrawTexture(new Rect(x, rect.y + (rect.height - IconSize) * 0.5f, IconSize, IconSize), icon, ScaleMode.ScaleToFit);
                x += IconSize + IconPad;
            }
            VRCFuryWrap.BarLabels(rect, x, string.IsNullOrEmpty(path) ? "No menu path" : path, param, 0.35f);
        }

        internal static bool IsWrappable(Component c) => VRCFuryWrap.HasFeature(c, FeatureType);

        public override VisualElement CreateInspectorGUI() => wrap.Build(this, DrawDefault, DrawInfo);
    }
}
#endif
