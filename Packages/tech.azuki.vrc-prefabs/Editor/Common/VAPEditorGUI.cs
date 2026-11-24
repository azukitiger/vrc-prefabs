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
        internal static string MenuName(string name) => name.Replace("/", "\u2215");

        internal const string NotInAvatar = "This VAP component is not inside an avatar, so it won't do anything.";

        static readonly GUIContent PickContent = new GUIContent("", "Pick a Blendshapes Set from this avatar.");
        static GUIContent warnIcon, foundIcon;

        internal static float Line => EditorGUIUtility.singleLineHeight;

        static GUIStyle rightMini;
        internal static GUIStyle RightMini => rightMini ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };

        static GUIStyle searchText, searchCancel;
        static GUIContent searchIcon;

        internal static string SearchBar(string controlName, string text)
        {
            var rect = EditorGUILayout.GetControlRect(false, Line + 2f);
            searchText ??= new GUIStyle(EditorStyles.textField) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(20, 20, 0, 0), fixedHeight = 0f };
            searchCancel ??= GUI.skin.FindStyle("SearchCancelButton") ?? GUI.skin.FindStyle("ToolbarSearchCancelButton") ?? GUIStyle.none;
            searchIcon ??= EditorGUIUtility.IconContent("Search Icon");
            bool hasText = !string.IsNullOrEmpty(text);
            float size = searchCancel.fixedWidth > 0f ? searchCancel.fixedWidth : 14f;
            var clear = new Rect(rect.xMax - size - 4f, rect.y + (rect.height - size) * 0.5f, size, size);
            var e = Event.current;
            if (hasText && e.type == EventType.MouseDown && e.button == 0 && clear.Contains(e.mousePosition))
            {
                GUI.FocusControl(null);
                GUI.changed = true;
                e.Use();
                return "";
            }
            if (hasText)
                EditorGUIUtility.AddCursorRect(clear, MouseCursor.Arrow);
            GUI.SetNextControlName(controlName);
            text = EditorGUI.TextField(rect, text, searchText);
            if (e.type == EventType.Repaint)
            {
                if (searchIcon?.image != null)
                    GUI.DrawTexture(new Rect(rect.x + 4f, rect.y + (rect.height - 14f) * 0.5f, 14f, 14f), searchIcon.image, ScaleMode.ScaleToFit);
                if (hasText)
                    searchCancel.Draw(clear, false, false, false, false);
            }
            return text ?? "";
        }

        internal static void EnabledBar(Rect bar, string title, SerializedProperty enabled, bool locked, bool changed)
        {
            var box = new Rect(bar.xMax - 16f, bar.y + (bar.height - Line) * 0.5f, 16f, Line);
            float width = RightMini.CalcSize(new GUIContent("Disabled")).x + 22f;
            using (new EditorGUI.DisabledScope(locked))
            {
                string tip = locked ? "Use Edit in Prefab to change it." : "";
                GUI.Label(new Rect(bar.xMax - width, bar.y, width - 20f, bar.height), new GUIContent(enabled.boolValue ? "Enabled" : "Disabled", tip), RightMini);
                var color = GUI.color;
                if (locked)
                    GUI.color = new Color(color.r, color.g, color.b, color.a * 0.5f);
                enabled.boolValue = EditorGUI.Toggle(box, new GUIContent("", tip), enabled.boolValue);
                GUI.color = color;
            }
            VRCFuryWrap.BarLabels(new Rect(bar.x, bar.y, bar.width - width - 8f, bar.height), bar.x, title, null, 0.6f, changed);
        }
        internal static float BoxHeaderHeight => Line + EditorStyles.helpBox.padding.vertical;

        internal static string OnlyOne(string what, Component other) =>
            $"This {what} does nothing because \"{other.name}\" game object already has one.";

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
                menu.AddItem(new GUIContent(VAPEditorGUI.MenuName(name)), prop.stringValue == name, () => set(name));
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
