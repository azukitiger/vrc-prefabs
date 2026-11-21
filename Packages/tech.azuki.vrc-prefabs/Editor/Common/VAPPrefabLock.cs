#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

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

        public static VisualElement AddEditButton(VisualElement root, UnityEditor.Editor editor)
        {
            var button = new Button(() =>
            {
                var target = editor.targets.FirstOrDefault(IsFromPrefab);
                if (target != null)
                    Open(target);
            })
            {
                text = "Edit in Prefab",
                tooltip = "Open the prefab this component comes from, where it can be changed.",
            };
            var style = button.style;
            style.position = Position.Absolute;
            style.top = 0f;
            style.right = 0f;
            style.marginLeft = style.marginRight = style.marginTop = style.marginBottom = 0f;
            style.paddingLeft = style.paddingRight = 10f;
            style.paddingTop = style.paddingBottom = 3f;
            void Refresh() => style.display = editor.targets.Any(IsFromPrefab) ? DisplayStyle.Flex : DisplayStyle.None;
            Refresh();
            button.schedule.Execute(Refresh).Every(500);
            root.Add(button);
            return root;
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
