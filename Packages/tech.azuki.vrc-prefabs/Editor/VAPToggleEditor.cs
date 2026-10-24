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
    [CustomEditor(typeof(VAPToggle))]
    public class VAPToggleEditor : UnityEditor.Editor
    {
        const string VRCFuryType = "VF.Model.VRCFury";
        const string ToggleType = "VF.Model.Feature.Toggle";

        UnityEditor.Editor inner;
        VAPToggle owner;
        Component wrapped;

        static Type FindType(string fullName) =>
            AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(fullName, false)).FirstOrDefault(t => t != null);

        void OnEnable()
        {
            owner = target as VAPToggle;
            if (owner == null || targets.Length > 1)
                return;
            wrapped = Ensure(owner);
            WrappedToggles.HideShownEditors();
        }

        void OnDisable()
        {
            if (inner != null)
                DestroyImmediate(inner);
            var o = owner;
            var w = wrapped;
            if (w == null)
                return;
            int group = Undo.GetCurrentGroup();
            // The wrapped component goes away with this one.
            EditorApplication.delayCall += () =>
            {
                if (o != null || w == null || EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                if (WrappedToggles.Released.Remove(w) ||
                    (PrefabUtility.IsPartOfPrefabInstance(w) && !PrefabUtility.IsAddedComponentOverride(w)))
                {
                    WrappedToggles.Set(w, false);
                    ActiveEditorTracker.sharedTracker.ForceRebuild();
                    return;
                }
                Undo.DestroyObjectImmediate(w);
                Undo.CollapseUndoOperations(group);
            };
        }

        static readonly GUIContent DefaultLabel = new GUIContent("Default",
            "Replaces the VRCFury Toggle's default when the avatar is built. Can be changed on prefab instances too.");

        void DrawDefault()
        {
            if (owner == null || wrapped == null)
                return;
            var (slider, ownOn, ownValue) = VRCFuryToggleDefaults.GetDefault(wrapped);
            bool on = owner.overrideDefault ? owner.defaultOn : ownOn;
            float value = owner.overrideDefault ? owner.defaultValue : ownValue;

            // Changed here, compared to the prefab this comes from (or to VRCFury's value when there is none).
            var source = PrefabUtility.GetCorrespondingObjectFromSource(owner);
            bool changed = source == null
                ? owner.overrideDefault
                : owner.overrideDefault != source.overrideDefault ||
                  (owner.overrideDefault && (owner.defaultOn != source.defaultOn || !Mathf.Approximately(owner.defaultValue, source.defaultValue)));

            EditorGUILayout.Space(2f);
            var rect = EditorGUILayout.GetControlRect();
            if (changed)
                ExpressionsOverrides.DrawBar(rect);
            if (changed && ExpressionsOverrides.ContextClick(rect))
            {
                if (source == null)
                    ExpressionsOverrides.ShowRevert("Revert Default", () => SetOverride(false, ownOn, ownValue));
                else
                    ExpressionsOverrides.ShowRevert("Revert Default", () => SetOverride(source.overrideDefault, source.defaultOn, source.defaultValue));
            }

            EditorGUI.BeginChangeCheck();
            if (slider)
                value = EditorGUI.Slider(rect, DefaultLabel, Mathf.Round(value * 100f), 0f, 100f) / 100f;
            else
                on = EditorGUI.Toggle(rect, DefaultLabel, on);
            if (!EditorGUI.EndChangeCheck())
                return;
            bool same = slider ? Mathf.Approximately(value, ownValue) : on == ownOn;
            SetOverride(!same, on, value);
        }

        void SetOverride(bool enabled, bool on, float value)
        {
            Undo.RecordObject(owner, enabled ? "Change Default" : "Revert Default");
            owner.overrideDefault = enabled;
            owner.defaultOn = on;
            owner.defaultValue = Mathf.Clamp01(value);
            EditorUtility.SetDirty(owner);
            PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
        }

        const float HeaderHeight = 26f, IconSize = 24f, IconPad = 4f;
        static GUIStyle pathStyle, paramStyle;

        VisualElement CreateWrappedSection()
        {
            var section = new VisualElement();
            section.style.marginTop = 6f;
            section.style.backgroundColor = new Color(0f, 0f, 0f, 0.12f);
            var border = new Color(0f, 0f, 0f, 0.35f);
            section.style.borderTopColor = section.style.borderBottomColor = border;
            section.style.borderLeftColor = section.style.borderRightColor = border;
            section.style.borderTopWidth = section.style.borderBottomWidth = section.style.borderLeftWidth = section.style.borderRightWidth = 1f;
            section.style.borderTopLeftRadius = section.style.borderTopRightRadius =
                section.style.borderBottomLeftRadius = section.style.borderBottomRightRadius = 3f;

            string key = "VAP.Toggle.Open." + wrapped.GetInstanceID();
            var body = new VisualElement();
            body.style.paddingLeft = 4f;
            body.style.paddingRight = 4f;
            body.style.paddingBottom = 4f;

            void Show(bool open)
            {
                body.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
                if (open && inner == null && wrapped != null)
                {
                    inner = CreateEditor(wrapped);
                    body.Add(new InspectorElement(inner));
                }
            }

            section.Add(new IMGUIContainer(() =>
            {
                if (wrapped == null)
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
                DrawHeader(header, open);
            }));
            section.Add(body);
            Show(SessionState.GetBool(key, false));
            return section;
        }

        void DrawHeader(Rect rect, bool open)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            pathStyle ??= new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, padding = new RectOffset(0, 0, 0, 0) };
            paramStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, clipping = TextClipping.Clip };

            using var so = new SerializedObject(wrapped);
            var content = so.FindProperty("content");
            string path = content?.FindPropertyRelative("name")?.stringValue ?? "";
            var icon = (content?.FindPropertyRelative("enableIcon")?.boolValue ?? false)
                ? content.FindPropertyRelative("icon.objRef")?.objectReferenceValue as Texture2D : null;
            string param = (content?.FindPropertyRelative("useGlobalParam")?.boolValue ?? false)
                ? content.FindPropertyRelative("globalParam")?.stringValue : null;

            float line = EditorGUIUtility.singleLineHeight;
            EditorStyles.foldout.Draw(new Rect(rect.x + 4f, rect.y + (rect.height - line) * 0.5f, 14f, line), false, false, open, false);
            float x = rect.x + 20f;
            if (icon != null)
            {
                GUI.DrawTexture(new Rect(x, rect.y + (rect.height - IconSize) * 0.5f, IconSize, IconSize), icon, ScaleMode.ScaleToFit);
                x += IconSize + IconPad;
            }
            float paramWidth = string.IsNullOrEmpty(param) ? 0f : Mathf.Min(paramStyle.CalcSize(new GUIContent(param)).x, rect.width * 0.35f);
            var pathRect = new Rect(x, rect.y, rect.xMax - x - paramWidth - 8f, rect.height);
            GUI.Label(pathRect, string.IsNullOrEmpty(path) ? "No menu path" : path, pathStyle);
            if (paramWidth > 0f)
                GUI.Label(new Rect(rect.xMax - paramWidth - 4f, rect.y, paramWidth, rect.height), param, paramStyle);
        }

        internal static bool IsWrappable(Component c)
        {
            if (c == null || c.GetType().FullName != VRCFuryType)
                return false;
            using var so = new SerializedObject(c);
            var content = so.FindProperty("content");
            return content != null && content.propertyType == SerializedPropertyType.ManagedReference &&
                   content.managedReferenceFullTypename.EndsWith(ToggleType);
        }

        // Makes sure the component has its own VRCFury Toggle on the same object, and hides it.
        static Component Ensure(VAPToggle owner)
        {
            var current = owner.toggle;
            if (current != null && current.gameObject == owner.gameObject && IsWrappable(current))
            {
                Hide(current);
                return current;
            }
            if (PrefabUtility.IsPartOfPrefabInstance(owner) || EditorUtility.IsPersistent(owner))
                return null;

            var vfType = FindType(VRCFuryType);
            var toggleType = FindType(ToggleType);
            if (vfType == null || toggleType == null)
                return null;

            var created = Undo.AddComponent(owner.gameObject, vfType);
            if (current != null && IsWrappable(current))
                EditorUtility.CopySerialized(current, created);
            else
            {
                using var so = new SerializedObject(created);
                so.FindProperty("content").managedReferenceValue = Activator.CreateInstance(toggleType, true);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            Hide(created);
            Undo.RecordObject(owner, "Add VRCFury Toggle");
            owner.toggle = created;
            EditorUtility.SetDirty(owner);
            return created;
        }

        static void Hide(Component c)
        {
            if (WrappedToggles.Set(c, true))
                EditorApplication.delayCall += () => ActiveEditorTracker.sharedTracker.ForceRebuild();
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            if (targets.Length > 1)
                root.Add(new IMGUIContainer(() => EditorGUILayout.HelpBox("Editing several of these at once isn't supported.", MessageType.Info)));
            else if (wrapped == null)
                root.Add(new IMGUIContainer(() => EditorGUILayout.HelpBox(
                    FindType(VRCFuryType) == null
                        ? "VRCFury is not installed."
                        : "The wrapped VRCFury Toggle is missing. Open the prefab to fix it.",
                    MessageType.Error)));
            else
            {
                root.Add(new IMGUIContainer(DrawDefault));
                root.Add(CreateWrappedSection());
            }
            VAPInspectorHeader.Attach(root, "VRCFury Toggle");
            return root;
        }
    }
}
#endif
