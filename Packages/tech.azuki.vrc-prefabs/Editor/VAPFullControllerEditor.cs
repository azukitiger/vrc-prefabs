#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VAP.Editor
{
    [CustomEditor(typeof(VAPFullController))]
    public class VAPFullControllerEditor : VAPExpressionsEditor
    {
        static GUIStyle titleStyle, filesStyle;

        VAPFullController owner;
        Component wrapped;
        WrappedSection section;

        void OnEnable()
        {
            owner = target as VAPFullController;
            if (owner == null || targets.Length > 1)
                return;
            wrapped = VRCFuryWrap.Ensure(owner, FullControllerSource.FeatureType);
            WrappedToggles.HideShownEditors();
        }

        void OnDisable()
        {
            section?.Dispose();
            VRCFuryWrap.ScheduleRemoval(owner, wrapped);
        }

        void DrawFullController()
        {
            if (owner == null || wrapped == null)
                return;
            FullControllerSource.Resolve(owner);
            if (owner.RootMenus().Count == 0 && owner.ParameterAssets().Count == 0)
            {
                EditorGUILayout.HelpBox("The Full Controller below has no menu or parameters yet.", MessageType.Info);
                return;
            }
            DrawContents(owner);
        }

        void DrawInfo(Rect rect)
        {
            titleStyle ??= new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, padding = new RectOffset(0, 0, 0, 0) };
            filesStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, clipping = TextClipping.Clip };

            using var so = new SerializedObject(wrapped);
            var controllers = so.FindProperty("content.controllers");
            var names = Enumerable.Range(0, controllers?.arraySize ?? 0)
                .Select(i => controllers.GetArrayElementAtIndex(i).FindPropertyRelative("controller.objRef")?.objectReferenceValue)
                .Where(o => o != null).Select(o => o.name).ToList();
            string files = string.Join(", ", names);
            float filesWidth = files.Length == 0 ? 0f : Mathf.Min(filesStyle.CalcSize(new GUIContent(files)).x, rect.width * 0.6f);
            GUI.Label(new Rect(rect.x, rect.y, rect.width - filesWidth - 8f, rect.height), "Full Controller", titleStyle);
            if (filesWidth > 0f)
                GUI.Label(new Rect(rect.xMax - filesWidth, rect.y, filesWidth, rect.height), files, filesStyle);
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            if (targets.Length > 1)
                root.Add(new IMGUIContainer(() => EditorGUILayout.HelpBox("Editing several of these at once isn't supported.", MessageType.Info)));
            else if (wrapped == null)
                root.Add(VRCFuryWrap.MissingMessage("VRCFury Full Controller"));
            else
            {
                root.Add(new IMGUIContainer(DrawFullController));
                section?.Dispose();
                section = new WrappedSection(wrapped, "VAP.FullController.Open.", DrawInfo);
                root.Add(section.Root);
            }
            VAPInspectorHeader.Attach(root, "VRCFury Full Controller");
            return root;
        }
    }
}
#endif
