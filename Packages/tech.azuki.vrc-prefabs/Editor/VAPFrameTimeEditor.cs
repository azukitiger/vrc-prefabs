#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.UIElements;

namespace VAP.Editor
{
    [CustomEditor(typeof(VAPFrameTime))]
    public class VAPFrameTimeEditor : UnityEditor.Editor
    {
        FrameTimeFactorsGUI factors;

        public override VisualElement CreateInspectorGUI() => VAPInspectorHeader.Create("Frame Time", DrawFields);

        void DrawFields()
        {
            if (target == null)
                return;
            serializedObject.Update();
            var f = (VAPFrameTime)target;
            EditorGUILayout.HelpBox("Frame Time is now a section of the VRC Avatar Descriptor component. Move these factors there.", MessageType.Warning);
            if (VAPAvatar.Descriptor(f) == null)
                EditorGUILayout.HelpBox(VAPEditorGUI.NotInAvatar, MessageType.Warning);
            factors ??= new FrameTimeFactorsGUI(serializedObject, nameof(VAPFrameTime.factors));
            factors.Draw(f.factors, VAPPrefabLock.IsFromPrefab(f));
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
