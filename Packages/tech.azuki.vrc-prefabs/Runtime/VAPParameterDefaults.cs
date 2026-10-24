using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;

namespace VAP
{
    [AddComponentMenu("VAP/VRC Parameter Defaults")]
    [Icon(VAPFaceTrackingDebug.IconPath)]
    public class VAPParameterDefaults : MonoBehaviour, IEditorOnly
    {
        public List<Entry> defaults = new List<Entry>();

        [Serializable]
        public class Entry
        {
            [Tooltip("Final parameter name, e.g. a VRCFury Global Parameter.")]
            public string name = "";
            public VRCExpressionParameters.ValueType valueType = VRCExpressionParameters.ValueType.Bool;
            public float value;
        }
    }
}
