#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VAP.Editor
{
    internal class ParametersSection
    {
        static readonly GUIContent ImportLabel = new GUIContent("Import Parameters...",
            "Pick an Expression Parameters asset to replace this list with its parameters.");
        static readonly GUIContent NameHeader = new GUIContent("Name");
        static readonly GUIContent TypeHeader = new GUIContent("Type");
        static readonly GUIContent DefaultHeader = new GUIContent("Default", "Start value. The only thing that can be changed on prefab instances.");
        static readonly GUIContent SavedHeader = new GUIContent("Saved", "Kept between worlds and avatar changes.");
        static readonly GUIContent SyncedHeader = new GUIContent("Synced", "Sent to other players.");
        const float TypeWidth = 60f, DefaultWidth = 60f, ToggleWidth = 46f, Gap = 4f;
        const string ImportPickerHint = "VAPParametersImport";

        readonly SerializedObject componentSO;
        readonly VAPExpressions owner;
        SerializedObject assetSO;
        ReorderableList list;
        bool locked;

        internal ParametersSection(SerializedObject so, VAPExpressions owner)
        {
            componentSO = so;
            this.owner = owner;
        }

        VRCExpressionParameters Asset => owner.parametersAsset;

        // Overrides are kept on the component when an instance can't change the asset.
        bool Overriding => locked && Asset != null;

        SerializedProperty Source()
        {
            if (Asset == null)
                return componentSO.FindProperty(nameof(VAPExpressions.parameters));
            if (assetSO == null || assetSO.targetObject != Asset)
                assetSO = new SerializedObject(Asset);
            assetSO.Update();
            return assetSO.FindProperty(nameof(VRCExpressionParameters.parameters));
        }

        internal void Draw(bool locked)
        {
            this.locked = locked;
            var prop = Source();
            if (!locked)
                DrawImport(prop);
            var l = GetList(prop);
            l.DoLayoutList();
            DrawCost(prop, GUILayoutUtility.GetLastRect(), l.footerHeight);
            if (Asset != null)
                assetSO.ApplyModifiedProperties();
        }

        void DrawImport(SerializedProperty prop)
        {
            int pickerId = GUIUtility.GetControlID(ImportPickerHint.GetHashCode(), FocusType.Passive);
            if (GUILayout.Button(ImportLabel))
                EditorGUIUtility.ShowObjectPicker<VRCExpressionParameters>(null, false, "", pickerId);
            var e = Event.current;
            if (e.type != EventType.ExecuteCommand || e.commandName != "ObjectSelectorClosed" ||
                EditorGUIUtility.GetObjectPickerControlID() != pickerId)
                return;
            var source = EditorGUIUtility.GetObjectPickerObject() as VRCExpressionParameters;
            e.Use();
            if (source == null || source == Asset || source.parameters == null)
                return;
            if (prop.arraySize > 0 && !EditorUtility.DisplayDialog("Import Parameters",
                    $"Replace the {prop.arraySize} parameters here with the {source.parameters.Length} in \"{source.name}\"?", "Replace", "Cancel"))
                return;
            var items = source.parameters.Where(p => p != null).ToList();
            prop.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++)
                prop.GetArrayElementAtIndex(i).boxedValue = Copy(items[i]);
            prop.serializedObject.ApplyModifiedProperties();
        }

        internal static VRCExpressionParameters.Parameter Copy(VRCExpressionParameters.Parameter p) => new VRCExpressionParameters.Parameter
        {
            name = p.name,
            valueType = p.valueType,
            defaultValue = p.defaultValue,
            saved = p.saved,
            networkSynced = p.networkSynced,
        };

        ReorderableList GetList(SerializedProperty prop)
        {
            if (list == null || list.serializedProperty == null || list.serializedProperty.serializedObject != prop.serializedObject)
            {
                list = new ReorderableList(prop.serializedObject, prop, true, true, true, true)
                {
                    drawHeaderCallback = DrawHeader,
                    drawElementCallback = (rect, i, active, focused) => DrawElement(rect, list.serializedProperty.GetArrayElementAtIndex(i)),
                    elementHeight = EditorGUIUtility.singleLineHeight + 4f,
                    onAddCallback = l =>
                    {
                        var p = l.serializedProperty;
                        p.arraySize++;
                        p.GetArrayElementAtIndex(p.arraySize - 1).boxedValue = new VRCExpressionParameters.Parameter
                        {
                            name = "",
                            valueType = VRCExpressionParameters.ValueType.Bool,
                            saved = true,
                            networkSynced = true,
                        };
                    },
                };
            }
            list.serializedProperty = prop;
            list.draggable = list.displayAdd = list.displayRemove = !locked;
            return list;
        }

        static void Columns(Rect row, out Rect name, out Rect type, out Rect value, out Rect saved, out Rect synced)
        {
            synced = new Rect(row.xMax - ToggleWidth, row.y, ToggleWidth, row.height);
            saved = new Rect(synced.x - ToggleWidth, row.y, ToggleWidth, row.height);
            value = new Rect(saved.x - Gap - DefaultWidth, row.y, DefaultWidth, row.height);
            type = new Rect(value.x - Gap - TypeWidth, row.y, TypeWidth, row.height);
            name = new Rect(row.x, row.y, Mathf.Max(20f, type.x - Gap - row.x), row.height);
        }

        void DrawHeader(Rect rect)
        {
            float indent = locked ? 0f : 14f;
            var row = new Rect(rect.x + indent, rect.y, rect.width - indent, rect.height);
            Columns(row, out var name, out var type, out var value, out var saved, out var synced);
            EditorGUI.LabelField(name, NameHeader);
            EditorGUI.LabelField(type, TypeHeader);
            EditorGUI.LabelField(value, DefaultHeader);
            EditorGUI.LabelField(saved, SavedHeader);
            EditorGUI.LabelField(synced, SyncedHeader);
        }

        void DrawElement(Rect rect, SerializedProperty element)
        {
            var name = element.FindPropertyRelative("name");
            var type = element.FindPropertyRelative("valueType");
            var value = element.FindPropertyRelative("defaultValue");
            var saved = element.FindPropertyRelative("saved");
            var synced = element.FindPropertyRelative("networkSynced");
            var over = Overriding ? owner.FindDefault(name.stringValue) : null;

            if (over != null && ExpressionsOverrides.ContextClick(rect))
            {
                string n = name.stringValue;
                ExpressionsOverrides.ShowRevert("Revert Default", () => ExpressionsOverrides.RevertDefault(owner, n));
            }
            else if (locked && Asset == null && VAPPrefabAsset.RevertMenu(rect, element, "Parameter"))
                return;

            rect.y += 2f;
            rect.height = EditorGUIUtility.singleLineHeight;
            Columns(rect, out var nameRect, out var typeRect, out var valueRect, out var savedRect, out var syncedRect);

            using (new EditorGUI.DisabledScope(locked))
            {
                EditorGUI.BeginChangeCheck();
                string n = EditorGUI.TextField(nameRect, name.stringValue);
                if (EditorGUI.EndChangeCheck())
                    name.stringValue = n;
                EditorGUI.PropertyField(typeRect, type, GUIContent.none);
                CenteredToggle(savedRect, saved);
                CenteredToggle(syncedRect, synced);
            }

            var valueType = (VRCExpressionParameters.ValueType)type.enumValueIndex;
            if (Overriding)
            {
                if (over != null)
                    ExpressionsOverrides.DrawBar(rect);
                EditorGUI.BeginChangeCheck();
                float v = ValueField(valueRect, valueType, over?.value ?? value.floatValue);
                if (EditorGUI.EndChangeCheck())
                    ExpressionsOverrides.SetDefault(owner, name.stringValue, v, value.floatValue);
                return;
            }
            EditorGUI.BeginProperty(valueRect, GUIContent.none, value);
            EditorGUI.BeginChangeCheck();
            float nv = ValueField(valueRect, valueType, value.floatValue);
            if (EditorGUI.EndChangeCheck())
                value.floatValue = nv;
            EditorGUI.EndProperty();
        }

        internal static float ValueField(Rect rect, VRCExpressionParameters.ValueType type, float v)
        {
            switch (type)
            {
                case VRCExpressionParameters.ValueType.Bool:
                    return EditorGUI.Toggle(new Rect(rect.x + (rect.width - 16f) / 2f, rect.y, 16f, rect.height), v != 0f) ? 1f : 0f;
                case VRCExpressionParameters.ValueType.Int:
                    return Mathf.Clamp(EditorGUI.IntField(rect, Mathf.RoundToInt(v)), 0, 255);
                default:
                    return Mathf.Clamp(EditorGUI.FloatField(rect, v), -1f, 1f);
            }
        }

        static void CenteredToggle(Rect rect, SerializedProperty prop)
        {
            var box = new Rect(rect.x + (rect.width - 16f) / 2f, rect.y, 16f, rect.height);
            EditorGUI.BeginProperty(box, GUIContent.none, prop);
            EditorGUI.BeginChangeCheck();
            bool b = EditorGUI.Toggle(box, prop.boolValue);
            if (EditorGUI.EndChangeCheck())
                prop.boolValue = b;
            EditorGUI.EndProperty();
        }

        static GUIStyle costStyle;

        void DrawCost(SerializedProperty prop, Rect listRect, float footerHeight)
        {
            int cost = 0;
            var names = new List<string>();
            for (int i = 0; i < prop.arraySize; i++)
            {
                var e = prop.GetArrayElementAtIndex(i);
                names.Add(e.FindPropertyRelative("name").stringValue);
                if (e.FindPropertyRelative("networkSynced").boolValue)
                    cost += (VRCExpressionParameters.ValueType)e.FindPropertyRelative("valueType").enumValueIndex == VRCExpressionParameters.ValueType.Bool ? 1 : 8;
            }
            costStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
            float buttons = locked ? 4f : 64f;
            var costRect = new Rect(listRect.x, listRect.yMax - footerHeight, listRect.width - buttons, footerHeight - 2f);
            if (Event.current.type == EventType.Repaint)
                costStyle.Draw(costRect, $"{cost} synced bits", false, false, false, false);
            var dupes = names.Where(n => !string.IsNullOrEmpty(n)).GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (dupes.Count > 0)
                EditorGUILayout.HelpBox("Duplicate names: " + string.Join(", ", dupes), MessageType.Warning);
            if (names.Any(string.IsNullOrEmpty))
                EditorGUILayout.HelpBox("Some parameters have no name.", MessageType.Warning);
        }
    }
}
#endif
