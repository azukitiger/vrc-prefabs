#if UNITY_EDITOR
using UnityEditor;
using AnimatorController = UnityEditor.Animations.AnimatorController;
using UnityEngine;
using UnityEngine.UIElements;

namespace VAP.Editor
{
    [CustomEditor(typeof(VAPPlayAudioClips))]
    public class VAPPlayAudioClipsEditor : UnityEditor.Editor
    {
        static readonly GUIContent ControllerLabel = new GUIContent("Controller",
            "Animator controller whose VRC Animator Play Audio behaviours get the clips below at build time. The controller asset itself is never changed.");

        SerializedProperty controller;
        SerializedProperty entries;
        PlayAudioClipsGUI clips;

        void OnEnable()
        {
            controller = serializedObject.FindProperty(nameof(VAPPlayAudioClips.controller));
            entries = serializedObject.FindProperty(nameof(VAPPlayAudioClips.entries));
            clips = new PlayAudioClipsGUI(serializedObject, nameof(VAPPlayAudioClips.entries));
        }

        public override VisualElement CreateInspectorGUI() => VAPInspectorHeader.Create("VRC Animator Play Audio Clips", DrawFields);

        void DrawFields()
        {
            if (target == null)
                return;
            serializedObject.Update();

            EditorGUILayout.HelpBox("Play Audio Clips is now a section of the VRC Avatar Descriptor and VRCFury Full Controller components. Move these clips there.", MessageType.Warning);
            if (VAPAvatar.Descriptor((Component)target) == null)
                EditorGUILayout.HelpBox(VAPEditorGUI.NotInAvatar, MessageType.Warning);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(controller, ControllerLabel);
            if (EditorGUI.EndChangeCheck())
            {
                entries.ClearArray();
                clips.Clear();
            }

            var value = controller.objectReferenceValue;
            if (value == null)
                EditorGUILayout.HelpBox("Select an animator controller.", MessageType.Error);
            else if (!(value is AnimatorController ac))
                EditorGUILayout.HelpBox("Select an Animator Controller, not an override controller.", MessageType.Error);
            else if (clips.Found(ac).Count == 0)
                EditorGUILayout.HelpBox("This controller has no VRC Animator Play Audio.", MessageType.Warning);
            else
                clips.Draw(ac, "");
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
