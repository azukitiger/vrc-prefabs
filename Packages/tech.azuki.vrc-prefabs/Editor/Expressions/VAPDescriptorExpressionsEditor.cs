#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using AnimatorController = UnityEditor.Animations.AnimatorController;
using UnityEngine;
using UnityEngine.UIElements;

namespace VAP.Editor {
    [CustomEditor(typeof(VAPDescriptorExpressions))]
    public class VAPDescriptorExpressionsEditor : VAPExpressionsEditor {
        const string Title = "VRC Avatar Descriptor Enhancements";
        static readonly GUIContent FoundLabel = new GUIContent("Avatar Descriptor found",
            "The menu, parameters and playable layers come from the avatar's descriptor. The files are never changed; values set below are applied to copies at build time.");

        VAPDescriptorExpressions Owner => target as VAPDescriptorExpressions;

        bool Usable => Owner != null && Owner.Descriptor != null && Owner.Other() == null;

        void DrawStatus() {
            var c = Owner;
            if (c == null) {
                return;
            }

            if (c.Descriptor == null) {
                return;
            }

            if (c.Other() is VAPDescriptorExpressions other) {
                EditorGUILayout.HelpBox(VAPEditorGUI.OnlyOne(Title, other), MessageType.Error);
                return;
            }
            EditorGUILayout.LabelField(FoundLabel, EditorStyles.miniLabel);
        }

        void DrawMenu() {
            if (!Usable) {
                return;
            }

            var c = Owner;
            if (c.RootMenus().Count == 0 && c.ParameterAssets().Count == 0) {
                EditorGUILayout.Space(6f);
                EditorGUILayout.HelpBox("The descriptor has no Expressions Menu or Parameters.", MessageType.Info);
                return;
            }
            DrawContents(c);
        }

        IEnumerable<AnimatorController> Controllers() => ControllerCopy.DescriptorControllers(Owner != null ? Owner.Descriptor : null).ToList();

        public override VisualElement CreateInspectorGUI() {
            var root = new VisualElement();
            root.Add(new IMGUIContainer(DrawStatus));
            root.Add(new ExpressionsSections(this, Controllers).Build(() => Owner != null && Owner.Other() == null));
            var menu = new IMGUIContainer(DrawMenu);
            menu.style.marginTop = 2f;
            root.Add(menu);
            VAPInspectorHeader.Attach(root, Title, this);
            return VAPPrefabLock.AddEditButton(root, this);
        }
    }
}
#endif
