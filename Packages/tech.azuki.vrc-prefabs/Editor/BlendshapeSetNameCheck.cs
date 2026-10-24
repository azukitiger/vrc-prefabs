#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using UnityEditor;
using VRC.SDK3.Avatars.Components;

namespace VAP.Editor
{
    internal static class BlendshapeSetNameCheck
    {
        internal static string EffectiveName(VAPFaceTrackingDebug c) =>
            string.IsNullOrEmpty(c.blendshapesSet?.setName) ? FaceTrackingDebugSetFiller.DefaultSetName : c.blendshapesSet.setName;

        internal static string Conflict(VAPFaceTrackingDebug c, VRCAvatarDescriptor descriptor)
        {
            if (c == null || descriptor == null)
                return null;
            string name = EffectiveName(c);
            using (var so = new SerializedObject(descriptor))
            {
                var sets = so.FindProperty("faceTrackingBlendshapesSetList");
                if (sets != null)
                    for (int i = 0; i < sets.arraySize; i++)
                        if (sets.GetArrayElementAtIndex(i).FindPropertyRelative("setName").stringValue == name)
                            return $"The avatar descriptor already has a Blendshapes Set named \"{name}\".";
            }
            foreach (var other in descriptor.GetComponentsInChildren<VAPFaceTrackingDebug>(true))
                if (other != c && EffectiveName(other) == name)
                    return $"Another BlendShape Set component ({other.name}) on this avatar already uses the name \"{name}\".";
            return null;
        }
    }
}
#endif
