using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace VAP {
    public static class VAPAvatar {
        public const string Docs = "https://github.com/azukitiger/vrc-prefabs";

        public static VRCAvatarDescriptor Descriptor(Component c) =>
            c == null ? null : c.GetComponentInParent<VRCAvatarDescriptor>(true);

        public static int Depth(Transform t) {
            int depth = 0;
            for (; t.parent != null; t = t.parent) {
                depth++;
            }

            return depth;
        }

        public static IEnumerable<T> RootFirst<T>(IEnumerable<T> components) where T : Component =>
            components.Select((c, i) => (c, i)).OrderBy(x => Depth(x.c.transform)).ThenBy(x => x.i).Select(x => x.c);

        public static T First<T>(Component inAvatar, bool includeInactive = true) where T : Component {
            var descriptor = Descriptor(inAvatar);
            return descriptor == null ? null : RootFirst(descriptor.GetComponentsInChildren<T>(includeInactive)).FirstOrDefault();
        }

        public static T Other<T>(T c, bool includeInactive = true) where T : Component {
            var first = First<T>(c, includeInactive);
            return first == c ? null : first;
        }
    }
}
