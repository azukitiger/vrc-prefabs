#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VAP.Editor
{
    internal static class VAPPrefabAsset
    {
        // The prefab file this component is edited in, or null outside a prefab.
        internal static string PrefabPath(Component component)
        {
            var stage = PrefabStageUtility.GetPrefabStage(component.gameObject);
            if (stage != null)
                return stage.assetPath;
            if (PrefabUtility.IsPartOfPrefabAsset(component))
                return AssetDatabase.GetAssetPath(component);
            return null;
        }
    }
}
#endif
