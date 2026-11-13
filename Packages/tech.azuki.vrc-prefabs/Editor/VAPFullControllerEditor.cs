#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VAP.Editor
{
    [CustomEditor(typeof(VAPFullController))]
    public class VAPFullControllerEditor : VAPExpressionsEditor
    {
        readonly WrappedEditor wrap = new WrappedEditor(FullControllerSource.FeatureType, "VRCFury Full Controller", "VAP.FullController.Open.");

        VAPFullController Owner => (VAPFullController)wrap.Owner;
        Component Wrapped => wrap.Wrapped;

        void OnEnable() => wrap.Enable(this);

        void OnDisable() => wrap.Disable();

        void DrawFullController()
        {
            if (Owner == null || Wrapped == null)
                return;
            FullControllerSource.Resolve(Owner);
            if (Owner.RootMenus().Count == 0 && Owner.ParameterAssets().Count == 0)
            {
                EditorGUILayout.HelpBox("The Full Controller below has no menu or parameters yet.", MessageType.Info);
                return;
            }
            DrawContents(Owner);
        }

        void DrawInfo(Rect rect)
        {
            using var so = new SerializedObject(Wrapped);
            var controllers = so.FindProperty("content.controllers");
            var names = Enumerable.Range(0, controllers?.arraySize ?? 0)
                .Select(i => controllers.GetArrayElementAtIndex(i).FindPropertyRelative("controller.objRef")?.objectReferenceValue)
                .Where(o => o != null).Select(o => o.name).ToList();
            VRCFuryWrap.BarLabels(rect, rect.x, "Full Controller", string.Join(", ", names), 0.6f);
        }

        public override VisualElement CreateInspectorGUI() => wrap.Build(this, DrawFullController, DrawInfo);
    }
}
#endif
