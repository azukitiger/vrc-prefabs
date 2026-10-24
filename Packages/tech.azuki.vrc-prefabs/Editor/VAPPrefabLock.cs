#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VAP.Editor
{
    public static class VAPPrefabLock
    {
        const string UnsavedMessage = "This prefab has changes from the original.";

        public static bool IsFromPrefab(Object target)
        {
            if (!(target is Component component) || !PrefabUtility.IsPartOfPrefabInstance(component))
                return false;
            return !PrefabUtility.IsAddedComponentOverride(component);
        }

        public static bool IsLocked(Object target) => false;

        public static bool HasUnsavedChanges(Object target) => ChangedFields(target, 1).Count > 0;

        public static System.Collections.Generic.List<string> ChangedFields(Object target, int max)
        {
            var result = new System.Collections.Generic.List<string>();
            if (!(target is Component component) || !PrefabUtility.IsPartOfPrefabInstance(component))
                return result;
            if (PrefabUtility.IsAddedComponentOverride(component))
            {
                result.Add("(component added)");
                return result;
            }
            using var so = new SerializedObject(component);
            var it = so.GetIterator();
            while (it.Next(true) && result.Count < max)
            {
                if (!it.prefabOverride || it.propertyPath == "m_Script")
                    continue;
                // A parent is marked when any child is; list only the fields themselves.
                if (it.hasChildren && it.propertyType != SerializedPropertyType.String)
                    continue;
                string path = it.propertyPath.Replace(".Array.data[", "[");
                if (!result.Contains(path))
                    result.Add(path);
            }
            return result;
        }

        public static void DrawWarning(Object[] targets)
        {
            var content = NoteContent(targets);
            if (content != null)
                EditorGUILayout.LabelField(content, NoteStyle);
        }

        public static void DrawInline(Object[] targets)
        {
            var content = NoteContent(targets);
            if (content != null)
                GUILayout.Label(content, NoteStyle, GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true));
            else
                GUILayout.FlexibleSpace();
        }

        static GUIStyle noteStyle;
        static GUIStyle NoteStyle => noteStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            wordWrap = false,
            clipping = TextClipping.Clip,
            alignment = TextAnchor.MiddleLeft,
            fixedHeight = EditorGUIUtility.singleLineHeight,
        };

        // One line: icon + message; what changed is only in the tooltip.
        static GUIContent NoteContent(Object[] targets)
        {
            var target = targets.FirstOrDefault(HasUnsavedChanges);
            if (target == null)
                return null;
            var fields = ChangedFields(target, 20);
            string tooltip = "Changed:\n" + string.Join("\n", fields) + (fields.Count >= 20 ? "\n..." : "");
            var icon = EditorGUIUtility.IconContent("console.infoicon.sml").image;
            return new GUIContent(UnsavedMessage, icon, tooltip);
        }

        public static bool Draw(Object[] targets)
        {
            DrawWarning(targets);
            return false;
        }

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
