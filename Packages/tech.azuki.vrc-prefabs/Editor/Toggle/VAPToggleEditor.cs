#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VAP.Editor
{
    [CustomEditor(typeof(VAPToggle))]
    public class VAPToggleEditor : UnityEditor.Editor
    {
        internal const string FeatureType = "VF.Model.Feature.Toggle";
        const float RowHeight = 26f, SliderWidth = 200f, CheckWidth = 16f;
        readonly WrappedEditor wrap = new WrappedEditor(FeatureType, "VRCFury Toggle", "VAP.Toggle.Open.");

        VAPToggle Owner => (VAPToggle)wrap.Owner;
        Component Wrapped => wrap.Wrapped;

        void OnEnable() => wrap.Enable(this);

        void OnDisable() => wrap.Disable();

        static readonly GUIContent DefaultTip = new GUIContent("",
            "Start value. Replaces the VRCFury Toggle's default when the avatar is built. Can be changed on prefab instances too.");

        (string path, Texture2D icon, string param) Info()
        {
            using var so = new SerializedObject(Wrapped);
            var content = so.FindProperty("content");
            string path = Owner != null ? VRCFuryToggleDefaults.MenuName(Owner) : content?.FindPropertyRelative("name")?.stringValue ?? "";
            var icon = (content?.FindPropertyRelative("enableIcon")?.boolValue ?? false)
                ? content.FindPropertyRelative("icon.objRef")?.objectReferenceValue as Texture2D : null;
            string param = (content?.FindPropertyRelative("useGlobalParam")?.boolValue ?? false)
                ? content.FindPropertyRelative("globalParam")?.stringValue : null;
            return (path, icon, param);
        }

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

            EditorGUILayout.Space(6f);
            VAPEditorGUI.BeginMenuBox();
            var header = EditorGUILayout.GetControlRect();
            var steps = MenuSection.CleanName(Info().path).Split('/').Select(n => n.Trim()).Where(n => n.Length > 0).ToList();
            if (steps.Count > 0)
                steps.RemoveAt(steps.Count - 1);
            if (steps.Count == 0)
                steps.Add("Main Menu");
            SubMenusSection.DrawHeader(header, steps, null, SyncedBits(slider) + " synced bits");
            serializedObject.Update();
            VAPEditorGUI.MenuPathField(serializedObject.FindProperty("menuPath"), MenuPaths.Folder(VRCFuryToggleDefaults.MenuName(Wrapped)), true);
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space(2f);

            MenuSection.DrawList(1, i => RowHeight + 4f, (row, content, i) =>
            {
                MenuSection.DrawRow(row, 0, false);
                if (changed)
                    ExpressionsOverrides.DrawRightEdgeBar(row);
                DrawItem(new Rect(content.x, content.y + 2f, content.width, RowHeight), slider, on, value, ownOn, ownValue, changed, source);
            });
            VAPEditorGUI.EndMenuBox();
        }

        int SyncedBits(bool slider)
        {
            if (!slider)
                return 1;
            using var so = new SerializedObject(Wrapped);
            int bits = so.FindProperty("content")?.FindPropertyRelative("sliderBitWidthOverride")?.intValue ?? 0;
            return bits == 0 ? 7 : Mathf.Clamp(bits, 1, 8);
        }

        bool Saved()
        {
            using var so = new SerializedObject(Wrapped);
            return so.FindProperty("content")?.FindPropertyRelative("saved")?.boolValue ?? false;
        }

        void DrawItem(Rect rect, bool slider, bool on, float value, bool ownOn, float ownValue, bool changed, VAPToggle source)
        {
            if (changed && ExpressionsOverrides.ContextClick(rect))
            {
                if (source == null)
                    ExpressionsOverrides.ShowRevert("Revert Default", () => SetOverride(false, ownOn, ownValue));
                else
                    ExpressionsOverrides.ShowRevert("Revert Default", () => SetOverride(source.overrideDefault, source.defaultOn, source.defaultValue));
            }

            float line = EditorGUIUtility.singleLineHeight;
            float y = rect.y + (rect.height - line) * 0.5f;
            var control = slider ? new Rect(rect.xMax - SliderWidth, y, SliderWidth, line) : new Rect(rect.xMax - CheckWidth, y, CheckWidth, line);
            var (path, icon, param) = Info();
            string full = MenuSection.CleanName(path);
            string name = full.Substring(full.LastIndexOf('/') + 1).Trim();
            string info = MenuSection.ParamInfo(string.IsNullOrEmpty(param) ? "VRCFury parameter" : param, slider ? "Float" : "Bool", SyncedBits(slider), true, Saved(),
                slider ? Mathf.Round(value * 100f) + "%" : on ? "On" : "Off");
            MenuSection.DrawName(new Rect(rect.x, rect.y, control.x - rect.x - 8f, rect.height), icon, new GUIContent(name.Length == 0 ? "No menu path" : name, full.Length == 0 ? info : full + "\n" + info));

            EditorGUI.BeginChangeCheck();
            if (slider)
                value = EditorGUI.Slider(control, Mathf.Round(value * 100f), 0f, 100f) / 100f;
            else
                on = EditorGUI.Toggle(control, DefaultTip, on);
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
            var (_, _, param) = Info();
            using var so = new SerializedObject(Wrapped);
            VRCFuryWrap.BarLabels(rect, rect.x, "Toggle", param, 0.6f, VRCFuryWrap.Changed(so.FindProperty("content")));
        }

        internal static bool IsWrappable(Component c) => VRCFuryWrap.HasFeature(c, FeatureType);

        public override VisualElement CreateInspectorGUI() => wrap.Build(this, DrawDefault, DrawInfo, null, true);
    }
}
#endif
