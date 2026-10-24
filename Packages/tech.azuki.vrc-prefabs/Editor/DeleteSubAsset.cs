#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VAP.Editor
{
    internal static class DeleteSubAsset
    {
        internal const string MenuPath = "Assets/VAP/Delete Sub-Asset";

        [MenuItem(MenuPath, true)]
        internal static bool Validate() => Selection.objects.Length > 0 && Selection.objects.All(IsSubAsset);

        [MenuItem(MenuPath, false, 2000)]
        internal static void Delete()
        {
            var targets = Selection.objects.Where(IsSubAsset).ToList();
            string names = string.Join("\n", targets.Select(t => t.name));
            if (!EditorUtility.DisplayDialog("Delete Sub-Asset", $"Delete these from their files? This can't be undone.\n\n{names}", "Delete", "Cancel"))
                return;
            foreach (var t in targets)
            {
                AssetDatabase.RemoveObjectFromAsset(t);
                Object.DestroyImmediate(t, true);
            }
            AssetDatabase.SaveAssets();
        }

        internal static bool IsSubAsset(Object o) =>
            o != null && EditorUtility.IsPersistent(o) && !AssetDatabase.IsMainAsset(o) && !(o is GameObject) && !(o is Component);
    }
}
#endif
