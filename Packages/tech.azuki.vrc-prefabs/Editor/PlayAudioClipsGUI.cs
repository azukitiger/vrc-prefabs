#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using AnimatorController = UnityEditor.Animations.AnimatorController;
using UnityEditorInternal;
using UnityEngine;

namespace VAP.Editor
{
    internal class PlayAudioClipsGUI
    {
        readonly SerializedObject serializedObject;
        readonly string property;
        readonly Dictionary<string, ReorderableList> lists = new Dictionary<string, ReorderableList>();
        readonly Dictionary<string, ReorderableList> unstored = new Dictionary<string, ReorderableList>();
        readonly Dictionary<StateMachineBehaviour, string> names = new Dictionary<StateMachineBehaviour, string>();
        readonly Dictionary<AnimatorController, (double at, List<PlayAudioClips.Found> found)> cache =
            new Dictionary<AnimatorController, (double, List<PlayAudioClips.Found>)>();

        internal PlayAudioClipsGUI(SerializedObject serializedObject, string property)
        {
            this.serializedObject = serializedObject;
            this.property = property;
        }

        SerializedProperty Entries => serializedObject.FindProperty(property);

        internal List<PlayAudioClips.Found> Found(AnimatorController c)
        {
            if (c == null)
                return new List<PlayAudioClips.Found>();
            double now = EditorApplication.timeSinceStartup;
            if (!cache.TryGetValue(c, out var hit) || now - hit.at >= 1.0)
            {
                hit = (now, PlayAudioClips.Find(c));
                cache[c] = hit;
                names.Clear();
            }
            return hit.found;
        }

        internal void Clear()
        {
            lists.Clear();
            unstored.Clear();
            cache.Clear();
        }

        internal void Draw(AnimatorController c, string prefix)
        {
            foreach (var item in Found(c))
            {
                EditorGUILayout.Space(4f);
                GetList(item, prefix).DoLayoutList();
            }
        }

        SerializedProperty Clips(string key)
        {
            var entries = Entries;
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative(nameof(VAPPlayAudioClips.Entry.key)).stringValue == key)
                    return entry.FindPropertyRelative(nameof(VAPPlayAudioClips.Entry.clips));
            }
            return null;
        }

        void Store(string key, List<AudioClip> values)
        {
            var entries = Entries;
            entries.arraySize++;
            var added = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            added.FindPropertyRelative(nameof(VAPPlayAudioClips.Entry.key)).stringValue = key;
            var clips = added.FindPropertyRelative(nameof(VAPPlayAudioClips.Entry.clips));
            clips.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                clips.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            lists.Remove(key);
            unstored.Remove(key);
        }

        string Title(StateMachineBehaviour behaviour)
        {
            if (!names.TryGetValue(behaviour, out var name))
                names[behaviour] = name = PlayAudioClips.SourceName(behaviour);
            return name;
        }

        ReorderableList GetList(PlayAudioClips.Found item, string prefix)
        {
            string key = prefix + item.key;
            var clips = Clips(key);
            string title = Title(item.behaviour);
            string path = item.path;
            int max = item.max;
            if (clips == null)
                return Unstored(item, key, title, path, max);
            if (!lists.TryGetValue(key, out var list) || list.serializedProperty == null || list.serializedProperty.propertyPath != clips.propertyPath)
            {
                list = new ReorderableList(serializedObject, clips, true, true, true, true);
                list.elementHeightCallback = _ => VAPEditorGUI.Line + 2f;
                lists[key] = list;
            }
            list.serializedProperty = clips;
            list.onCanAddCallback = l => l.serializedProperty.arraySize < max;
            list.drawHeaderCallback = rect => Header(rect, path, title, clips.arraySize, max);
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

        ReorderableList Unstored(PlayAudioClips.Found item, string key, string title, string path, int max)
        {
            if (unstored.TryGetValue(key, out var cached))
                return cached;
            var values = PlayAudioClips.Clips(item.behaviour);
            var list = new ReorderableList(values, typeof(AudioClip), true, true, true, true);
            unstored[key] = list;
            list.elementHeightCallback = _ => VAPEditorGUI.Line + 2f;
            list.onCanAddCallback = l => values.Count < max;
            list.drawHeaderCallback = rect => Header(rect, path, title, values.Count, max);
            list.onAddCallback = l =>
            {
                values.Add(null);
                Store(key, values);
            };
            list.onRemoveCallback = l =>
            {
                if (l.index >= 0 && l.index < values.Count)
                    values.RemoveAt(l.index);
                Store(key, values);
            };
            list.onReorderCallback = l => Store(key, values);
            list.drawElementCallback = (rect, index, active, focused) =>
            {
                rect.y += 1f;
                rect.height = VAPEditorGUI.Line;
                EditorGUI.BeginChangeCheck();
                var clip = (AudioClip)EditorGUI.ObjectField(rect, values[index], typeof(AudioClip), false);
                if (EditorGUI.EndChangeCheck())
                {
                    values[index] = clip;
                    Store(key, values);
                }
            };
            return list;
        }

        static void Header(Rect rect, string path, string title, int count, int max)
        {
            string side = title + "  " + (max == int.MaxValue ? count.ToString() : count + "/" + max);
            var size = EditorStyles.miniLabel.CalcSize(new GUIContent(side));
            float width = Mathf.Min(size.x, rect.width * 0.4f);
            EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width - width - 4f, rect.height), new GUIContent(path, "Clips for the VRC Animator Play Audio on " + path + "."), EditorStyles.boldLabel);
            EditorGUI.LabelField(new Rect(rect.xMax - width, rect.y, width, rect.height), new GUIContent(side, max == int.MaxValue ? "Clips set." : "Clips set, out of what the parameter driver allows."), EditorStyles.miniLabel);
        }
    }
}
#endif
