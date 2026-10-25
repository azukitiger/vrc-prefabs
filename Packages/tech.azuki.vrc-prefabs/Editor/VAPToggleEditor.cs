#if UNITY_EDITOR && VRC_SDK_VRCSDK3
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
        static GUIStyle pathStyle, paramStyle;

        VAPToggle owner;
        Component wrapped;
        WrappedSection section;

        void OnEnable()
        {
            owner = target as VAPToggle;
            if (owner == null || targets.Length > 1)
                return;
            wrapped = VRCFuryWrap.Ensure(owner, FeatureType);
            WrappedToggles.HideShownEditors();
        }

        void OnDisable()
        {
            section?.Dispose();
            VRCFuryWrap.ScheduleRemoval(owner, wrapped);
        }

        static readonly GUIContent DefaultLabel = new GUIContent("Default",
            "Replaces the VRCFury Toggle's default when the avatar is built. Can be changed on prefab instances too.");

        void DrawDefault()
        {
            if (owner == null || wrapped == null)
                return;
            var (slider, ownOn, ownValue) = VRCFuryToggleDefaults.GetDefault(wrapped);
            bool on = owner.overrideDefault ? owner.defaultOn : ownOn;
            float value = owner.overrideDefault ? owner.defaultValue : ownValue;

            // Changed here, compared to the prefab this comes from (or to VRCFury's value when there is none).
            var source = PrefabUtility.GetCorrespondingObjectFromSource(owner);
            bool changed = source == null
                ? owner.overrideDefault
                : owner.overrideDefault != source.overrideDefault ||
                  (owner.overrideDefault && (owner.defaultOn != source.defaultOn || !Mathf.Approximately(owner.defaultValue, source.defaultValue)));

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
            Undo.RecordObject(owner, enabled ? "Change Default" : "Revert Default");
            owner.overrideDefault = enabled;
            owner.defaultOn = on;
            owner.defaultValue = Mathf.Clamp01(value);
            EditorUtility.SetDirty(owner);
            PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
        }

        void DrawInfo(Rect rect)
        {
            pathStyle ??= new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, padding = new RectOffset(0, 0, 0, 0) };
            paramStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, clipping = TextClipping.Clip };

            using var so = new SerializedObject(wrapped);
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
            float paramWidth = string.IsNullOrEmpty(param) ? 0f : Mathf.Min(paramStyle.CalcSize(new GUIContent(param)).x, rect.width * 0.35f);
            GUI.Label(new Rect(x, rect.y, rect.xMax - x - paramWidth - 8f, rect.height), string.IsNullOrEmpty(path) ? "No menu path" : path, pathStyle);
            if (paramWidth > 0f)
                GUI.Label(new Rect(rect.xMax - paramWidth, rect.y, paramWidth, rect.height), param, paramStyle);
        }

        internal static bool IsWrappable(Component c) => VRCFuryWrap.HasFeature(c, FeatureType);

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            if (targets.Length > 1)
                root.Add(new IMGUIContainer(() => EditorGUILayout.HelpBox("Editing several of these at once isn't supported.", MessageType.Info)));
            else if (wrapped == null)
                root.Add(VRCFuryWrap.MissingMessage("VRCFury Toggle"));
            else
            {
                root.Add(new IMGUIContainer(DrawDefault));
                section?.Dispose();
                section = new WrappedSection(wrapped, "VAP.Toggle.Open.", DrawInfo);
                root.Add(section.Root);
            }
            VAPInspectorHeader.Attach(root, "VRCFury Toggle");
            return root;
        }
    }
}
#endif
