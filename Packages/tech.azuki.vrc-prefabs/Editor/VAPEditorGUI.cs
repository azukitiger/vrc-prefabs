#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace VAP.Editor
{
    internal static class VAPEditorGUI
    {
        internal const string NotInAvatar = "Put this inside an avatar (under its VRC Avatar Descriptor).";

        static readonly GUIContent PickContent = new GUIContent("", "Pick a Blendshapes Set from this avatar.");
        static GUIContent warnIcon, foundIcon;

        internal static float Line => EditorGUIUtility.singleLineHeight;
        internal static float BoxHeaderHeight => Line + EditorStyles.helpBox.padding.vertical;

        internal static string OnlyOne(string what, Component other) =>
            $"Only one {what} is allowed per avatar. \"{other.name}\" has one too.";

        internal static Action<string> Setter(SerializedProperty prop)
        {
            var so = prop.serializedObject;
            string path = prop.propertyPath;
            return value =>
            {
                so.Update();
                so.FindProperty(path).stringValue = value;
                so.ApplyModifiedProperties();
            };
        }

        internal static void StringField(Rect rect, SerializedProperty prop, GUIContent label = null)
        {
            EditorGUI.BeginChangeCheck();
            string value = label == null ? EditorGUI.TextField(rect, prop.stringValue) : EditorGUI.TextField(rect, label, prop.stringValue);
            if (EditorGUI.EndChangeCheck() && value != prop.stringValue)
                prop.stringValue = value;
        }

        internal static void StringField(SerializedProperty prop, GUIContent label) =>
            StringField(EditorGUILayout.GetControlRect(), prop, label);

        internal static void SetFieldWithPicker(SerializedProperty prop, GUIContent label, Func<IEnumerable<string>> names)
        {
            const float pickWidth = 20f;
            var rect = EditorGUILayout.GetControlRect();
            EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width - pickWidth - 2f, rect.height), prop, label);
            var pick = new Rect(rect.xMax - pickWidth, rect.y, pickWidth, rect.height);
            if (!EditorGUI.DropdownButton(pick, PickContent, FocusType.Passive))
                return;
            var menu = new GenericMenu();
            var list = names().OrderBy(n => n).ToList();
            if (list.Count == 0)
                menu.AddDisabledItem(new GUIContent("No Blendshapes Sets on this avatar"));
            var set = Setter(prop);
            foreach (var n in list)
            {
                string name = n;
                menu.AddItem(new GUIContent(name.Replace("/", "\u2215")), prop.stringValue == name, () => set(name));
            }
            menu.DropDown(pick);
        }

        static void FieldIcon(Rect field, GUIContent icon) =>
            GUI.Label(new Rect(field.xMax - 18f, field.y + (field.height - 16f) / 2f, 16f, 16f), icon, GUIStyle.none);

        internal static void WarningIcon(Rect field, string tooltip)
        {
            warnIcon ??= new GUIContent(EditorGUIUtility.IconContent("console.warnicon.sml").image);
            warnIcon.tooltip = tooltip;
            FieldIcon(field, warnIcon);
        }

        internal static void FoundIcon(Rect field, string tooltip)
        {
            foundIcon ??= new GUIContent(EditorGUIUtility.IconContent("TestPassed").image);
            foundIcon.tooltip = tooltip;
            FieldIcon(field, foundIcon);
        }

        internal static Dictionary<string, string[]> AvatarSets(VRCAvatarDescriptor descriptor)
        {
            if (descriptor == null)
                return new Dictionary<string, string[]>();
            var sets = ParameterBehaviorsBuild.ReadSets(descriptor);
            foreach (var c in descriptor.GetComponentsInChildren<VAPFaceTrackingDebug>(true))
                using (var so = new SerializedObject(c))
                {
                    var set = so.FindProperty("blendshapesSet");
                    if (set != null)
                        ParameterBehaviorsBuild.AddSet(sets, set);
                }
            return sets;
        }
    }

    internal static class Blendshapes
    {
        internal static string[] Names(Mesh mesh)
        {
            if (mesh == null)
                return Array.Empty<string>();
            var names = new string[mesh.blendShapeCount];
            for (int i = 0; i < names.Length; i++)
                names[i] = mesh.GetBlendShapeName(i);
            return names;
        }
    }

    internal class Cached<T>
    {
        readonly double interval;
        readonly Func<T> make;
        double time = -10;
        T value;

        internal Cached(double interval, Func<T> make)
        {
            this.interval = interval;
            this.make = make;
        }

        internal T Value
        {
            get
            {
                if (EditorApplication.timeSinceStartup - time >= interval)
                {
                    time = EditorApplication.timeSinceStartup;
                    value = make();
                }
                return value;
            }
        }
    }
}
#endif
