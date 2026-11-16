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
        static readonly GUIContent ListLabel = new GUIContent("Blendshapes",
            "Blendshapes whose normals and tangents are recalculated at build time the way Blender shades them, on every mesh in the avatar that has them.");
        static readonly GUIContent PickContent = new GUIContent("", "Pick a blendshape from this avatar's meshes.");

        SerializedProperty names;
        ReorderableList list;
        Cached<HashSet<string>> available;

        void OnEnable()
        {
            names = serializedObject.FindProperty(nameof(VAPBlendshapeNormals.blendShapes));
            available = new Cached<HashSet<string>>(1.0, Available);
        }

        public override VisualElement CreateInspectorGUI() => VAPInspectorHeader.Create("Blendshape Normals", DrawFields);

        HashSet<string> Available()
        {
            var descriptor = VAPAvatar.Descriptor((Component)target);
            var set = new HashSet<string>();
            if (descriptor != null)
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

            if (VAPAvatar.Descriptor((Component)target) == null)
                EditorGUILayout.HelpBox(VAPEditorGUI.NotInAvatar, MessageType.Warning);

            using (new EditorGUI.DisabledScope(locked))
            {
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
                    VAPEditorGUI.WarningIcon(field, "No mesh in this avatar has it.");
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
                        menu.AddItem(new GUIContent(name.Replace("/", "\u2215")), value == name, () => set(name));
                    }
                    menu.DropDown(pick);
                }
            };
            return list;
        }
    }
}
#endif
