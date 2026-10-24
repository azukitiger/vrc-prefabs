#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VAP.Editor
{
    public static class VAPPrefabLock
    {
        public static bool IsFromPrefab(Object target)
        {
            if (!(target is Component component) || !PrefabUtility.IsPartOfPrefabInstance(component))
                return false;
            return !PrefabUtility.IsAddedComponentOverride(component);
        }

        public static bool IsLocked(Object target) => false;

        public static bool Draw(Object[] targets) => false;

        public static void Open(Object target)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(target) as Component;
            if (source == null)
                return;
            string path = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(path))
                return;

            string relative = AnimationUtility.CalculateTransformPath(source.transform, source.transform.root);
            var stage = PrefabStageUtility.OpenPrefab(path);
            if (stage == null)
                return;
            var root = stage.prefabContentsRoot.transform;
            var found = string.IsNullOrEmpty(relative) ? root : root.Find(relative);
            if (found != null)
            {
                Selection.activeGameObject = found.gameObject;
                EditorGUIUtility.PingObject(found.gameObject);
            }
        }
    }
}
#endif
