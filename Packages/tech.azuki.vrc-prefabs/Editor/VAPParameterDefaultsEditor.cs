#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VAP.Editor
{
    [CustomEditor(typeof(VAPParameterDefaults))]
    public class VAPParameterDefaultsEditor : UnityEditor.Editor
    {
        static readonly GUIContent NameHeader = new GUIContent("Name", "Final parameter name. VRCFury parameters need Global Parameter set.");
        static readonly GUIContent TypeHeader = new GUIContent("Type");
        static readonly GUIContent ValueHeader = new GUIContent("Default");
        const float TypeWidth = 60f, ValueWidth = 60f, Gap = 4f;

        ReorderableList list;
        static bool locked;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new IMGUIContainer(DrawFields));
            VAPInspectorHeader.Attach(root, "VRC Parameter Defaults");
            return root;
        }

        void DrawFields()
        {
            if (target == null)
                return;
            VAPPrefabLock.Draw(targets);
            serializedObject.Update();
            locked = PrefabUtility.IsPartOfPrefabInstance(target);
            var l = GetList();
            l.draggable = l.displayAdd = l.displayRemove = !locked;
            l.DoLayoutList();
            EditorGUILayout.LabelField("Applied after VRCFury builds the avatar.", EditorStyles.miniLabel);
            serializedObject.ApplyModifiedProperties();
        }

        ReorderableList GetList()
        {
            var prop = serializedObject.FindProperty(nameof(VAPParameterDefaults.defaults));
            if (list == null || list.serializedProperty == null || list.serializedProperty.serializedObject != serializedObject)
            {
                list = new ReorderableList(serializedObject, prop, true, true, true, true)
                {
                    drawHeaderCallback = rect =>
                    {
                        float indent = locked ? 0f : 14f;
                        var row = new Rect(rect.x + indent, rect.y, rect.width - indent, rect.height);
                        Columns(row, out var n, out var t, out var v);
                        EditorGUI.LabelField(n, NameHeader);
                        EditorGUI.LabelField(t, TypeHeader);
                        EditorGUI.LabelField(v, ValueHeader);
                    },
                    elementHeight = EditorGUIUtility.singleLineHeight + 4f,
                    drawElementCallback = (rect, i, active, focused) =>
                    {
                        var element = list.serializedProperty.GetArrayElementAtIndex(i);
                        if (!VAPPrefabAsset.RevertMenu(rect, element, "Default"))
                            DrawElement(rect, element);
                    },
                    onAddCallback = l =>
                    {
                        var p = l.serializedProperty;
                        p.arraySize++;
                        p.GetArrayElementAtIndex(p.arraySize - 1).boxedValue = new VAPParameterDefaults.Entry();
                    },
                };
            }
            list.serializedProperty = prop;
            return list;
        }

        static void Columns(Rect row, out Rect name, out Rect type, out Rect value)
        {
            value = new Rect(row.xMax - ValueWidth, row.y, ValueWidth, row.height);
            type = new Rect(value.x - Gap - TypeWidth, row.y, TypeWidth, row.height);
            name = new Rect(row.x, row.y, Mathf.Max(20f, type.x - Gap - row.x), row.height);
        }

        static void DrawElement(Rect rect, SerializedProperty element)
        {
            rect.y += 2f;
            rect.height = EditorGUIUtility.singleLineHeight;
            Columns(rect, out var nameRect, out var typeRect, out var valueRect);
            var name = element.FindPropertyRelative("name");
            var type = element.FindPropertyRelative("valueType");
            var value = element.FindPropertyRelative("value");

            using (new EditorGUI.DisabledScope(locked))
            {
                EditorGUI.BeginProperty(nameRect, GUIContent.none, name);
                EditorGUI.BeginChangeCheck();
                string n = EditorGUI.TextField(nameRect, name.stringValue);
                if (EditorGUI.EndChangeCheck())
                    name.stringValue = n;
                EditorGUI.EndProperty();
                EditorGUI.PropertyField(typeRect, type, GUIContent.none);
            }

            EditorGUI.BeginProperty(valueRect, GUIContent.none, value);
            EditorGUI.BeginChangeCheck();
            float v = value.floatValue;
            switch ((VRCExpressionParameters.ValueType)type.enumValueIndex)
            {
                case VRCExpressionParameters.ValueType.Bool:
                    v = EditorGUI.Toggle(new Rect(valueRect.x + (valueRect.width - 16f) / 2f, valueRect.y, 16f, valueRect.height), v != 0f) ? 1f : 0f;
                    break;
                case VRCExpressionParameters.ValueType.Int:
                    v = Mathf.Clamp(EditorGUI.IntField(valueRect, Mathf.RoundToInt(v)), 0, 255);
                    break;
                default:
                    v = Mathf.Clamp(EditorGUI.FloatField(valueRect, v), -1f, 1f);
                    break;
            }
            if (EditorGUI.EndChangeCheck())
                value.floatValue = v;
            EditorGUI.EndProperty();
        }
    }
}
#endif
