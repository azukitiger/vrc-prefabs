#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace VAP.Editor
{
    internal static class TrackingKeyGroups
    {
        internal static readonly string[] Keys = Enumerable.Range(0, (int)VRCAvatarDescriptor.FaceTrackingKey.Max)
            .Select(i => ((VRCAvatarDescriptor.FaceTrackingKey)i).ToString()).ToArray();

        // Groups that the name rules can't find: the mouth moving sideways.
        static readonly (string group, string[] keys)[] CustomGroups =
        {
            ("MouthRight", new[] { "MouthUpperRight", "MouthLowerRight" }),
            ("MouthLeft", new[] { "MouthUpperLeft", "MouthLowerLeft" }),
        };

        static Dictionary<string, int[]> members;
        static List<(string path, string name, bool group)> menu;

        static string[] Words(string name) => Regex.Split(name, "(?<!^)(?=[A-Z])");

        internal static int[] Resolve(string name)
        {
            Build();
            return !string.IsNullOrEmpty(name) && members.TryGetValue(name, out var keys) ? keys : null;
        }

        static void Build()
        {
            if (members != null)
                return;
            members = new Dictionary<string, int[]>();
            for (int i = 0; i < Keys.Length; i++)
                members[Keys[i]] = new[] { i };

            var groups = new Dictionary<string, List<int>>();
            void Add(string group, int key)
            {
                if (!groups.TryGetValue(group, out var list))
                    groups[group] = list = new List<int>();
                list.Add(key);
            }
            for (int i = 0; i < Keys.Length; i++)
            {
                var w = Words(Keys[i]);
                int n = w.Length;
                for (int k = 2; k < n; k++)                     // LipFunnel, LipFunnelUpper
                    Add(string.Concat(w.Take(k)), i);
                string side = w[n - 1];
                if (side == "Left" || side == "Right")
                    for (int k = 2; k < n - 1; k++)             // LipFunnelLeft
                        Add(string.Concat(w.Take(k)) + side, i);
            }

            var seen = new HashSet<string>();
            foreach (var g in groups.OrderByDescending(g => g.Key.Length))
            {
                if (g.Value.Count < 2 || members.ContainsKey(g.Key))
                    continue;
                bool sideOnly = g.Value.Any(i => Keys[i] == g.Key + "Left" || Keys[i] == g.Key + "Right");
                bool longer = g.Value.Any(i => Keys[i] != g.Key + "Left" && Keys[i] != g.Key + "Right");
                if (sideOnly && longer)
                    continue;
                if (!seen.Add(string.Join(",", g.Value)))
                    continue;
                members[g.Key] = g.Value.ToArray();
            }

            foreach (var (customName, customKeys) in CustomGroups)
            {
                var indices = customKeys.Select(k => Array.IndexOf(Keys, k)).Where(i => i >= 0).ToArray();
                if (indices.Length > 1 && !members.ContainsKey(customName))
                    members[customName] = indices;
            }

            // Menu: Lip / LipFunnel / groups (broadest first), then the single keys.
            var items = members.Select(m => (name: m.Key, keys: m.Value, group: m.Value.Length > 1)).ToList();
            var bucketSize = items.GroupBy(m => Bucket(m.name)).ToDictionary(g => g.Key, g => g.Count());
            string Folder(string name)
            {
                var w = Words(name);
                return bucketSize[Bucket(name)] > 1 ? w[0] + "/" + Bucket(name) : w[0];
            }
            var folderStart = new Dictionary<string, int>();
            foreach (var m in items)
            {
                string f = Folder(m.name);
                folderStart[f] = Math.Min(folderStart.TryGetValue(f, out int s) ? s : int.MaxValue, m.keys.Min());
            }
            menu = items
                .OrderBy(m => folderStart[Folder(m.name)])
                .ThenBy(m => m.group ? 0 : 1)
                .ThenBy(m => m.group ? -m.keys.Length : 0)
                .ThenBy(m => m.keys.Min())
                .Select(m => (Folder(m.name) + "/" + (m.group ? $"{m.name}  ({m.keys.Length} keys)" : m.name), m.name, m.group))
                .ToList();
        }

        static string Bucket(string name)
        {
            foreach (var (customName, customKeys) in CustomGroups)
                if (name == customName || customKeys.Contains(name))
                    return customName;
            return string.Concat(Words(name).Take(2));
        }

        internal static void ShowMenu(Rect rect, string current, Action<string> onPick)
        {
            Build();
            new Dropdown(onPick).Show(rect);
        }

        class Dropdown : AdvancedDropdown
        {
            readonly Action<string> onPick;
            readonly Dictionary<int, string> names = new Dictionary<int, string>();

            public Dropdown(Action<string> onPick) : base(new AdvancedDropdownState())
            {
                this.onPick = onPick;
                minimumSize = new Vector2(280f, 360f);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem("Tracking Key");
                var folders = new Dictionary<string, AdvancedDropdownItem>();
                int id = 1;
                // No separators: search lists them as blank lines.
                foreach (var (path, name, group) in menu)
                {
                    var parts = path.Split('/');
                    var parent = root;
                    string folderPath = "";
                    for (int i = 0; i < parts.Length - 1; i++)
                    {
                        folderPath += "/" + parts[i];
                        if (!folders.TryGetValue(folderPath, out var folder))
                        {
                            folder = new AdvancedDropdownItem(parts[i]) { id = id++ };
                            parent.AddChild(folder);
                            folders[folderPath] = folder;
                        }
                        parent = folder;
                    }
                    var item = new AdvancedDropdownItem(parts[parts.Length - 1]) { id = id++ };
                    names[item.id] = name;
                    parent.AddChild(item);
                }
                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                if (names.TryGetValue(item.id, out var name))
                    onPick(name);
            }
        }
    }
}
#endif
