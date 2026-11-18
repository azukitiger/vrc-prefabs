#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using AnimatorController = UnityEditor.Animations.AnimatorController;
using UnityEditorInternal;
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
        readonly Dictionary<string, ReorderableList> lists = new Dictionary<string, ReorderableList>();
        List<PlayAudioClips.Found> found = new List<PlayAudioClips.Found>();
        readonly Dictionary<StateMachineBehaviour, string> names = new Dictionary<StateMachineBehaviour, string>();
        Object foundFor;
        double foundAt = -10;

        void OnEnable()
        {
            controller = serializedObject.FindProperty(nameof(VAPPlayAudioClips.controller));
            entries = serializedObject.FindProperty(nameof(VAPPlayAudioClips.entries));
        }

        public override VisualElement CreateInspectorGUI() => VAPInspectorHeader.Create("VRC Animator Play Audio Clips", DrawFields);

        List<PlayAudioClips.Found> Found(AnimatorController c)
        {
            if (c != foundFor || EditorApplication.timeSinceStartup - foundAt >= 1.0)
            {
                foundFor = c;
                foundAt = EditorApplication.timeSinceStartup;
                found = PlayAudioClips.Find(c);
                names.Clear();
            }
            return found;
        }

        void DrawFields()
        {
            if (target == null)
                return;
            serializedObject.Update();

            if (VAPAvatar.Descriptor((Component)target) == null)
                EditorGUILayout.HelpBox(VAPEditorGUI.NotInAvatar, MessageType.Warning);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(controller, ControllerLabel);
            if (EditorGUI.EndChangeCheck())
            {
                entries.ClearArray();
                lists.Clear();
            }

            var value = controller.objectReferenceValue;
            if (value == null)
                EditorGUILayout.HelpBox("Select an animator controller.", MessageType.Error);
            else if (!(value is AnimatorController ac))
                EditorGUILayout.HelpBox("Select an Animator Controller, not an override controller.", MessageType.Error);
            else
            {
                var items = Found(ac);
                if (items.Count == 0)
                    EditorGUILayout.HelpBox("This controller has no VRC Animator Play Audio.", MessageType.Warning);
                foreach (var item in items)
                {
                    EditorGUILayout.Space(4f);
                    GetList(item).DoLayoutList();
                }
            }
            serializedObject.ApplyModifiedProperties();
        }

        SerializedProperty Clips(PlayAudioClips.Found item)
        {
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative(nameof(VAPPlayAudioClips.Entry.key)).stringValue == item.key)
                    return entry.FindPropertyRelative(nameof(VAPPlayAudioClips.Entry.clips));
            }
            entries.arraySize++;
            var added = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            added.FindPropertyRelative(nameof(VAPPlayAudioClips.Entry.key)).stringValue = item.key;
            var clips = added.FindPropertyRelative(nameof(VAPPlayAudioClips.Entry.clips));
            var current = PlayAudioClips.Clips(item.behaviour);
            clips.arraySize = current.Count;
            for (int i = 0; i < current.Count; i++)
                clips.GetArrayElementAtIndex(i).objectReferenceValue = current[i];
            return clips;
        }

        string Title(StateMachineBehaviour behaviour)
        {
            if (!names.TryGetValue(behaviour, out var name))
                names[behaviour] = name = PlayAudioClips.SourceName(behaviour);
            return name;
        }

        ReorderableList GetList(PlayAudioClips.Found item)
        {
            var clips = Clips(item);
            if (!lists.TryGetValue(item.key, out var list) || list.serializedProperty.propertyPath != clips.propertyPath)
            {
                list = new ReorderableList(serializedObject, clips, true, true, true, true);
                list.elementHeightCallback = _ => VAPEditorGUI.Line + 2f;
                lists[item.key] = list;
            }
            list.serializedProperty = clips;
            string title = Title(item.behaviour);
            string path = item.path;
            int max = item.max;
            list.onCanAddCallback = l => l.serializedProperty.arraySize < max;
            list.drawHeaderCallback = rect =>
            {
                int count = clips.arraySize;
                string side = title + "  " + (max == int.MaxValue ? count.ToString() : count + "/" + max);
                var size = EditorStyles.miniLabel.CalcSize(new GUIContent(side));
                float width = Mathf.Min(size.x, rect.width * 0.4f);
                EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width - width - 4f, rect.height), new GUIContent(path, "Clips for the VRC Animator Play Audio on " + path + "."), EditorStyles.boldLabel);
                EditorGUI.LabelField(new Rect(rect.xMax - width, rect.y, width, rect.height), new GUIContent(side, max == int.MaxValue ? "Clips set." : "Clips set, out of what the parameter driver allows."), EditorStyles.miniLabel);
            };
            list.drawElementCallback = (rect, index, active, focused) =>
            {
                rect.y += 1f;
                rect.height = VAPEditorGUI.Line;
                EditorGUI.PropertyField(rect, clips.GetArrayElementAtIndex(index), GUIContent.none);
                if (index >= max)
                    VAPEditorGUI.WarningIcon(rect, "The parameter driver only allows " + max + (max == 1 ? " clip" : " clips") + "; this one is left out.");
            };
            return list;
        }
    }
}
#endif
