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
    // Hides the VRCFury component a VAP wrapper (VAPToggle, VAPFullController) wraps, in the editor only. The flag is cleared while
    // scenes and prefabs are saved, so files always hold a normal, visible VRCFury component.
    [InitializeOnLoad]
    internal static class WrappedToggles
    {
        // Wrapped components that must stay when their wrapper goes away.
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
                if (IsWrapped(c) && rebuilt.Add(c.GetInstanceID()))
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
            foreach (var w in root.GetComponentsInChildren<MonoBehaviour>(true).OfType<IVRCFuryWrapper>())
                changed |= Set(w.Wrapped, hidden);
            if (changed)
                ActiveEditorTracker.sharedTracker.ForceRebuild();
        }

        static bool IsWrapped(Component c) => c.GetComponents<MonoBehaviour>().OfType<IVRCFuryWrapper>().Any(w => w.Wrapped == c);

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

        static System.Type WrapperFor(Component c) =>
            VAPToggleEditor.IsWrappable(c) ? typeof(VAPToggle)
            : VRCFuryWrap.HasFeature(c, FullControllerSource.FeatureType) ? typeof(VAPFullController)
            : null;

        [MenuItem("CONTEXT/VRCFury/Wrap with VAP", true)]
        static bool ValidateWrap(MenuCommand command) =>
            command.context is Component c && WrapperFor(c) != null && !IsWrapped(c);

        // Puts a VAP wrapper in place of an existing VRCFury Toggle or Full Controller and hides the original under it.
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

        // Replaces the wrapper with the plain VRCFury Toggle, keeping its default override.
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
            Release(t, t.toggle);
            Undo.CollapseUndoOperations(group);
        }

        [MenuItem("CONTEXT/VAPFullController/Unwrap", true)]
        static bool ValidateUnwrapFullController(MenuCommand command) => CanUnwrap(command.context);

        // Replaces the wrapper with the plain VRCFury Full Controller. Start values and removed items set on
        // the wrapper live only on it, so they are lost.
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
            Release(f, f.wrapped);
            Undo.CollapseUndoOperations(group);
        }

        static void Release(MonoBehaviour wrapper, Component vf)
        {
            Released.Add(vf);
            Set(vf, false);
            Undo.DestroyObjectImmediate(wrapper);
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }
    }
}
#endif
