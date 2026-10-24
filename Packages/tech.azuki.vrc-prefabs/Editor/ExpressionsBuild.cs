#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
#if VAP_NDMF
using nadena.dev.ndmf;
#endif
using Object = UnityEngine.Object;

#if VAP_NDMF
[assembly: ExportsPlugin(typeof(VAP.Editor.ExpressionsPlugin))]
#endif

namespace VAP.Editor
{
    internal static class ExpressionsBuild
    {
        const string Log = "[VAP VRC Menu & Parameters] ";

        internal static void Process(GameObject avatarRoot, Action<Object> saveAsset)
        {
            // Each component's asset is replaced by a copy with this instance's values.
            var swaps = new List<(Component owner, Object original, Object copy)>();
            var components = avatarRoot.GetComponentsInChildren<VAPExpressions>(true);
            foreach (var c in components)
            {
                if (c.parametersAsset == null && c.menuAsset == null)
                {
                    Debug.LogWarning(Log + $"{c.name}: VRC Menu & Parameters has no assets; skipped.", c);
                    continue;
                }
                VRCExpressionParameters parametersCopy = null;
                if (c.parametersAsset != null)
                {
                    parametersCopy = ScriptableObject.CreateInstance<VRCExpressionParameters>();
                    parametersCopy.name = c.parametersAsset.name + " (VAP)";
                    parametersCopy.parameters = c.BuildParameters();
                    swaps.Add((c, c.parametersAsset, parametersCopy));
                }
                if (c.menuAsset != null)
                {
                    var menuCopy = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                    menuCopy.name = c.menuAsset.name + " (VAP)";
                    menuCopy.controls = c.BuildControls(c.menuAsset);
                    menuCopy.Parameters = parametersCopy != null ? parametersCopy : c.menuAsset.Parameters;
                    swaps.Add((c, c.menuAsset, menuCopy));
                }
                foreach (var sub in c.SubMenus())
                {
                    var subCopy = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                    subCopy.name = sub.name + " (VAP)";
                    subCopy.controls = c.BuildControls(sub);
                    subCopy.Parameters = parametersCopy != null ? parametersCopy : sub.Parameters;
                    swaps.Add((c, sub, subCopy));
                }
            }

            // Menu copies point at the other copies (sub menus, their parameters).
            var copyOf = new Dictionary<Object, Object>();
            foreach (var (_, original, copy) in swaps)
                if (!copyOf.ContainsKey(original))
                    copyOf[original] = copy;
            foreach (var (_, _, copy) in swaps)
                if (copy is VRCExpressionsMenu m)
                {
                    if (m.Parameters != null && copyOf.TryGetValue(m.Parameters, out var p))
                        m.Parameters = (VRCExpressionParameters)p;
                    foreach (var control in m.controls)
                        if (control?.subMenu != null && copyOf.TryGetValue(control.subMenu, out var sub))
                            control.subMenu = (VRCExpressionsMenu)sub;
                }

            foreach (var (owner, original, copy) in swaps)
            {
                saveAsset?.Invoke(copy);
                // References under this component's object first; if there are none, the whole avatar.
                int replaced = Replace(owner.gameObject, original, copy, owner);
                if (replaced == 0)
                    replaced = Replace(avatarRoot, original, copy, owner);
                bool inMenu = swaps.Any(s => s.copy is VRCExpressionsMenu m && m.controls.Any(c => c?.subMenu == copy));
                if (replaced == 0 && !inMenu)
                    Debug.LogWarning(Log + $"{owner.name}: nothing on the avatar references \"{original.name}\", so this instance's values aren't used.", owner);
                else if (replaced > 0)
                    Debug.Log(Log + $"{owner.name}: {replaced} reference{(replaced == 1 ? "" : "s")} to \"{original.name}\" now use this instance's values.", owner);
            }
            foreach (var c in components)
                Object.DestroyImmediate(c);
        }

        static readonly HashSet<string> PlainTypes = new HashSet<string>
        {
            "float", "double", "int", "uint", "long", "ulong", "short", "ushort", "byte", "sbyte", "bool", "char", "string",
            "Vector2", "Vector3", "Vector4", "Quaternion", "Color", "Matrix4x4", "BoneWeight",
        };

        static int Replace(GameObject root, Object from, Object to, Component skip)
        {
            int count = 0;
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component == skip || component is Transform)
                    continue;
                using var so = new SerializedObject(component);
                var it = so.GetIterator();
                bool enter = true;
                bool changed = false;
                while (it.Next(enter))
                {
                    // Don't walk into arrays of plain values (e.g. mesh or curve data); they hold no references.
                    enter = !(it.isArray && it.propertyType == SerializedPropertyType.Generic && PlainTypes.Contains(it.arrayElementType))
                            && it.propertyType != SerializedPropertyType.String;
                    if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue == from)
                    {
                        it.objectReferenceValue = to;
                        changed = true;
                        count++;
                    }
                }
                if (changed)
                    so.ApplyModifiedPropertiesWithoutUndo();
            }
            return count;
        }
    }

#if VAP_NDMF
    public class ExpressionsPlugin : Plugin<ExpressionsPlugin>
    {
        public override string QualifiedName => "tech.azuki.vrc-prefabs.expressions";
        public override string DisplayName => "VAP VRC Menu & Parameters";

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .Run("Apply parameter start values and menu names", ctx =>
                    ExpressionsBuild.Process(ctx.AvatarRootObject, o => ctx.AssetSaver.SaveAsset(o)));
        }
    }
#endif
}
#endif
