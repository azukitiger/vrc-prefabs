#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3A.Editor;

namespace VAP.Editor
{
    [CustomEditor(typeof(VAPParameterBehaviors))]
    [CanEditMultipleObjects]
    public class VAPParameterBehaviorsEditor : UnityEditor.Editor
    {
        static readonly GUIContent ListLabel = new GUIContent("Parameter Behaviors",
            "Added to the avatar descriptor's face tracking Parameter Behaviors at build time, after the ones already there.");
        static readonly GUIContent TrackingListLabel = new GUIContent("Tracking Key Limits",
            "Each entry limits the blendshapes a Blendshapes Set uses for a tracking key or group of keys. At build time it " +
            "becomes one Limit Blendshape behavior per distinct blendshape, all using the same inputs or expression.");
        static readonly GUIContent CopyAll = new GUIContent("Copy All", "Copy every behavior here, to paste into the avatar descriptor or another component. Tracking Key entries are not included.");
        static readonly GUIContent Paste = new GUIContent("Paste", "Add behaviors copied from the avatar descriptor (Copy All) or another component.");

        const int TabAnimator = 0, TabTrackingKey = 1, TabBlendshape = 2, TabBlendSet = 3;
        static readonly string[] TabNames = { "Animator", "Tracking Key", "Blendshape", "Blend Set" };
        static readonly string[] TabTips =
        {
            "Forward to Animator: sends a value to an animator parameter.",
            "Limits the blendshapes a Blendshapes Set uses for a tracking key or group (LipFunnel, LipFunnelUpper, ...).",
            "Apply to Blendshape, Limit Blendshape (Max) and Limit Blendshape (Min).",
            "Apply Blend Set Weight: drives a Blendshapes Set's weight.",
        };
        const string TabKey = "VAP.ParameterBehaviors.Tab";
        static int Tab
        {
            get => Mathf.Clamp(SessionState.GetInt(TabKey, 0), 0, TabNames.Length - 1);
            set => SessionState.SetInt(TabKey, value);
        }

        // VRChat's target modes, in the order of its popup.
        const int ModeForward = 0, ModeBlendSet = 1, ModeApply = 2, ModeLimitMax = 3, ModeLimitMin = 4;
        static readonly int[] BlendshapeModes = { ModeApply, ModeLimitMax, ModeLimitMin };
        static readonly GUIContent[] BlendshapeModeNames =
            { new GUIContent("Apply to Blendshape"), new GUIContent("Limit Blendshape (Max)"), new GUIContent("Limit Blendshape (Min)") };
        static readonly GUIContent[] LimitModeNames =
            { new GUIContent("Limit Blendshape (Max)"), new GUIContent("Limit Blendshape (Min)") };

        static int TabOfMode(int mode) => mode == ModeForward ? TabAnimator : mode == ModeBlendSet ? TabBlendSet : TabBlendshape;
        static int ModeOf(SerializedProperty element) => element.FindPropertyRelative("targetMode").intValue;

        ReorderableList reorderable, trackingList;
        readonly List<int> shown = new List<int>();   // array indices shown in the current tab
        int[] shownSnapshot = new int[0];

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new IMGUIContainer(DrawFields));
            VAPInspectorHeader.Attach(root, "Face Tracking Parameter Behaviors");
            return root;
        }

        void DrawFields()
        {
            if (target == null)
                return;
            serializedObject.Update();

            var list = serializedObject.FindProperty("parameterBehaviors");
            var keyList = serializedObject.FindProperty("trackingKeyBehaviors");
            bool locked = targets.Any(VAPPrefabLock.IsFromPrefab);
            using (new EditorGUI.DisabledScope(targets.Length > 1))
            using (new EditorGUILayout.HorizontalScope())
            {
                // The prefab note shares this row, so showing it never moves anything.
                VAPPrefabLock.DrawInline(targets);
                if (GUILayout.Button(CopyAll, EditorStyles.miniButtonLeft, GUILayout.Width(70)))
                    EditorGUIUtility.systemCopyBuffer = FaceTrackingClipboard.SerializeParameterBehaviors(
                        Enumerable.Range(0, ((VAPParameterBehaviors)target).parameterBehaviors.Count).Select(BuildCopy).ToList());
                using (new EditorGUI.DisabledScope(locked))
                    if (GUILayout.Button(Paste, EditorStyles.miniButtonRight, GUILayout.Width(70)))
                        PasteBehaviors(list);
            }

            if (targets.Length > 1)
            {
                EditorGUILayout.HelpBox("Select one component to edit its Parameter Behaviors.", MessageType.None);
            }
            else
            {
                using (new EditorGUI.DisabledScope(locked))
                    DrawSharedSet();
                DrawTabs(list, keyList);
                using var disabled = new EditorGUI.DisabledScope(locked);
                if (Tab == TabTrackingKey)
                {
                    GetTrackingList(keyList).DoLayoutList();
                }
                else
                {
                    shown.Clear();
                    for (int i = 0; i < list.arraySize; i++)
                        if (TabOfMode(ModeOf(list.GetArrayElementAtIndex(i))) == Tab)
                            shown.Add(i);
                    shownSnapshot = shown.ToArray();
                    DrawTabNote(list);
                    GetList(list).DoLayoutList();
                }
            }
            serializedObject.ApplyModifiedProperties();
        }

        void DrawTabs(SerializedProperty list, SerializedProperty keyList)
        {
            var counts = new int[TabNames.Length];
            for (int i = 0; i < list.arraySize; i++)
                counts[TabOfMode(ModeOf(list.GetArrayElementAtIndex(i)))]++;
            counts[TabTrackingKey] = keyList?.arraySize ?? 0;
            var labels = new GUIContent[TabNames.Length];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = new GUIContent($"{TabNames[i]} ({counts[i]})", TabTips[i]);
            int picked = GUILayout.Toolbar(Tab, labels, EditorStyles.miniButton);
            if (picked != Tab)
            {
                Tab = picked;
                if (reorderable != null)
                    reorderable.index = -1;
                if (trackingList != null)
                    trackingList.index = -1;
            }
        }

        // One list shows the entries of the open tab; its rows map to indices in the real array.
        ReorderableList GetList(SerializedProperty list)
        {
            if (reorderable != null && reorderable.serializedProperty == null && listOwner == serializedObject)
            {
                listArray = list;
                return reorderable;
            }
            listOwner = serializedObject;
            listArray = list;
            reorderable = new ReorderableList(shown, typeof(int), true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, ListLabels[Tab] ?? ListLabel),
                elementHeightCallback = i =>
                {
                    if (i >= shownSnapshot.Length)
                        return EditorGUIUtility.singleLineHeight;
                    var element = listArray.GetArrayElementAtIndex(shownSnapshot[i]);
                    element.isExpanded = true;   // height must match the open entry drawn
                    return ElementHeight(element, 0f) + 4f;
                },
                drawElementCallback = (rect, i, active, focused) =>
                {
                    if (i >= shownSnapshot.Length)
                        return;
                    rect.y += 2f;
                    rect.height -= 4f;
                    if (ContextMenu(rect, reorderable, i, listArray, shownSnapshot[i], true))
                        return;
                    DrawBehavior(rect, listArray.GetArrayElementAtIndex(shownSnapshot[i]), listArray);
                },
                onAddCallback = l =>
                {
                    listArray.arraySize++;
                    var element = listArray.GetArrayElementAtIndex(listArray.arraySize - 1);
                    element.boxedValue = new VRCAvatarDescriptor.FaceTrackingParameterBehavior();
                    // A new entry is the type of the open tab.
                    element.FindPropertyRelative("targetMode").intValue =
                        Tab == TabAnimator ? ModeForward : Tab == TabBlendSet ? ModeBlendSet : ModeApply;
                    element.isExpanded = true;
                    listArray.serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                },
                onRemoveCallback = l =>
                {
                    if (l.index < 0 || l.index >= shownSnapshot.Length)
                        return;
                    listArray.DeleteArrayElementAtIndex(shownSnapshot[l.index]);
                    l.index = -1;
                    listArray.serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                },
                onReorderCallbackWithDetails = (l, from, to) =>
                {
                    if (from < 0 || to < 0 || from >= shownSnapshot.Length || to >= shownSnapshot.Length)
                        return;
                    listArray.MoveArrayElement(shownSnapshot[from], shownSnapshot[to]);
                    listArray.serializedObject.ApplyModifiedProperties();
                },
            };
            return reorderable;
        }

        SerializedObject listOwner;
        SerializedProperty listArray;

        void DrawBehavior(Rect rect, SerializedProperty element, SerializedProperty array)
        {
            float headerWidth = rect.width + 25f;
            var g = HeaderGeometry(rect, headerWidth, 25f);
            int tab = TabOfMode(ModeOf(element));
            float stripEnd = g.targetXMax;
            if (!element.isExpanded)
                element.isExpanded = true;   // entries are always open; no fold arrow

            DrawSdk(rect, element, rect.width, headerWidth, g.row.x, stripEnd, 0f, null, array, () => DrawBehaviorStrip(g, element, tab, stripEnd));
        }

        // The build copy of one behavior (shared set applied), for Copy and Copy All.
        VRCAvatarDescriptor.FaceTrackingParameterBehavior BuildCopy(int index)
        {
            var c = (VAPParameterBehaviors)target;
            return ParameterBehaviorsBuild.WithSharedSet(ParameterBehaviorsBuild.Copy(c.parameterBehaviors[index]), c);
        }

        void SearchButton(Rect search, SerializedProperty nameProp)
        {
            if (!GUI.Button(search, SearchContent, SearchStyle))
                return;
            var so = nameProp.serializedObject;
            string path = nameProp.propertyPath;
            new BlendshapeSearch(AvatarBlendshapes(), picked =>
            {
                so.Update();
                so.FindProperty(path).stringValue = picked;
                so.ApplyModifiedProperties();
            }).Show(search);
        }

        static readonly GUIContent SharedSetLabel = new GUIContent("Blendshapes Set",
            "Used by every Tracking Key and Blendshape behavior here. If empty, each behavior's own set is used.");
        static readonly GUIContent PerBehaviorLabel = new GUIContent("Per Behavior",
            "Give each behavior its own Blendshapes Set instead of this one.");

        void DrawSharedSet()
        {
            var setProp = serializedObject.FindProperty("blendshapesSet");
            var perProp = serializedObject.FindProperty("perBehaviorSet");
            var row = EditorGUILayout.GetControlRect();
            const float toggleWidth = 95f;
            var labelRect = new Rect(row.x, row.y, EditorGUIUtility.labelWidth, row.height);
            var toggleRect = new Rect(row.xMax - toggleWidth, row.y, toggleWidth, row.height);
            var field = new Rect(labelRect.xMax, row.y, toggleRect.x - labelRect.xMax - 6f, row.height);
            EditorGUI.LabelField(labelRect, SharedSetLabel);
            using (new EditorGUI.DisabledScope(perProp.boolValue))
            {
                DrawSetField(field, setProp);
                if (!perProp.boolValue && !string.IsNullOrEmpty(setProp.stringValue) && SetProblem(setProp.stringValue) is string problem)
                    WarningIcon(field, problem);
            }
            EditorGUI.BeginChangeCheck();
            bool per = EditorGUI.ToggleLeft(toggleRect, PerBehaviorLabel, perProp.boolValue);
            if (EditorGUI.EndChangeCheck())
                perProp.boolValue = per;
            EditorGUILayout.Space(2f);
        }

        bool PerBehavior => serializedObject.FindProperty("perBehaviorSet").boolValue;

        // The shared set name when it applies, otherwise null (each behavior uses its own).
        string SharedSet
        {
            get
            {
                if (serializedObject.FindProperty("perBehaviorSet").boolValue)
                    return null;
                string name = serializedObject.FindProperty("blendshapesSet").stringValue;
                return string.IsNullOrEmpty(name) ? null : name;
            }
        }

        static GUIContent warnIcon;

        static void WarningIcon(Rect field, string tooltip)
        {
            warnIcon ??= new GUIContent(EditorGUIUtility.IconContent("console.warnicon.sml").image);
            warnIcon.tooltip = tooltip;
            GUI.Label(new Rect(field.xMax - 18f, field.y + (field.height - 16f) / 2f, 16f, 16f), warnIcon, GUIStyle.none);
        }

        string BlendshapeProblem(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "No blendshape selected: this behavior has no effect without one.";
            var shapes = FaceTrackingBlendshapes();
            if (shapes != null && !shapes.Contains(name))
                return "Not found on any Face Tracking Mesh. It still applies at runtime if the blendshape is added (e.g. by a build-time tool).";
            return null;
        }

        string SetProblem(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "No Blendshapes Set selected.";
            return SetSources().ContainsKey(name) ? null : "No Blendshapes Set with this name on this avatar.";
        }

        void DrawTabNote(SerializedProperty list)
        {
            bool any = false;
            foreach (int i in shown)
            {
                var e = list.GetArrayElementAtIndex(i);
                any |= Tab == TabBlendshape ? BlendshapeProblem(e.FindPropertyRelative("targetBlendshapeName").stringValue) != null
                     : Tab == TabBlendSet && SetProblem(e.FindPropertyRelative("targetBlendSetName").stringValue) != null;
                if (any)
                    break;
            }
            if (any)
                EditorGUILayout.HelpBox("Some entries have a problem. Hover the warning icon in the field for details.", MessageType.Warning);
        }

        HashSet<string> ftShapes;
        double ftShapesTime = -10;

        HashSet<string> FaceTrackingBlendshapes()
        {
            if (EditorApplication.timeSinceStartup - ftShapesTime < 1.0)
                return ftShapes;
            ftShapesTime = EditorApplication.timeSinceStartup;
            ftShapes = null;
            var descriptor = ((Component)target).GetComponentInParent<VRCAvatarDescriptor>(true);
            if (descriptor != null)
                ftShapes = ParameterBehaviorsBuild.FaceTrackingBlendshapes(descriptor);
            return ftShapes;
        }

        static readonly GUIContent DeleteContent = new GUIContent("Delete");
        static readonly GUIContent CopyContent = new GUIContent("Copy Behavior");

        // Right-click an item: select it, then Copy (VRChat behaviors) or Delete.
        bool ContextMenu(Rect rect, ReorderableList list, int listIndex, SerializedProperty array, int arrayIndex, bool canCopy)
        {
            var e = Event.current;
            bool right = e.type == EventType.ContextClick || (e.type == EventType.MouseDown && e.button == 1);
            if (!right || !rect.Contains(e.mousePosition))
                return false;
            list.index = listIndex;
            var so = array.serializedObject;
            string path = array.propertyPath;
            var menu = new GenericMenu();
            if (canCopy)
            {
                serializedObject.ApplyModifiedProperties();
                var copies = new List<VRCAvatarDescriptor.FaceTrackingParameterBehavior> { BuildCopy(arrayIndex) };
                menu.AddItem(CopyContent, false, () => EditorGUIUtility.systemCopyBuffer =
                    FaceTrackingClipboard.SerializeParameterBehaviors(copies));
            }
            menu.AddItem(DeleteContent, false, () =>
            {
                so.Update();
                var a = so.FindProperty(path);
                if (arrayIndex < a.arraySize)
                    a.DeleteArrayElementAtIndex(arrayIndex);
                so.ApplyModifiedProperties();
                list.index = -1;
            });
            menu.ShowAsContext();
            e.Use();
            return true;
        }

        static readonly GUIContent ParameterLabel = new GUIContent("Parameter", "Animator parameter that receives the value.");
        static readonly GUIContent BlendSetLabel = new GUIContent("Blendshapes Set", "Blendshapes Set whose weight this drives.");
        static readonly GUIContent[] ListLabels =
        {
            new GUIContent("Animator Parameters", "Forward to Animator behaviors, added to the avatar descriptor at build time."),
            null,
            new GUIContent("Blendshape Behaviors", "Apply to Blendshape and Limit Blendshape behaviors, added to the avatar descriptor at build time."),
            new GUIContent("Blend Set Behaviors", "Apply Blend Set Weight behaviors, added to the avatar descriptor at build time."),
        };

        void DrawBehaviorStrip(Header g, SerializedProperty element, int tab, float stripEnd)
        {
            // Label and field line up with the Expression row below.
            float itemX = g.row.x - EditorStyles.helpBox.padding.left;
            float fieldX = itemX + ItemLabelWidth;
            if (tab == TabAnimator || tab == TabBlendSet)
                EditorGUI.LabelField(new Rect(itemX + 15f, g.mode.y, fieldX - itemX - 15f, Line),
                    tab == TabAnimator ? ParameterLabel : BlendSetLabel);
            if (tab == TabAnimator)
            {
                var name = element.FindPropertyRelative("outputParameterName");
                var r = new Rect(fieldX, g.mode.y, stripEnd - fieldX, Line);
                EditorGUI.BeginChangeCheck();
                string value = EditorGUI.TextField(r, name.stringValue);
                if (EditorGUI.EndChangeCheck())
                {
                    value = value.Replace(" ", "_");
                    name.stringValue = value.Length > 64 ? value.Substring(0, 64) : value;
                }
            }
            else if (tab == TabBlendSet)
            {
                var r = new Rect(fieldX, g.mode.y, stripEnd - fieldX, Line);
                var setName = element.FindPropertyRelative("targetBlendSetName");
                DrawSetField(r, setName);
                if (SetProblem(setName.stringValue) is string problem)
                    WarningIcon(r, problem);
            }
            else
            {
                var mode = element.FindPropertyRelative("targetMode");
                int at = System.Array.IndexOf(BlendshapeModes, mode.intValue);
                var r = new Rect(g.row.x, g.mode.y, g.mode.xMax - g.row.x, Line);
                int next = EditorGUI.Popup(r, Mathf.Max(0, at), BlendshapeModeNames);
                if (next != at)
                    mode.intValue = BlendshapeModes[next];

                // Target blendshape: typed, with a search button beside it.
                const float searchWidth = 22f;
                var nameProp = element.FindPropertyRelative("targetBlendshapeName");
                var field = new Rect(g.mode.xMax + 4f, g.mode.y, stripEnd - g.mode.xMax - 6f - searchWidth, Line);
                DrawSetField(field, nameProp);
                if (BlendshapeProblem(nameProp.stringValue) is string problem)
                    WarningIcon(field, problem);
                SearchButton(new Rect(field.xMax + 2f, g.mode.y, searchWidth, Line), nameProp);
            }
        }

        static GUIContent searchContent;
        static GUIContent SearchContent => searchContent ??= new GUIContent(
            EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ? "d_Search Icon" : "Search Icon").image,
            "Search the blendshapes on this avatar's meshes.");
        static GUIStyle searchStyle;
        // Little padding, so the icon is drawn at full size.
        static GUIStyle SearchStyle => searchStyle ??= new GUIStyle(EditorStyles.miniButton)
        {
            padding = new RectOffset(3, 3, 1, 1),
            imagePosition = ImagePosition.ImageOnly,
        };

        // Blendshape names per mesh on this avatar (under its VRC Avatar Descriptor only).
        List<(string mesh, string[] names)> AvatarBlendshapes()
        {
            var result = new List<(string, string[])>();
            var descriptor = ((Component)target).GetComponentInParent<VRCAvatarDescriptor>(true);
            if (descriptor == null)
                return result;
            foreach (var smr in descriptor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = smr.sharedMesh;
                if (mesh == null || mesh.blendShapeCount == 0)
                    continue;
                var names = new string[mesh.blendShapeCount];
                for (int i = 0; i < names.Length; i++)
                    names[i] = mesh.GetBlendShapeName(i);
                result.Add((smr.name, names));
            }
            return result;
        }

        class BlendshapeSearch : UnityEditor.IMGUI.Controls.AdvancedDropdown
        {
            readonly List<(string mesh, string[] names)> meshes;
            readonly System.Action<string> onPick;
            readonly Dictionary<int, string> ids = new Dictionary<int, string>();

            public BlendshapeSearch(List<(string mesh, string[] names)> meshes, System.Action<string> onPick)
                : base(new UnityEditor.IMGUI.Controls.AdvancedDropdownState())
            {
                this.meshes = meshes;
                this.onPick = onPick;
                minimumSize = new Vector2(320f, 400f);
            }

            protected override UnityEditor.IMGUI.Controls.AdvancedDropdownItem BuildRoot()
            {
                var root = new UnityEditor.IMGUI.Controls.AdvancedDropdownItem("Blendshape");
                int id = 1;
                if (meshes.Count == 0)
                    root.AddChild(new UnityEditor.IMGUI.Controls.AdvancedDropdownItem("No blendshapes on this avatar") { id = id++, enabled = false });
                foreach (var (mesh, names) in meshes)
                {
                    // A single mesh lists its blendshapes directly.
                    var parent = meshes.Count == 1 ? root : new UnityEditor.IMGUI.Controls.AdvancedDropdownItem(mesh) { id = id++ };
                    if (parent != root)
                        root.AddChild(parent);
                    foreach (var name in names)
                    {
                        var item = new UnityEditor.IMGUI.Controls.AdvancedDropdownItem(name) { id = id++ };
                        ids[item.id] = name;
                        parent.AddChild(item);
                    }
                }
                return root;
            }

            protected override void ItemSelected(UnityEditor.IMGUI.Controls.AdvancedDropdownItem item)
            {
                if (ids.TryGetValue(item.id, out var name))
                    onPick(name);
            }
        }

        ReorderableList GetTrackingList(SerializedProperty list)
        {
            if (trackingList != null && trackingList.serializedProperty.serializedObject == serializedObject)
            {
                trackingList.serializedProperty = list;
                return trackingList;
            }
            trackingList = new ReorderableList(serializedObject, list, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, TrackingListLabel),
                elementHeightCallback = i =>
                {
                    var behavior = trackingList.serializedProperty.GetArrayElementAtIndex(i).FindPropertyRelative("behavior");
                    behavior.isExpanded = true;
                    var entry = trackingList.serializedProperty.GetArrayElementAtIndex(i);
                    return ElementHeight(behavior, InsertHeightFor(entry)) + 4f;
                },
                drawElementCallback = (rect, i, active, focused) =>
                {
                    rect.y += 2f;
                    rect.height -= 4f;
                    if (ContextMenu(rect, trackingList, i, trackingList.serializedProperty, i, false))
                        return;
                    DrawTrackingEntry(rect, trackingList.serializedProperty, i);
                },
                onAddCallback = l =>
                {
                    var p = l.serializedProperty;
                    p.arraySize++;
                    var element = p.GetArrayElementAtIndex(p.arraySize - 1);
                    element.boxedValue = new VAPParameterBehaviors.TrackingKeyBehavior();
                    var sets = SetSources();
                    if (SharedSet == null && sets.Count == 1)
                        element.FindPropertyRelative("blendshapesSet").stringValue = sets.Keys.First();
                    element.FindPropertyRelative("behavior").isExpanded = true;
                },
            };
            return trackingList;
        }

        // Rows under the header: Blendshapes Set (Per Behavior only) and the preview line.
        float InsertHeightFor(SerializedProperty entry) => (PerBehavior ? Step : 0f) + Step;

        static readonly GUIContent SetLabel = new GUIContent("Blendshapes Set",
            "Required. The Blendshapes Set whose blendshapes for these tracking keys are limited. " +
            "It also scales how strongly the limit applies (its weight).");
        static readonly GUIContent KeyPrompt = new GUIContent("Select Tracking Key", "A tracking key, or a group of keys such as LipFunnel or LipFunnelUpper.");

        void DrawTrackingEntry(Rect rect, SerializedProperty array, int index)
        {
            var entry = array.GetArrayElementAtIndex(index);
            var behavior = entry.FindPropertyRelative("behavior");
            var keyProp = entry.FindPropertyRelative("trackingKey");
            var modeProp = entry.FindPropertyRelative("mode");
            var setProp = entry.FindPropertyRelative("blendshapesSet");

            float sdkWidth = rect.width;
            var g = HeaderGeometry(rect, sdkWidth, 0f);
            var full = g;

            if (!behavior.isExpanded)
                behavior.isExpanded = true;   // entries are always open; no fold arrow
            float insertH = InsertHeightFor(entry);
            var insert = new Rect(rect.x, rect.y + HeaderHeight, sdkWidth, insertH);
            DrawSdk(rect, behavior, sdkWidth, sdkWidth, g.row.x, g.targetXMax, insertH, insert, null, () =>
                DrawTrackingControls(rect, array, index, g, full, insert, keyProp, modeProp, setProp));
        }

        void DrawTrackingControls(Rect rect, SerializedProperty array, int index, Header g, Header full, Rect insert,
            SerializedProperty keyProp, SerializedProperty modeProp, SerializedProperty setProp)
        {
            var modeRect = new Rect(g.row.x, g.mode.y, g.mode.xMax - g.row.x, Line);
            int limit = modeProp.intValue == ModeLimitMin ? 1 : 0;
            int pickedLimit = EditorGUI.Popup(modeRect, limit, LimitModeNames);
            if (pickedLimit != limit)
                modeProp.intValue = pickedLimit == 1 ? ModeLimitMin : ModeLimitMax;

            var keyRect = new Rect(modeRect.xMax + 4f, modeRect.y, g.targetXMax - modeRect.xMax - 4f, Line);
            var keys = TrackingKeyGroups.Resolve(keyProp.stringValue);
            var keyLabel = string.IsNullOrEmpty(keyProp.stringValue) ? KeyPrompt
                : new GUIContent(keys != null && keys.Length > 1 ? $"{keyProp.stringValue}  ({keys.Length} keys)" : keyProp.stringValue,
                    keys == null ? "Unknown tracking key." : string.Join("\n", keys.Select(k => TrackingKeyGroups.Keys[k])));
            if (EditorGUI.DropdownButton(keyRect, keyLabel, FocusType.Keyboard))
            {
                var keyPath = keyProp.propertyPath;
                var so = keyProp.serializedObject;
                TrackingKeyGroups.ShowMenu(keyRect, keyProp.stringValue, picked =>
                {
                    so.Update();
                    so.FindProperty(keyPath).stringValue = picked;
                    so.ApplyModifiedProperties();
                });
            }

            if (insert.height > 0f)
            {
                // With a shared set the row is hidden; the entry's own value is kept.
                bool per = PerBehavior;
                float previewY = insert.y + 2f;
                if (per)
                {
                    DrawSetRow(insert.x, previewY, insert.width, SetLabel, setProp);
                    previewY += Step;
                }
                var preview = Preview(keyProp.stringValue, keys, per ? setProp.stringValue : SharedSet ?? setProp.stringValue);
                float previewX = insert.x + ItemLabelWidth;
                var previewRect = new Rect(previewX, previewY, insert.xMax - previewX, Line);
                EditorGUI.LabelField(previewRect, preview, EditorStyles.miniLabel);
            }
        }

        GUIContent Preview(string key, int[] keys, string setName)
        {
            if (string.IsNullOrEmpty(key))
                return new GUIContent("Select a tracking key.");
            if (keys == null)
                return new GUIContent($"Unknown tracking key \"{key}\".");
            if (string.IsNullOrEmpty(setName))
                return new GUIContent("Select the Blendshapes Set to limit.");
            if (!SetSources().TryGetValue(setName, out var names))
                return new GUIContent($"No Blendshapes Set named \"{setName}\" on this avatar.");
            var shapes = ParameterBehaviorsBuild.BlendshapesFor(names, keys);
            if (shapes.Count == 0)
                return new GUIContent($"\"{setName}\" has no blendshapes for these keys.");
            string list = string.Join(", ", shapes);
            return new GUIContent($"Found \"{setName}\". Creates {shapes.Count} behavior{(shapes.Count == 1 ? "" : "s")}: {list}",
                "One Limit Blendshape behavior per distinct blendshape:\n" + string.Join("\n", shapes));
        }

        Dictionary<string, string[]> setCache;
        double setCacheTime;

        Dictionary<string, string[]> SetSources()
        {
            if (setCache != null && EditorApplication.timeSinceStartup - setCacheTime < 1.0)
                return setCache;
            setCacheTime = EditorApplication.timeSinceStartup;
            setCache = new Dictionary<string, string[]>();
            var descriptor = ((Component)target).GetComponentInParent<VRCAvatarDescriptor>(true);
            if (descriptor == null)
                return setCache;
            setCache = ParameterBehaviorsBuild.ReadSets(descriptor);
            foreach (var c in descriptor.GetComponentsInChildren<VAPFaceTrackingDebug>(true))
                using (var so = new SerializedObject(c))
                {
                    var set = so.FindProperty("blendshapesSet");
                    if (set != null)
                        ParameterBehaviorsBuild.AddSet(setCache, set);
                }
            return setCache;
        }

        static readonly GUIContent WeightSetLabel = new GUIContent("Blendshapes Set",
            "This Blendshapes Set's weight controls how strongly to apply. Leave empty to always fully apply.");

        static Rect DrawSetRow(float x, float y, float width, GUIContent label, SerializedProperty prop, string shared = null)
        {
            float labelWidth = ItemLabelWidth;
            EditorGUI.LabelField(new Rect(x + 15f, y, labelWidth - 15f, Line), label);
            var field = new Rect(x + labelWidth, y, (width - labelWidth) * 0.5f, Line);
            if (shared != null)
                using (new EditorGUI.DisabledScope(true))
                    EditorGUI.TextField(field, shared);
            else
                DrawSetField(field, prop);
            return field;
        }

        // A plain text field (no picker), so nothing is written unless the name is typed.
        static void DrawSetField(Rect rect, SerializedProperty prop)
        {
            EditorGUI.BeginChangeCheck();
            string value = EditorGUI.TextField(rect, prop.stringValue);
            if (EditorGUI.EndChangeCheck() && value != prop.stringValue)
                prop.stringValue = value;
        }

        static float Line => EditorGUIUtility.singleLineHeight;

        static float ItemLabelWidth
        {
            get
            {
                float longest = EditorStyles.label.CalcSize(WeightSetLabel).x;   // "Blendshapes Set"
                float unity = EditorGUIUtility.labelWidth;
                return Mathf.Max(15f + longest + 4f, 15f + longest + (unity - 15f - longest) * 0.5f);
            }
        }
        static float Step => Line + EditorGUIUtility.standardVerticalSpacing;
        static float HeaderHeight => Line + EditorStyles.helpBox.padding.vertical;

        struct Header
        {
            public Rect row;      // inside the header box
            public Rect mode;     // VRChat's target mode popup
            public float targetXMax;   // end of VRChat's target field
        }

        // Mirrors the header layout of VRChat's FaceTrackingParameterBehaviorDrawer (embedded).
        static Header HeaderGeometry(Rect rect, float width, float controls)
        {
            var header = new Rect(rect.x, rect.y, width, HeaderHeight);
            var row = EditorStyles.helpBox.padding.Remove(header);
            row.height = Line;
            float combineX = row.xMax - controls - 30f - 70f;
            var mode = new Rect(row.x + 20f, row.y, 150f, Line);
            return new Header { row = row, mode = mode, targetXMax = combineX - 4f };
        }

        void DrawSdk(Rect rect, SerializedProperty sdk, float sdkWidth, float headerWidth, float strip0, float strip1, float insertH,
            Rect? insertRect, SerializedProperty array, System.Action drawOwn)
        {
            float sdkH = EditorGUI.GetPropertyHeight(sdk, GUIContent.none, true);
            float headerH = HeaderHeight;
            bool custom = HasCustomExpression(sdk);
            float bodyEnd = custom ? sdkH - 5f - Step : sdkH;
            // Where VRChat's header box would end; past it, our box piece finishes the header.
            float rightEnd = headerWidth > rect.width ? rect.xMax - EditorStyles.helpBox.padding.right : rect.xMax;

            var stripBox = new Rect(strip0, rect.y, strip1 - strip0, headerH);
            var rightBox = new Rect(rightEnd, rect.y, rect.xMax - rightEnd, headerH);
            var e = Event.current;
            if (e.type == EventType.Repaint)
            {
                BoxPiece(stripBox, rect, headerH);   // the header box behind our controls
                BoxPiece(rightBox, rect, headerH);
            }

            // Our controls first, so they get the clicks.
            drawOwn();

            // Rows are found from the bottom: [input warning] [weight row] [inputs] 5px.
            int targetMode = sdk.FindPropertyRelative("targetMode").intValue;
            bool blendshapeMode = targetMode == ModeApply || targetMode == ModeLimitMax || targetMode == ModeLimitMin;
            int count = sdk.FindPropertyRelative("inputTrackingKeys").arraySize;
            float inputsH = custom ? Step : Step * (count + 2) + (count == 32 ? 40f : 0f);
            float inputsTop = sdkH - 5f - inputsH;

            // VRChat's input warning ("Add at least one input...") is drawn at the very bottom instead.
            string warnText = sdk.isExpanded ? InputWarning(sdk, custom, count) : null;
            float warnH = WarningHeight(warnText, sdkWidth);
            float warnTop = inputsTop - (blendshapeMode ? Step : 0f) - warnH;
            float extra = custom ? ExpressionHeight(sdk) - Line : 0f;
            float targetH = sdk.isExpanded ? Mathf.Max(0f, warnTop - headerH) : 0f;
            // With a shared Blendshapes Set (Per Behavior off) the weight row is hidden; its value is kept.
            bool hideWeight = sdk.isExpanded && blendshapeMode && !PerBehavior;
            float hiddenH = hideWeight ? Step : 0f;
            float Shift(float y) => y - (targetH > 0f && y >= warnTop ? targetH : 0f)
                                      - (warnH > 0f && y >= warnTop + warnH ? warnH : 0f)
                                      - (hideWeight && y >= inputsTop ? hiddenH : 0f);
            var warnRow = new Rect(rect.x, rect.y + insertH + sdkH - 5f + extra - targetH - hiddenH - warnH,
                sdkWidth, warnH - EditorGUIUtility.standardVerticalSpacing);
            lastWidth = sdkWidth;

            // VRChat's "Bendshapes Set (Optional)" row (blendshape modes) is replaced by ours.
            Rect weightRow = default;
            float weightTop = -1f;
            if (sdk.isExpanded && blendshapeMode)
            {
                weightTop = inputsTop - Step;
                if (!hideWeight)
                {
                    weightRow = new Rect(rect.x, rect.y + insertH + Shift(weightTop), sdkWidth, Step);
                    DrawSetRow(rect.x, weightRow.y, sdkWidth, WeightSetLabel, sdk.FindPropertyRelative("weightBlendSetName"));
                }
            }

            // Custom expressions get a word-wrapped box instead of VRChat's single line.
            Rect expressionRow = default;
            if (custom)
            {
                expressionRow = new Rect(rect.x + 15f, rect.y + insertH + Shift(bodyEnd), sdkWidth - 15f, ExpressionHeight(sdk));
                DrawExpression(expressionRow, sdk);
            }

            // Clicks on our areas never reach VRChat's hidden controls underneath.
            var owned = new List<Rect> { stripBox, rightBox };
            if (insertRect.HasValue && insertRect.Value.height > 0f)
                owned.Add(insertRect.Value);
            if (custom)
                owned.Add(expressionRow);
            if (weightTop > 0f && !hideWeight)
                owned.Add(weightRow);
            if (warnH > 0f)
                owned.Add(warnRow);
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ContextClick)
                && owned.Any(r => r.Contains(e.mousePosition)))
                e.Use();

            int size = array?.arraySize ?? -1;
            bool Changed()
            {
                if (array == null || array.arraySize == size)
                    return false;
                array.serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
                return true;
            }

            Piece(new Rect(rect.x, rect.y, strip0 - rect.x, headerH), rect.position, sdk, headerWidth, sdkH);
            if (Changed())
                return;
            Piece(new Rect(strip1, rect.y, rightEnd - strip1, headerH), rect.position, sdk, headerWidth, sdkH);
            if (Changed())
                return;

            if (sdk.isExpanded && bodyEnd > headerH)
            {
                float top = rect.y + insertH;
                var cuts = new List<(float a, float b)>();   // VRChat's own y ranges we leave out
                if (weightTop > headerH)
                    cuts.Add((weightTop, weightTop + Step));
                if (warnH > 0f)
                    cuts.Add((warnTop, warnTop + warnH));
                if (targetH > 0f)
                    cuts.Add((headerH, warnTop));
                cuts.Sort((x, y) => x.a.CompareTo(y.a));
                float from = headerH;
                cuts.Add((bodyEnd, bodyEnd));   // end of the body
                foreach (var cut in cuts)
                {
                    float to = Mathf.Min(cut.a, bodyEnd);
                    if (to > from)
                    {
                        float shift = Shift(from) - from;   // 0 above the warning, -warnH below it
                        Piece(new Rect(rect.x, top + from + shift, sdkWidth, to - from),
                            new Vector2(rect.x, top + shift), sdk, sdkWidth, sdkH);
                        if (Changed())
                            return;
                    }
                    from = Mathf.Max(from, cut.b);
                }
                if (warnH > 0f && e.type == EventType.Repaint)
                    EditorGUI.HelpBox(warnRow, warnText, MessageType.Warning);
            }
        }

        static void Piece(Rect clip, Vector2 origin, SerializedProperty sdk, float width, float height)
        {
            if (clip.width <= 0f || clip.height <= 0f)
                return;
            var e = Event.current;
            bool mouse = e.isMouse || e.type == EventType.ContextClick;
            var saved = e.mousePosition;
            if (mouse && !clip.Contains(saved))
                e.mousePosition = new Vector2(-100000f, -100000f);
            GUI.BeginClip(clip);
            EditorGUI.PropertyField(new Rect(origin.x - clip.x, origin.y - clip.y, width, height), sdk, GUIContent.none, true);
            GUI.EndClip();
            if (mouse)
                e.mousePosition = saved;
        }

        // VRChat's input warning text, from its own validator (internal, so found by reflection).
        static System.Reflection.MethodInfo inputWarningMethod;
        static bool inputWarningLooked;

        static string InputWarning(SerializedProperty sdk, bool custom, int count)
        {
            if (!inputWarningLooked)
            {
                inputWarningLooked = true;
                var type = typeof(FaceTrackingClipboard).Assembly.GetTypes()
                    .FirstOrDefault(t => t.Name == "FaceTrackingMappingValidator");
                inputWarningMethod = type?.GetMethod("GetParameterBehaviorInputWarning",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                    null, new[] { typeof(bool), typeof(string), typeof(int) }, null);
            }
            if (inputWarningMethod == null)
                return null;   // not found: VRChat's warning stays where it is
            try
            {
                return inputWarningMethod.Invoke(null, new object[]
                    { custom || sdk.FindPropertyRelative("combineMode").intValue == CustomCombineMode,
                      sdk.FindPropertyRelative("customExpression").stringValue, count }) as string;
            }
            catch
            {
                return null;
            }
        }

        // Same as VRChat's drawer.
        static float WarningHeight(string text, float width) => string.IsNullOrEmpty(text) ? 0f
            : Mathf.Max(38f, EditorStyles.helpBox.CalcHeight(new GUIContent(text), Mathf.Max(100f, width - 35f)))
              + EditorGUIUtility.standardVerticalSpacing;

        static void BoxPiece(Rect clip, Rect rect, float headerH)
        {
            if (clip.width <= 0f || clip.height <= 0f)
                return;
            GUI.BeginClip(clip);
            EditorStyles.helpBox.Draw(new Rect(rect.x - clip.x, rect.y - clip.y, rect.width, headerH), false, false, false, false);
            GUI.EndClip();
        }

        const int CustomCombineMode = 4;
        static readonly GUIContent ExpressionLabel = new GUIContent("Expression",
            "Use tracking key names, Voice (0-1) and viseme keys such as Viseme_aa (0-1).\n" +
            "Supported: + - * / abs() min() max() clamp(). Output is clamped between -1 and 1.");
        static GUIStyle wrapStyle;
        static GUIStyle WrapStyle => wrapStyle ??= new GUIStyle(EditorStyles.textArea) { wordWrap = true };
        float fieldWidth;

        static bool HasCustomExpression(SerializedProperty sdk) =>
            sdk.isExpanded && sdk.FindPropertyRelative("combineMode").intValue == CustomCombineMode;

        float ExpressionHeight(SerializedProperty sdk)
        {
            float width = fieldWidth > 50f ? fieldWidth : EditorGUIUtility.currentViewWidth - ItemLabelWidth - 90f;
            string text = sdk.FindPropertyRelative("customExpression").stringValue;
            return Mathf.Max(Line, WrapStyle.CalcHeight(new GUIContent(text), width));
        }

        float lastWidth;

        float ElementHeight(SerializedProperty sdk, float insertH)
        {
            float sdkH = EditorGUI.GetPropertyHeight(sdk, GUIContent.none, true);
            float height = sdkH + insertH;
            bool custom = HasCustomExpression(sdk);
            if (custom)
                height += ExpressionHeight(sdk) - Line;
            if (sdk.isExpanded)
            {
                // Less VRChat's target warning, which isn't drawn (see DrawSdk).
                int mode = sdk.FindPropertyRelative("targetMode").intValue;
                bool blendshapeMode = mode == ModeApply || mode == ModeLimitMax || mode == ModeLimitMin;
                int count = sdk.FindPropertyRelative("inputTrackingKeys").arraySize;
                float inputsH = custom ? Step : Step * (count + 2) + (count == 32 ? 40f : 0f);
                float width = lastWidth > 50f ? lastWidth : EditorGUIUtility.currentViewWidth - 60f;
                float warnH = WarningHeight(InputWarning(sdk, custom, count), width);
                height -= Mathf.Max(0f, sdkH - 5f - inputsH - (blendshapeMode ? Step : 0f) - warnH - HeaderHeight);
                if (blendshapeMode && !PerBehavior)
                    height -= Step;   // hidden weight row (shared Blendshapes Set)
            }
            return height;
        }

        void DrawExpression(Rect row, SerializedProperty sdk)
        {
            var labelRect = new Rect(row.x, row.y, ItemLabelWidth - 15f, Line);
            var fieldRect = new Rect(labelRect.xMax, row.y, row.xMax - labelRect.xMax, row.height);
            if (Event.current.type == EventType.Repaint && fieldRect.width > 50f)
                fieldWidth = fieldRect.width;
            var expression = sdk.FindPropertyRelative("customExpression");
            EditorGUI.LabelField(labelRect, ExpressionLabel);

            string controlName = "VAP.Expression." + expression.propertyPath;
            var e = Event.current;
            if (e.type == EventType.KeyDown && GUI.GetNameOfFocusedControl() == controlName
                && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.character == '\n' || e.character == '\r'))
            {
                e.Use();
                if (e.keyCode != KeyCode.None)
                    GUI.FocusControl(null);
            }

            EditorGUI.BeginChangeCheck();
            GUI.SetNextControlName(controlName);
            string value = EditorGUI.TextArea(fieldRect, expression.stringValue, WrapStyle);
            if (EditorGUI.EndChangeCheck())
                expression.stringValue = value.Replace("\n", " ").Replace("\r", "");
        }

        void PasteBehaviors(SerializedProperty list)
        {
            if (!FaceTrackingClipboard.TryParseParameterBehaviors(EditorGUIUtility.systemCopyBuffer, out var parsed, out string error))
            {
                EditorUtility.DisplayDialog("Paste Parameter Behaviors",
                    "The clipboard doesn't hold Parameter Behaviors.\n\n" + error, "OK");
                return;
            }
            int first = list.arraySize;
            list.arraySize += parsed.Count;
            for (int i = 0; i < parsed.Count; i++)
            {
                var element = list.GetArrayElementAtIndex(first + i);
                element.boxedValue = parsed[i];
                element.isExpanded = true;
            }
            list.isExpanded = true;
            list.serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
