#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using ControlType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType;

namespace VAP.Editor
{
    internal class MenuSection
    {
        static readonly GUIContent NameLabel = new GUIContent("Name");
        static readonly GUIContent TypeLabel = new GUIContent("Type");
        static readonly GUIContent ParameterLabel = new GUIContent("Parameter", "Set to Value while the control is used.");
        static readonly GUIContent ValueLabel = new GUIContent("Value");
        static readonly GUIContent SubMenuLabel = new GUIContent("Sub Menu");
        static readonly GUIContent LabelsHeader = new GUIContent("Labels");
        static readonly GUIContent PickContent = new GUIContent("", "Pick one of the parameters above.");
        static readonly GUIContent ImportLabel = new GUIContent("Import Menu...",
            "Pick an Expressions Menu asset to replace these controls with its controls.");
        static readonly string[] RadialParams = { "Rotation" };
        static readonly string[] TwoAxisParams = { "Horizontal", "Vertical" };
        static readonly string[] FourAxisParams = { "Up", "Right", "Down", "Left" };
        static readonly string[] LabelNames = { "Up", "Right", "Down", "Left" };
        const string ImportPickerHint = "VAPMenuImport";
        const float LabelWidth = 80f, IconWidth = 150f;

        static float Line => EditorGUIUtility.singleLineHeight;
        static float Step => Line + EditorGUIUtility.standardVerticalSpacing;

        readonly SerializedObject componentSO;
        readonly VAPExpressions owner;
        readonly VRCExpressionsMenu subMenu;
        SerializedObject assetSO;
        ReorderableList list;
        bool locked;

        // subMenu is null for the main menu.
        internal MenuSection(SerializedObject so, VAPExpressions owner, VRCExpressionsMenu subMenu = null)
        {
            componentSO = so;
            this.owner = owner;
            this.subMenu = subMenu;
        }

        bool IsSub => !ReferenceEquals(subMenu, null);

        VRCExpressionsMenu Asset => IsSub ? subMenu : owner.menuAsset;

        bool Overriding => locked && Asset != null;

        SerializedProperty Source()
        {
            var asset = Asset;
            if (asset == null)
                return componentSO.FindProperty(nameof(VAPExpressions.controls));
            if (assetSO == null || assetSO.targetObject != asset)
                assetSO = new SerializedObject(asset);
            assetSO.Update();
            return assetSO.FindProperty(nameof(VRCExpressionsMenu.controls));
        }

        internal void Draw(bool locked)
        {
            this.locked = locked;
            var prop = Source();
            if (!locked)
                DrawImport(prop);
            GetList(prop).DoLayoutList();
            if (prop.arraySize > VRCExpressionsMenu.MAX_CONTROLS)
                EditorGUILayout.HelpBox($"VRChat menus show at most {VRCExpressionsMenu.MAX_CONTROLS} controls.", MessageType.Warning);
            if (Asset != null)
                assetSO.ApplyModifiedProperties();
        }

        void DrawImport(SerializedProperty prop)
        {
            int pickerId = GUIUtility.GetControlID((ImportPickerHint + (IsSub ? subMenu.GetInstanceID() : 0)).GetHashCode(), FocusType.Passive);
            if (GUILayout.Button(ImportLabel))
                EditorGUIUtility.ShowObjectPicker<VRCExpressionsMenu>(null, false, "", pickerId);
            var e = Event.current;
            if (e.type != EventType.ExecuteCommand || e.commandName != "ObjectSelectorClosed" ||
                EditorGUIUtility.GetObjectPickerControlID() != pickerId)
                return;
            var source = EditorGUIUtility.GetObjectPickerObject() as VRCExpressionsMenu;
            e.Use();
            if (source == null || source == Asset || source.controls == null)
                return;
            if (prop.arraySize > 0 && !EditorUtility.DisplayDialog("Import Menu",
                    $"Replace the {prop.arraySize} controls here with the {source.controls.Count} in \"{source.name}\"?", "Replace", "Cancel"))
                return;
            prop.arraySize = source.controls.Count;
            for (int i = 0; i < source.controls.Count; i++)
                prop.GetArrayElementAtIndex(i).boxedValue = VAPExpressions.Copy(source.controls[i]);
            prop.serializedObject.ApplyModifiedProperties();
        }

        ReorderableList GetList(SerializedProperty prop)
        {
            if (list == null || list.serializedProperty == null || list.serializedProperty.serializedObject != prop.serializedObject)
            {
                list = new ReorderableList(prop.serializedObject, prop, true, true, true, true)
                {
                    drawHeaderCallback = rect => EditorGUI.LabelField(rect, $"Controls ({list.count}/{VRCExpressionsMenu.MAX_CONTROLS})"),
                    elementHeightCallback = i => Height(list.serializedProperty.GetArrayElementAtIndex(i)) + 6f,
                    drawElementCallback = (rect, i, active, focused) => DrawControl(rect, list.serializedProperty.GetArrayElementAtIndex(i), i),
                    onAddCallback = l =>
                    {
                        var p = l.serializedProperty;
                        p.arraySize++;
                        p.GetArrayElementAtIndex(p.arraySize - 1).boxedValue = new VRCExpressionsMenu.Control
                        {
                            name = "New Control",
                            type = ControlType.Toggle,
                            value = 1f,
                            parameter = new VRCExpressionsMenu.Control.Parameter { name = "" },
                            subParameters = new VRCExpressionsMenu.Control.Parameter[0],
                            labels = new VRCExpressionsMenu.Control.Label[0],
                        };
                    },
                };
            }
            list.serializedProperty = prop;
            list.draggable = list.displayRemove = !locked;
            list.displayAdd = !locked && prop.arraySize < VRCExpressionsMenu.MAX_CONTROLS;
            return list;
        }

        static ControlType TypeOf(SerializedProperty control) =>
            (ControlType)control.FindPropertyRelative("type").intValue;

        static string[] SubParamNames(ControlType type) =>
            type == ControlType.RadialPuppet ? RadialParams
            : type == ControlType.TwoAxisPuppet ? TwoAxisParams
            : type == ControlType.FourAxisPuppet ? FourAxisParams
            : new string[0];

        static bool HasLabels(ControlType type) => type == ControlType.TwoAxisPuppet || type == ControlType.FourAxisPuppet;

        static float Height(SerializedProperty control)
        {
            var type = TypeOf(control);
            int rows = 3;
            if (type == ControlType.SubMenu)
                rows++;
            rows += SubParamNames(type).Length;
            if (HasLabels(type))
                rows += 1 + LabelNames.Length;
            return rows * Step;
        }

        void DrawControl(Rect rect, SerializedProperty control, int index)
        {
            if (Overriding && ExpressionsOverrides.HasControl(owner, Asset, index))
            {
                ExpressionsOverrides.DrawBar(rect);
                if (ExpressionsOverrides.ContextClick(rect))
                {
                    var menu = Asset;
                    ExpressionsOverrides.ShowRevert("Revert Menu Item", () => ExpressionsOverrides.RevertControl(owner, menu, index));
                }
            }
            else if (locked && Asset == null && VAPPrefabAsset.RevertMenu(rect, control, "Menu Item"))
                return;

            var type = TypeOf(control);
            FitArrays(control, type);
            var row = new Rect(rect.x, rect.y + 3f, rect.width, Line);

            DrawNameIcon(row, NameLabel, control.FindPropertyRelative("name"), control.FindPropertyRelative("icon"), index, -1);
            row.y += Step;

            using (new EditorGUI.DisabledScope(locked))
            {
                DrawLabelled(row, TypeLabel, control.FindPropertyRelative("type"));
                row.y += Step;

                const float valueWidth = 110f;
                DrawParameter(new Rect(row.x, row.y, row.width - valueWidth - 8f, Line), ParameterLabel,
                    control.FindPropertyRelative("parameter").FindPropertyRelative("name"));
                var valueRect = new Rect(row.xMax - valueWidth, row.y, valueWidth, Line);
                float old = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 40f;
                EditorGUI.PropertyField(valueRect, control.FindPropertyRelative("value"), ValueLabel);
                EditorGUIUtility.labelWidth = old;
                row.y += Step;

                if (type == ControlType.SubMenu)
                {
                    DrawLabelled(row, SubMenuLabel, control.FindPropertyRelative("subMenu"));
                    row.y += Step;
                }

                var names = SubParamNames(type);
                var subs = control.FindPropertyRelative("subParameters");
                for (int i = 0; i < names.Length && i < subs.arraySize; i++)
                {
                    DrawParameter(row, new GUIContent(names[i]), subs.GetArrayElementAtIndex(i).FindPropertyRelative("name"));
                    row.y += Step;
                }
            }

            if (HasLabels(type))
            {
                EditorGUI.LabelField(row, LabelsHeader, EditorStyles.miniBoldLabel);
                row.y += Step;
                var labels = control.FindPropertyRelative("labels");
                for (int i = 0; i < LabelNames.Length && i < labels.arraySize; i++)
                {
                    var label = labels.GetArrayElementAtIndex(i);
                    DrawNameIcon(row, new GUIContent(LabelNames[i]), label.FindPropertyRelative("name"), label.FindPropertyRelative("icon"), index, i);
                    row.y += Step;
                }
            }
        }

        // Name and icon: on instances they are stored as overrides when the menu is an asset.
        void DrawNameIcon(Rect row, GUIContent label, SerializedProperty name, SerializedProperty icon, int control, int labelIndex)
        {
            var nameRect = new Rect(row.x, row.y, row.width - IconWidth - 8f, Line);
            var iconRect = new Rect(row.xMax - IconWidth, row.y, IconWidth, Line);
            if (!Overriding)
            {
                DrawLabelled(nameRect, label, name);
                EditorGUI.PropertyField(iconRect, icon, GUIContent.none);
                return;
            }
            var menu = Asset;
            var over = owner.FindControl(menu, control, labelIndex);
            float old = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = LabelWidth;
            EditorGUI.BeginChangeCheck();
            string n = EditorGUI.TextField(nameRect, label, over != null && over.overrideName ? over.name : name.stringValue);
            if (EditorGUI.EndChangeCheck())
                ExpressionsOverrides.SetName(owner, menu, control, labelIndex, n, name.stringValue);
            EditorGUIUtility.labelWidth = old;
            var assetIcon = icon.objectReferenceValue as Texture2D;
            EditorGUI.BeginChangeCheck();
            var t = EditorGUI.ObjectField(iconRect, over != null && over.overrideIcon ? over.icon : assetIcon, typeof(Texture2D), false) as Texture2D;
            if (EditorGUI.EndChangeCheck())
                ExpressionsOverrides.SetIcon(owner, menu, control, labelIndex, t, assetIcon);
        }

        void FitArrays(SerializedProperty control, ControlType type)
        {
            if (locked)
                return;
            var subs = control.FindPropertyRelative("subParameters");
            int wantSubs = SubParamNames(type).Length;
            if (subs.arraySize < wantSubs)
                subs.arraySize = wantSubs;
            var labels = control.FindPropertyRelative("labels");
            if (HasLabels(type) && labels.arraySize < LabelNames.Length)
                labels.arraySize = LabelNames.Length;
        }

        static void DrawLabelled(Rect rect, GUIContent label, SerializedProperty prop)
        {
            float old = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = LabelWidth;
            EditorGUI.PropertyField(rect, prop, label);
            EditorGUIUtility.labelWidth = old;
        }

        void DrawParameter(Rect rect, GUIContent label, SerializedProperty nameProp)
        {
            const float pickWidth = 20f;
            DrawLabelled(new Rect(rect.x, rect.y, rect.width - pickWidth - 2f, rect.height), label, nameProp);
            var pick = new Rect(rect.xMax - pickWidth, rect.y, pickWidth, rect.height);
            if (!EditorGUI.DropdownButton(pick, PickContent, FocusType.Passive))
                return;
            var names = KnownParameters();
            var menu = new GenericMenu();
            if (names.Count == 0)
                menu.AddDisabledItem(new GUIContent("No parameters yet (add them above)"));
            var so = nameProp.serializedObject;
            string path = nameProp.propertyPath;
            foreach (var n in names)
            {
                string name = n;
                menu.AddItem(new GUIContent(name.Replace("/", "\u2215")), nameProp.stringValue == name, () =>
                {
                    so.Update();
                    so.FindProperty(path).stringValue = name;
                    so.ApplyModifiedProperties();
                });
            }
            menu.DropDown(pick);
        }

        List<string> KnownParameters()
        {
            IEnumerable<VRCExpressionParameters.Parameter> own = owner.parametersAsset != null
                ? owner.parametersAsset.parameters ?? new VRCExpressionParameters.Parameter[0]
                : (IEnumerable<VRCExpressionParameters.Parameter>)owner.parameters;
            var names = own.Where(x => x != null).Select(x => x.name).ToList();
            var descriptor = owner.GetComponentInParent<VRCAvatarDescriptor>(true);
            if (descriptor != null && descriptor.expressionParameters != null && descriptor.expressionParameters.parameters != null)
                names.AddRange(descriptor.expressionParameters.parameters.Where(x => x != null).Select(x => x.name));
            return names.Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
        }
    }
}
#endif
