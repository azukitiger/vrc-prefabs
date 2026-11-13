using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace VAP
{
    public static class VAPAvatar
    {
        public static VRCAvatarDescriptor Descriptor(Component c) =>
            c == null ? null : c.GetComponentInParent<VRCAvatarDescriptor>(true);

        public static T Other<T>(T c) where T : Component
        {
            var descriptor = Descriptor(c);
            if (descriptor == null)
                return null;
            foreach (var o in descriptor.GetComponentsInChildren<T>(true))
                if (o != c)
                    return o;
            return null;
        }
    }
}
