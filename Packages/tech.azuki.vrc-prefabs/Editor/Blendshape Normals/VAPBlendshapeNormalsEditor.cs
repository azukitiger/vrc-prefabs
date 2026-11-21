#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace VAP.Editor
{
    [CustomEditor(typeof(VAPBlendshapeNormals))]
    public class VAPBlendshapeNormalsEditor : UnityEditor.Editor
    {
        static readonly GUIContent MeshLabel = new GUIContent("Mesh",
            "Mesh to recalculate. Leave empty to use every mesh in the avatar that has the listed blendshapes.");
        static readonly GUIContent ListLabel = new GUIContent("Blendshapes",
            "Blendshapes whose normals and tangents are recalculated at build time the way Blender shades them.");
        static readonly GUIContent PickContent = new GUIContent("", "Pick a blendshape from this avatar's meshes.");

        SerializedProperty mesh;
        SerializedProperty names;
        ReorderableList list;
        Cached<HashSet<string>> available;

        void OnEnable()
        {
            mesh = serializedObject.FindProperty(nameof(VAPBlendshapeNormals.targetRenderer));
            names = serializedObject.FindProperty(nameof(VAPBlendshapeNormals.blendShapes));
            available = new Cached<HashSet<string>>(1.0, Available);
        }

        public override VisualElement CreateInspectorGUI() => VAPInspectorHeader.Create("Mesh Blendshape Normals", DrawFields, this);

        HashSet<string> Available()
        {
            var component = (VAPBlendshapeNormals)target;
            var descriptor = VAPAvatar.Descriptor(component);
            var set = new HashSet<string>();
            if (component.targetRenderer != null)
                set.UnionWith(Blendshapes.Names(component.targetRenderer.sharedMesh));
            else if (descriptor != null)
                foreach (var smr in descriptor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    set.UnionWith(Blendshapes.Names(smr.sharedMesh));
            return set;
        }

        void DrawFields()
        {
            if (target == null)
                return;
            bool locked = VAPPrefabLock.Draw(targets);
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(locked))
            {
                EditorGUILayout.PropertyField(mesh, MeshLabel);
                GetList(available.Value).DoLayoutList();
            }
            serializedObject.ApplyModifiedProperties();
        }

        ReorderableList GetList(HashSet<string> available)
        {
            if (list == null)
            {
                list = new ReorderableList(serializedObject, names, true, true, true, true);
                list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, ListLabel);
                list.elementHeightCallback = _ => VAPEditorGUI.Line + 2f;
            }
            list.drawElementCallback = (rect, index, active, focused) =>
            {
                var element = names.GetArrayElementAtIndex(index);
                rect.y += 1f;
                rect.height = VAPEditorGUI.Line;
                const float pickWidth = 20f;
                var field = new Rect(rect.x, rect.y, rect.width - pickWidth - 2f, rect.height);
                VAPEditorGUI.StringField(field, element);
                string value = element.stringValue;
                if (string.IsNullOrEmpty(value))
                    VAPEditorGUI.WarningIcon(field, "Empty; pick a blendshape.");
                else if (!available.Contains(value))
                    VAPEditorGUI.WarningIcon(field, mesh.objectReferenceValue != null ? "The mesh doesn't have it." : "No mesh in this avatar has it.");
                var pick = new Rect(rect.xMax - pickWidth, rect.y, pickWidth, rect.height);
                if (EditorGUI.DropdownButton(pick, PickContent, FocusType.Passive))
                {
                    var menu = new GenericMenu();
                    var set = VAPEditorGUI.Setter(element);
                    var options = available.OrderBy(n => n).ToList();
                    if (options.Count == 0)
                        menu.AddDisabledItem(new GUIContent("No blendshapes found"));
                    foreach (var n in options)
                    {
                        string name = n;
                        menu.AddItem(new GUIContent(VAPEditorGUI.MenuName(name)), value == name, () => set(name));
                    }
                    menu.DropDown(pick);
                }
            };
            return list;
        }
    }
}
#endif
