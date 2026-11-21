#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
#if VAP_NDMF
using nadena.dev.ndmf;
#endif
using Object = UnityEngine.Object;

#if VAP_NDMF
[assembly: ExportsPlugin(typeof(VAP.Editor.ContactSmoothingPlugin))]
#endif

namespace VAP.Editor
{
    internal static class ContactSmoothing
    {
        const string Log = "[VAP Smoothing] ";
        internal const string Prefix = "VAP/Smooth/";
        const string StepSize = Prefix + "StepSizeOne";
        const string ReceiverType = "VRCContactReceiver";
        const string PhysBoneType = "VRCPhysBone";
        static readonly string[] PhysBoneSuffixes = { "_IsGrabbed", "_IsPosed", "_Angle", "_Stretch", "_Squish" };
        const string FrameTimeGuid = "d98fd14472eed544694d47b4b61947df";
        const string FrameTimePrefabGuid = "2e48dbcd6b001364bb18b2e986ba24cd";
        const string FrameTime = "FrameTime";
        const string TimeName = "Time";
        const string LastTimeName = "LastTime";
        const string Weight = "FXWeight";
        static readonly float[] SpeedSteps = { 0f, 0.2f, 0.364f, 0.663f, 1.207f, 2.197f, 4f };
        static readonly float[] SpeedValues = { 0f, 0.19f, 0.292f, 0.502f, 0.716f, 0.901f, 0.998f };

        internal struct Contact
        {
            internal string input, output, pass, delta, timer;
        }

        internal static string Parameter(Component receiver)
        {
            using var so = new SerializedObject(receiver);
            string value = so.FindProperty("parameter")?.stringValue;
            return string.IsNullOrEmpty(value) ? null : value;
        }

        internal static string SourceName(VAPSmoothing c) => c is VAPPhysBoneSmoothing ? "VRC PhysBone" : "VRC Contact Receiver";

        internal static List<string> Parameters(VAPSmoothing c)
        {
            bool physBone = c is VAPPhysBoneSmoothing;
            string type = physBone ? PhysBoneType : ReceiverType;
            return c.GetComponentsInChildren<Component>(true)
                .Where(x => x != null && x.GetType().Name == type)
                .Select(Parameter)
                .Where(p => p != null)
                .SelectMany(p => physBone ? PhysBoneSuffixes.Select(s => p + s) : new[] { p })
                .Distinct()
                .ToList();
        }

        internal static string Input(VAPSmoothing c) =>
            !string.IsNullOrEmpty(c.InputParameter) ? c.InputParameter : Parameters(c).FirstOrDefault();

        internal static string Output(VAPSmoothing c, string input) =>
            !string.IsNullOrEmpty(c.outputParameter) ? c.outputParameter : input == null ? null : "VAP/" + input;

        internal static string TimerFor(VAPSmoothing c, string output) =>
            !c.timer ? null : !string.IsNullOrEmpty(c.timerParameter) ? c.timerParameter : output == null ? null : output + "Timer";

        internal static Contact Resolve(VAPSmoothing c)
        {
            string input = Input(c);
            string output = Output(c, input);
            return new Contact
            {
                input = input,
                output = output,
                pass = c.passes == VAPSmoothing.PassMode.Double ? output + "/Pass" : null,
                delta = c.splitSmoothing ? output + "/Delta" : null,
                timer = TimerFor(c, output),
            };
        }

        internal static bool IsCustom(string smoothing) => string.IsNullOrEmpty(smoothing);

        static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        static string DefaultFor(VAPFrameTime.Kind kind, float value) =>
            Defaults().FirstOrDefault(d => d.kind == kind && Number(d.value) == Number(value))?.parameter;

        internal static string CustomParameter(float speed) =>
            DefaultFor(VAPFrameTime.Kind.Smoothing, speed) ?? Prefix + "Speed" + Number(speed);

        internal static string ValueName(VAPFrameTime.Factor f) =>
            f.kind == VAPFrameTime.Kind.Linear ? DefaultFor(f.kind, f.value) ?? Prefix + "Linear" + Number(f.value) : CustomParameter(f.value);

        internal static string SpeedParameter(string smoothing, float customSpeed) =>
            smoothing == VAPSmoothing.None ? null
            : IsCustom(smoothing) ? CustomParameter(customSpeed)
            : SmoothingSpeeds.Find(smoothing)?.parameter;

        internal static IEnumerable<(string smoothing, float customSpeed)> Sides(VAPSmoothing c)
        {
            yield return (c.smoothing, c.customSpeed);
            if (c.splitSmoothing)
                yield return (c.decreaseSmoothing, c.decreaseCustomSpeed);
        }

        static string SideProblem(string smoothing, float customSpeed, bool split)
        {
            if (smoothing == VAPSmoothing.None)
                return split ? null : "Smoothing can only be None when Split is on.";
            if (IsCustom(smoothing))
                return customSpeed > 0f ? null : "The custom speed must be above 0.";
            return SmoothingSpeeds.Find(smoothing) == null ? $"There is no smoothing named \"{smoothing}\"." : null;
        }

        internal static string Problem(VAPSmoothing c, Contact x)
        {
            if (x.input == null)
                return $"No {SourceName(c)} with a parameter on this object or its children.";
            if (!Parameters(c).Contains(x.input))
                return $"No {SourceName(c)} on this object or its children gives \"{x.input}\".";
            foreach (var (smoothing, speed) in Sides(c))
                if (SideProblem(smoothing, speed, c.splitSmoothing) is string problem)
                    return problem;
            if (c.splitSmoothing && c.smoothing == VAPSmoothing.None && c.decreaseSmoothing == VAPSmoothing.None)
                return "Both directions are None, so nothing is smoothed.";
            var reserved = new[] { Weight, x.pass, x.delta }.Where(p => p != null).ToArray();
            if (new[] { x.input, x.output, x.timer }.Any(p => p != null && reserved.Contains(p)))
                return string.Join(", ", reserved.Select(p => $"\"{p}\"")) + " are used inside the smoothing and can't be parameter names here.";
            if (x.output == x.input)
                return "The output parameter can't be the input parameter.";
            if (x.timer != null && (x.timer == x.input || x.timer == x.output))
                return "The timer parameter must be different from the input and output parameters.";
            return null;
        }

        internal static void Process(GameObject avatarRoot, Action<Object> saveAsset)
        {
            var all = avatarRoot.GetComponentsInChildren<VAPSmoothing>(true);
            var frameTimes = avatarRoot.GetComponentsInChildren<VAPFrameTime>(true);
            var fullControllers = avatarRoot.GetComponentsInChildren<VAPFullController>(true).Where(f => f.frameTime).ToArray();
            var vfType = VRCFuryWrap.FindType(VRCFuryWrap.VRCFuryType);
            var feature = VRCFuryWrap.FindType(FullControllerSource.FeatureType);
            var descriptors = avatarRoot.GetComponentsInChildren<VAPDescriptorExpressions>(true).Where(d => d.frameTime && d.Other() == null).ToArray();
            bool needed = fullControllers.Length > 0 || descriptors.Length > 0;
            if (all.Length == 0 && frameTimes.Length == 0 && !needed)
                return;
            if (vfType == null || feature == null)
                Debug.LogWarning(Log + "VRCFury is not installed; smoothing skipped.", avatarRoot);
            else
            {
                var outputs = new HashSet<string>();
                var inputs = new Dictionary<string, VAPSmoothing>();
                var speeds = new Dictionary<string, float>();
                var contacts = new List<(VAPSmoothing c, Contact x)>();
                bool any = false;
                foreach (var c in VAPAvatar.RootFirst(all))
                {
                    var contact = Resolve(c);
                    if (Problem(c, contact) is string problem)
                    {
                        Debug.LogWarning(Log + $"{c.name}: {problem} Skipped.", c);
                        continue;
                    }
                    if (inputs.TryGetValue(contact.input, out var owner))
                    {
                        Debug.LogError(Log + $"{c.name}: \"{contact.input}\" is already smoothed on {owner.name}, so this does nothing.", c);
                        continue;
                    }
                    inputs[contact.input] = c;
                    if (!outputs.Add(contact.output) || (contact.timer != null && !outputs.Add(contact.timer)))
                    {
                        Debug.LogWarning(Log + $"{c.name}: another smoothing already writes \"{contact.output}\" or its timer; skipped.", c);
                        continue;
                    }
                    contacts.Add((c, contact));
                    any = true;
                    foreach (var (smoothing, speed) in Sides(c))
                        if (IsCustom(smoothing))
                            speeds[CustomParameter(speed)] = speed;
                }
                var linear = new Dictionary<string, float>();
                var renames = new Dictionary<Component, Dictionary<string, string>>();
                var ordered = VAPAvatar.RootFirst(frameTimes.Cast<Component>().Concat(fullControllers).Concat(descriptors)).ToList();
                foreach (var f in ordered)
                {
                    var own = new HashSet<string>();
                    foreach (var factor in FactorsOf(f))
                    {
                        if (FactorProblem(factor) is string problem)
                        {
                            Debug.LogWarning(Log + $"{f.name}: {problem} Skipped.", f);
                            continue;
                        }
                        if (!own.Add(factor.parameter))
                        {
                            Debug.LogWarning(Log + $"{f.name}: \"{factor.parameter}\" is listed more than once; only the first one is used.", f);
                            continue;
                        }
                        string name = ValueName(factor);
                        if (name != factor.parameter)
                        {
                            if (!renames.TryGetValue(f, out var names))
                                renames[f] = names = new Dictionary<string, string>();
                            names[factor.parameter] = name;
                        }
                        if (factor.kind == VAPFrameTime.Kind.Linear)
                            linear[name] = factor.value;
                        else
                            speeds[name] = factor.value;
                    }
                }
                if (any || frameTimes.Length > 0 || needed)
                {
                    foreach (var pair in renames)
                        Rename(pair.Key, pair.Value, saveAsset);
                    var globals = AddFrameTime(avatarRoot, vfType, feature, speeds, linear, contacts, saveAsset);
                    ExposeFrameTime(avatarRoot, vfType, globals);
                }
            }
            foreach (var c in all)
                Object.DestroyImmediate(c);
            foreach (var f in frameTimes)
                Object.DestroyImmediate(f);
        }

        static IEnumerable<string> Globals(VAPSmoothing c, Contact x)
        {
            yield return x.input;
            yield return x.output;
            foreach (var (smoothing, speed) in Sides(c))
                if (SpeedParameter(smoothing, speed) is string p)
                    yield return p;
            if (x.timer != null)
            {
                yield return x.timer;
                yield return StepSize;
            }
        }

        internal static IEnumerable<VAPFrameTime.Factor> Defaults()
        {
            foreach (var entry in SmoothingSpeeds.Entries)
                yield return new VAPFrameTime.Factor { parameter = entry.parameter, kind = VAPFrameTime.Kind.Smoothing, value = entry.speed };
            yield return new VAPFrameTime.Factor { parameter = StepSize, kind = VAPFrameTime.Kind.Linear, value = 1f };
        }

        static IEnumerable<VAPFrameTime.Factor> FactorsOf(Component c) =>
            c is VAPFrameTime ft ? ft.factors : ((VAPExpressions)c).frameTimeFactors;

        static void Rename(Component owner, Dictionary<string, string> names, Action<Object> saveAsset)
        {
            AnimatorController Renamed(AnimatorController controller) => ControllerCopy.Rename(controller, names, saveAsset);
            if (owner is VAPFullController f)
                ControllerCopy.SwapFullController(f.wrapped, Renamed);
            else if (owner is VAPDescriptorExpressions d)
                ControllerCopy.SwapDescriptor(d.Descriptor, Renamed);
        }

        static void ExposeFrameTime(GameObject avatarRoot, Type vfType, IEnumerable<string> parameters)
        {
            var names = new HashSet<string>(parameters);
            foreach (var vf in avatarRoot.GetComponentsInChildren(vfType, true))
            {
                var used = ControllerCopy.FullControllers(vf).SelectMany(c => c.parameters).Select(p => p.name).Where(names.Contains).Distinct().ToList();
                if (used.Count == 0)
                    continue;
                using var so = new SerializedObject(vf);
                var globals = so.FindProperty("content.globalParams");
                if (globals == null || !globals.isArray)
                    continue;
                var existing = new HashSet<string>();
                for (int i = 0; i < globals.arraySize; i++)
                    existing.Add(globals.GetArrayElementAtIndex(i).stringValue);
                if (existing.Contains("*"))
                    continue;
                foreach (var p in used.Where(p => !existing.Contains(p)))
                    globals.GetArrayElementAtIndex(globals.arraySize++).stringValue = p;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        internal static VAPSmoothing SmoothedBy(VAPSmoothing c)
        {
            var root = VAPAvatar.Descriptor(c);
            string input = Input(c);
            if (root == null || input == null)
                return null;
            foreach (var other in VAPAvatar.RootFirst(root.GetComponentsInChildren<VAPSmoothing>(true)))
            {
                if (other == c)
                    return null;
                if (Input(other) == input && Problem(other, Resolve(other)) == null)
                    return other;
            }
            return null;
        }



        internal static string FactorProblem(VAPFrameTime.Factor f)
        {
            if (string.IsNullOrEmpty(f.parameter) || f.parameter == Prefix)
                return "A factor has no parameter name.";
            if (!f.parameter.StartsWith(Prefix))
                return $"\"{f.parameter}\" must start with \"{Prefix}\".";
            if (Defaults().Any(d => d.parameter == f.parameter))
                return $"\"{f.parameter}\" is already added by default.";
            if (f.kind == VAPFrameTime.Kind.Smoothing && f.value <= 0f)
                return $"\"{f.parameter}\" needs a speed above 0.";
            return null;
        }

        static List<string> AddFrameTime(GameObject avatarRoot, Type vfType, Type feature, Dictionary<string, float> customSpeeds,
            Dictionary<string, float> customLinear, List<(VAPSmoothing c, Contact x)> contacts, Action<Object> saveAsset)
        {
            RemoveFrameTime(avatarRoot, vfType);
            var speeds = new Dictionary<string, float>();
            var linear = new Dictionary<string, float>();
            foreach (var f in Defaults())
                (f.kind == VAPFrameTime.Kind.Linear ? linear : speeds)[f.parameter] = f.value;
            foreach (var pair in customSpeeds)
                speeds[pair.Key] = pair.Value;
            foreach (var pair in customLinear)
                linear[pair.Key] = pair.Value;
            var made = new List<Object>();
            var controller = FrameTimeController(speeds, linear, contacts, made);
            foreach (var o in made)
                saveAsset?.Invoke(o);
            var globals = speeds.Keys.Concat(linear.Keys).Concat(contacts.SelectMany(x => Globals(x.c, x.x))).Distinct().ToList();
            AddFullController(avatarRoot, vfType, feature, controller, globals);
            return globals;
        }

        static void RemoveFrameTime(GameObject avatarRoot, Type vfType)
        {
            var objects = new HashSet<GameObject>();
            foreach (var t in avatarRoot.GetComponentsInChildren<Transform>(true))
                if (t != avatarRoot.transform && IsFrameTimePrefab(t.gameObject))
                    objects.Add(t.gameObject);
            foreach (var vf in avatarRoot.GetComponentsInChildren(vfType, true))
            {
                using var so = new SerializedObject(vf);
                var controllers = so.FindProperty("content.controllers");
                if (controllers == null || !controllers.isArray)
                    continue;
                bool removed = false;
                for (int i = controllers.arraySize - 1; i >= 0; i--)
                {
                    var wrapper = controllers.GetArrayElementAtIndex(i).FindPropertyRelative("controller");
                    string id = wrapper?.FindPropertyRelative("id")?.stringValue ?? "";
                    var obj = wrapper?.FindPropertyRelative("objRef")?.objectReferenceValue;
                    if (id.StartsWith(FrameTimeGuid) ||
                        (obj != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string guid, out long _) && guid == FrameTimeGuid))
                    {
                        controllers.DeleteArrayElementAtIndex(i);
                        removed = true;
                    }
                }
                if (!removed)
                    continue;
                so.ApplyModifiedPropertiesWithoutUndo();
                if (controllers.arraySize > 0)
                    continue;
                var go = vf.gameObject;
                Object.DestroyImmediate(vf);
                if (go != avatarRoot && go.transform.childCount == 0 && go.GetComponents<Component>().Length == 1)
                    objects.Add(go);
            }
            foreach (var go in objects)
                if (go != null)
                    Object.DestroyImmediate(go);
        }

        static bool IsFrameTimePrefab(GameObject go)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
            return source != null && source.transform.parent == null
                && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long _) && guid == FrameTimePrefabGuid;
        }

        static AnimatorController FrameTimeController(Dictionary<string, float> speeds, Dictionary<string, float> linear,
            List<(VAPSmoothing c, Contact x)> contacts, List<Object> made)
        {
            var controller = new AnimatorController { name = "VAP Frame Time Controller" };
            made.Add(controller);
            AddFloat(controller, Weight, 1f);
            AddFloat(controller, TimeName, 0f);
            AddFloat(controller, LastTimeName, 0f);
            AddFloat(controller, FrameTime, 0f);
            foreach (var p in linear.Keys)
                AddFloat(controller, p, 0f);
            var groups = speeds.GroupBy(s => s.Value, s => s.Key).Select(g => (speed: g.Key, parameters: g.ToArray())).ToList();
            var rates = groups.ToDictionary(g => g.parameters[0], g => g.parameters[0] + "K");
            foreach (var p in speeds.Keys)
                AddFloat(controller, p, 0f);
            foreach (var r in rates.Values)
                AddFloat(controller, r, 0f);

            var time = new AnimationClip { name = "Time" };
            AnimationUtility.SetEditorCurve(time, EditorCurveBinding.FloatCurve("", typeof(Animator), TimeName),
                AnimationCurve.Linear(0f, 0f, 10000000f, 10000000f));
            made.Add(time);

            var children = new List<ChildMotion>
            {
                Child(Clip("Reset Frame Time Logic AAP", new[] { TimeName, LastTimeName, FrameTime }, 0f, made), Weight),
                Child(time, Weight),
                Child(Clip("Frame Time & Last Time", new[] { FrameTime, LastTimeName }, 1f, made), TimeName),
                Child(Clip("Frame Time -1", new[] { FrameTime }, -1f, made), LastTimeName),
                Child(Clip("Reset Frame Time Smoothing AAP", speeds.Keys.Concat(rates.Values).Concat(linear.Keys), 0f, made), Weight),
                Child(Clip("Smoothing Values", groups.Select(g => (rates[g.parameters[0]], g.speed)).Concat(linear.Select(l => (l.Key, l.Value))), made), FrameTime),
            };
            foreach (var (_, parameters) in groups)
            {
                string p = parameters[0];
                var steps = new ChildMotion[SpeedSteps.Length];
                for (int i = 0; i < steps.Length; i++)
                    steps[i] = Child(Clip(p + " " + SpeedValues[i].ToString("0.###", CultureInfo.InvariantCulture),
                        parameters, SpeedValues[i], made), Weight, SpeedSteps[i]);
                children.Add(Child(Tree(string.Join(", ", parameters), BlendTreeType.Simple1D, rates[p], made, steps), Weight));
            }
            foreach (var (c, x) in contacts)
                children.Add(Child(ContactTree(c, x, controller, made), Weight));
            Layer(controller, "Frame Time", Tree("Frame Time", BlendTreeType.Direct, null, made, children.ToArray()), made);
            return controller;
        }

        static void Layer(AnimatorController controller, string name, Motion motion, List<Object> made)
        {
            var machine = new AnimatorStateMachine { name = name, hideFlags = HideFlags.HideInHierarchy };
            var state = machine.AddState(name);
            state.motion = motion;
            state.writeDefaultValues = true;
            machine.defaultState = state;
            made.Add(machine);
            made.Add(state);
            controller.layers = new[] { new AnimatorControllerLayer { name = name, defaultWeight = 1f, stateMachine = machine } };
        }

        static void AddFullController(GameObject owner, Type vfType, Type feature, RuntimeAnimatorController controller, IEnumerable<string> globals)
        {
            var vf = owner.AddComponent(vfType);
            using var so = new SerializedObject(vf);
            so.FindProperty("content").managedReferenceValue = Activator.CreateInstance(feature, true);
            so.ApplyModifiedPropertiesWithoutUndo();
            so.Update();
            var content = so.FindProperty("content");
            var controllers = content.FindPropertyRelative("controllers");
            controllers.arraySize = 1;
            var entry = controllers.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("controller.objRef").objectReferenceValue = controller;
            entry.FindPropertyRelative("type").intValue = (int)VRCAvatarDescriptor.AnimLayerType.FX;
            var list = globals.ToList();
            var globalParams = content.FindPropertyRelative("globalParams");
            globalParams.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++)
                globalParams.GetArrayElementAtIndex(i).stringValue = list[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Motion ContactTree(VAPSmoothing c, Contact x, AnimatorController controller, List<Object> made)
        {
            string increase = SpeedParameter(c.smoothing, c.customSpeed);
            string decrease = c.splitSmoothing ? SpeedParameter(c.decreaseSmoothing, c.decreaseCustomSpeed) : null;
            foreach (string p in new[] { x.input, x.output, x.pass, x.delta, increase, decrease }.Where(p => p != null))
                AddFloat(controller, p, 0f);
            if (x.timer != null)
            {
                AddFloat(controller, x.timer, 0f);
                AddFloat(controller, StepSize, 0f);
            }

            var reset = Clip("Reset Contact Smoothing AAP",
                new[] { x.output, x.pass, x.delta, x.timer }.Where(p => p != null), 0f, made);
            var outClip = Clip(x.output, new[] { x.output }, 1f, made);
            var passClip = x.pass == null ? null : Clip(x.pass, new[] { x.pass }, 1f, made);
            ChildMotion[] Write(string pass, string output) => passClip == null
                ? new[] { Child(outClip, output) }
                : new[] { Child(passClip, pass), Child(outClip, output) };
            BlendTree keep = null, step = null, snap = null;
            Motion Follow(string speed, string label)
            {
                if (speed == null)
                    return snap ??= Tree("Direct", BlendTreeType.Direct, null, made, Write(x.input, x.input));
                keep ??= Tree("Output", BlendTreeType.Direct, null, made, Write(x.pass, x.output));
                step ??= Tree("Input", BlendTreeType.Direct, null, made, Write(x.input, x.pass ?? x.input));
                return Tree(label, BlendTreeType.Simple1D, speed, made, Child(keep, Weight, 0f), Child(step, Weight, 1f));
            }

            var children = new List<ChildMotion> { Child(reset, Weight) };
            if (!c.splitSmoothing)
                children.Add(Child(Follow(increase, "Smooth Setup"), Weight));
            else
            {
                var delta = Tree("Delta", BlendTreeType.Direct, null, made,
                    Child(Clip("Delta 1", new[] { x.delta }, 1f, made), x.input),
                    Child(Clip("Delta -1", new[] { x.delta }, -1f, made), x.output));
                children.Add(Child(delta, Weight));
                children.Add(Child(Tree("Direction", BlendTreeType.Simple1D, x.delta, made,
                    Child(Follow(decrease, "Decrease Smoothing"), Weight, 0.00001f),
                    Child(Follow(increase, "Increase Smoothing"), Weight, 0.00002f)), Weight));
            }
            if (x.timer != null)
            {
                var empty = Clip("Single Frame", Enumerable.Empty<string>(), 0f, made);
                var timerClip = Clip(x.timer, new[] { x.timer }, 1f, made);
                var count = Tree("Timer Count", BlendTreeType.Direct, null, made, Child(timerClip, x.timer), Child(timerClip, StepSize));
                children.Add(Child(Tree("Timer", BlendTreeType.Simple1D, x.input, made,
                    Child(empty, Weight, 0.00001f), Child(count, Weight, 0.00002f)), Weight));
            }
            return Tree(x.output, BlendTreeType.Direct, null, made, children.ToArray());
        }

        static void AddFloat(AnimatorController controller, string name, float value)
        {
            if (controller.parameters.Any(p => p.name == name))
                return;
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = name,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = value,
            });
        }

        static AnimationClip Clip(string name, IEnumerable<string> parameters, float value, List<Object> made) =>
            Clip(name, parameters.Select(p => (p, value)), made);

        static AnimationClip Clip(string name, IEnumerable<(string parameter, float value)> values, List<Object> made)
        {
            var clip = new AnimationClip { name = name };
            foreach (var (p, v) in values)
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), p),
                    new AnimationCurve(new Keyframe(0f, v)));
            made.Add(clip);
            return clip;
        }

        static BlendTree Tree(string name, BlendTreeType type, string parameter, List<Object> made, params ChildMotion[] children)
        {
            var tree = new BlendTree
            {
                name = name,
                blendType = type,
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            if (parameter != null)
                tree.blendParameter = parameter;
            tree.children = children;
            made.Add(tree);
            return tree;
        }

        static ChildMotion Child(Motion motion, string weight, float threshold = 0f) =>
            new ChildMotion { motion = motion, directBlendParameter = weight, threshold = threshold, timeScale = 1f };
    }

#if VAP_NDMF
    public class ContactSmoothingPlugin : Plugin<ContactSmoothingPlugin>
    {
        public override string QualifiedName => "tech.azuki.vrc-prefabs.contact-smoothing";
        public override string DisplayName => "VAP Contact Smoothing";

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .AfterPlugin("tech.azuki.vrc-prefabs.play-audio-clips")
                .BeforePlugin("tech.azuki.vrc-prefabs.expressions")
                .Run("Add contact smoothing", ctx =>
                    ContactSmoothing.Process(ctx.AvatarRootObject, o => ctx.AssetSaver.SaveAsset(o)));
        }
    }
#endif
}
#endif
