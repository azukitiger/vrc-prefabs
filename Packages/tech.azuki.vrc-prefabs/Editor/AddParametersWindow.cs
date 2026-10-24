#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VAP.Editor
{
    // Copies parameters from one Expression Parameters asset into another.
    internal class AddParametersWindow : EditorWindow
    {
        internal const string AssetsMenu = "Assets/VAP/Add Parameters From...";
        const string InspectorMenu = "CONTEXT/VRCExpressionParameters/Add Parameters From...";

        static readonly Color RowTint = new Color(1f, 1f, 1f, 0.035f);

        VRCExpressionParameters target;
        VRCExpressionParameters source;
        readonly HashSet<string> picked = new HashSet<string>();
        Vector2 scroll;

        [MenuItem(AssetsMenu, true)]
        internal static bool ValidateAssets() => Selection.activeObject is VRCExpressionParameters;

        [MenuItem(AssetsMenu, false, 2001)]
        internal static void FromAssets() => Open((VRCExpressionParameters)Selection.activeObject);

        [MenuItem(InspectorMenu)]
        static void FromContext(MenuCommand command) => Open((VRCExpressionParameters)command.context);

        static void Open(VRCExpressionParameters asset)
        {
            var window = CreateInstance<AddParametersWindow>();
            window.titleContent = new GUIContent("Add Parameters");
            window.target = asset;
            window.minSize = new Vector2(360f, 240f);
            window.ShowUtility();
        }

        static IEnumerable<VRCExpressionParameters.Parameter> Valid(VRCExpressionParameters asset) =>
            asset == null || asset.parameters == null
                ? Enumerable.Empty<VRCExpressionParameters.Parameter>()
                : asset.parameters.Where(p => p != null && !string.IsNullOrEmpty(p.name));

        static int Cost(VRCExpressionParameters.Parameter p) =>
            !p.networkSynced ? 0 : p.valueType == VRCExpressionParameters.ValueType.Bool ? 1 : 8;

        void OnGUI()
        {
            if (target == null)
            {
                Close();
                return;
            }
            EditorGUILayout.LabelField("Into", target.name, EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            source = (VRCExpressionParameters)EditorGUILayout.ObjectField(
                new GUIContent("From", "The parameters file to copy from. It isn't changed."), source, typeof(VRCExpressionParameters), false);
            if (EditorGUI.EndChangeCheck())
            {
                picked.Clear();
                if (source == target)
                    source = null;
            }
            if (source == null)
                return;

            var existing = new HashSet<string>(Valid(target).Select(p => p.name));
            var candidates = Valid(source).ToList();

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("All", EditorStyles.miniButtonLeft, GUILayout.Width(50f)))
                    foreach (var p in candidates.Where(p => !existing.Contains(p.name)))
                        picked.Add(p.name);
                if (GUILayout.Button("None", EditorStyles.miniButtonRight, GUILayout.Width(50f)))
                    picked.Clear();
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < candidates.Count; i++)
            {
                var p = candidates[i];
                bool there = existing.Contains(p.name);
                var rect = EditorGUILayout.GetControlRect();
                if (i % 2 == 1 && Event.current.type == EventType.Repaint)
                    EditorGUI.DrawRect(rect, RowTint);
                using (new EditorGUI.DisabledScope(there))
                {
                    var label = new GUIContent(p.name, there ? "Already in this file." : null);
                    bool on = EditorGUI.ToggleLeft(rect, label, !there && picked.Contains(p.name));
                    if (!there)
                    {
                        if (on)
                            picked.Add(p.name);
                        else
                            picked.Remove(p.name);
                    }
                }
            }
            EditorGUILayout.EndScrollView();

            var adding = candidates.Where(p => picked.Contains(p.name) && !existing.Contains(p.name)).ToList();
            int total = Valid(target).Sum(Cost) + adding.Sum(Cost);
            bool over = total > VRCExpressionParameters.MAX_PARAMETER_COST;
            var bitsStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
            if (over)
                bitsStyle.normal.textColor = new Color(1f, 0.45f, 0.4f);
            EditorGUILayout.LabelField($"{total} / {VRCExpressionParameters.MAX_PARAMETER_COST} synced bits", bitsStyle);

            using (new EditorGUI.DisabledScope(adding.Count == 0))
                if (GUILayout.Button(adding.Count > 0 ? $"Add {adding.Count}" : "Add"))
                {
                    Add(adding);
                    picked.Clear();
                }
        }

        void Add(List<VRCExpressionParameters.Parameter> adding)
        {
            Undo.RecordObject(target, "Add Parameters");
            var list = (target.parameters ?? new VRCExpressionParameters.Parameter[0]).ToList();
            foreach (var p in adding)
                list.Add(new VRCExpressionParameters.Parameter
                {
                    name = p.name,
                    valueType = p.valueType,
                    defaultValue = p.defaultValue,
                    saved = p.saved,
                    networkSynced = p.networkSynced,
                });
            target.parameters = list.ToArray();
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
        }
    }
}
#endif
