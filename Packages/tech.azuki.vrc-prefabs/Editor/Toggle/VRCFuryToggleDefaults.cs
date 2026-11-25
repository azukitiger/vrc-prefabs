#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#if VAP_NDMF
using nadena.dev.ndmf;
#endif
using Object = UnityEngine.Object;

#if VAP_NDMF
[assembly: ExportsPlugin(typeof(VAP.Editor.VRCFuryToggleDefaultsPlugin))]
#endif

namespace VAP.Editor
{
    internal static class VRCFuryToggleDefaults
    {
        internal static void SetDefault(Component c, bool isOn, float sliderValue, bool record = false)
        {
            using var so = new SerializedObject(c);
            var content = so.FindProperty("content");
            var on = content.FindPropertyRelative("defaultOn");
            var value = content.FindPropertyRelative("defaultSliderValue");
            if (on != null)
                on.boolValue = isOn;
            if (value != null)
                value.floatValue = Mathf.Clamp01(sliderValue);
            if (record)
                so.ApplyModifiedProperties();
            else
                so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static string MenuName(Component c)
        {
            using var so = new SerializedObject(c);
            return so.FindProperty("content")?.FindPropertyRelative("name")?.stringValue ?? "";
        }

        internal static string MenuName(VAPToggle t)
        {
            string name = t.toggle != null ? MenuName(t.toggle) : "";
            return MenuPaths.Apply(name, MenuPaths.Folder(name), t.menuPath);
        }

        internal static void WriteMenuPath(VAPToggle t, bool record)
        {
            if (t.toggle == null || MenuPaths.Clean(t.menuPath).Length == 0)
                return;
            using var so = new SerializedObject(t.toggle);
            var name = so.FindProperty("content")?.FindPropertyRelative("name");
            if (name == null)
                return;
            name.stringValue = MenuPaths.Apply(name.stringValue, MenuPaths.Folder(name.stringValue), t.menuPath);
            if (record)
                so.ApplyModifiedProperties();
            else
                so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static (bool slider, bool on, float value) GetDefault(Component c)
        {
            using var so = new SerializedObject(c);
            var content = so.FindProperty("content");
            if (content == null)
                return (false, false, 0f);
            return (content.FindPropertyRelative("slider")?.boolValue ?? false,
                content.FindPropertyRelative("defaultOn")?.boolValue ?? false,
                content.FindPropertyRelative("defaultSliderValue")?.floatValue ?? 0f);
        }

        internal static void ProcessVAPToggles(GameObject avatarRoot)
        {
            foreach (var t in avatarRoot.GetComponentsInChildren<VAPToggle>(true))
            {
                if (t.overrideDefault && t.toggle != null)
                    SetDefault(t.toggle, t.defaultOn, t.defaultValue);
                WriteMenuPath(t, false);
                Object.DestroyImmediate(t);
            }
        }
    }

#if VAP_NDMF
    public class VRCFuryToggleDefaultsPlugin : Plugin<VRCFuryToggleDefaultsPlugin>
    {
        public override string QualifiedName => "tech.azuki.vrc-prefabs.vrcfury-toggle-defaults";
        public override string DisplayName => "VAP VRCFury Toggle";

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .Run("Set VRCFury Toggle defaults", ctx => VRCFuryToggleDefaults.ProcessVAPToggles(ctx.AvatarRootObject));
        }
    }
#endif
}
#endif
