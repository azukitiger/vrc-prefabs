#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VAP.Editor
{
    [InitializeOnLoad]
    internal static class FindPropertyAnimations
    {
        static readonly GUIContent MenuLabel = new GUIContent("Find Animations Using This Property");
        static readonly Dictionary<string, (long time, List<(AnimationClip clip, EditorCurveBinding[] bindings)> clips)> cache =
            new Dictionary<string, (long, List<(AnimationClip, EditorCurveBinding[])>)>();

        static FindPropertyAnimations() => EditorApplication.contextualPropertyMenu += OnMenu;

        static void OnMenu(GenericMenu menu, SerializedProperty property)
        {
            var target = property.serializedObject.targetObject;
            GameObject owner = target is Component c ? c.gameObject : target as GameObject;
            if (owner == null || EditorUtility.IsPersistent(target))
                return;
            var type = target.GetType();
            string path = property.propertyPath;
            string label = property.displayName;
            const string Weights = "m_BlendShapeWeights.Array.data[";
            if (target is SkinnedMeshRenderer smr && smr.sharedMesh != null && path.StartsWith(Weights)
                && int.TryParse(path.Substring(Weights.Length).TrimEnd(']'), out int index) && index < smr.sharedMesh.blendShapeCount)
            {
                label = smr.sharedMesh.GetBlendShapeName(index);
                path = "blendShape." + label;
            }
            if (!Animatable(owner, type, path))
                return;
            menu.AddItem(MenuLabel, false, () => Run(owner, type, path, label));
        }

        static GameObject bindingsOwner;
        static double bindingsTime;
        static EditorCurveBinding[] bindings;

        static EditorCurveBinding[] Bindings(GameObject owner)
        {
            if (owner != bindingsOwner || EditorApplication.timeSinceStartup - bindingsTime > 1.0)
            {
                var animator = owner.GetComponentInParent<Animator>(true);
                var root = animator != null ? animator.gameObject : owner.transform.root.gameObject;
                bindings = AnimationUtility.GetAnimatableBindings(owner, root);
                bindingsOwner = owner;
                bindingsTime = EditorApplication.timeSinceStartup;
            }
            return bindings;
        }

        static bool SameProperty(string a, string b) => a == b || a.StartsWith(b + ".") || b.StartsWith(a + ".");

        static bool Animatable(GameObject owner, Type type, string property) =>
            Bindings(owner).Any(b => b.type == type && SameProperty(b.propertyName, property));

        [MenuItem("CONTEXT/Component/Find Animations Using This Component")]
        static void FromComponent(MenuCommand command)
        {
            if (command.context is Component component && !EditorUtility.IsPersistent(component))
                PropertyAnimationsWindow.Open(component);
        }

        [MenuItem("CONTEXT/Component/Find Animations Using This Component", true)]
        static bool CanFromComponent(MenuCommand command) =>
            command.context is Component component && !EditorUtility.IsPersistent(component)
            && Bindings(component.gameObject).Any(b => b.type == component.GetType() && b.propertyName != "m_Enabled");

        static void Run(GameObject owner, Type type, string path, string label)
        {
            var found = Find(owner, type, path);
            if (found != null)
                PropertyAnimationsWindow.Open(owner.name, type.Name, label, found);
        }

        internal static List<(AnimationClip clip, string path, string property)> Find(GameObject owner, Type type, string property)
        {
            bool isRoot = owner.GetComponent<Animator>() != null;
            var guids = AssetDatabase.FindAssets("t:AnimationClip");
            var assetPaths = guids.Select(AssetDatabase.GUIDToAssetPath).Distinct().Where(p => !IsBuildOutput(p)).ToList();
            var results = new List<(AnimationClip, string, string)>();
            try
            {
                for (int i = 0; i < assetPaths.Count; i++)
                {
                    if (i % 50 == 0 && EditorUtility.DisplayCancelableProgressBar("Find Animations Using This Property", assetPaths[i], (float)i / assetPaths.Count))
                        return null;
                    foreach (var (clip, bindings) in Clips(assetPaths[i]))
                        foreach (var b in bindings)
                            if (Matches(b, owner.name, isRoot, type, property))
                                results.Add((clip, b.path, b.propertyName));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            return results;
        }

        static bool IsBuildOutput(string assetPath) =>
            assetPath.StartsWith("Packages/nadena.dev.ndmf/__Generated/") || assetPath.StartsWith("Packages/com.vrcfury.temp/")
            || assetPath.StartsWith("Assets/_TrashTemp/") || assetPath.StartsWith("Assets/ZZZ_GeneratedAssets_");

        static List<(AnimationClip clip, EditorCurveBinding[] bindings)> Clips(string assetPath)
        {
            long time = File.Exists(assetPath) ? File.GetLastWriteTimeUtc(assetPath).Ticks : 0;
            if (cache.TryGetValue(assetPath, out var cached) && cached.time == time && cached.clips.All(x => x.clip != null))
                return cached.clips;
            var clips = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .Select(c => (c, AnimationUtility.GetCurveBindings(c).Concat(AnimationUtility.GetObjectReferenceCurveBindings(c)).ToArray()))
                .ToList();
            cache[assetPath] = (time, clips);
            return clips;
        }

        static bool Matches(EditorCurveBinding b, string name, bool isRoot, Type type, string property)
        {
            if (b.type == null || !b.type.IsAssignableFrom(type) || b.type == typeof(Object))
                return false;
            string last = b.path.Substring(b.path.LastIndexOf('/') + 1);
            if (b.path.Length == 0 ? !isRoot : last != name)
                return false;
            return property == null || SameProperty(b.propertyName, property);
        }
    }

    internal class PropertyAnimationsWindow : EditorWindow
    {
        string header;
        List<(AnimationClip clip, string path, string property)> results = new List<(AnimationClip, string, string)>();
        Vector2 scroll;
        bool single;

        internal static void Open(string objectName, string typeName, string label, List<(AnimationClip clip, string path, string property)> results)
        {
            var window = GetWindow<PropertyAnimationsWindow>();
            window.titleContent = new GUIContent("Property Animations");
            window.header = label == null ? $"{objectName} > {typeName}" : $"{objectName} > {typeName} > {label}";
            window.results = results;
            window.single = label != null;
            window.scroll = Vector2.zero;
            window.Show();
        }

        internal static void Open(Component component)
        {
            var found = FindPropertyAnimations.Find(component.gameObject, component.GetType(), null);
            if (found == null)
                return;
            var active = FindPropertyAnimations.Find(component.gameObject, typeof(GameObject), "m_IsActive");
            if (active == null)
                return;
            Open(component.gameObject.name, component.GetType().Name, null, active.Concat(found).ToList());
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField(header ?? "", EditorStyles.boldLabel);
            results.RemoveAll(r => r.clip == null);
            var clips = results.GroupBy(r => r.clip).ToList();
            if (clips.Count == 0)
            {
                EditorGUILayout.LabelField("No animations found", EditorStyles.miniLabel);
                return;
            }
            var names = new HashSet<string>(clips.GroupBy(g => g.Key.name).Where(g => g.Count() > 1).Select(g => g.Key));
            EditorGUILayout.LabelField(clips.Count == 1 ? "1 animation" : $"{clips.Count} animations", EditorStyles.miniLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var group in clips)
            {
                var clip = group.Key;
                var properties = group.Select(r => r.property).Distinct().ToList();
                string list = string.Join("\n", group.Select(r => $"{r.path} : {r.property}").Distinct());
                string folder = names.Contains(clip.name) ? System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(clip))?.Replace('\\', '/') + "\n" : "";
                var rect = EditorGUILayout.GetControlRect();
                var e = Event.current;
                if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
                {
                    if (e.clickCount > 1)
                        AssetDatabase.OpenAsset(clip);
                    else
                    {
                        Selection.activeObject = clip;
                        EditorGUIUtility.PingObject(clip);
                    }
                    e.Use();
                }
                float half = rect.width * 0.5f;
                var content = new GUIContent(EditorGUIUtility.ObjectContent(clip, typeof(AnimationClip))) { tooltip = folder + list };
                EditorGUI.LabelField(new Rect(rect.x, rect.y, half - 4f, rect.height), content);
                string right = single ? string.Join(", ", properties) : properties.Count == 1 ? "1 property" : $"{properties.Count} properties";
                EditorGUI.LabelField(new Rect(rect.x + half, rect.y, half, rect.height), new GUIContent(right, folder + list), EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
#endif
