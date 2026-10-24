#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VAP.Editor
{
    // Hides the VRCFury component a VAPToggle wraps, in the editor only. The flag is cleared while
    // scenes and prefabs are saved, so files always hold a normal, visible VRCFury component.
    [InitializeOnLoad]
    internal static class WrappedToggles
    {
        // Wrapped components that must stay when their VAPToggle goes away.
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

        // Components the inspector was already rebuilt for, so a rebuild can never repeat endlessly.
        static readonly HashSet<int> rebuilt = new HashSet<int>();

        // Rebuilds the inspector when it still holds an editor for a wrapped component.
        internal static void HideShownEditors()
        {
            var tracker = ActiveEditorTracker.sharedTracker;
            foreach (var editor in tracker.activeEditors)
            {
                if (editor == null || !(editor.target is Component c) || (c.hideFlags & HideFlags.HideInInspector) == 0)
                    continue;
                if (c.GetComponents<VAPToggle>().Any(t => t.toggle == c) && rebuilt.Add(c.GetInstanceID()))
                {
                    EditorApplication.delayCall += tracker.ForceRebuild;
                    return;
                }
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
            bool changed = false;
            foreach (var t in root.GetComponentsInChildren<VAPToggle>(true))
                changed |= Set(t.toggle, hidden);
            if (changed)
                ActiveEditorTracker.sharedTracker.ForceRebuild();
        }

        internal static bool Set(Component c, bool hidden)
        {
            if (c == null)
                return false;
            var flags = hidden ? c.hideFlags | HideFlags.HideInInspector : c.hideFlags & ~HideFlags.HideInInspector;
            if (flags == c.hideFlags)
                return false;
            c.hideFlags = flags;
            return true;
        }

        [MenuItem("CONTEXT/VRCFury/Wrap with VAP", true)]
        static bool ValidateWrap(MenuCommand command) =>
            command.context is Component c && VAPToggleEditor.IsWrappable(c) &&
            !c.GetComponents<VAPToggle>().Any(t => t.toggle == c);

        // Puts a VAPToggle in place of an existing VRCFury Toggle and hides the original under it.
        [MenuItem("CONTEXT/VRCFury/Wrap with VAP")]
        static void Wrap(MenuCommand command)
        {
            var vf = (Component)command.context;
            Undo.SetCurrentGroupName("Wrap with VAP");
            int group = Undo.GetCurrentGroup();
            var t = Undo.AddComponent<VAPToggle>(vf.gameObject);
            t.toggle = vf;
            var components = vf.GetComponents<Component>();
            int target = System.Array.IndexOf(components, vf);
            for (int i = components.Length - 1; i > target; i--)
                if (!ComponentUtility.MoveComponentUp(t))
                    break;
            EditorUtility.SetDirty(t);
            Set(vf, true);
            Undo.CollapseUndoOperations(group);
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }

        [MenuItem("CONTEXT/VAPToggle/Unwrap", true)]
        static bool ValidateUnwrap(MenuCommand command) =>
            command.context is VAPToggle t && t.toggle != null &&
            (!PrefabUtility.IsPartOfPrefabInstance(t) || PrefabUtility.IsAddedComponentOverride(t));

        // Replaces the wrapper with the plain VRCFury Toggle, keeping its default override.
        [MenuItem("CONTEXT/VAPToggle/Unwrap")]
        static void Unwrap(MenuCommand command)
        {
            var t = (VAPToggle)command.context;
            var vf = t.toggle;
            Undo.SetCurrentGroupName("Unwrap VRCFury Toggle");
            int group = Undo.GetCurrentGroup();
            if (t.overrideDefault)
            {
                Undo.RecordObject(vf, "Unwrap VRCFury Toggle");
                VRCFuryToggleDefaults.SetDefault(vf, t.defaultOn, t.defaultValue, true);
            }
            Released.Add(vf);
            Set(vf, false);
            Undo.DestroyObjectImmediate(t);
            Undo.CollapseUndoOperations(group);
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }
    }
}
#endif
