using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VAP
{
    public abstract class VAPBlendshapeSetBase : MonoBehaviour, IEditorOnly
    {
        public const string IconPath = "Packages/tech.azuki.vrc-prefabs/Editor/Icons/VAP Icon.png";

        public VRCAvatarDescriptor.FaceTrackingBlendshapesSet blendshapesSet =
            new VRCAvatarDescriptor.FaceTrackingBlendshapesSet { setName = "", setWeight = 1f };

        [Tooltip("Optional. How blendshapes are named for auto-fill. * is the tracking key name: " +
                 "\"*Debug\" maps JawOpen to JawOpenDebug, \"Debug*\" to DebugJawOpen, " +
                 "\"Debug*Debug\" to DebugJawOpenDebug. Leave empty to turn auto-fill off.")]
        public string nameFormat = "";

        [Tooltip("Meshes this component uses. Leave empty to use every Skinned Mesh Renderer on this " +
                 "object and its children that has blendshapes used by this set or matching the auto-fill format.")]
        public SkinnedMeshRenderer[] meshes = new SkinnedMeshRenderer[0];
    }
}
