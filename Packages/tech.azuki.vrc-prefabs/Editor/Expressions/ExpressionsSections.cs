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
            bool changed = VRCFuryWrap.Changed(enabled) || VRCFuryWrap.Changed(Serialized.FindProperty(nameof(VAPExpressions.frameTimeFactors)));
            VAPEditorGUI.EnabledBar(bar, "Frame Time", enabled, Locked, changed);
            Serialized.ApplyModifiedProperties();
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
            var list = AudioControllers();
            int count = list.Sum(c => Clips.Found(c).Count);
            bool changed = list.Any(c => Clips.AnyChanged(c, PlayAudioClips.Prefix(c)));
            VRCFuryWrap.BarLabels(bar, bar.x, "Play Audio Clips", count + (count == 1 ? " Play Audio" : " Play Audios"), 0.6f, changed);
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
