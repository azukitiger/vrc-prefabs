#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VAP.Editor
{
    // Creates "<Prefab> Menu" and "<Prefab> Parameters" inside a prefab file.
    internal static class ExpressionsAssets
    {
        internal const string AssetsMenu = "Assets/VAP/Create Menu && Parameters";
        const string ComponentMenu = "CONTEXT/VAPExpressions/Create Menu && Parameters";
        const string PendingKey = "VAP.Expressions.LinkPending.";

        [MenuItem(AssetsMenu, true)]
        internal static bool ValidateAssets() => Selection.objects.Length > 0 && Selection.objects.All(IsPrefab);

        [MenuItem(AssetsMenu, false, 2001)]
        internal static void CreateFromAssets()
        {
            foreach (var o in Selection.objects.Where(IsPrefab).ToList())
                Create(AssetDatabase.GetAssetPath(o));
        }

        [MenuItem(ComponentMenu, true)]
        static bool ValidateComponent(MenuCommand cmd) =>
            cmd.context is VAPExpressions c && !PrefabUtility.IsPartOfPrefabInstance(c) && VAPPrefabAsset.PrefabPath(c) != null;

        [MenuItem(ComponentMenu)]
        static void CreateFromComponent(MenuCommand cmd)
        {
            var c = (VAPExpressions)cmd.context;
            string path = VAPPrefabAsset.PrefabPath(c);
            if (path == null)
                return;
            // Saving the prefab file can reload Prefab Mode, so the fields are filled on the next inspector draw.
            SessionState.SetBool(PendingKey + path, true);
            Create(path);
        }

        static bool IsPrefab(Object o) =>
            o is GameObject && AssetDatabase.GetAssetPath(o).EndsWith(".prefab");

        internal static string MenuName(string prefabPath) => Path.GetFileNameWithoutExtension(prefabPath) + " Menu";
        internal static string ParametersName(string prefabPath) => Path.GetFileNameWithoutExtension(prefabPath) + " Parameters";

        static void Create(string prefabPath)
        {
            var existing = AssetDatabase.LoadAllAssetsAtPath(prefabPath);
            var created = new List<string>();
            var parameters = existing.OfType<VRCExpressionParameters>().FirstOrDefault(a => a.name == ParametersName(prefabPath));
            if (parameters == null)
            {
                parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
                parameters.name = ParametersName(prefabPath);
                parameters.parameters = new VRCExpressionParameters.Parameter[0];
                AssetDatabase.AddObjectToAsset(parameters, prefabPath);
                created.Add(parameters.name);
            }
            var menu = existing.OfType<VRCExpressionsMenu>().FirstOrDefault(a => a.name == MenuName(prefabPath));
            if (menu == null)
            {
                menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                menu.name = MenuName(prefabPath);
                menu.controls = new List<VRCExpressionsMenu.Control>();
                menu.Parameters = parameters;
                AssetDatabase.AddObjectToAsset(menu, prefabPath);
                created.Add(menu.name);
            }
            if (created.Count == 0)
                return;
            AssetDatabase.SaveAssets();
            Debug.Log($"[VAP] Created {string.Join(" and ", created)} in {Path.GetFileName(prefabPath)}.");
        }

        // When a prefab is renamed, its menu and parameters assets named after it follow.
        class RenameWatcher : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                for (int i = 0; i < moved.Length; i++)
                {
                    if (!moved[i].EndsWith(".prefab"))
                        continue;
                    string oldName = Path.GetFileNameWithoutExtension(movedFrom[i]);
                    string newName = Path.GetFileNameWithoutExtension(moved[i]);
                    if (oldName != newName)
                    {
                        string path = moved[i];
                        EditorApplication.delayCall += () => RenameAssets(path, oldName, newName);
                    }
                }
            }
        }

        static void RenameAssets(string prefabPath, string oldName, string newName)
        {
            bool changed = false;
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(prefabPath))
            {
                if (!(a is VRCExpressionsMenu || a is VRCExpressionParameters) || !a.name.StartsWith(oldName + " "))
                    continue;
                a.name = newName + a.name.Substring(oldName.Length);
                EditorUtility.SetDirty(a);
                changed = true;
            }
            if (!changed)
                return;
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(prefabPath);
        }

        // Fills empty asset fields with the prefab's assets right after Create Menu & Parameters.
        internal static void LinkCreated(VAPExpressions c, SerializedProperty menuProp, SerializedProperty paramsProp)
        {
            string path = VAPPrefabAsset.PrefabPath(c);
            if (path == null || !SessionState.GetBool(PendingKey + path, false))
                return;
            SessionState.EraseBool(PendingKey + path);
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (menuProp.objectReferenceValue == null)
                menuProp.objectReferenceValue = assets.OfType<VRCExpressionsMenu>().FirstOrDefault(a => a.name == MenuName(path));
            if (paramsProp.objectReferenceValue == null)
                paramsProp.objectReferenceValue = assets.OfType<VRCExpressionParameters>().FirstOrDefault(a => a.name == ParametersName(path));
            menuProp.serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
