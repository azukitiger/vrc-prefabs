#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;

namespace VAP.Editor
{
    [CustomEditor(typeof(VAPBlendshapeSetHook))]
    [CanEditMultipleObjects]
    public class VAPBlendshapeSetHookEditor : UnityEditor.Editor
    {
        static readonly GUIContent TargetLabel = new GUIContent("Target Blendshapes Set",
            "The Blendshapes Set to hook onto. At build time, each hooked blendshape (e.g. MouthClosedHook) is renamed to the " +
            "blendshape this set uses for the same tracking key (e.g. \"Mouth Closed Corrective\"), so this set drives it. " +
            "Where this set has no blendshape for a key, the hooked one only loses the format (MouthClosedHook becomes MouthClosed).");
        static readonly GUIContent FormatLabel = new GUIContent("Blendshape Format",
            "How the hooked blendshapes are named. * is the tracking key name: *Hook finds JawOpenHook, Hook* finds HookJawOpen. " +
            "Without *, it is a suffix.");
        static readonly GUIContent MeshesLabel = new GUIContent("Face Tracking Meshes",
            "Meshes whose blendshapes are hooked. The ones with blendshapes renamed to the target set are added to the " +
            "avatar's Face Tracking Meshes at build time.");

        public override VisualElement CreateInspectorGUI() => VAPInspectorHeader.Create("Face Tracking BlendShape Set Hook", DrawFields);

        void DrawFields()
        {
            if (target == null)
                return;
            bool locked = VAPPrefabLock.Draw(targets);
            serializedObject.Update();
            var nameProp = serializedObject.FindProperty("blendshapesSet").FindPropertyRelative("setName");
            var formatProp = serializedObject.FindProperty("nameFormat");
            using (new EditorGUI.DisabledScope(locked))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("meshes"), MeshesLabel, true);
                VAPEditorGUI.SetFieldWithPicker(nameProp, TargetLabel, () => VAPEditorGUI.AvatarSets(Descriptor).Keys);
                VAPEditorGUI.StringField(formatProp, FormatLabel);
            }
            serializedObject.ApplyModifiedProperties();

            if (targets.Length == 1)
            {
                status ??= new Cached<(string, MessageType)>(0.5, MakeStatus);
                var (text, type) = status.Value;
                EditorGUILayout.HelpBox(text, type);
            }
        }

        VRCAvatarDescriptor Descriptor => VAPAvatar.Descriptor(target as Component);

        Cached<(string, MessageType)> status;

        (string, MessageType) MakeStatus()
        {
            var hook = (VAPBlendshapeSetHook)target;
            var descriptor = Descriptor;
            if (descriptor == null)
                return (VAPEditorGUI.NotInAvatar, MessageType.Warning);
            string targetName = hook.blendshapesSet?.setName ?? "";
            var sets = VAPEditorGUI.AvatarSets(descriptor);
            if (string.IsNullOrEmpty(targetName))
                return ("Pick the Blendshapes Set to hook onto.", MessageType.Info);
            if (!sets.TryGetValue(targetName, out var targetNames))
                return ($"No Blendshapes Set named \"{targetName}\" on this avatar.", MessageType.Warning);
            string format = FaceTrackingDebugSetFiller.Normalize(hook.nameFormat);
            if (format.Length == 0)
                return ("Enter the Blendshape Format, e.g. *Hook for JawOpenHook.", MessageType.Info);

            var meshes = BlendshapeSetHookBuild.HookMeshes(hook);
            if (meshes.Count == 0)
                return ("Add the meshes with the hooked blendshapes to Face Tracking Meshes.", MessageType.Info);
            var shapes = FaceTrackingDebugSetFiller.BlendshapeNames(meshes);
            var keyNames = new HashSet<string>();
            int renamed = 0, stripped = 0;
            for (int k = 0; k < (int)VRCAvatarDescriptor.FaceTrackingKey.Max; k++)
            {
                string key = ((VRCAvatarDescriptor.FaceTrackingKey)k).ToString();
                string shape = FaceTrackingDebugSetFiller.FormatName(format, key);
                keyNames.Add(shape);
                if (!shapes.Contains(shape))
                    continue;
                if (k < targetNames.Length && !string.IsNullOrEmpty(targetNames[k]))
                    renamed++;
                else
                    stripped++;
            }
            var unknown = shapes.Where(s => FaceTrackingDebugSetFiller.Matches(format, s) && !keyNames.Contains(s)).OrderBy(s => s).ToList();
            string unknownText = unknown.Count == 0 ? ""
                : $"\n{unknown.Count} blendshape{(unknown.Count == 1 ? "" : "s")} fit the format but aren't tracking key names, so they're left alone: " +
                  string.Join(", ", unknown.Take(8)) + (unknown.Count > 8 ? ", ..." : "");
            if (renamed + stripped == 0)
                return ($"No blendshapes named like {format} on the Face Tracking Meshes." + unknownText, MessageType.Warning);

            string text = $"Format is correct: found {renamed + stripped} hooked blendshape{(renamed + stripped == 1 ? "" : "s")} " +
                          $"on {meshes.Count} mesh{(meshes.Count == 1 ? "" : "es")}.\n" +
                          $"{renamed} renamed to \"{targetName}\"'s blendshapes." +
                          (stripped > 0 ? $" {stripped} need to be linked with a VRCFury Blendshape Link." : "");
            if (format != (hook.nameFormat ?? "").Trim())
                text += $"\nRead as {format}.";
            return (text + unknownText, unknown.Count > 0 ? MessageType.Warning : MessageType.Info);
        }
    }
}
#endif
