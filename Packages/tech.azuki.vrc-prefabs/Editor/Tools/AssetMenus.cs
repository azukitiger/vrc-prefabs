#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace VAP.Editor
{
    [InitializeOnLoad]
    internal static class AssetMenus
    {
        class Item
        {
            public string path;
            public int priority;
            public Action run;
            public Func<bool> valid;
        }

        static readonly Item[] Items =
        {
            new Item { path = AddParametersWindow.AssetsMenu, priority = 2001, run = AddParametersWindow.FromAssets, valid = AddParametersWindow.ValidateAssets },
        };

        const BindingFlags Internal = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        static readonly MethodInfo AddMethod = typeof(Menu).GetMethod("AddMenuItem", Internal, null,
            new[] { typeof(string), typeof(string), typeof(bool), typeof(int), typeof(Action), typeof(Func<bool>) }, null);
        static readonly MethodInfo RemoveMethod = typeof(Menu).GetMethod("RemoveMenuItem", Internal, null, new[] { typeof(string) }, null);
        static readonly MethodInfo UpdateMethod = typeof(EditorUtility).GetMethod("Internal_UpdateAllMenus", Internal);

        static readonly HashSet<string> removed = new HashSet<string>();

        static AssetMenus()
        {
            if (AddMethod == null || RemoveMethod == null)
                return;
            Selection.selectionChanged += Refresh;
            EditorApplication.delayCall += Refresh;
        }

        static void Refresh()
        {
            bool changed = false;
            foreach (var item in Items)
            {
                bool valid;
                try
                {
                    valid = item.valid();
                }
                catch
                {
                    valid = false;
                }
                try
                {
                    if (!valid && removed.Add(item.path))
                    {
                        RemoveMethod.Invoke(null, new object[] { item.path });
                        changed = true;
                    }
                    else if (valid && removed.Remove(item.path))
                    {
                        AddMethod.Invoke(null, new object[] { item.path, "", false, item.priority, item.run, item.valid });
                        changed = true;
                    }
                }
                catch
                {
                    return;
                }
            }
            if (changed)
                UpdateMethod?.Invoke(null, null);
        }
    }
}
#endif
