#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace VAP.Editor
{
    [CustomEditor(typeof(VAPSkinnedMeshRenderer))]
    public class VAPSkinnedMeshRendererEditor : UnityEditor.Editor
    {
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
        const string HeightPref = "VAP.SkinnedMeshRenderer.BlendShapesHeight";
        float dragStart, dragHeight;
        static readonly GUIContent ShowActiveLabel = new GUIContent("Show Active", "List the blendshapes above 0.");
        static readonly GUIContent BackLabel = new GUIContent("Back to Search");
        static readonly GUIContent DuplicateLabel = new GUIContent("DUPE", "Save a copy of the material and replace it.");
        const float DuplicateWidth = 50f;
        static GUIContent editBoundsLabel;
        static readonly UnityEditor.IMGUI.Controls.BoxBoundsHandle boundsHandle = new UnityEditor.IMGUI.Controls.BoxBoundsHandle();
        static readonly GUIContent NormalsLabel = new GUIContent("Override Blendshape Normals", "Recalculates the normals and tangents of the listed blendshapes at build time the way Blender shades them.");
        const string NormalsSearchControl = "VAP.SkinnedMeshRenderer.NormalsSearch";
        const int MaxSuggestions = 8;
        static readonly GUIContent CopyLabel = new GUIContent("Copy", "Copy the list as text, one blendshape per line.");
        static readonly GUIContent PasteLabel = new GUIContent("Paste", "Add blendshapes from copied text, one per line. Names already in the list are skipped.");
        static readonly GUIContent AddLabel = new GUIContent("Add", "Add the typed name to the list.");
        ReorderableList normals;
        string normalSearch = "";
        static GUIStyle stateStyle, suggestionStyle;

        VAPSkinnedMeshRenderer Owner => target as VAPSkinnedMeshRenderer;

        void OnEnable()
        {
            active = null;
            search = "";
            normalSearch = "";
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new IMGUIContainer(() => Run(DrawMain)));
            root.Add(Foldout("Bounds", DrawBoundsBar, DrawBounds, false, "m_AABB"));
            root.Add(Foldout("Lighting", DrawLightingBar, DrawLighting, false, "m_CastShadows", "m_ProbeAnchor"));
            root.Add(Foldout("Materials", DrawMaterialsBar, DrawMaterials, true, "m_Materials"));
            root.Add(Foldout("BlendShapes", DrawBlendShapesBar, () => DrawBlendShapes(Owner.wrapped), true, "m_BlendShapeWeights"));
            var normalsFoldout = new VAPFoldout("VAP.SkinnedMeshRenderer.BlendshapeNormals", DrawNormalsBar, () => Run(DrawNormals), () => Owner != null && Owner.blendShapeNormals, true)
            {
                Overrides = () =>
                {
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
            VAPInspectorHeader.Attach(root, Title, null, new VAPInspectorHeader.Toggle
            {
                Get = () => Owner != null && Owner.wrapped != null && Owner.wrapped.enabled,
                Set = SetEnabled,
            });
            return root;
        }

        void OnSceneGUI()
        {
            var smr = Owner != null ? Owner.wrapped : null;
            if (smr == null || !smr.enabled || !smr.gameObject.activeInHierarchy)
                return;
            var bounds = smr.localBounds;
            var space = smr.rootBone != null ? smr.rootBone : smr.transform;
            using (new Handles.DrawingScope(Handles.color, space.localToWorldMatrix))
            {
                if (!EditingBounds)
                {
                    Handles.DrawWireCube(bounds.center, bounds.size);
                    return;
                }
                boundsHandle.center = bounds.center;
                boundsHandle.size = bounds.size;
                EditorGUI.BeginChangeCheck();
                boundsHandle.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(smr, "Resize Bounds");
                    smr.localBounds = new Bounds(boundsHandle.center, boundsHandle.size);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(smr);
                    Repaint();
                }
            }
        }

        bool EditingBounds => EditMode.editMode == EditMode.SceneViewEditMode.Collider && EditMode.IsOwner(this);

        void DrawBoundsBar(Rect bar)
        {
            const float width = 28f;
            var button = new Rect(bar.xMax - width, bar.y + (bar.height - 18f) * 0.5f, width, 18f);
            bool editing = EditingBounds;
            var smr = Prepare() ? Owner.wrapped : null;
            using (new EditorGUI.DisabledScope(smr == null))
                if (GUI.Toggle(button, editing, editBoundsLabel ??= new GUIContent(EditorGUIUtility.IconContent("EditCollider").image, "Edit the bounds in the Scene view."), EditorStyles.miniButton) != editing)
                {
                    if (editing)
                        EditMode.QuitEditMode();
                    else
                        EditMode.ChangeEditMode(EditMode.SceneViewEditMode.Collider, smr.bounds, this);
                    SceneView.RepaintAll();
                }
            VRCFuryWrap.BarLabels(new Rect(bar.x, bar.y, bar.width - width - 8f, bar.height), bar.x, BoundsLabel.text, "", 0.6f, Changed("m_AABB"));
        }

        VisualElement Foldout(string name, System.Action<Rect> header, System.Action body, bool openByDefault, params string[] paths) =>
            new VAPFoldout("VAP.SkinnedMeshRenderer." + name, header, () => Run(body), null, true, openByDefault)
            {
                Overrides = () =>
                {
                    if (!Prepare())
                        return null;
                    rendererSO.Update();
                    return paths.Select(rendererSO.FindProperty).ToArray();
                },
            }.Root;

        void SetEnabled(bool on)
        {
            var smr = Owner != null ? Owner.wrapped : null;
            if (smr == null)
                return;
            Undo.RecordObject(smr, on ? "Enable Renderer" : "Disable Renderer");
            smr.enabled = on;
            PrefabUtility.RecordPrefabInstancePropertyModifications(smr);
            Repaint();
        }

        bool Prepare()
        {
            var owner = Owner;
            if (owner == null)
                return false;
            if (owner.wrapped == null)
            {
                owner.wrapped = owner.GetComponent<SkinnedMeshRenderer>();
                if (owner.wrapped == null)
                    return false;
                EditorUtility.SetDirty(owner);
            }
            if (rendererSO == null || rendererSO.targetObject != owner.wrapped)
                rendererSO = new SerializedObject(owner.wrapped);
            return true;
        }

        void Run(System.Action draw)
        {
            if (!Prepare())
                return;
            rendererSO.Update();
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Max(labelWidth, 170f);
            draw();
            rendererSO.ApplyModifiedProperties();
            EditorGUIUtility.labelWidth = labelWidth;
        }

        void DrawMain()
        {
            EditorGUILayout.PropertyField(rendererSO.FindProperty("m_Mesh"), MeshLabel);
            EditorGUILayout.PropertyField(rendererSO.FindProperty("m_RootBone"), RootBoneLabel);
        }

        void DrawBounds()
        {
            var bounds = rendererSO.FindProperty("m_AABB");
            bool wide = EditorGUIUtility.wideMode;
            EditorGUIUtility.wideMode = true;
            EditorGUILayout.PropertyField(bounds.FindPropertyRelative("m_Center"), CenterLabel);
            EditorGUILayout.PropertyField(bounds.FindPropertyRelative("m_Extent"), ExtentLabel);
            EditorGUIUtility.wideMode = wide;
        }

        bool Changed(string path)
        {
            if (!Prepare() || !PrefabUtility.IsPartOfPrefabInstance(Owner.wrapped))
                return false;
            rendererSO.Update();
            return VRCFuryWrap.Changed(rendererSO.FindProperty(path));
        }

        void DrawLightingBar(Rect bar)
        {
            string shadows = Prepare() ? ShadowNames[Mathf.Clamp(rendererSO.FindProperty("m_CastShadows").intValue, 0, ShadowNames.Length - 1)].text : "";
            VRCFuryWrap.BarLabels(bar, bar.x, LightingLabel.text, "Shadows " + shadows, 0.6f, Changed("m_CastShadows") || Changed("m_ProbeAnchor"));
        }

        void DrawLighting()
        {
            Popup(rendererSO.FindProperty("m_CastShadows"), CastShadowsLabel, ShadowNames, ShadowValues);
            EditorGUILayout.PropertyField(rendererSO.FindProperty("m_ProbeAnchor"), AnchorLabel);
        }

        void DrawMaterialsBar(Rect bar)
        {
            int count = Prepare() ? rendererSO.FindProperty("m_Materials").arraySize : 0;
            VRCFuryWrap.BarLabels(bar, bar.x, MaterialsLabel.text, count + (count == 1 ? " Material" : " Materials"), 0.6f, Changed("m_Materials"));
        }

        void DrawMaterials()
        {
            Materials(rendererSO.FindProperty("m_Materials")).DoLayoutList();
            GUILayout.Space(-4f);
        }

        void Duplicate(SerializedProperty element)
        {
            var mat = element.objectReferenceValue as Material;
            var smr = Owner != null ? Owner.wrapped : null;
            if (mat == null || smr == null)
                return;
            var descriptor = VAPAvatar.Descriptor(smr);
            string owner = descriptor != null ? descriptor.gameObject.name : smr.gameObject.name;
            string name = string.Join("_", (owner + " " + mat.name).Split(System.IO.Path.GetInvalidFileNameChars()));
            string scene = smr.gameObject.scene.path;
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(smr.gameObject);
            if (string.IsNullOrEmpty(scene) && stage != null)
                scene = stage.assetPath;
            string folder = string.IsNullOrEmpty(scene) ? "Assets" : System.IO.Path.GetDirectoryName(scene).Replace('\\', '/');
            string path = EditorUtility.SaveFilePanelInProject("Duplicate & Save Material", name, "mat", "Where to save the copy of " + mat.name + ".", folder);
            if (!string.IsNullOrEmpty(path))
            {
                var copy = new Material(mat);
                AssetDatabase.CreateAsset(copy, path);
                AssetDatabase.SaveAssets();
                element.objectReferenceValue = copy;
                element.serializedObject.ApplyModifiedProperties();
            }
            GUIUtility.ExitGUI();
        }

        void DrawBlendShapesBar(Rect bar)
        {
            var smr = Prepare() ? Owner.wrapped : null;
            var mesh = smr != null ? smr.sharedMesh : null;
            int count = mesh != null ? mesh.blendShapeCount : 0;
            int on = 0;
            for (int i = 0; i < count; i++)
                if (smr.GetBlendShapeWeight(i) > 0f)
                    on++;
            VRCFuryWrap.BarLabels(bar, bar.x, BlendShapesLabel.text, $"{on} of {count} Active", 0.6f, Changed("m_BlendShapeWeights"));
        }

        ReorderableList Materials(SerializedProperty prop)
        {
            if (materials == null || materials.serializedProperty == null || materials.serializedProperty.serializedObject != prop.serializedObject)
            {
                materials = new ReorderableList(prop.serializedObject, prop, true, false, true, true)
                {
                    headerHeight = 2f,
                    elementHeight = EditorGUIUtility.singleLineHeight + 2f,
                    drawElementCallback = (rect, i, active, focused) =>
                    {
                        rect.y += 1f;
                        rect.height = EditorGUIUtility.singleLineHeight;
                        var element = materials.serializedProperty.GetArrayElementAtIndex(i);
                        var button = new Rect(rect.xMax - DuplicateWidth, rect.y, DuplicateWidth, rect.height);
                        rect.width -= DuplicateWidth + 4f;
                        EditorGUI.PropertyField(rect, element, new GUIContent("Element " + i));
                        using (new EditorGUI.DisabledScope(!(element.objectReferenceValue is Material)))
                            if (GUI.Button(button, DuplicateLabel, EditorStyles.miniButton))
                                Duplicate(element);
                    },
                };
            }
            materials.serializedProperty = prop;
            return materials;
        }

        void DrawBlendShapes(SkinnedMeshRenderer smr)
        {
            var mesh = smr.sharedMesh;
            int count = mesh != null ? mesh.blendShapeCount : 0;
            if (count == 0)
                return;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (active == null)
                {
                    search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
                    if (GUILayout.Button(ShowActiveLabel, GUILayout.Width(ButtonWidth)))
                    {
                        active = new List<int>();
                        for (int i = 0; i < count; i++)
                            if (smr.GetBlendShapeWeight(i) > 0f)
                                active.Add(i);
                        scroll = Vector2.zero;
                        GUIUtility.ExitGUI();
                    }
                }
                else
                {
                    EditorGUILayout.LabelField($"{active.Count} active", EditorStyles.miniLabel);
                    if (GUILayout.Button(BackLabel, GUILayout.Width(ButtonWidth)))
                    {
                        active = null;
                        scroll = Vector2.zero;
                        GUIUtility.ExitGUI();
                    }
                }
            }

            var rows = new List<int>();
            if (active != null)
                rows.AddRange(active.Where(i => i < count));
            else
                for (int i = 0; i < count; i++)
                    if (string.IsNullOrEmpty(search) || mesh.GetBlendShapeName(i).IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        rows.Add(i);
            if (rows.Count == 0)
                return;

            float step = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            listStyle ??= new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(6, 6, 6, 6) };
            float content = rows.Count * step + listStyle.padding.vertical + 2f;
            float listHeight = Mathf.Clamp(EditorPrefs.GetFloat(HeightPref, MaxListHeight), MaxListHeight, MaxListHeight * 2f);
            float height = Mathf.Min(content, listHeight);
            scroll = EditorGUILayout.BeginScrollView(scroll, listStyle, GUILayout.Height(height));
            var weights = rendererSO.FindProperty("m_BlendShapeWeights");
            float outerLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Max(80f, (EditorGUIUtility.currentViewWidth - 70f) * 0.5f);
            for (int r = 0; r < rows.Count; r++)
            {
                int i = rows[r];
                string name = mesh.GetBlendShapeName(i);
                var label = new GUIContent(name, name);
                var rowRect = EditorGUILayout.GetControlRect();
                if (r % 2 == 1 && Event.current.type == EventType.Repaint)
                {
                    float gap = EditorGUIUtility.standardVerticalSpacing * 0.5f;
                    EditorGUI.DrawRect(new Rect(rowRect.x - 4f, rowRect.y - gap, rowRect.width + 8f, rowRect.height + gap * 2f), StripeColor);
                }
                if (i < weights.arraySize)
                {
                    EditorGUI.Slider(rowRect, weights.GetArrayElementAtIndex(i), 0f, 100f, label);
                    continue;
                }
                EditorGUI.BeginChangeCheck();
                float v = EditorGUI.Slider(rowRect, label, 0f, 0f, 100f);
                if (EditorGUI.EndChangeCheck())
                {
                    rendererSO.ApplyModifiedProperties();
                    Undo.RecordObject(smr, "Change BlendShape");
                    smr.SetBlendShapeWeight(i, v);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(smr);
                    rendererSO.Update();
                    weights = rendererSO.FindProperty("m_BlendShapeWeights");
                }
            }
            EditorGUIUtility.labelWidth = outerLabelWidth;
            EditorGUILayout.EndScrollView();
            if (content > MaxListHeight)
                ResizeHandle(listHeight, content);
        }

        bool ShowNormals()
        {
            var mesh = Owner != null && Owner.wrapped != null ? Owner.wrapped.sharedMesh : null;
            if (mesh == null)
                return false;
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mesh)) as ModelImporter;
            return importer == null || importer.importBlendShapeNormals == ModelImporterNormals.None;
        }

        void DrawNormalsBar(Rect bar)
        {
            if (Owner == null)
                return;
            serializedObject.Update();
            var enabled = serializedObject.FindProperty(nameof(VAPSkinnedMeshRenderer.blendShapeNormals));
            float line = EditorGUIUtility.singleLineHeight;
            var box = new Rect(bar.xMax - 16f, bar.y + (bar.height - line) * 0.5f, 16f, line);
            stateStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
            float width = stateStyle.CalcSize(new GUIContent("Disabled")).x + 22f;
            GUI.Label(new Rect(bar.xMax - width, bar.y, width - 20f, bar.height), enabled.boolValue ? "Enabled" : "Disabled", stateStyle);
            enabled.boolValue = EditorGUI.Toggle(box, enabled.boolValue);
            serializedObject.ApplyModifiedProperties();
            bool changed = VRCFuryWrap.Changed(enabled) || VRCFuryWrap.Changed(serializedObject.FindProperty(nameof(VAPSkinnedMeshRenderer.normalBlendShapes)));
            VRCFuryWrap.BarLabels(new Rect(bar.x, bar.y, bar.width - width - 8f, bar.height), bar.x, NormalsLabel.text, null, 0.6f, changed);
        }

        void DrawNormals()
        {
            serializedObject.Update();
            var prop = serializedObject.FindProperty(nameof(VAPSkinnedMeshRenderer.normalBlendShapes));
            var names = Blendshapes.Names(Owner.wrapped.sharedMesh);
            var added = new HashSet<string>();
            for (int i = 0; i < prop.arraySize; i++)
                added.Add(prop.GetArrayElementAtIndex(i).stringValue);

            var e = Event.current;
            bool enter = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && GUI.GetNameOfFocusedControl() == NormalsSearchControl;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.SetNextControlName(NormalsSearchControl);
                normalSearch = EditorGUILayout.TextField(normalSearch, EditorStyles.toolbarSearchField);
                string typed = (normalSearch ?? "").Trim();
                using (new EditorGUI.DisabledScope(typed.Length == 0 || added.Contains(typed)))
                    if (GUILayout.Button(AddLabel, GUILayout.Width(50f)))
                    {
                        Add(prop, typed);
                        normalSearch = "";
                        GUI.FocusControl(null);
                        GUIUtility.ExitGUI();
                    }
            }
            if (!string.IsNullOrEmpty(normalSearch))
            {
                var matches = names.Distinct()
                    .Where(n => !added.Contains(n) && n.IndexOf(normalSearch, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(n => n.StartsWith(normalSearch, System.StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ToList();
                if (enter)
                {
                    if (matches.Count > 0)
                        Add(prop, matches[0]);
                    else if (!added.Contains(normalSearch))
                        Add(prop, normalSearch);
                    e.Use();
                    GUIUtility.ExitGUI();
                }
                if (matches.Count == 0)
                    EditorGUILayout.LabelField("No matching blendshapes, click Enter or press Add", EditorStyles.miniLabel);
                suggestionStyle ??= new GUIStyle(EditorStyles.miniButton) { alignment = TextAnchor.MiddleLeft };
                foreach (var n in matches.Take(MaxSuggestions))
                    if (GUILayout.Button(new GUIContent(n, "Add " + n + "."), suggestionStyle))
                    {
                        Add(prop, n);
                        GUIUtility.ExitGUI();
                    }
                if (matches.Count > MaxSuggestions)
                    EditorGUILayout.LabelField($"{matches.Count - MaxSuggestions} more, keep typing", EditorStyles.miniLabel);
                GUILayout.Space(2f);
            }
            NormalsList(prop, new HashSet<string>(names)).DoLayoutList();
            GUILayout.Space(-18f);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(prop.arraySize == 0))
                    if (GUILayout.Button(CopyLabel, EditorStyles.miniButtonLeft, GUILayout.Width(50f)))
                        EditorGUIUtility.systemCopyBuffer = string.Join("\n", Enumerable.Range(0, prop.arraySize).Select(i => prop.GetArrayElementAtIndex(i).stringValue).Where(n => !string.IsNullOrEmpty(n)));
                if (GUILayout.Button(PasteLabel, EditorStyles.miniButtonRight, GUILayout.Width(50f)))
                {
                    foreach (var line in (EditorGUIUtility.systemCopyBuffer ?? "").Split('\n'))
                    {
                        string name = line.Trim();
                        if (name.Length > 0 && added.Add(name))
                            Add(prop, name);
                    }
                    GUIUtility.ExitGUI();
                }
                GUILayout.FlexibleSpace();
            }
            GUILayout.Space(-2f);
            serializedObject.ApplyModifiedProperties();
        }

        void Add(SerializedProperty prop, string name)
        {
            int index = prop.arraySize;
            prop.arraySize = index + 1;
            prop.GetArrayElementAtIndex(index).stringValue = name;
            prop.serializedObject.ApplyModifiedProperties();
        }

        ReorderableList NormalsList(SerializedProperty prop, HashSet<string> available)
        {
            normals ??= new ReorderableList(prop.serializedObject, prop, true, false, false, true)
            {
                headerHeight = 2f,
                elementHeight = EditorGUIUtility.singleLineHeight + 2f,
            };
            normals.serializedProperty = prop;
            normals.drawElementCallback = (rect, i, isActive, focused) =>
            {
                rect.y += 1f;
                rect.height = EditorGUIUtility.singleLineHeight;
                var element = prop.GetArrayElementAtIndex(i);
                VAPEditorGUI.StringField(rect, element);
                string value = element.stringValue;
                if (string.IsNullOrEmpty(value))
                    VAPEditorGUI.WarningIcon(rect, "Empty; type in the search field to pick a blendshape.");
                else if (!available.Contains(value))
                    VAPEditorGUI.WarningIcon(rect, "The mesh doesn't have it.");
            };
            return normals;
        }

        void ResizeHandle(float listHeight, float content)
        {
            var rect = GUILayoutUtility.GetRect(0f, 8f, GUILayout.ExpandWidth(true));
            int id = GUIUtility.GetControlID(FocusType.Passive);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeVertical);
            var e = Event.current;
            switch (e.GetTypeForControl(id))
            {
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
        static void Wrap(MenuCommand command)
        {
            var smr = (SkinnedMeshRenderer)command.context;
            Undo.SetCurrentGroupName("Wrap with VAP");
            int group = Undo.GetCurrentGroup();
            var w = Undo.AddComponent<VAPSkinnedMeshRenderer>(smr.gameObject);
            w.wrapped = smr;
            var components = smr.GetComponents<Component>();
            int index = System.Array.IndexOf(components, smr);
            for (int i = components.Length - 1; i > index; i--)
                if (!ComponentUtility.MoveComponentUp(w))
                    break;
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
        static void Unwrap(MenuCommand command)
        {
            var w = (VAPSkinnedMeshRenderer)command.context;
            Undo.SetCurrentGroupName("Unwrap Skinned Mesh Renderer");
            int group = Undo.GetCurrentGroup();
            WrappedToggles.Release(w, w.wrapped);
            Undo.CollapseUndoOperations(group);
        }

        static void Popup(SerializedProperty prop, GUIContent label, GUIContent[] names, int[] values)
        {
            var rect = EditorGUILayout.GetControlRect();
            label = EditorGUI.BeginProperty(rect, label, prop);
            EditorGUI.BeginChangeCheck();
            int v = EditorGUI.IntPopup(rect, label, prop.intValue, names, values);
            if (EditorGUI.EndChangeCheck())
                prop.intValue = v;
            EditorGUI.EndProperty();
        }
    }
}
#endif
