#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using UnityEditor;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VAP.Editor
{
    // Reads the menus and parameter files of the VRCFury Full Controller a VAPFullController wraps.
    internal static class FullControllerSource
    {
        internal const string FeatureType = "VF.Model.Feature.FullController";

        internal static void Resolve(VAPFullController f)
        {
            f.menus.Clear();
            f.menuPrefixes.Clear();
            f.parameterFiles.Clear();
            if (f.wrapped == null)
                return;
            using var so = new SerializedObject(f.wrapped);
            var content = so.FindProperty("content");
            if (content == null || content.propertyType != SerializedPropertyType.ManagedReference ||
                !content.managedReferenceFullTypename.EndsWith(FeatureType))
                return;
            var menus = content.FindPropertyRelative("menus");
            for (int i = 0; menus != null && i < menus.arraySize; i++)
            {
                var entry = menus.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("menu.objRef")?.objectReferenceValue is VRCExpressionsMenu menu)
                {
                    f.menus.Add(menu);
                    f.menuPrefixes.Add(entry.FindPropertyRelative("prefix")?.stringValue ?? "");
                }
            }
            var prms = content.FindPropertyRelative("prms");
            for (int i = 0; prms != null && i < prms.arraySize; i++)
                if (prms.GetArrayElementAtIndex(i).FindPropertyRelative("parameters.objRef")?.objectReferenceValue is VRCExpressionParameters p)
                    f.parameterFiles.Add(p);
        }
    }
}
#endif
