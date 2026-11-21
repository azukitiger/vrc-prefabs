#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VAP.Editor
{
    internal static class SmoothingSpeeds
    {
        const string Path = "Packages/tech.azuki.vrc-prefabs/Editor/Smoothing/SmoothingSpeeds.json";

        [Serializable]
        internal class Entry
        {
            public string name;
            public string parameter;
            public float speed;
        }

        [Serializable]
        class File
        {
            public Entry[] speeds = Array.Empty<Entry>();
        }

        static string loadedText;
        static Entry[] loaded = Array.Empty<Entry>();

        internal static Entry[] Entries
        {
            get
            {
                string text = AssetDatabase.LoadAssetAtPath<TextAsset>(Path)?.text;
                if (text == loadedText)
                    return loaded;
                loadedText = text;
                loaded = Array.Empty<Entry>();
                if (string.IsNullOrEmpty(text))
                {
                    Debug.LogWarning($"[VAP Contact Smoothing] {Path} is missing.");
                    return loaded;
                }
                try
                {
                    loaded = (JsonUtility.FromJson<File>(text)?.speeds ?? Array.Empty<Entry>())
                        .Where(e => e != null && !string.IsNullOrEmpty(e.name) && !string.IsNullOrEmpty(e.parameter) && e.speed > 0f)
                        .ToArray();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[VAP Contact Smoothing] {Path} couldn't be read: {e.Message}");
                }
                return loaded;
            }
        }

        internal static Entry Find(string name) => Entries.FirstOrDefault(e => e.name == name);
    }
}
#endif
