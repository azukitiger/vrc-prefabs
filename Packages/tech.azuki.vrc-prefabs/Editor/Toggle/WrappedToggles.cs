#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VAP.Editor
{
    [InitializeOnLoad]
    internal static class WrappedToggles
    {
        internal static readonly HashSet<Component> Released = new HashSet<Component>();

        static WrappedToggles()
        {
            EditorSceneManager.sceneSaving += (scene, path) => SetAll(scene, false);
            EditorSceneManager.sceneSaved += scene => SetAll(scene, true);
            EditorSceneManager.sceneOpened += (scene, mode) => SetAll(scene, true);
            PrefabStage.prefabSaving += root => SetAll(root, false);
            PrefabStage.prefabSaved += root => SetAll(root, true);
            PrefabStage.prefabStageOpened += stage => SetAll(stage.prefabContentsRoot, true);
            PrefabUtility.prefabInstanceUpdated += root => SetAll(root, true);
            Selection.selectionChanged += () =>
            {
                rebuilt.Clear();
                HideSelected();
            };
            EditorApplication.update += Check;
            EditorApplication.hierarchyChanged += HideSelected;
            Undo.undoRedoPerformed += HideSelected;
            EditorApplication.delayCall += () =>
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    SetAll(SceneManager.GetSceneAt(i), true);
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null)
                    SetAll(stage.prefabContentsRoot, true);
            };
        }

        static void HideSelected()
        {
            foreach (var go in Selection.gameObjects)
                SetAll(go, true);
            HideShownEditors();
        }

        static readonly Dictionary<int, int> rebuilt = new Dictionary<int, int>();
        const int MaxRebuilds = 3;
        static double nextCheck;
        static bool animating;

        static void Check()
        {
            if (EditorApplication.timeSinceStartup < nextCheck)
                return;
            nextCheck = EditorApplication.timeSinceStartup + 0.25;
            if (animating != AnimationMode.InAnimationMode())
            {
                animating = !animating;
                foreach (var go in Selection.gameObjects)
                    SetAll(go, true);
            }
            HideShownEditors();
        }

        internal static void HideShownEditors()
        {
            var tracker = ActiveEditorTracker.sharedTracker;
            foreach (var editor in tracker.activeEditors)
            {
                if (editor == null || !(editor.target is Component c) || c == null || Released.Contains(c) || !IsWrapped(c))
                    continue;
                Set(c, true);
                int id = c.GetInstanceID();
                rebuilt.TryGetValue(id, out int count);
                if (count >= MaxRebuilds)
                    continue;
                rebuilt[id] = count + 1;
                EditorApplication.delayCall += tracker.ForceRebuild;
                return;
            }
        }

        static void SetAll(Scene scene, bool hidden)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;
            foreach (var root in scene.GetRootGameObjects())
                SetAll(root, hidden);
        }

        static void SetAll(GameObject root, bool hidden)
        {
            if (root == null)
                return;
            if (hidden)
                MergeDuplicates(root);
            bool changed = false;
            foreach (var w in root.GetComponentsInChildren<MonoBehaviour>(true).OfType<IVRCFuryWrapper>())
                changed |= Set(w.Wrapped, hidden);
            if (changed)
                ActiveEditorTracker.sharedTracker.ForceRebuild();
        }

        static void MergeDuplicates(GameObject root)
        {
            var extras = new List<(MonoBehaviour from, MonoBehaviour to)>();
            var groups = root.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(m => m is IVRCFuryWrapper w && w.Wrapped != null)
                .GroupBy(m => ((IVRCFuryWrapper)m).Wrapped);
            foreach (var g in groups)
            {
                var list = g.ToList();
                if (list.Count < 2)
                    continue;
                var kept = list.FirstOrDefault(m => !PrefabUtility.IsAddedComponentOverride(m));
                if (kept == null)
                    continue;
                foreach (var m in list)
                    if (m != kept && m.GetType() == kept.GetType() && PrefabUtility.IsAddedComponentOverride(m))
                        extras.Add((m, kept));
            }
            if (extras.Count == 0)
                return;
            EditorApplication.delayCall += () =>
            {
                foreach (var (from, to) in extras)
                    if (from != null && to != null)
                        Merge(from, to);
            };
        }

        static void Merge(MonoBehaviour from, MonoBehaviour to)
        {
            var source = new SerializedObject(from);
            var target = new SerializedObject(to);
            var p = source.GetIterator();
            for (bool enter = true; p.Next(enter); enter = false)
                if (!p.name.StartsWith("m_") || p.name == "m_Enabled")
                    target.CopyFromSerializedProperty(p);
            target.ApplyModifiedProperties();
            Undo.DestroyObjectImmediate(from);
            Debug.Log($"[VAP VRCFury Wrapper] Moved the overrides of the wrapper added on {to.name} into the one from its prefab.", to);
        }

        static bool IsWrapped(Component c) => c.GetComponents<MonoBehaviour>().OfType<IVRCFuryWrapper>().Any(w => w.Wrapped == c);

        internal static bool Set(Component c, bool hidden)
        {
            if (c == null)
                return false;
            if (hidden && c is Renderer && AnimationMode.InAnimationMode())
            {
                if ((c.hideFlags & HideFlags.HideInInspector) == 0)
                    return false;
                c.hideFlags &= ~HideFlags.HideInInspector;
                InternalEditorUtility.SetIsInspectorExpanded(c, false);
                return true;
            }
            var flags = hidden ? c.hideFlags | HideFlags.HideInInspector : c.hideFlags & ~HideFlags.HideInInspector;
            if (flags == c.hideFlags)
                return false;
            c.hideFlags = flags;
            return true;
        }

        static System.Type WrapperFor(Component c) =>
            VAPToggleEditor.IsWrappable(c) ? typeof(VAPToggle)
            : VRCFuryWrap.HasFeature(c, FullControllerSource.FeatureType) ? typeof(VAPFullController)
            : null;

        [MenuItem("CONTEXT/VRCFury/Wrap with VAP", true)]
        static bool ValidateWrap(MenuCommand command) =>
            command.context is Component c && WrapperFor(c) != null && !IsWrapped(c);

        [MenuItem("CONTEXT/VRCFury/Wrap with VAP")]
        static void Wrap(MenuCommand command)
        {
            var vf = (Component)command.context;
            Undo.SetCurrentGroupName("Wrap with VAP");
            int group = Undo.GetCurrentGroup();
            var w = Undo.AddComponent(vf.gameObject, WrapperFor(vf));
            ((IVRCFuryWrapper)w).Wrapped = vf;
            var components = vf.GetComponents<Component>();
            int target = System.Array.IndexOf(components, vf);
            for (int i = components.Length - 1; i > target; i--)
                if (!ComponentUtility.MoveComponentUp(w))
                    break;
            EditorUtility.SetDirty(w);
            Set(vf, true);
            Undo.CollapseUndoOperations(group);
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }

        static bool CanUnwrap(Object o) =>
            o is MonoBehaviour m && m is IVRCFuryWrapper w && w.Wrapped != null &&
            (!PrefabUtility.IsPartOfPrefabInstance(m) || PrefabUtility.IsAddedComponentOverride(m));

        [MenuItem("CONTEXT/VAPToggle/Unwrap", true)]
        static bool ValidateUnwrapToggle(MenuCommand command) => CanUnwrap(command.context);

        [MenuItem("CONTEXT/VAPToggle/Unwrap")]
        static void UnwrapToggle(MenuCommand command)
        {
            var t = (VAPToggle)command.context;
            Undo.SetCurrentGroupName("Unwrap VRCFury Toggle");
            int group = Undo.GetCurrentGroup();
            if (t.overrideDefault)
            {
                Undo.RecordObject(t.toggle, "Unwrap VRCFury Toggle");
                VRCFuryToggleDefaults.SetDefault(t.toggle, t.defaultOn, t.defaultValue, true);
            }
            VRCFuryToggleDefaults.WriteMenuPath(t, true);
            Release(t, t.toggle);
            Undo.CollapseUndoOperations(group);
        }

        [MenuItem("CONTEXT/VAPFullController/Unwrap", true)]
        static bool ValidateUnwrapFullController(MenuCommand command) => CanUnwrap(command.context);

        [MenuItem("CONTEXT/VAPFullController/Unwrap")]
        static void UnwrapFullController(MenuCommand command)
        {
            var f = (VAPFullController)command.context;
            bool hasChanges = f.defaultOverrides.Count > 0 || f.controlOverrides.Exists(o => o != null && !o.IsEmpty);
            if (hasChanges && !EditorUtility.DisplayDialog("Unwrap VRCFury Full Controller",
                    "The start values and removed menu items set on this component can't be kept, because they would have to change the menu and parameter files. Unwrap anyway?",
                    "Unwrap", "Cancel"))
                return;
            Undo.SetCurrentGroupName("Unwrap VRCFury Full Controller");
            int group = Undo.GetCurrentGroup();
            if (f.wrapped != null)
                Undo.RecordObject(f.wrapped, "Unwrap VRCFury Full Controller");
            FullControllerSource.WriteMenuPath(f, true);
            Release(f, f.wrapped);
            Undo.CollapseUndoOperations(group);
        }

        internal static void Release(MonoBehaviour wrapper, Component vf)
        {
            Released.Add(vf);
            Set(vf, false);
            Undo.DestroyObjectImmediate(wrapper);
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }
    }
}
#endif
