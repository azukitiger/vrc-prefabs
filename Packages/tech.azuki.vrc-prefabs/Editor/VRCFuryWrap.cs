#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace VAP.Editor
{
    // Shared parts of the VAP components that wrap a VRCFury component (see IVRCFuryWrapper).
    internal static class VRCFuryWrap
    {
        internal const string VRCFuryType = "VF.Model.VRCFury";

        internal static Type FindType(string fullName) =>
            AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(fullName, false)).FirstOrDefault(t => t != null);

        internal static bool HasFeature(Component c, string featureType)
        {
            if (c == null || c.GetType().FullName != VRCFuryType)
                return false;
            using var so = new SerializedObject(c);
            var content = so.FindProperty("content");
            return content != null && content.propertyType == SerializedPropertyType.ManagedReference &&
                   content.managedReferenceFullTypename.EndsWith(featureType);
        }

        // Makes sure the wrapper has its own VRCFury component with the given feature on the same object, and hides it.
        internal static Component Ensure(MonoBehaviour owner, string featureType)
        {
            var wrapper = (IVRCFuryWrapper)owner;
            var current = wrapper.Wrapped;
            if (current != null && current.gameObject == owner.gameObject && HasFeature(current, featureType))
            {
                Hide(current);
                return current;
            }
            if (PrefabUtility.IsPartOfPrefabInstance(owner) || EditorUtility.IsPersistent(owner))
                return null;

            var vfType = FindType(VRCFuryType);
            var feature = FindType(featureType);
            if (vfType == null || feature == null)
                return null;

            var created = Undo.AddComponent(owner.gameObject, vfType);
            if (current != null && HasFeature(current, featureType))
                EditorUtility.CopySerialized(current, created);
            else
            {
                using var so = new SerializedObject(created);
                so.FindProperty("content").managedReferenceValue = Activator.CreateInstance(feature, true);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            Hide(created);
            Undo.RecordObject(owner, "Add VRCFury Component");
            wrapper.Wrapped = created;
            EditorUtility.SetDirty(owner);
            return created;
        }

        static void Hide(Component c)
        {
            if (WrappedToggles.Set(c, true))
                EditorApplication.delayCall += () => ActiveEditorTracker.sharedTracker.ForceRebuild();
        }

        // Call from the wrapper's editor OnDisable: when the wrapper was removed, the wrapped component goes too.
        internal static void ScheduleRemoval(Object owner, Component wrapped)
        {
            if (wrapped == null)
                return;
            int group = Undo.GetCurrentGroup();
            EditorApplication.delayCall += () =>
            {
                if (owner != null || wrapped == null || EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                if (WrappedToggles.Released.Remove(wrapped) ||
                    (PrefabUtility.IsPartOfPrefabInstance(wrapped) && !PrefabUtility.IsAddedComponentOverride(wrapped)))
                {
                    WrappedToggles.Set(wrapped, false);
                    ActiveEditorTracker.sharedTracker.ForceRebuild();
                    return;
                }
                Undo.DestroyObjectImmediate(wrapped);
                Undo.CollapseUndoOperations(group);
            };
        }

        internal static VisualElement MissingMessage(string what) =>
            new IMGUIContainer(() => EditorGUILayout.HelpBox(
                FindType(VRCFuryType) == null ? "VRCFury is not installed." : $"The wrapped {what} is missing. Open the prefab to fix it.",
                MessageType.Error));
    }

    // A collapsed box holding VRCFury's own inspector for the wrapped component.
    internal class WrappedSection : IDisposable
    {
        const float HeaderHeight = 26f;

        readonly Component wrapped;
        readonly Action<Rect> drawInfo;
        readonly VisualElement body = new VisualElement();
        UnityEditor.Editor inner;

        internal VisualElement Root { get; } = new VisualElement();

        // drawInfo draws what the collapsed bar shows, in the space after the arrow.
        internal WrappedSection(Component wrapped, string key, Action<Rect> drawInfo)
        {
            this.wrapped = wrapped;
            this.drawInfo = drawInfo;
            var style = Root.style;
            style.marginTop = 6f;
            style.backgroundColor = new Color(0f, 0f, 0f, 0.12f);
            var border = new Color(0f, 0f, 0f, 0.35f);
            style.borderTopColor = style.borderBottomColor = border;
            style.borderLeftColor = style.borderRightColor = border;
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1f;
            style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = 3f;
            body.style.paddingLeft = 4f;
            body.style.paddingRight = 4f;
            body.style.paddingBottom = 4f;

            key += wrapped.GetInstanceID();
            Root.Add(new IMGUIContainer(() =>
            {
                if (this.wrapped == null)
                    return;
                bool open = SessionState.GetBool(key, false);
                var header = EditorGUILayout.GetControlRect(false, HeaderHeight);
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && header.Contains(Event.current.mousePosition))
                {
                    open = !open;
                    SessionState.SetBool(key, open);
                    Show(open);
                    Event.current.Use();
                }
                if (Event.current.type != EventType.Repaint)
                    return;
                float line = EditorGUIUtility.singleLineHeight;
                EditorStyles.foldout.Draw(new Rect(header.x + 4f, header.y + (header.height - line) * 0.5f, 14f, line), false, false, open, false);
                this.drawInfo(new Rect(header.x + 20f, header.y, header.width - 24f, header.height));
            }));
            Root.Add(body);
            Show(SessionState.GetBool(key, false));
        }

        void Show(bool open)
        {
            body.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open && inner == null && wrapped != null)
            {
                inner = UnityEditor.Editor.CreateEditor(wrapped);
                body.Add(new InspectorElement(inner));
            }
        }

        public void Dispose()
        {
            if (inner != null)
                Object.DestroyImmediate(inner);
            inner = null;
        }
    }
}
#endif
