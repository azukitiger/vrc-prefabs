#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VAP.Editor
{
    // Runs after VRCFury (-10000), so its parameters already have their final names.
    internal class ParameterDefaultsHook : IVRCSDKPreprocessAvatarCallback
    {
        const string Log = "[VAP VRC Parameter Defaults] ";
        const string VrcfuryTemp = "Packages/com.vrcfury.temp";

        public int callbackOrder => -9000;

        public bool OnPreprocessAvatar(GameObject avatarRoot)
        {
            var components = avatarRoot.GetComponentsInChildren<VAPParameterDefaults>(true);
            if (components.Length == 0)
                return true;
            var descriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            var prms = descriptor != null ? descriptor.expressionParameters : null;
            if (prms == null || prms.parameters == null)
                Debug.LogWarning(Log + "The avatar has no Expression Parameters; skipped.", avatarRoot);
            else if (!IsBuildCopy(prms))
                Debug.LogWarning(Log + $"\"{prms.name}\" is still the project asset (VRCFury didn't rebuild it), so it wasn't changed.", avatarRoot);
            else
                foreach (var c in components)
                    Apply(c, descriptor, prms);

            foreach (var c in components)
                Object.DestroyImmediate(c);
            return true;
        }

        static bool IsBuildCopy(Object asset)
        {
            if (!EditorUtility.IsPersistent(asset))
                return true;
            return AssetDatabase.GetAssetPath(asset).StartsWith(VrcfuryTemp, System.StringComparison.Ordinal);
        }

        static void Apply(VAPParameterDefaults c, VRCAvatarDescriptor descriptor, VRCExpressionParameters prms)
        {
            int applied = 0;
            foreach (var entry in c.defaults)
            {
                if (entry == null || string.IsNullOrEmpty(entry.name))
                    continue;
                var p = prms.parameters.FirstOrDefault(x => x != null && x.name == entry.name);
                if (p == null)
                {
                    Debug.LogWarning(Log + $"{c.name}: no parameter named \"{entry.name}\" on the avatar. In VRCFury it needs to be a Global Parameter.", c);
                    continue;
                }
                if (p.valueType != entry.valueType)
                    Debug.LogWarning(Log + $"{c.name}: \"{entry.name}\" is {p.valueType} on the avatar, not {entry.valueType}.", c);
                p.defaultValue = entry.value;
                SetAnimatorDefaults(descriptor, entry.name, entry.value);
                applied++;
            }
            EditorUtility.SetDirty(prms);
            Debug.Log(Log + $"{c.name}: set {applied} default{(applied == 1 ? "" : "s")}.", c);
        }

        // Keeps the controllers in step, for emulators that read the animator defaults.
        static void SetAnimatorDefaults(VRCAvatarDescriptor descriptor, string name, float value)
        {
            var layers = (descriptor.baseAnimationLayers ?? new VRCAvatarDescriptor.CustomAnimLayer[0])
                .Concat(descriptor.specialAnimationLayers ?? new VRCAvatarDescriptor.CustomAnimLayer[0]);
            foreach (var layer in layers)
            {
                if (!(layer.animatorController is AnimatorController controller) || !IsBuildCopy(controller))
                    continue;
                var parameters = controller.parameters;
                bool changed = false;
                foreach (var p in parameters)
                {
                    if (p.name != name)
                        continue;
                    p.defaultBool = value != 0f;
                    p.defaultInt = Mathf.RoundToInt(value);
                    p.defaultFloat = value;
                    changed = true;
                }
                if (changed)
                {
                    controller.parameters = parameters;
                    EditorUtility.SetDirty(controller);
                }
            }
        }
    }
}
#endif
