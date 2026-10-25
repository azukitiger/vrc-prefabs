using UnityEngine;

namespace VAP
{
    // A VAP component that wraps a VRCFury component on the same object.
    public interface IVRCFuryWrapper
    {
        Component Wrapped { get; set; }
    }
}
