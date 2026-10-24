#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VAP.Editor
{
    // Moves assets into a prefab file as sub-assets, and back out into their own files.
    // References inside the prefab file are pointed at the moved copies.
    internal static class AssetMoves
    {
        internal const string InsertMenu = "Assets/VAP/Insert Into Prefab";
        internal const string InsertPickMenu = "Assets/VAP/Insert Assets...";
        internal const string ExtractMenu = "Assets/VAP/Extract From Prefab";

        static readonly HashSet<string> NativeExtensions = new HashSet<string>
        {
            ".asset", ".mat", ".anim", ".mask", ".overrideController", ".physicMaterial", ".physicsMaterial2D", ".flare", ".controller",
        };

        static bool IsPrefab(Object o) =>
            o is GameObject && AssetDatabase.IsMainAsset(o) &&
            AssetDatabase.GetAssetPath(o).EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase);

        static bool IsMovable(Object o)
        {
            if (o == null || !EditorUtility.IsPersistent(o) || o is GameObject || o is Component || o is DefaultAsset)
                return false;
            string path = AssetDatabase.GetAssetPath(o);
            if (!AssetDatabase.IsMainAsset(o))
                return true;
            return NativeExtensions.Contains(Path.GetExtension(path));
        }

        // The object plus the hidden objects in its file it uses, like an Animator Controller's states.
        // A main asset brings everything in its file.
        static List<Object> Bundle(Object o)
        {
            string path = AssetDatabase.GetAssetPath(o);
            if (AssetDatabase.IsMainAsset(o))
                return new[] { o }.Concat(AssetDatabase.LoadAllAssetsAtPath(path).Where(x => x != null && x != o)).ToList();
            var result = new List<Object> { o };
            var seen = new HashSet<Object> { o };
            for (int i = 0; i < result.Count; i++)
            {
                var it = new SerializedObject(result[i]).GetIterator();
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference)
                        continue;
                    var x = it.objectReferenceValue;
                    if (x == null || seen.Contains(x) || x is GameObject || x is Component || AssetDatabase.GetAssetPath(x) != path)
                        continue;
                    if ((x.hideFlags & HideFlags.HideInHierarchy) == 0 && !IsAnimatorPart(x))
                        continue;
                    seen.Add(x);
                    result.Add(x);
                }
            }
            return result;
        }

        static HideFlags FlagsFor(Object o) =>
            !AssetDatabase.IsMainAsset(o) && IsAnimatorPart(o) ? o.hideFlags | HideFlags.HideInHierarchy : o.hideFlags;

        static Object Copy(Object o)
        {
            var copy = Object.Instantiate(o);
            copy.name = o.name;
            copy.hideFlags = o.hideFlags;
            return copy;
        }

        static GameObject TargetPrefab() => Selection.objects.Where(IsPrefab).Cast<GameObject>().SingleOrDefault();

        static List<Object> ToInsert(GameObject prefab)
        {
            return Selection.objects.Where(o => CanInsert(prefab, o)).ToList();
        }

        [MenuItem(InsertMenu, true)]
        internal static bool ValidateInsert()
        {
            if (Selection.objects.Count(IsPrefab) != 1)
                return false;
            var prefab = TargetPrefab();
            var items = ToInsert(prefab);
            return items.Count > 0 && items.Count == Selection.objects.Length - 1;
        }

        [MenuItem(InsertMenu, false, 2002)]
        internal static void Insert()
        {
            var prefab = TargetPrefab();
            InsertInto(prefab, ToInsert(prefab));
        }

        [MenuItem(InsertPickMenu, true)]
        internal static bool ValidateInsertPick() => Selection.objects.Length == 1 && IsPrefab(Selection.activeObject);

        [MenuItem(InsertPickMenu, false, 2002)]
        internal static void InsertPick() => InsertWindow.Open((GameObject)Selection.activeObject);

        internal static bool CanInsert(GameObject prefab, Object o) =>
            o != null && !IsPrefab(o) && IsMovable(o) && AssetDatabase.GetAssetPath(o) != AssetDatabase.GetAssetPath(prefab);

        internal static bool InsertInto(GameObject prefab, List<Object> items)
        {
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            items = items.Where(o => CanInsert(prefab, o)).Distinct().ToList();
            if (items.Count == 0)
                return false;
            string names = string.Join("\n", items.Select(o => o.name));
            if (!EditorUtility.DisplayDialog("Insert Into Prefab",
                    $"Move these into {prefab.name}?\n\n{names}\n\nReferences inside {prefab.name} are kept. Other files that use them will lose the reference. This can't be undone.",
                    "Move", "Cancel"))
                return false;

            var map = new Dictionary<Object, Object>();
            var moved = new List<Object>();
            foreach (var o in items)
                foreach (var x in Bundle(o))
                {
                    if (map.ContainsKey(x))
                        continue;
                    var copy = Copy(x);
                    AssetDatabase.AddObjectToAsset(copy, prefabPath);
                    copy.hideFlags = FlagsFor(x);
                    map[x] = copy;
                    moved.Add(x);
                }
            Repoint(AssetDatabase.LoadAllAssetsAtPath(prefabPath), map);
            RemoveOriginals(moved);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(prefabPath);
            if (map.Values.Any(IsAnimatorPart))
            {
                Watch(prefabPath);
                HidePartsInFile(prefabPath);
            }
            Selection.objects = items.Select(o => map[o]).ToArray();
            return true;
        }

        static List<Object> ToExtract() =>
            Selection.objects.Where(o => DeleteSubAsset.IsSubAsset(o) && IsPrefab(AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GetAssetPath(o)))).ToList();

        [MenuItem(ExtractMenu, true)]
        internal static bool ValidateExtract() => Selection.objects.Length > 0 && ToExtract().Count == Selection.objects.Length;

        [MenuItem(ExtractMenu, false, 2003)]
        internal static void Extract()
        {
            var items = ToExtract();
            string names = string.Join("\n", items.Select(o => o.name));
            if (!EditorUtility.DisplayDialog("Extract From Prefab",
                    $"Move these into their own files next to the prefab?\n\n{names}\n\nReferences inside the prefab are kept. This can't be undone.",
                    "Extract", "Cancel"))
                return;

            var created = new List<Object>();
            foreach (var group in items.GroupBy(AssetDatabase.GetAssetPath))
            {
                string prefabPath = group.Key;
                string folder = Path.GetDirectoryName(prefabPath)?.Replace('\\', '/');
                var map = new Dictionary<Object, Object>();
                var moved = new List<Object>();
                foreach (var o in group)
                {
                    if (map.ContainsKey(o))
                        continue;
                    var copy = Copy(o);
                    copy.hideFlags = HideFlags.None;
                    string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{Sanitize(o.name)}{ExtensionFor(o)}");
                    AssetDatabase.CreateAsset(copy, path);
                    map[o] = copy;
                    moved.Add(o);
                    created.Add(copy);
                    foreach (var x in Bundle(o).Skip(1))
                    {
                        if (map.ContainsKey(x))
                            continue;
                        var part = Copy(x);
                        AssetDatabase.AddObjectToAsset(part, path);
                        part.hideFlags = FlagsFor(x);
                        map[x] = part;
                        moved.Add(x);
                    }
                }
                Repoint(AssetDatabase.LoadAllAssetsAtPath(prefabPath).Concat(map.Values), map);
                RemoveOriginals(moved);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(prefabPath);
                if (Watched().Contains(AssetDatabase.AssetPathToGUID(prefabPath)))
                    HidePartsInFile(prefabPath);
            }
            foreach (var path in created.Select(AssetDatabase.GetAssetPath).Distinct().ToList())
                HidePartsInFile(path);
            Selection.objects = created.ToArray();
        }

        internal class InsertWindow : EditorWindow
        {
            GameObject prefab;
            readonly List<Object> items = new List<Object>();
            Vector2 scroll;

            internal static void Open(GameObject prefab)
            {
                var window = CreateInstance<InsertWindow>();
                window.prefab = prefab;
                window.titleContent = new GUIContent("Insert Assets");
                window.minSize = new Vector2(340f, 220f);
                window.ShowUtility();
            }

            void OnGUI()
            {
                if (prefab == null)
                {
                    Close();
                    return;
                }
                EditorGUILayout.LabelField("Into", prefab.name, EditorStyles.boldLabel);

                var drop = GUILayoutUtility.GetRect(0f, 48f, GUILayout.ExpandWidth(true));
                GUI.Box(drop, new GUIContent("Drop assets here", "Assets saved by Unity (.asset, .anim, .mat and similar) or sub-assets of other files. Imported files like models and textures can't be moved."), EditorStyles.helpBox);
                var e = Event.current;
                if ((e.type == EventType.DragUpdated || e.type == EventType.DragPerform) && drop.Contains(e.mousePosition))
                {
                    bool any = DragAndDrop.objectReferences.Any(o => CanInsert(prefab, o));
                    DragAndDrop.visualMode = any ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                    if (e.type == EventType.DragPerform && any)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (var o in DragAndDrop.objectReferences)
                            if (CanInsert(prefab, o) && !items.Contains(o))
                                items.Add(o);
                    }
                    e.Use();
                }

                scroll = EditorGUILayout.BeginScrollView(scroll);
                for (int i = 0; i < items.Count; i++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(true))
                            EditorGUILayout.ObjectField(items[i], typeof(Object), false);
                        if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(60f)))
                        {
                            items.RemoveAt(i);
                            GUIUtility.ExitGUI();
                        }
                    }
                }
                EditorGUILayout.EndScrollView();

                items.RemoveAll(o => o == null);
                using (new EditorGUI.DisabledScope(items.Count == 0))
                    if (GUILayout.Button(items.Count > 0 ? $"Insert {items.Count}" : "Insert") && InsertInto(prefab, items.ToList()))
                        Close();
            }
        }

        internal const string HidePartsMenu = "Assets/VAP/Hide Animator Parts";

        // States, transitions and blend trees are normally hidden inside the file that holds them.
        static bool IsAnimatorPart(Object o) =>
            o is UnityEditor.Animations.AnimatorState || o is UnityEditor.Animations.AnimatorStateMachine ||
            o is UnityEditor.Animations.AnimatorTransitionBase || o is UnityEditor.Animations.BlendTree || o is StateMachineBehaviour;

        static List<Object> VisibleParts(string path) =>
            AssetDatabase.LoadAllAssetsAtPath(path)
                .Where(o => o != null && !AssetDatabase.IsMainAsset(o) && IsAnimatorPart(o) && (o.hideFlags & HideFlags.HideInHierarchy) == 0)
                .ToList();

        static IEnumerable<string> SelectedPaths() =>
            Selection.objects.Where(o => o != null && EditorUtility.IsPersistent(o)).Select(AssetDatabase.GetAssetPath).Distinct();

        [MenuItem(HidePartsMenu, true)]
        internal static bool ValidateHideParts() => SelectedPaths().Any(p => VisibleParts(p).Count > 0);

        [MenuItem(HidePartsMenu, false, 2004)]
        internal static void HideParts()
        {
            foreach (var path in SelectedPaths().ToList())
            {
                if (VisibleParts(path).Count == 0)
                    continue;
                Watch(path);
                HidePartsInFile(path);
            }
            EditorApplication.RepaintProjectWindow();
        }

        // Unity doesn't keep hide flags set from code on objects inside prefab files, so they're set in the file.
        // AnimatorState, AnimatorStateMachine, BlendTree, AnimatorStateTransition, AnimatorTransition.
        static readonly HashSet<string> PartClassIds = new HashSet<string> { "1102", "1107", "206", "1101", "1109" };
        static readonly Regex ObjectHeader = new Regex(@"^--- !u!(\d+) &(-?\d+)", RegexOptions.Compiled);
        static readonly Regex BehaviourList = new Regex(@"m_StateMachineBehaviours:\r?\n((?:[ \t]*- \{fileID: -?\d+\}\r?\n)*)", RegexOptions.Compiled);
        static readonly Regex FileId = new Regex(@"fileID: (-?\d+)", RegexOptions.Compiled);
        static readonly Regex VisibleFlag = new Regex(@"(?m)^(  m_ObjectHideFlags: )0(\r?)$", RegexOptions.Compiled);

        // Prefabs whose animator parts VAP keeps hidden: ones it moved a controller into, or ones
        // Hide Animator Parts was used on. Stored per project in UserSettings.
        const string WatchedKey = "VAP.HiddenAnimatorParts";

        static HashSet<string> Watched() =>
            new HashSet<string>((EditorUserSettings.GetConfigValue(WatchedKey) ?? "").Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries));

        static void Watch(string path)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid) || !path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                return;
            var set = Watched();
            if (set.Add(guid))
                EditorUserSettings.SetConfigValue(WatchedKey, string.Join(";", set));
        }

        // Unity writes these flags back as visible whenever it saves a prefab, so watched prefabs are checked after each import.
        class PartsWatcher : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                var watched = Watched();
                if (watched.Count == 0)
                    return;
                var prefabs = imported.Where(p => watched.Contains(AssetDatabase.AssetPathToGUID(p))).ToList();
                if (prefabs.Count == 0)
                    return;
                EditorApplication.delayCall += () =>
                {
                    foreach (var path in prefabs)
                        HidePartsInFile(path);
                };
            }
        }

        static void HidePartsInFile(string path)
        {
            string full = Path.GetFullPath(path);
            if (!File.Exists(full))
                return;
            string text = File.ReadAllText(full);
            if (!text.StartsWith("%YAML"))
                return;
            var parts = Regex.Split(text, @"(?m)^(?=--- !u!)");
            // Behaviours on states and state machines (like Parameter Drivers) are hidden too.
            var behaviours = new HashSet<string>();
            foreach (Match list in BehaviourList.Matches(text))
                foreach (Match id in FileId.Matches(list.Groups[1].Value))
                    behaviours.Add(id.Groups[1].Value);
            bool changed = false;
            for (int i = 0; i < parts.Length; i++)
            {
                var m = ObjectHeader.Match(parts[i]);
                if (!m.Success)
                    continue;
                bool part = PartClassIds.Contains(m.Groups[1].Value) ||
                            (m.Groups[1].Value == "114" && behaviours.Contains(m.Groups[2].Value));
                if (!part)
                    continue;
                string fixedPart = VisibleFlag.Replace(parts[i], "${1}1${2}", 1);
                if (fixedPart != parts[i])
                {
                    parts[i] = fixedPart;
                    changed = true;
                }
            }
            if (!changed)
                return;
            File.WriteAllText(full, string.Concat(parts));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        static string ExtensionFor(Object o) =>
            o is UnityEditor.Animations.AnimatorController ? ".controller"
            : o is AnimationClip ? ".anim"
            : o is Material ? ".mat"
            : o is AvatarMask ? ".mask"
            : o is AnimatorOverrideController ? ".overrideController"
            : o is PhysicMaterial ? ".physicMaterial"
            : ".asset";

        static string Sanitize(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return string.IsNullOrWhiteSpace(name) ? "Asset" : name;
        }

        static void Repoint(IEnumerable<Object> objects, Dictionary<Object, Object> map)
        {
            foreach (var target in objects)
            {
                if (target == null)
                    continue;
                var so = new SerializedObject(target);
                var it = so.GetIterator();
                bool changed = false;
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null)
                        continue;
                    if (map.TryGetValue(it.objectReferenceValue, out var copy))
                    {
                        it.objectReferenceValue = copy;
                        changed = true;
                    }
                }
                if (changed)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(target);
                }
            }
        }

        static void RemoveOriginals(List<Object> items)
        {
            foreach (var path in items.Where(o => o != null && AssetDatabase.IsMainAsset(o)).Select(AssetDatabase.GetAssetPath).ToList())
                AssetDatabase.DeleteAsset(path);
            foreach (var o in items)
            {
                if (o == null)
                    continue;
                AssetDatabase.RemoveObjectFromAsset(o);
                Object.DestroyImmediate(o, true);
            }
        }
    }
}
#endif
