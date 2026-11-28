#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace VAP.Editor {
    [CustomEditor(typeof(VAPSkinnedMeshRenderer))]
    public class VAPSkinnedMeshRendererEditor : UnityEditor.Editor {
        const string Title = "Skinned Mesh Renderer";

        static readonly GUIContent MeshLabel = new GUIContent("Mesh");
        static readonly GUIContent RootBoneLabel = new GUIContent("Root Bone", "The bone the bounds follow.");
        static readonly GUIContent CenterLabel = new GUIContent("Center");
        static readonly GUIContent ExtentLabel = new GUIContent("Extent");
        static readonly GUIContent BoundsLabel = new GUIContent("Bounds", "Relative to the Root Bone. The mesh is hidden when this box is off screen, so it should fit the mesh in every pose.");
        static readonly GUIContent MaterialsLabel = new GUIContent("Materials");
        static readonly GUIContent BlendShapesLabel = new GUIContent("BlendShapes");
        static readonly GUIContent LightingLabel = new GUIContent("Lighting");
        static readonly GUIContent CastShadowsLabel = new GUIContent("Cast Shadows");
        static readonly GUIContent AnchorLabel = new GUIContent("Anchor Override", "Where light and reflection probes are sampled. Use the same transform on every mesh of the avatar so they are lit the same.");

        static readonly GUIContent[] ShadowNames = { new GUIContent("Off"), new GUIContent("On"), new GUIContent("Two Sided"), new GUIContent("Shadows Only") };
        static readonly int[] ShadowValues = { 0, 1, 2, 3 };

        SerializedObject rendererSO;
        ReorderableList materials;
        string search = "";
        List<int> active;
        static GUIStyle listStyle;
        Vector2 scroll;

        const float ButtonWidth = 110f;
        const float MaxListHeight = 300f;
        static readonly Color StripeColor = new Color(1f, 1f, 1f, 0.04f);
        static readonly Color SelectedColor = new Color(0.17f, 0.36f, 0.53f, 0.9f);
        const string ShapeSearchControl = "VAP.SkinnedMeshRenderer.ShapeSearch", NormalsSearchControl = "VAP.SkinnedMeshRenderer.NormalsSearch";
        string selectedShape;
        bool reveal;
        const string HeightPref = "VAP.SkinnedMeshRenderer.BlendShapesHeight";
        float dragStart, dragHeight;
        static readonly GUIContent ShowActiveLabel = new GUIContent("Show Active", "List the blendshapes above 0.");
        static readonly GUIContent BackLabel = new GUIContent("Back to Search");
        static readonly GUIContent MaterialsHint = new GUIContent("Right-click for more options");
        static GUIContent editBoundsLabel;
        static readonly UnityEditor.IMGUI.Controls.BoxBoundsHandle boundsHandle = new UnityEditor.IMGUI.Controls.BoxBoundsHandle();
        static readonly GUIContent NormalsLabel = new GUIContent("Calculate Blendshape Normals", "Recalculates the normals and tangents of the listed blendshapes at build time the way Blender shades them.");
        const int MaxSuggestions = 8;
        static readonly GUIContent CopyLabel = new GUIContent("Copy", "Copy the list as text, one blendshape per line.");
        static readonly GUIContent PasteLabel = new GUIContent("Paste", "Add blendshapes from copied text, one per line. Names already in the list are skipped.");
        static readonly GUIContent AddLabel = new GUIContent("Add", "Add the typed name to the list.");
        ReorderableList normals;
        string normalSearch = "";
        static GUIStyle suggestionStyle;

        VAPSkinnedMeshRenderer Owner => target as VAPSkinnedMeshRenderer;

        static readonly Dictionary<int, (string search, List<int> active, Vector2 scroll, string selected, string normalSearch)> kept =
            new Dictionary<int, (string, List<int>, Vector2, string, string)>();

        void OnEnable() {
            active = null;
            search = "";
            normalSearch = "";
            if (target != null && kept.TryGetValue(target.GetInstanceID(), out var state)) {
                kept.Remove(target.GetInstanceID());
                (search, active, scroll, selectedShape, normalSearch) = state;
            }
        }

        void OnDisable() {
            if (Owner != null && Selection.gameObjects.Contains(Owner.gameObject)) {
                kept[target.GetInstanceID()] = (search, active, scroll, selectedShape, normalSearch);
            }
        }

        public override VisualElement CreateInspectorGUI() {
            var root = new VisualElement();
            root.Add(new IMGUIContainer(() => Run(DrawMain)));
            root.Add(Foldout("Bounds", DrawBoundsBar, DrawBounds, false, "m_AABB"));
            root.Add(Foldout("Lighting", DrawLightingBar, DrawLighting, false, "m_CastShadows", "m_ProbeAnchor"));
            root.Add(Foldout("Materials", DrawMaterialsBar, DrawMaterials, true, "m_Materials"));
            root.Add(Foldout("BlendShapes", DrawBlendShapesBar, () => DrawBlendShapes(Owner.wrapped), true, "m_BlendShapeWeights"));
            var normalsFoldout = new VAPFoldout("VAP.SkinnedMeshRenderer.BlendshapeNormals", DrawNormalsBar, () => Run(DrawNormals), () => Owner != null && Owner.blendShapeNormals, true) {
                Overrides = () => {
                    if (NormalsLocked) {
                        return new SerializedProperty[0];
                    }

                    serializedObject.Update();
                    return new[]
                    {
                        serializedObject.FindProperty(nameof(VAPSkinnedMeshRenderer.blendShapeNormals)),
                        serializedObject.FindProperty(nameof(VAPSkinnedMeshRenderer.normalBlendShapes)),
                    };
                },
            }.Root;
            void RefreshNormals() => normalsFoldout.style.display = ShowNormals() ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshNormals();
            normalsFoldout.schedule.Execute(RefreshNormals).Every(500);
            root.Add(normalsFoldout);
            VAPInspectorHeader.Attach(root, Title, null, new VAPInspectorHeader.Toggle {
                Get = () => Owner != null && Owner.wrapped != null && Owner.wrapped.enabled,
                Set = SetEnabled,
            });
            return VAPPrefabLock.AddEditButton(root, this);
        }

        void OnSceneGUI() {
            var smr = Owner != null ? Owner.wrapped : null;
            if (smr == null || !smr.enabled || !smr.gameObject.activeInHierarchy) {
                return;
            }

            var bounds = smr.localBounds;
            var space = smr.rootBone != null ? smr.rootBone : smr.transform;
            using (new Handles.DrawingScope(Handles.color, space.localToWorldMatrix)) {
                if (!EditingBounds) {
                    Handles.DrawWireCube(bounds.center, bounds.size);
                    return;
                }
                boundsHandle.center = bounds.center;
                boundsHandle.size = bounds.size;
                EditorGUI.BeginChangeCheck();
                boundsHandle.DrawHandle();
                if (EditorGUI.EndChangeCheck()) {
                    Undo.RecordObject(smr, "Resize Bounds");
                    smr.localBounds = new Bounds(boundsHandle.center, boundsHandle.size);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(smr);
                    Repaint();
                }
            }
        }

        bool EditingBounds => EditMode.editMode == EditMode.SceneViewEditMode.Collider && EditMode.IsOwner(this);

        void DrawBoundsBar(Rect bar) {
            const float width = 28f;
            var button = new Rect(bar.xMax - width, bar.y + (bar.height - 18f) * 0.5f, width, 18f);
            bool editing = EditingBounds;
            var smr = Prepare() ? Owner.wrapped : null;
            using (new EditorGUI.DisabledScope(smr == null)) {
                if (GUI.Toggle(button, editing, editBoundsLabel ??= new GUIContent(EditorGUIUtility.IconContent("EditCollider").image, "Edit the bounds in the Scene view."), EditorStyles.miniButton) != editing) {
                    if (editing) {
                        EditMode.QuitEditMode();
                    } else {
                        EditMode.ChangeEditMode(EditMode.SceneViewEditMode.Collider, smr.bounds, this);
                    }

                    SceneView.RepaintAll();
                }
            }

            VRCFuryWrap.BarLabels(new Rect(bar.x, bar.y, bar.width - width - 8f, bar.height), bar.x, BoundsLabel.text, "", 0.6f, Changed("m_AABB"));
        }

        VisualElement Foldout(string name, System.Action<Rect> header, System.Action body, bool openByDefault, params string[] paths) =>
            new VAPFoldout("VAP.SkinnedMeshRenderer." + name, header, () => Run(body), null, true, openByDefault) {
                Overrides = () => {
                    if (!Prepare()) {
                        return null;
                    }

                    rendererSO.Update();
                    return paths.Select(rendererSO.FindProperty).ToArray();
                },
            }.Root;

        void SetEnabled(bool on) {
            var smr = Owner != null ? Owner.wrapped : null;
            if (smr == null) {
                return;
            }

            using (var so = new SerializedObject(smr)) {
                so.FindProperty("m_Enabled").boolValue = on;
                so.ApplyModifiedProperties();
            }
            Repaint();
        }

        bool Prepare() {
            var owner = Owner;
            if (owner == null) {
                return false;
            }

            if (owner.wrapped == null) {
                owner.wrapped = owner.GetComponent<SkinnedMeshRenderer>();
                if (owner.wrapped == null) {
                    return false;
                }

                EditorUtility.SetDirty(owner);
            }
            if (rendererSO == null || rendererSO.targetObject != owner.wrapped) {
                rendererSO = new SerializedObject(owner.wrapped);
            }

            return true;
        }

        void Run(System.Action draw) {
            if (!Prepare()) {
                return;
            }

            rendererSO.Update();
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Max(labelWidth, 170f);
            draw();
            rendererSO.ApplyModifiedProperties();
            EditorGUIUtility.labelWidth = labelWidth;
        }

        void DrawMain() {
            EditorGUILayout.PropertyField(rendererSO.FindProperty("m_Mesh"), MeshLabel);
            EditorGUILayout.PropertyField(rendererSO.FindProperty("m_RootBone"), RootBoneLabel);
        }

        void DrawBounds() {
            var bounds = rendererSO.FindProperty("m_AABB");
            bool wide = EditorGUIUtility.wideMode;
            EditorGUIUtility.wideMode = true;
            EditorGUILayout.PropertyField(bounds.FindPropertyRelative("m_Center"), CenterLabel);
            EditorGUILayout.PropertyField(bounds.FindPropertyRelative("m_Extent"), ExtentLabel);
            EditorGUIUtility.wideMode = wide;
        }

        bool Changed(string path) {
            if (!Prepare() || !PrefabUtility.IsPartOfPrefabInstance(Owner.wrapped)) {
                return false;
            }

            rendererSO.Update();
            return VRCFuryWrap.Changed(rendererSO.FindProperty(path));
        }

        void DrawLightingBar(Rect bar) {
            string shadows = Prepare() ? ShadowNames[Mathf.Clamp(rendererSO.FindProperty("m_CastShadows").intValue, 0, ShadowNames.Length - 1)].text : "";
            VRCFuryWrap.BarLabels(bar, bar.x, LightingLabel.text, "Shadows " + shadows, 0.6f, Changed("m_CastShadows") || Changed("m_ProbeAnchor"));
        }

        void DrawLighting() {
            Popup(rendererSO.FindProperty("m_CastShadows"), CastShadowsLabel, ShadowNames, ShadowValues);
            EditorGUILayout.PropertyField(rendererSO.FindProperty("m_ProbeAnchor"), AnchorLabel);
        }

        void DrawMaterialsBar(Rect bar) {
            int count = Prepare() ? rendererSO.FindProperty("m_Materials").arraySize : 0;
            VRCFuryWrap.BarLabels(bar, bar.x, MaterialsLabel.text, count + (count == 1 ? " Material" : " Materials"), 0.6f, Changed("m_Materials"));
        }

        void DrawMaterials() {
            Materials(rendererSO.FindProperty("m_Materials")).DoLayoutList();
            var list = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.Repaint) {
                EditorStyles.miniLabel.Draw(new Rect(list.x + 2f, list.yMax - 20f, list.width - 70f, 18f), MaterialsHint, false, false, false, false);
            }

            GUILayout.Space(-4f);
        }

        void Duplicate(SerializedProperty element) {
            var mat = element.objectReferenceValue as Material;
            var smr = Owner != null ? Owner.wrapped : null;
            if (mat == null || smr == null) {
                return;
            }

            var descriptor = VAPAvatar.Descriptor(smr);
            string owner = descriptor != null ? descriptor.gameObject.name : smr.gameObject.name;
            string name = string.Join("_", (owner + " " + mat.name).Split(System.IO.Path.GetInvalidFileNameChars()));
            string scene = smr.gameObject.scene.path;
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(smr.gameObject);
            if (string.IsNullOrEmpty(scene) && stage != null) {
                scene = stage.assetPath;
            }

            string folder = string.IsNullOrEmpty(scene) ? "Assets" : System.IO.Path.GetDirectoryName(scene).Replace('\\', '/');
            string path = EditorUtility.SaveFilePanelInProject("Duplicate & Save Material", name, "mat", "Where to save the copy of " + mat.name + ".", folder);
            if (!string.IsNullOrEmpty(path)) {
                var copy = new Material(mat);
                AssetDatabase.CreateAsset(copy, path);
                AssetDatabase.SaveAssets();
                element.objectReferenceValue = copy;
                element.serializedObject.ApplyModifiedProperties();
            }
        }

        void MaterialMenu(SerializedProperty element) {
            var menu = new GenericMenu();
            var mat = element.objectReferenceValue as Material;
            var revert = new GUIContent("Revert");
            if (element.prefabOverride) {
                menu.AddItem(revert, false, () => {
                    element.serializedObject.Update();
                    PrefabUtility.RevertPropertyOverride(element, InteractionMode.UserAction);
                });
            } else {
                menu.AddDisabledItem(revert);
            }

            var show = new GUIContent("Show in Project");
            var dupe = new GUIContent(Application.platform == RuntimePlatform.WindowsEditor ? "Duplicate, Save && Replace" : "Duplicate, Save & Replace");
            if (mat != null) {
                menu.AddItem(show, false, () => EditorGUIUtility.PingObject(mat));
                menu.AddItem(dupe, false, () => {
                    element.serializedObject.Update();
                    Duplicate(element);
                });
            } else {
                menu.AddDisabledItem(show);
                menu.AddDisabledItem(dupe);
            }
            menu.ShowAsContext();
        }

        void DrawBlendShapesBar(Rect bar) {
            var smr = Prepare() ? Owner.wrapped : null;
            var mesh = smr != null ? smr.sharedMesh : null;
            int count = mesh != null ? mesh.blendShapeCount : 0;
            var headers = count > 0 ? EmptyShapes(mesh) : new bool[0];
            int on = 0, shapes = 0;
            for (int i = 0; i < count; i++) {
                if (headers[i]) {
                    continue;
                }

                shapes++;
                if (smr.GetBlendShapeWeight(i) > 0f) {
                    on++;
                }
            }
            VRCFuryWrap.BarLabels(bar, bar.x, BlendShapesLabel.text, $"{on} of {shapes} Active", 0.6f, Changed("m_BlendShapeWeights"));
        }

        ReorderableList Materials(SerializedProperty prop) {
            if (materials == null || materials.serializedProperty == null || materials.serializedProperty.serializedObject != prop.serializedObject) {
                materials = new ReorderableList(prop.serializedObject, prop, true, false, true, true) {
                    headerHeight = 2f,
                    elementHeight = EditorGUIUtility.singleLineHeight + 2f,
                    drawElementCallback = (rect, i, active, focused) => {
                        rect.y += 1f;
                        rect.height = EditorGUIUtility.singleLineHeight;
                        var element = materials.serializedProperty.GetArrayElementAtIndex(i);
                        if (Event.current.type == EventType.ContextClick && rect.Contains(Event.current.mousePosition) && !AnimationMode.InAnimationMode()) {
                            MaterialMenu(element.Copy());
                            Event.current.Use();
                        }
                        EditorGUI.PropertyField(rect, element, new GUIContent("Element " + i));
                    },
                };
            }
            materials.serializedProperty = prop;
            return materials;
        }

        void DrawBlendShapes(SkinnedMeshRenderer smr) {
            var mesh = smr.sharedMesh;
            int count = mesh != null ? mesh.blendShapeCount : 0;
            if (count == 0) {
                return;
            }

            var headers = EmptyShapes(mesh);
            using (new EditorGUILayout.HorizontalScope()) {
                if (active == null) {
                    string before = search;
                    search = VAPEditorGUI.SearchBar(ShapeSearchControl, search);
                    if (!string.IsNullOrEmpty(before) && search.Length == 0) {
                        reveal = true;
                    }

                    if (GUILayout.Button(ShowActiveLabel, GUILayout.Width(ButtonWidth))) {
                        active = new List<int>();
                        for (int i = 0; i < count; i++) {
                            if (!headers[i] && smr.GetBlendShapeWeight(i) > 0f) {
                                active.Add(i);
                            }
                        }

                        scroll = Vector2.zero;
                        GUIUtility.ExitGUI();
                    }
                } else {
                    EditorGUILayout.LabelField($"{active.Count} active", EditorStyles.miniLabel);
                    if (GUILayout.Button(BackLabel, GUILayout.Width(ButtonWidth))) {
                        active = null;
                        scroll = Vector2.zero;
                        reveal = true;
                        GUIUtility.ExitGUI();
                    }
                }
            }

            var rows = new List<int>();
            bool filtered = active != null || !string.IsNullOrEmpty(search);
            bool Matches(int i) => active != null
                ? active.Contains(i)
                : string.IsNullOrEmpty(search) || mesh.GetBlendShapeName(i).IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0;
            int group = -1;
            var members = new List<int>();
            void Flush() {
                if (group < 0) {
                    rows.AddRange(members.Where(Matches));
                } else if (!filtered) {
                    rows.Add(group);
                    if (GroupOpen(mesh, group)) {
                        rows.AddRange(members);
                    }
                } else {
                    bool groupMatch = active == null && Matches(group);
                    var shown = groupMatch ? members : members.Where(Matches).ToList();
                    if (shown.Count > 0 || groupMatch) {
                        rows.Add(group);
                        rows.AddRange(shown);
                    }
                }
                members.Clear();
            }
            for (int i = 0; i < count; i++) {
                if (headers[i]) {
                    Flush();
                    group = i;
                } else {
                    members.Add(i);
                }
            }
            Flush();

            float step = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            listStyle ??= new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(6, 6, 6, 6) };
            float content = count * step + listStyle.padding.vertical + 2f;
            float listHeight = Mathf.Clamp(EditorPrefs.GetFloat(HeightPref, MaxListHeight), MaxListHeight, MaxListHeight * 2f);
            float height = Mathf.Min(content, listHeight);
            if (reveal && Event.current.type == EventType.Layout) {
                reveal = false;
                int at = rows.FindIndex(i => mesh.GetBlendShapeName(i) == selectedShape);
                if (at >= 0) {
                    scroll.y = Mathf.Max(0f, at * step - (height - step) * 0.5f);
                }
            }
            scroll = EditorGUILayout.BeginScrollView(scroll, listStyle, GUILayout.Height(height));
            var weights = rendererSO.FindProperty("m_BlendShapeWeights");
            float outerLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Max(80f, (EditorGUIUtility.currentViewWidth - 70f) * 0.5f);
            if (rows.Count == 0) {
                EditorGUILayout.LabelField(active != null ? "No active blendshapes" : "No matching blendshapes", EditorStyles.miniLabel);
            }

            for (int r = 0; r < rows.Count; r++) {
                int i = rows[r];
                string name = mesh.GetBlendShapeName(i);
                var label = new GUIContent(name, name);
                var rowRect = EditorGUILayout.GetControlRect();
                float gap = EditorGUIUtility.standardVerticalSpacing * 0.5f;
                var back = new Rect(rowRect.x - 4f, rowRect.y - gap, rowRect.width + 8f, rowRect.height + gap * 2f);
                bool selected = name == selectedShape;
                var e = Event.current;
                if (e.type == EventType.Repaint && (selected || r % 2 == 1)) {
                    EditorGUI.DrawRect(back, selected ? SelectedColor : StripeColor);
                }

                if (headers[i]) {
                    if (e.type == EventType.ContextClick && rowRect.Contains(e.mousePosition)) {
                        GroupMenu(mesh, headers, i);
                        e.Use();
                    }
                    bool open = filtered || GroupOpen(mesh, i);
                    bool now = EditorGUI.Foldout(rowRect, open, new GUIContent(GroupTitle(name), name), true);
                    if (!filtered && now != open) {
                        SessionState.SetBool(GroupKey(mesh, i), now);
                    }

                    continue;
                }
                var labelRect = new Rect(rowRect.x, rowRect.y, EditorGUIUtility.labelWidth, rowRect.height);
                if (e.type == EventType.MouseDown && e.button == 0 && labelRect.Contains(e.mousePosition)) {
                    selectedShape = selected ? null : name;
                    GUIUtility.keyboardControl = 0;
                    e.Use();
                    Repaint();
                }
                if (i < weights.arraySize) {
                    EditorGUI.Slider(rowRect, weights.GetArrayElementAtIndex(i), 0f, 100f, label);
                    continue;
                }
                EditorGUI.BeginChangeCheck();
                float v = EditorGUI.Slider(rowRect, label, smr.GetBlendShapeWeight(i), 0f, 100f);
                if (EditorGUI.EndChangeCheck()) {
                    int size = weights.arraySize;
                    weights.arraySize = i + 1;
                    for (int k = size; k < i; k++) {
                        weights.GetArrayElementAtIndex(k).floatValue = smr.GetBlendShapeWeight(k);
                    }

                    weights.GetArrayElementAtIndex(i).floatValue = v;
                    rendererSO.ApplyModifiedProperties();
                    rendererSO.Update();
                    weights = rendererSO.FindProperty("m_BlendShapeWeights");
                }
            }
            EditorGUIUtility.labelWidth = outerLabelWidth;
            EditorGUILayout.EndScrollView();
            if (content > MaxListHeight) {
                ResizeHandle(listHeight, content);
            }
        }

        static readonly Dictionary<Mesh, (int dirty, bool[] empty)> emptyShapes = new Dictionary<Mesh, (int, bool[])>();

        internal static void ClearEmptyShapes() => emptyShapes.Clear();

        internal static bool[] EmptyShapes(Mesh mesh) {
            int dirty = EditorUtility.GetDirtyCount(mesh);
            if (emptyShapes.TryGetValue(mesh, out var cached) && cached.dirty == dirty && cached.empty.Length == mesh.blendShapeCount) {
                return cached.empty;
            }

            var empty = new bool[mesh.blendShapeCount];
            var vertices = new Vector3[mesh.vertexCount];
            var normals = new Vector3[mesh.vertexCount];
            var tangents = new Vector3[mesh.vertexCount];
            for (int i = 0; i < empty.Length; i++) {
                string name = mesh.GetBlendShapeName(i);
                if (IsSilence(name) || !StartsWithSymbol(name)) {
                    continue;
                }

                int frames = mesh.GetBlendShapeFrameCount(i);
                bool moves = false;
                for (int f = 0; f < frames && !moves; f++) {
                    mesh.GetBlendShapeFrameVertices(i, f, vertices, normals, tangents);
                    moves = vertices.Any(v => v != Vector3.zero) || normals.Any(v => v != Vector3.zero) || tangents.Any(v => v != Vector3.zero);
                }
                empty[i] = !moves;
            }
            emptyShapes[mesh] = (dirty, empty);
            return empty;
        }

        static bool StartsWithSymbol(string name) {
            string trimmed = name.TrimStart();
            return trimmed.Length > 0 && trimmed[0] < 128 && (char.IsPunctuation(trimmed[0]) || char.IsSymbol(trimmed[0]));
        }

        static bool IsSilence(string name) {
            string lower = name.ToLowerInvariant();
            return lower == "sil" || lower.EndsWith("v_sil");
        }

        static string GroupTitle(string name) {
            string title = name.Trim(' ', '-');
            return title.Length == 0 ? name : title;
        }

        static string GroupKey(Mesh mesh, int index) => $"VAP.SkinnedMeshRenderer.Group.{mesh.GetInstanceID()}.{mesh.GetBlendShapeName(index)}";

        static bool GroupOpen(Mesh mesh, int index) => SessionState.GetBool(GroupKey(mesh, index), true);

        void GroupMenu(Mesh mesh, bool[] headers, int index) {
            var groups = Enumerable.Range(0, headers.Length).Where(k => headers[k]).ToList();
            bool open = GroupOpen(mesh, index);
            var menu = new GenericMenu();
            void Item(string label, bool enabled, System.Func<int, bool?> state) {
                if (!enabled) {
                    return;
                }

                menu.AddItem(new GUIContent(label), false, () => {
                    foreach (int g in groups) {
                        var value = state(g);
                        if (value.HasValue) {
                            SessionState.SetBool(GroupKey(mesh, g), value.Value);
                        }
                    }
                    Repaint();
                });
            }
            bool anyOpen = groups.Any(g => GroupOpen(mesh, g));
            bool anyClosed = groups.Any(g => !GroupOpen(mesh, g));
            bool others = groups.Any(g => GroupOpen(mesh, g) != (g == index));
            Item(open ? "Collapse" : "Expand", true, g => g == index ? !open : (bool?)null);
            if (anyOpen || anyClosed || others) {
                menu.AddSeparator("");
            }

            Item("Collapse All", anyOpen, g => false);
            Item("Expand All", anyClosed, g => true);
            Item("Collapse All But This One", others, g => g == index);
            menu.ShowAsContext();
        }

        bool ShowNormals() {
            var mesh = Owner != null && Owner.wrapped != null ? Owner.wrapped.sharedMesh : null;
            if (mesh == null) {
                return false;
            }

            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mesh)) as ModelImporter;
            return importer == null || importer.importBlendShapeNormals == ModelImporterNormals.None;
        }

        void DrawNormalsBar(Rect bar) {
            if (Owner == null) {
                return;
            }

            serializedObject.Update();
            var enabled = serializedObject.FindProperty(nameof(VAPSkinnedMeshRenderer.blendShapeNormals));
            bool changed = VRCFuryWrap.Changed(enabled) || VRCFuryWrap.Changed(serializedObject.FindProperty(nameof(VAPSkinnedMeshRenderer.normalBlendShapes)));
            VAPEditorGUI.EnabledBar(bar, NormalsLabel.text, enabled, NormalsLocked, changed);
            serializedObject.ApplyModifiedProperties();
        }

        bool NormalsLocked => VAPPrefabLock.IsFromPrefab(Owner);

        void DrawNormals() {
            serializedObject.Update();
            bool wasEnabled = GUI.enabled;
            bool locked = NormalsLocked;
            GUI.enabled = wasEnabled && !locked;
            var prop = serializedObject.FindProperty(nameof(VAPSkinnedMeshRenderer.normalBlendShapes));
            var names = Blendshapes.Names(Owner.wrapped.sharedMesh);
            var added = new HashSet<string>();
            for (int i = 0; i < prop.arraySize; i++) {
                added.Add(prop.GetArrayElementAtIndex(i).stringValue);
            }

            var e = Event.current;
            bool enter = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && GUI.GetNameOfFocusedControl() == NormalsSearchControl;
            using (new EditorGUILayout.HorizontalScope()) {
                normalSearch = VAPEditorGUI.SearchBar(NormalsSearchControl, normalSearch);
                string typed = normalSearch.Trim();
                using (new EditorGUI.DisabledScope(typed.Length == 0 || added.Contains(typed))) {
                    if (GUILayout.Button(AddLabel, GUILayout.Width(ButtonWidth))) {
                        Add(prop, typed);
                        normalSearch = "";
                        GUI.FocusControl(null);
                        GUIUtility.ExitGUI();
                    }
                }
            }
            if (!string.IsNullOrEmpty(normalSearch)) {
                var matches = names.Distinct()
                    .Where(n => !added.Contains(n) && n.IndexOf(normalSearch, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(n => n.StartsWith(normalSearch, System.StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ToList();
                if (enter) {
                    if (matches.Count > 0) {
                        Add(prop, matches[0]);
                    } else if (!added.Contains(normalSearch)) {
                        Add(prop, normalSearch);
                    }

                    e.Use();
                    GUIUtility.ExitGUI();
                }
                if (matches.Count == 0) {
                    EditorGUILayout.LabelField("No matching blendshapes, click Enter or press Add", EditorStyles.miniLabel);
                }

                suggestionStyle ??= new GUIStyle(EditorStyles.miniButton) { alignment = TextAnchor.MiddleLeft };
                foreach (var n in matches.Take(MaxSuggestions)) {
                    if (GUILayout.Button(new GUIContent(n, "Add " + n + "."), suggestionStyle)) {
                        Add(prop, n);
                        GUIUtility.ExitGUI();
                    }
                }

                if (matches.Count > MaxSuggestions) {
                    EditorGUILayout.LabelField($"{matches.Count - MaxSuggestions} more, keep typing", EditorStyles.miniLabel);
                }

                GUILayout.Space(2f);
            }
            NormalsList(prop, new HashSet<string>(names)).DoLayoutList();
            GUILayout.Space(-18f);
            GUI.enabled = wasEnabled;
            using (new EditorGUILayout.HorizontalScope()) {
                using (new EditorGUI.DisabledScope(prop.arraySize == 0)) {
                    if (GUILayout.Button(CopyLabel, EditorStyles.miniButtonLeft, GUILayout.Width(50f))) {
                        EditorGUIUtility.systemCopyBuffer = string.Join("\n", Enumerable.Range(0, prop.arraySize).Select(i => prop.GetArrayElementAtIndex(i).stringValue).Where(n => !string.IsNullOrEmpty(n)));
                    }
                }

                GUI.enabled = wasEnabled && !locked;
                if (GUILayout.Button(PasteLabel, EditorStyles.miniButtonRight, GUILayout.Width(50f))) {
                    foreach (var line in (EditorGUIUtility.systemCopyBuffer ?? "").Split('\n')) {
                        string name = line.Trim();
                        if (name.Length > 0 && added.Add(name)) {
                            Add(prop, name);
                        }
                    }
                    GUIUtility.ExitGUI();
                }
                GUI.enabled = wasEnabled;
                GUILayout.FlexibleSpace();
            }
            GUILayout.Space(-2f);
            serializedObject.ApplyModifiedProperties();
        }

        void Add(SerializedProperty prop, string name) {
            int index = prop.arraySize;
            prop.arraySize = index + 1;
            prop.GetArrayElementAtIndex(index).stringValue = name;
            prop.serializedObject.ApplyModifiedProperties();
        }

        ReorderableList NormalsList(SerializedProperty prop, HashSet<string> available) {
            normals ??= new ReorderableList(prop.serializedObject, prop, true, false, false, true) {
                headerHeight = 2f,
                elementHeight = EditorGUIUtility.singleLineHeight + 2f,
            };
            normals.serializedProperty = prop;
            normals.drawElementCallback = (rect, i, isActive, focused) => {
                rect.y += 1f;
                rect.height = EditorGUIUtility.singleLineHeight;
                var element = prop.GetArrayElementAtIndex(i);
                VAPEditorGUI.StringField(rect, element);
                string value = element.stringValue;
                if (string.IsNullOrEmpty(value)) {
                    VAPEditorGUI.WarningIcon(rect, "Empty; type in the search field to pick a blendshape.");
                } else if (!available.Contains(value)) {
                    VAPEditorGUI.WarningIcon(rect, "The mesh doesn't have it.");
                }
            };
            return normals;
        }

        void ResizeHandle(float listHeight, float content) {
            var rect = GUILayoutUtility.GetRect(0f, 8f, GUILayout.ExpandWidth(true));
            int id = GUIUtility.GetControlID(FocusType.Passive);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeVertical);
            var e = Event.current;
            switch (e.GetTypeForControl(id)) {
                case EventType.MouseDown when e.button == 0 && rect.Contains(e.mousePosition):
                    GUIUtility.hotControl = id;
                    dragStart = e.mousePosition.y;
                    dragHeight = Mathf.Min(listHeight, content);
                    e.Use();
                    break;
                case EventType.MouseDrag when GUIUtility.hotControl == id:
                    EditorPrefs.SetFloat(HeightPref, Mathf.Clamp(dragHeight + e.mousePosition.y - dragStart, MaxListHeight, Mathf.Clamp(content, MaxListHeight, MaxListHeight * 2f)));
                    Repaint();
                    e.Use();
                    break;
                case EventType.MouseUp when GUIUtility.hotControl == id:
                    GUIUtility.hotControl = 0;
                    e.Use();
                    break;
                case EventType.Repaint:
                    var grip = new Rect(rect.center.x - 15f, rect.center.y - 1f, 30f, 2f);
                    EditorGUI.DrawRect(grip, new Color(0.5f, 0.5f, 0.5f, GUIUtility.hotControl == id || rect.Contains(e.mousePosition) ? 0.9f : 0.5f));
                    break;
            }
        }

        [MenuItem("CONTEXT/SkinnedMeshRenderer/Wrap with VAP", true)]
        static bool ValidateWrap(MenuCommand command) =>
            command.context is SkinnedMeshRenderer smr && smr.GetComponent<VAPSkinnedMeshRenderer>() == null;

        [MenuItem("CONTEXT/SkinnedMeshRenderer/Wrap with VAP")]
        static void Wrap(MenuCommand command) {
            var smr = (SkinnedMeshRenderer)command.context;
            Undo.SetCurrentGroupName("Wrap with VAP");
            int group = Undo.GetCurrentGroup();
            var w = Undo.AddComponent<VAPSkinnedMeshRenderer>(smr.gameObject);
            w.wrapped = smr;
            var components = smr.GetComponents<Component>();
            int index = System.Array.IndexOf(components, smr);
            for (int i = components.Length - 1; i > index; i--) {
                if (!ComponentUtility.MoveComponentUp(w)) {
                    break;
                }
            }

            Undo.RecordObject(smr, "Wrap with VAP");
            SkinnedMeshRendererBuild.Enforce(smr);
            PrefabUtility.RecordPrefabInstancePropertyModifications(smr);
            EditorUtility.SetDirty(w);
            WrappedToggles.Set(smr, true);
            Undo.CollapseUndoOperations(group);
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }

        [MenuItem("CONTEXT/VAPSkinnedMeshRenderer/Unwrap", true)]
        static bool ValidateUnwrap(MenuCommand command) =>
            command.context is VAPSkinnedMeshRenderer w && w.wrapped != null &&
            (!PrefabUtility.IsPartOfPrefabInstance(w) || PrefabUtility.IsAddedComponentOverride(w));

        [MenuItem("CONTEXT/VAPSkinnedMeshRenderer/Unwrap")]
        static void Unwrap(MenuCommand command) {
            var w = (VAPSkinnedMeshRenderer)command.context;
            Undo.SetCurrentGroupName("Unwrap Skinned Mesh Renderer");
            int group = Undo.GetCurrentGroup();
            WrappedToggles.Release(w, w.wrapped);
            Undo.CollapseUndoOperations(group);
        }

        static void Popup(SerializedProperty prop, GUIContent label, GUIContent[] names, int[] values) {
            var rect = EditorGUILayout.GetControlRect();
            label = EditorGUI.BeginProperty(rect, label, prop);
            EditorGUI.BeginChangeCheck();
            int v = EditorGUI.IntPopup(rect, label, prop.intValue, names, values);
            if (EditorGUI.EndChangeCheck()) {
                prop.intValue = v;
            }

            EditorGUI.EndProperty();
        }
    }
}

namespace VAP.Editor {
    internal class EmptyBlendShapesReset : AssetPostprocessor {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom) =>
            VAPSkinnedMeshRendererEditor.ClearEmptyShapes();
    }
}
#endif
