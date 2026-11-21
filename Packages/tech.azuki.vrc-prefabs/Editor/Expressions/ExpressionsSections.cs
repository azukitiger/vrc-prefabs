#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using AnimatorController = UnityEditor.Animations.AnimatorController;
using UnityEngine;
using UnityEngine.UIElements;

namespace VAP.Editor
{
    internal class ExpressionsSections
    {
        static GUIStyle stateStyle;

        readonly UnityEditor.Editor editor;
        readonly Func<IEnumerable<AnimatorController>> controllers;
        FrameTimeFactorsGUI factors;
        PlayAudioClipsGUI clips;

        internal ExpressionsSections(UnityEditor.Editor editor, Func<IEnumerable<AnimatorController>> controllers)
        {
            this.editor = editor;
            this.controllers = controllers;
        }

        VAPExpressions Owner => editor.target as VAPExpressions;
        SerializedObject Serialized => editor.serializedObject;
        bool Locked => VAPPrefabLock.IsFromPrefab(Owner);
        PlayAudioClipsGUI Clips => clips ??= new PlayAudioClipsGUI(Serialized, nameof(VAPExpressions.audioClips));

        List<AnimatorController> AudioControllers() =>
            (controllers() ?? Enumerable.Empty<AnimatorController>()).Where(c => c != null).Distinct().Where(c => Clips.Found(c).Count > 0).ToList();

        internal VisualElement Build(Func<bool> visible = null)
        {
            var root = new VisualElement();
            string key = "VAP.Expressions." + editor.target.GetInstanceID();
            var frame = new VAPFoldout(key + ".FrameTime", DrawFrameTimeHeader, DrawFrameTimeBody, () => Owner != null && Owner.frameTime).Root;
            var audio = new VAPFoldout(key + ".Audio", DrawAudioHeader, DrawAudioBody).Root;
            void Refresh()
            {
                bool show = Owner != null && (visible == null || visible());
                frame.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                audio.style.display = show && AudioControllers().Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            Refresh();
            root.schedule.Execute(Refresh).Every(500);
            root.Add(frame);
            root.Add(audio);
            return root;
        }

        void DrawFrameTimeHeader(Rect bar)
        {
            if (Owner == null)
                return;
            Serialized.Update();
            var enabled = Serialized.FindProperty(nameof(VAPExpressions.frameTime));
            float line = EditorGUIUtility.singleLineHeight;
            var box = new Rect(bar.xMax - 16f, bar.y + (bar.height - line) * 0.5f, 16f, line);
            stateStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
            float width = stateStyle.CalcSize(new GUIContent("Disabled")).x + 22f;
            GUI.Label(new Rect(bar.xMax - width, bar.y, width - 20f, bar.height), enabled.boolValue ? "Enabled" : "Disabled", stateStyle);
            using (new EditorGUI.DisabledScope(Locked))
                enabled.boolValue = EditorGUI.Toggle(box, enabled.boolValue);
            Serialized.ApplyModifiedProperties();
            VRCFuryWrap.BarLabels(new Rect(bar.x, bar.y, bar.width - width - 8f, bar.height), bar.x, "Frame Time", null, 0.6f);
        }

        void DrawFrameTimeBody()
        {
            if (Owner == null)
                return;
            Serialized.Update();
            factors ??= new FrameTimeFactorsGUI(Serialized, nameof(VAPExpressions.frameTimeFactors));
            using (new EditorGUI.DisabledScope(!Owner.frameTime))
                factors.Draw(Owner.frameTimeFactors, Locked);
            Serialized.ApplyModifiedProperties();
        }

        void DrawAudioHeader(Rect bar)
        {
            int count = AudioControllers().Sum(c => Clips.Found(c).Count);
            VRCFuryWrap.BarLabels(bar, bar.x, "Play Audio Clips", count + (count == 1 ? " Play Audio" : " Play Audios"), 0.6f);
        }

        void DrawAudioBody()
        {
            if (Owner == null)
                return;
            Serialized.Update();
            var list = AudioControllers();
            foreach (var c in list)
            {
                if (list.Count > 1)
                {
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField(c.name, EditorStyles.boldLabel);
                }
                Clips.Draw(c, PlayAudioClips.Prefix(c));
            }
            Serialized.ApplyModifiedProperties();
        }
    }
}
#endif
