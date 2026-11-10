#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using UnityEditor;
using UnityEditor.Callbacks;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VAP.Editor
{
    internal static class OpenSubAssets
    {
        [OnOpenAsset(0)]
        static bool Open(int instanceId, int line)
        {
            var o = EditorUtility.InstanceIDToObject(instanceId);
            if (!(o is VRCExpressionParameters || o is VRCExpressionsMenu) || AssetDatabase.IsMainAsset(o))
                return false;
            Selection.activeObject = o;
            return true;
        }
    }
}
#endif
