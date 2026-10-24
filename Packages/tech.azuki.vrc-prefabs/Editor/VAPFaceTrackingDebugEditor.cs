#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3A.Editor;

namespace VAP.Editor
{
    [CustomEditor(typeof(VAPFaceTrackingDebug))]
    [CanEditMultipleObjects]
    public class VAPFaceTrackingDebugEditor : VAPBlendshapeSetEditorBase
    {
        string conflict;
        double conflictTime = -10;

        // The set name must be unique on this avatar (checked about once a second).
        protected override void DrawNotices()
        {
            if (targets.Length != 1)
                return;
            if (EditorApplication.timeSinceStartup - conflictTime > 1.0)
            {
                conflictTime = EditorApplication.timeSinceStartup;
                var c = (VAPFaceTrackingDebug)target;
                conflict = BlendshapeSetNameCheck.Conflict(c, c.GetComponentInParent<VRCAvatarDescriptor>(true));
            }
            if (conflict != null)
                EditorGUILayout.HelpBox(conflict + " Use a different name. The avatar won't build until this is fixed.", MessageType.Error);
        }
    }

    public abstract class VAPBlendshapeSetEditorBase : UnityEditor.Editor
    {
        // What differs between the components using this inspector.
        protected virtual string Title => "Face Tracking BlendShape Set";
        protected virtual string LogName => "VAP Face Tracking BlendShape Set";
        protected virtual GUIContent SetNameLabel => NameLabel;
        protected virtual GUIContent MeshesFieldLabel => MeshesLabel;
        protected virtual bool ShowWeight => true;
        protected virtual bool IncludeAvatarMeshes => true;
        protected virtual void DrawSetName(Rect rect, SerializedProperty nameProp) =>
            EditorGUI.PropertyField(rect, nameProp, SetNameLabel);
        protected virtual void DrawNotices() { }

        const float Padding = 10f;
        protected static float Line => EditorGUIUtility.singleLineHeight;
        protected static float Step => Line + EditorGUIUtility.standardVerticalSpacing;
        // VRChat's drawer starts with: name, weight, 5px, filter, 5px, buttons.
        static float HiddenTop => Step * 4f + 10f;

        static readonly GUIContent CopyContent = new GUIContent("Copy", "Copy this Blendshapes Set, to paste into the avatar descriptor or another component.");
        static readonly GUIContent PasteContent = new GUIContent("Paste", "Replace this set with one copied from the avatar descriptor or another component.");
        static readonly GUIContent NameLabel = new GUIContent("Blendshapes Set Name", "Name of the set added to the avatar descriptor. It must not match another Blendshapes Set on this avatar.");
        static readonly GUIContent WeightLabel = new GUIContent("Default Weight", "Initial weight for this set at avatar load.");
        static readonly GUIContent FormatLabel = new GUIContent("Auto-Fill Format",
            "Optional. How blendshapes are named. * is the tracking key name: *Debug finds JawOpenDebug, Debug* finds DebugJawOpen. " +
            "Empty mappings are also auto-filled this way when the avatar is built. Leave empty to turn auto-fill off.");
        static readonly GUIContent MeshesLabel = new GUIContent("Face Tracking Meshes",
            "Added to the avatar's Face Tracking Meshes at build time. Leave empty to use meshes under this object " +
            "that have blendshapes used by this set or matching the auto-fill format.");

        List<SkinnedMeshRenderer> catalogMeshes = new List<SkinnedMeshRenderer>();
        // Our own copy of VRChat's drawer, so we can reach its scroll position.
        readonly FaceTrackingBlendshapesSetDrawer drawer = new FaceTrackingBlendshapesSetDrawer();
        static readonly System.Reflection.FieldInfo StatesField = typeof(FaceTrackingBlendshapesSetDrawer).GetField(
            "_states", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new IMGUIContainer(SafeDrawFields));
            VAPInspectorHeader.Attach(root, Title);
            return root;
        }

        string lastError;

        // Shows an error in the inspector instead of a blank component if drawing fails.
        void SafeDrawFields()
        {
            try
            {
                DrawFields();
                lastError = null;
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch (System.Exception e)
            {
                if (lastError != e.Message)
                    Debug.LogException(e, target);
                lastError = e.Message;
            }
            if (lastError != null)
                EditorGUILayout.HelpBox("Couldn't draw this component:\n" + lastError, MessageType.Error);
        }

        void DrawFields()
        {
            if (target == null)
                return;
            serializedObject.Update();
            bool locked = VAPPrefabLock.Draw(targets);
            DrawNotices();

            if (targets.Length > 1)
            {
                EditorGUILayout.HelpBox("Select one component to edit its Blendshapes Set.", MessageType.None);
                return;
            }

            var component = (VAPBlendshapeSetBase)target;
            var set = serializedObject.FindProperty("blendshapesSet");
            var format = serializedObject.FindProperty("nameFormat");
            var meshes = serializedObject.FindProperty("meshes");
            FaceTrackingDebugSetFiller.EnsureArrays(set);
            UpdateCatalog(component, set);

            using (new EditorGUI.DisabledScope(locked))
                EditorGUILayout.PropertyField(meshes, MeshesFieldLabel, true);
            EditorGUILayout.Space(4f);

            float sdkHeight = drawer.GetPropertyHeight(set, GUIContent.none);
            bool formatHint = FaceTrackingDebugSetFiller.HasFormat(format.stringValue) && !format.stringValue.Contains("*");
            float hintHeight = formatHint ? 34f : 0f;
            float ourRows = ShowWeight ? HiddenTop : HiddenTop - Step;
            float boxHeight = Padding * 2 + Step + ourRows + hintHeight + (sdkHeight - HiddenTop);
            var box = GUILayoutUtility.GetRect(0f, boxHeight, GUILayout.ExpandWidth(true));
            GUI.Box(box, GUIContent.none, GUI.skin.box);
            var position = new Rect(box.x + Padding, box.y + Padding, box.width - Padding * 2, box.height - Padding * 2);

            // Header: like the descriptor's "Blendshapes Sets" row.
            var header = TakeLine(ref position);
            EditorGUI.LabelField(header, "Blendshapes Set", EditorStyles.boldLabel);
            if (GUI.Button(new Rect(header.xMax - 134f, header.y, 64f, Line), CopyContent))
                EditorGUIUtility.systemCopyBuffer = FaceTrackingClipboard.SerializeBlendshapesSets(new[] { component.blendshapesSet });
            using (new EditorGUI.DisabledScope(locked))
            {
                if (GUI.Button(new Rect(header.xMax - 64f, header.y, 64f, Line), PasteContent))
                {
                    PasteSet(set);
                    GUIUtility.ExitGUI();
                }
            }

            using (new EditorGUI.DisabledScope(locked))
            {
                var nameProp = set.FindPropertyRelative("setName");
                DrawSetName(TakeLine(ref position), nameProp);
                if (nameProp.stringValue.Length > 100)
                    nameProp.stringValue = nameProp.stringValue.Substring(0, 100);
                if (ShowWeight)
                    EditorGUI.Slider(TakeLine(ref position), set.FindPropertyRelative("setWeight"), 0f, 1f, WeightLabel);
                position.y += 5f;
                EditorGUI.PropertyField(TakeLine(ref position), format, FormatLabel);
                if (formatHint)
                {
                    string read = FaceTrackingDebugSetFiller.Normalize(format.stringValue);
                    EditorGUI.HelpBox(new Rect(position.x, position.y, position.width, hintHeight - 4f),
                        $"Use * for the tracking key name, like *Debug, Debug* or Debug*Debug. Without *, this is read as {read}.",
                        MessageType.Info);
                    position.y += hintHeight;
                }
                position.y += 5f;

                var buttons = TakeLine(ref position);
                using (new EditorGUI.DisabledScope(catalogMeshes.Count == 0 || !FaceTrackingDebugSetFiller.HasFormat(format.stringValue)))
                {
                    if (GUI.Button(new Rect(buttons.x, buttons.y, buttons.width / 2 - 2f, Line), "Auto-Fill Blendshapes"))
                        AutoFill(set, format.stringValue);
                }
                if (GUI.Button(new Rect(buttons.x + buttons.width / 2 + 2f, buttons.y, buttons.width / 2 - 2f, Line), "Clear Blendshapes")
                    && EditorUtility.DisplayDialog("Clear Blendshapes", "Clear every blendshape mapping in this set?", "Clear", "Cancel"))
                {
                    Undo.SetCurrentGroupName("Clear Blendshapes");
                    FaceTrackingDebugSetFiller.Clear(set);
                    RefreshDrawer();
                }

                // VRChat's tabs, search, multipliers and mapping rows.
                var clip = new Rect(position.x, position.y, position.width, sdkHeight - HiddenTop);
                HandleScrollWheel(clip);
                GUI.BeginClip(clip);
                drawer.OnGUI(new Rect(0f, -HiddenTop, clip.width, sdkHeight), set, GUIContent.none);
                GUI.EndClip();
            }
            if (locked)
                DrawViewControls(new Rect(position.x, position.y, position.width, sdkHeight - HiddenTop));

            serializedObject.ApplyModifiedProperties();
        }

        object ViewState()
        {
            if (StatesField == null || !(StatesField.GetValue(drawer) is System.Collections.IDictionary states))
                return null;
            return states[(target.GetInstanceID(), "$inspector")];
        }

        static readonly string[] Tabs = { "All", "Eyes", "Brows", "Nose", "Cheek", "Jaw", "Lip", "Mouth", "Tongue", "Throat", "Visemes" };

        void DrawViewControls(Rect area)
        {
            object state = ViewState();
            if (state == null)
                return;
            var type = state.GetType();
            var tabField = type.GetField("tab");
            var searchField = type.GetField("search");
            if (tabField == null || searchField == null)
                return;
            var tabs = new Rect(area.x, area.y, area.width, Line);
            var search = new Rect(area.x, area.y + Step, area.width, Line);
            tabField.SetValue(state, GUI.Toolbar(tabs, (int)tabField.GetValue(state), Tabs, EditorStyles.toolbarButton));
            searchField.SetValue(state, EditorGUI.TextField(search, (string)searchField.GetValue(state) ?? "",
                GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarTextField));

            // Working scrollbar over the greyed-out one.
            var scrollField = type.GetField("scroll");
            var heightField = type.GetField("contentHeight");
            if (scrollField == null || heightField == null)
                return;
            float content = (float)heightField.GetValue(state);
            float view = Mathf.Min(280f, content);
            if (content <= view)
                return;
            float barWidth = GUI.skin.verticalScrollbar.fixedWidth;
            var bar = new Rect(area.xMax - barWidth, area.y + Step * 4f, barWidth, view);
            var scroll = (Vector2)scrollField.GetValue(state);
            scroll.y = GUI.VerticalScrollbar(bar, scroll.y, view, 0f, content);
            scrollField.SetValue(state, scroll);
        }

        void HandleScrollWheel(Rect clip)
        {
            var e = Event.current;
            if (e.type != EventType.ScrollWheel)
                return;
            object state = ViewState();
            if (state == null)
                return;
            var type = state.GetType();
            var scrollField = type.GetField("scroll");
            var heightField = type.GetField("contentHeight");
            if (scrollField == null || heightField == null)
                return;

            float content = (float)heightField.GetValue(state);
            float view = Mathf.Min(280f, content);
            // Below the hidden rows: tabs, search, multipliers and column headers.
            var viewport = new Rect(clip.x, clip.y + Step * 4f, clip.width, view);
            if (content <= view || !viewport.Contains(e.mousePosition))
                return;
            var scroll = (Vector2)scrollField.GetValue(state);
            scroll.y = Mathf.Clamp(scroll.y + e.delta.y * 20f, 0f, content - view);
            scrollField.SetValue(state, scroll);
            e.Use();
        }

        protected static Rect TakeLine(ref Rect position)
        {
            var line = new Rect(position.x, position.y, position.width, Line);
            position.y += Step;
            return line;
        }

        // The mapping pickers list blendshapes from the avatar's face tracking meshes plus this component's meshes.
        void UpdateCatalog(VAPBlendshapeSetBase component, SerializedProperty set)
        {
            var list = new List<SkinnedMeshRenderer>();
            var descriptor = component.GetComponentInParent<VRCAvatarDescriptor>(true);
            if (IncludeAvatarMeshes && descriptor != null && descriptor.faceTrackingMeshes != null)
                list.AddRange(descriptor.faceTrackingMeshes.Where(m => m != null));
            string format = FaceTrackingDebugSetFiller.Normalize(component.nameFormat);
            foreach (var smr in FaceTrackingDebugBuild.MeshesFor(component, format))
                if (smr != null && !list.Contains(smr))
                    list.Add(smr);
            if (list.Count > 16)
                list.RemoveRange(16, list.Count - 16);

            var context = FaceTrackingEditorContext.ForProperty(set);
            if (context.SourceAvatar != null)
                context.SourceAvatar = null;
            if (!list.SequenceEqual(catalogMeshes))
            {
                catalogMeshes = list;
                context.SetMeshes(list);
            }
        }

        void AutoFill(SerializedProperty set, string format)
        {
            Undo.SetCurrentGroupName("Auto-Fill Blendshapes");
            var missing = new List<string>();
            int filled = FaceTrackingDebugSetFiller.AutoFill(set, format, FaceTrackingDebugSetFiller.BlendshapeNames(catalogMeshes), missing);
            set.serializedObject.ApplyModifiedProperties();
            RefreshDrawer();
            Debug.Log($"[{LogName}] Auto-filled {filled} keys with {FaceTrackingDebugSetFiller.Normalize(format)}." +
                      (missing.Count > 0 ? $" Not found ({missing.Count}): {string.Join(", ", missing.Take(10))}{(missing.Count > 10 ? ", ..." : "")}" : ""),
                      target);
        }

        // VRChat's drawer caches its rows; its refresh method is internal, so call it by name.
        static readonly System.Reflection.MethodInfo InvalidateBindings = typeof(FaceTrackingEditorContext).GetMethod(
            "InvalidateBindings", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

        static void RefreshDrawer() => InvalidateBindings?.Invoke(null, null);

        static void PasteSet(SerializedProperty set)
        {
            if (!FaceTrackingClipboard.TryParseBlendshapesSets(EditorGUIUtility.systemCopyBuffer, out var parsed, out string error) || parsed.Count == 0)
            {
                EditorUtility.DisplayDialog("Paste Blendshapes Set", "The clipboard doesn't hold a Blendshapes Set.\n\n" + error, "OK");
                return;
            }
            Undo.SetCurrentGroupName("Paste Blendshapes Set");
            set.boxedValue = parsed[0];
            FaceTrackingDebugSetFiller.EnsureArrays(set);
            set.serializedObject.ApplyModifiedProperties();
            RefreshDrawer();
        }
    }
}
#endif
