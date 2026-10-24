#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VAP.Editor
{
    internal static class VAPPrefabAsset
    {
        static readonly GUIContent CreateInPrefab = new GUIContent("Create in Prefab", "Create the missing assets inside this prefab's file.");
        static readonly GUIContent CreateFile = new GUIContent("Create Asset...", "This object isn't in a prefab, so each asset is saved as its own file.");

        internal static string PrefabPath(Component component)
        {
            var stage = PrefabStageUtility.GetPrefabStage(component.gameObject);
            if (stage != null)
                return stage.assetPath;
            if (PrefabUtility.IsPartOfPrefabAsset(component))
                return AssetDatabase.GetAssetPath(component);
            return null;
        }

        static GameObject PrefabRoot(Component component)
        {
            var stage = PrefabStageUtility.GetPrefabStage(component.gameObject);
            return stage != null ? stage.prefabContentsRoot : component.transform.root.gameObject;
        }

        internal static string AssetName(Component component, string prefabPath, string suffix)
        {
            string prefab = System.IO.Path.GetFileNameWithoutExtension(prefabPath);
            return component.gameObject == PrefabRoot(component) ? $"{prefab} {suffix}" : $"{prefab} {component.gameObject.name} {suffix}";
        }

        internal static T AutoLink<T>(SerializedProperty assetProp, Component component, bool locked, string suffix) where T : ScriptableObject
        {
            if (assetProp.objectReferenceValue != null || locked || !(PrefabPath(component) is string path))
                return null;
            string name = AssetName(component, path, suffix);
            var existing = AssetDatabase.LoadAllAssetsAtPath(path).OfType<T>().FirstOrDefault(a => a.name == name);
            if (existing == null)
                return null;
            assetProp.objectReferenceValue = existing;
            assetProp.serializedObject.ApplyModifiedProperties();
            return existing;
        }

        internal static Object Field(SerializedProperty assetProp, GUIContent label, bool locked)
        {
            using (new EditorGUI.DisabledScope(locked))
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(assetProp, label);
                if (EditorGUI.EndChangeCheck())
                {
                    assetProp.serializedObject.ApplyModifiedProperties();
                    return assetProp.objectReferenceValue;
                }
            }
            return null;
        }

        internal static bool RevertMenu(Rect rect, SerializedProperty element, string what)
        {
            var e = Event.current;
            bool right = e.type == EventType.ContextClick || (e.type == EventType.MouseDown && e.button == 1);
            if (!right || !rect.Contains(e.mousePosition) || !element.prefabOverride)
                return false;
            var so = element.serializedObject;
            string path = element.propertyPath;
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent($"Revert {what}"), false, () =>
            {
                so.Update();
                var prop = so.FindProperty(path);
                if (prop != null)
                    PrefabUtility.RevertPropertyOverride(prop, InteractionMode.UserAction);
                so.Update();
            });
            menu.ShowAsContext();
            e.Use();
            return true;
        }

        internal static void CreateButton(Component component, bool locked, params (SerializedProperty prop, string suffix, Func<ScriptableObject> make)[] assets)
        {
            if (locked)
            {
                if (assets.Any(a => a.prop.objectReferenceValue == null))
                    EditorGUILayout.HelpBox("No assets yet. Open the prefab to create them.", MessageType.Info);
                return;
            }
            string prefabPath = PrefabPath(component);
            var missing = assets.Where(a => a.prop.objectReferenceValue == null).ToList();
            using (new EditorGUI.DisabledScope(missing.Count == 0))
            {
                if (!GUILayout.Button(prefabPath != null ? CreateInPrefab : CreateFile) || missing.Count == 0)
                    return;
            }
            foreach (var (prop, suffix, make) in missing)
            {
                var asset = make();
                if (prefabPath != null)
                {
                    asset.name = AssetName(component, prefabPath, suffix);
                    AssetDatabase.AddObjectToAsset(asset, prefabPath);
                }
                else
                {
                    string path = EditorUtility.SaveFilePanelInProject("Create " + suffix, component.name + " " + suffix, "asset", "Where to save the asset.");
                    if (string.IsNullOrEmpty(path))
                        continue;
                    AssetDatabase.CreateAsset(asset, path);
                }
                prop.objectReferenceValue = asset;
            }
            assets[0].prop.serializedObject.ApplyModifiedProperties();
            if (prefabPath != null)
                AssetDatabase.SaveAssets();
            GUIUtility.ExitGUI();
        }
    }
}
#endif
