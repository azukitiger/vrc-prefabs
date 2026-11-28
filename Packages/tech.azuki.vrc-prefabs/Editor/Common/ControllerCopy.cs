#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace VAP.Editor {
    internal static class ControllerCopy {
        internal static IEnumerable<AnimatorController> FullControllers(Component vf) {
            if (vf == null) {
                yield break;
            }

            using var so = new SerializedObject(vf);
            var controllers = so.FindProperty("content.controllers");
            for (int i = 0; i < (controllers?.arraySize ?? 0); i++) {
                if (controllers.GetArrayElementAtIndex(i).FindPropertyRelative("controller.objRef")?.objectReferenceValue is AnimatorController c) {
                    yield return c;
                }
            }
        }

        internal static IEnumerable<AnimatorController> DescriptorControllers(VRCAvatarDescriptor descriptor) {
            if (descriptor == null) {
                return Enumerable.Empty<AnimatorController>();
            }

            return (descriptor.baseAnimationLayers ?? Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>())
                .Concat(descriptor.specialAnimationLayers ?? Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>())
                .Where(l => !l.isDefault)
                .Select(l => l.animatorController as AnimatorController)
                .Where(c => c != null)
                .Distinct();
        }

        internal static void SwapFullController(Component vf, Func<AnimatorController, AnimatorController> swap) {
            if (vf == null) {
                return;
            }

            using var so = new SerializedObject(vf);
            var controllers = so.FindProperty("content.controllers");
            if (controllers == null || !controllers.isArray) {
                return;
            }

            bool changed = false;
            for (int i = 0; i < controllers.arraySize; i++) {
                var wrapper = controllers.GetArrayElementAtIndex(i).FindPropertyRelative("controller");
                var objRef = wrapper?.FindPropertyRelative("objRef");
                if (!(objRef?.objectReferenceValue is AnimatorController controller) || !(swap(controller) is AnimatorController copy)) {
                    continue;
                }

                objRef.objectReferenceValue = copy;
                var id = wrapper.FindPropertyRelative("id");
                if (id != null) {
                    id.stringValue = "";
                }

                changed = true;
            }
            if (changed) {
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        internal static void SwapDescriptor(VRCAvatarDescriptor descriptor, Func<AnimatorController, AnimatorController> swap) {
            if (descriptor == null) {
                return;
            }

            var copies = new Dictionary<AnimatorController, AnimatorController>();
            VRCAvatarDescriptor.CustomAnimLayer[] Patch(VRCAvatarDescriptor.CustomAnimLayer[] layers) {
                if (layers == null) {
                    return null;
                }

                for (int i = 0; i < layers.Length; i++) {
                    if (layers[i].isDefault || !(layers[i].animatorController is AnimatorController controller)) {
                        continue;
                    }

                    if (!copies.TryGetValue(controller, out var copy)) {
                        copies[controller] = copy = swap(controller);
                    }

                    if (copy != null) {
                        layers[i].animatorController = copy;
                    }
                }
                return layers;
            }
            descriptor.baseAnimationLayers = Patch(descriptor.baseAnimationLayers);
            descriptor.specialAnimationLayers = Patch(descriptor.specialAnimationLayers);
        }

        internal static AnimatorController Rename(AnimatorController source, IReadOnlyDictionary<string, string> names, Action<Object> saveAsset) {
            if (source == null || names.Count == 0 || !source.parameters.Any(p => names.ContainsKey(p.name))) {
                return null;
            }

            var map = new Dictionary<Object, Object>();
            var copy = Clone(source, map);
            var parameters = copy.parameters;
            foreach (var p in parameters) {
                if (names.TryGetValue(p.name, out string to)) {
                    p.name = to;
                }
            }

            copy.parameters = parameters.GroupBy(p => p.name).Select(g => g.First()).ToArray();
            foreach (var o in map.Values.ToList()) {
                if (o is AnimatorController) {
                    continue;
                }

                RenameStrings(o, names);
                if (o is AnimatorState state) {
                    state.motion = RenameClip(state.motion, names, map);
                } else if (o is BlendTree tree) {
                    tree.children = tree.children.Select(c => {
                        c.motion = RenameClip(c.motion, names, map);
                        return c;
                    }).ToArray();
                }
            }
            foreach (var o in map.Values) {
                saveAsset?.Invoke(o);
            }

            return copy;
        }

        static readonly HashSet<string> KeepStrings = new HashSet<string> { "m_Name", "m_Tag" };

        static void RenameStrings(Object o, IReadOnlyDictionary<string, string> names) {
            using var so = new SerializedObject(o);
            var it = so.GetIterator();
            bool changed = false;
            bool enter = true;
            while (it.Next(enter)) {
                enter = it.propertyType != SerializedPropertyType.String;
                if (!enter && !KeepStrings.Contains(it.name) && names.TryGetValue(it.stringValue, out string to)) {
                    it.stringValue = to;
                    changed = true;
                }
            }
            if (changed) {
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static Motion RenameClip(Motion motion, IReadOnlyDictionary<string, string> names, Dictionary<Object, Object> map) {
            if (!(motion is AnimationClip clip)) {
                return motion;
            }

            if (map.TryGetValue(clip, out var done)) {
                return (Motion)done;
            }

            var bindings = AnimationUtility.GetCurveBindings(clip)
                .Where(b => b.type == typeof(Animator) && b.path == "" && names.ContainsKey(b.propertyName)).ToList();
            if (bindings.Count == 0) {
                return clip;
            }

            var copy = Object.Instantiate(clip);
            copy.name = clip.name;
            foreach (var b in bindings) {
                var curve = AnimationUtility.GetEditorCurve(copy, b);
                AnimationUtility.SetEditorCurve(copy, b, null);
                AnimationUtility.SetEditorCurve(copy, EditorCurveBinding.FloatCurve("", typeof(Animator), names[b.propertyName]), curve);
            }
            map[clip] = copy;
            return copy;
        }

        internal static AnimatorController Clone(AnimatorController source, Dictionary<Object, Object> map) {
            var copy = Object.Instantiate(source);
            copy.name = source.name;
            map[source] = copy;
            var layers = copy.layers;
            foreach (var layer in layers) {
                layer.stateMachine = CloneMachine(layer.stateMachine, map);
            }

            copy.layers = layers;

            foreach (var pair in map.ToList()) {
                if (pair.Value is AnimatorState state) {
                    state.transitions = ((AnimatorState)pair.Key).transitions.Select(t => CloneTransition(t, map)).ToArray();
                } else if (pair.Value is AnimatorStateMachine machine) {
                    var original = (AnimatorStateMachine)pair.Key;
                    machine.defaultState = Mapped(original.defaultState, map);
                    machine.entryTransitions = original.entryTransitions.Select(t => CloneTransition(t, map)).ToArray();
                    machine.anyStateTransitions = original.anyStateTransitions.Select(t => CloneTransition(t, map)).ToArray();
                    foreach (var child in original.stateMachines) {
                        if (child.stateMachine != null) {
                            machine.SetStateMachineTransitions(Mapped(child.stateMachine, map),
                                original.GetStateMachineTransitions(child.stateMachine).Select(t => CloneTransition(t, map)).ToArray());
                        }
                    }
                }
            }
            return copy;
        }

        static AnimatorStateMachine CloneMachine(AnimatorStateMachine source, Dictionary<Object, Object> map) {
            if (source == null) {
                return null;
            }

            if (map.TryGetValue(source, out var done)) {
                return (AnimatorStateMachine)done;
            }

            var copy = Object.Instantiate(source);
            copy.name = source.name;
            map[source] = copy;
            copy.states = source.states.Select(s => new ChildAnimatorState { position = s.position, state = CloneState(s.state, map) }).ToArray();
            copy.stateMachines = source.stateMachines.Select(s => new ChildAnimatorStateMachine { position = s.position, stateMachine = CloneMachine(s.stateMachine, map) }).ToArray();
            copy.behaviours = CloneBehaviours(source.behaviours, map);
            return copy;
        }

        static AnimatorState CloneState(AnimatorState source, Dictionary<Object, Object> map) {
            if (source == null) {
                return null;
            }

            if (map.TryGetValue(source, out var done)) {
                return (AnimatorState)done;
            }

            var copy = Object.Instantiate(source);
            copy.name = source.name;
            map[source] = copy;
            copy.motion = CloneTree(source.motion, map);
            copy.behaviours = CloneBehaviours(source.behaviours, map);
            return copy;
        }

        static Motion CloneTree(Motion motion, Dictionary<Object, Object> map) {
            if (!(motion is BlendTree source)) {
                return motion;
            }

            if (map.TryGetValue(source, out var done)) {
                return (Motion)done;
            }

            var copy = Object.Instantiate(source);
            copy.name = source.name;
            map[source] = copy;
            copy.children = source.children.Select(c => {
                c.motion = CloneTree(c.motion, map);
                return c;
            }).ToArray();
            return copy;
        }

        static StateMachineBehaviour[] CloneBehaviours(StateMachineBehaviour[] source, Dictionary<Object, Object> map) {
            return source.Where(b => b != null).Select(b => {
                var copy = Object.Instantiate(b);
                copy.name = b.name;
                map[b] = copy;
                return copy;
            }).ToArray();
        }

        static T CloneTransition<T>(T source, Dictionary<Object, Object> map) where T : AnimatorTransitionBase {
            var copy = Object.Instantiate(source);
            copy.name = source.name;
            map[source] = copy;
            if (source.destinationState != null) {
                copy.destinationState = Mapped(source.destinationState, map);
            }

            if (source.destinationStateMachine != null) {
                copy.destinationStateMachine = Mapped(source.destinationStateMachine, map);
            }

            return copy;
        }

        static T Mapped<T>(T source, Dictionary<Object, Object> map) where T : Object {
            if (source == null) {
                return null;
            }

            return map.TryGetValue(source, out var copy) ? (T)copy : source;
        }
    }
}
#endif
