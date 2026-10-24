#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace VAP.Editor
{
    // Unity only greys out menu items that don't apply. This takes the VAP asset items out of the
    // menu while they don't apply to the selection, and puts them back when they do.
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
            new Item { path = DeleteSubAsset.MenuPath, priority = 2000, run = DeleteSubAsset.Delete, valid = DeleteSubAsset.Validate },
            new Item { path = AddParametersWindow.AssetsMenu, priority = 2001, run = AddParametersWindow.FromAssets, valid = AddParametersWindow.ValidateAssets },
            new Item { path = ExpressionsAssets.AssetsMenu, priority = 2001, run = ExpressionsAssets.CreateFromAssets, valid = ExpressionsAssets.ValidateAssets },
            new Item { path = AssetMoves.InsertMenu, priority = 2002, run = AssetMoves.Insert, valid = AssetMoves.ValidateInsert },
            new Item { path = AssetMoves.InsertPickMenu, priority = 2002, run = AssetMoves.InsertPick, valid = AssetMoves.ValidateInsertPick },
            new Item { path = AssetMoves.ExtractMenu, priority = 2003, run = AssetMoves.Extract, valid = AssetMoves.ValidateExtract },
            new Item { path = AssetMoves.HidePartsMenu, priority = 2004, run = AssetMoves.HideParts, valid = AssetMoves.ValidateHideParts },
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
                    // The menu API changed; leave the menu as Unity built it.
                    return;
                }
            }
            if (changed)
                UpdateMethod?.Invoke(null, null);
        }
    }
}
#endif
