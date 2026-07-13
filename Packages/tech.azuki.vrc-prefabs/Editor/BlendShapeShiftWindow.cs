#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace VAP.Editor
{
    /// <summary>
    /// Standalone tool window (not a native inspector extension - Unity doesn't allow
    /// hooking custom context menus into the built-in SkinnedMeshRenderer blendshape list).
    ///
    /// Lists blendshapes on a chosen SkinnedMeshRenderer with their current weights.
    /// Right-click a row to shift that value (and everything below it) down the list
    /// by N slots. No wrap: anything shifted past the end of the list is dropped.
    /// The N slots vacated at the top of the shifted range are zeroed.
    ///
    /// Use case: you inserted new shape keys in the middle of the mesh in Blender,
    /// which shifted every blendshape after that point to a higher index. The
    /// SkinnedMeshRenderer's weight array is stored by index, not name, so after
    /// reimport the old weights are now sitting on the wrong blendshapes. This tool
    /// re-aligns them.
    /// </summary>
    public class BlendShapeShiftWindow : EditorWindow
    {
        private SkinnedMeshRenderer _renderer;
        private int _shiftAmount = 1;
        private string _search = "";
        private Vector2 _scroll;

        [MenuItem("Tools/VAP/Blend Shape Shift Tool")]
        private static void Open()
        {
            var window = GetWindow<BlendShapeShiftWindow>("BlendShape Shift");
            window.minSize = new Vector2(360, 400);
            window.TryAutoAssignFromSelection();
        }

        private void OnSelectionChange()
        {
            if (_renderer == null) TryAutoAssignFromSelection();
            Repaint();
        }

        private void TryAutoAssignFromSelection()
        {
            var go = Selection.activeGameObject;
            if (go == null) return;

            var smr = go.GetComponent<SkinnedMeshRenderer>();
            if (smr == null) smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null) _renderer = smr;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4);
            _renderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(
                "Skinned Mesh Renderer", _renderer, typeof(SkinnedMeshRenderer), true);

            if (_renderer == null || _renderer.sharedMesh == null)
            {
                EditorGUILayout.HelpBox("Assign a SkinnedMeshRenderer with a mesh.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                _shiftAmount = EditorGUILayout.IntField("Shift Amount", _shiftAmount);
                if (_shiftAmount < 1) _shiftAmount = 1;
            }
            EditorGUILayout.HelpBox(
                "Right-click a blendshape row to shift its value (and everything below it) " +
                "down by the Shift Amount above. Values pushed past the end of the list are dropped. " +
                "Vacated slots at the top of the shifted range are zeroed.", MessageType.None);

            EditorGUILayout.Space(4);
            _search = EditorGUILayout.TextField("Search", _search);

            var mesh = _renderer.sharedMesh;
            int count = mesh.blendShapeCount;

            EditorGUILayout.Space(4);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            for (int i = 0; i < count; i++)
            {
                string name = mesh.GetBlendShapeName(i);
                if (!string.IsNullOrEmpty(_search) &&
                    name.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                Rect rowRect = EditorGUILayout.BeginHorizontal();

                GUILayout.Label(i.ToString(), GUILayout.Width(30));
                GUILayout.Label(name, GUILayout.MinWidth(120));

                float current = _renderer.GetBlendShapeWeight(i);
                float edited = EditorGUILayout.FloatField(current, GUILayout.Width(70));
                if (!Mathf.Approximately(edited, current))
                {
                    Undo.RecordObject(_renderer, "Edit Blend Shape Weight");
                    _renderer.SetBlendShapeWeight(i, edited);
                }

                EditorGUILayout.EndHorizontal();

                HandleRowContextClick(rowRect, i, count);
            }

            EditorGUILayout.EndScrollView();
        }

        private void HandleRowContextClick(Rect rowRect, int index, int count)
        {
            Event e = Event.current;
            if (e.type != EventType.ContextClick || !rowRect.Contains(e.mousePosition))
                return;

            var menu = new GenericMenu();

            menu.AddItem(new GUIContent($"Shift down by {_shiftAmount} from here (index {index})"), false,
                () => ShiftFrom(index, _shiftAmount, count));

            menu.AddItem(new GUIContent($"Shift up by {_shiftAmount} from here (index {index})"), false,
                () => ShiftFrom(index, -_shiftAmount, count));

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Zero this value"), false, () =>
            {
                Undo.RecordObject(_renderer, "Zero Blend Shape Weight");
                _renderer.SetBlendShapeWeight(index, 0f);
            });

            menu.AddItem(new GUIContent("Zero from here down"), false, () =>
            {
                Undo.RecordObject(_renderer, "Zero Blend Shape Weights");
                for (int i = index; i < count; i++)
                    _renderer.SetBlendShapeWeight(i, 0f);
            });

            menu.ShowAsContext();
            e.Use();
        }

        /// <summary>
        /// Shifts weights at/after startIndex by offset slots. Positive offset pushes
        /// values to higher indices (down the list); negative pushes to lower indices.
        /// No wrap: any value whose target index falls outside [0, count) is dropped.
        /// Slots that end up with no source value land at 0.
        /// </summary>
        private void ShiftFrom(int startIndex, int offset, int count)
        {
            if (offset == 0 || _renderer == null) return;

            float[] original = new float[count];
            for (int i = 0; i < count; i++)
                original[i] = _renderer.GetBlendShapeWeight(i);

            float[] result = new float[count];

            // Unchanged region before startIndex.
            for (int i = 0; i < startIndex; i++)
                result[i] = original[i];

            // Shifted region: startIndex..count-1 moves by offset, dropping anything
            // that lands outside the valid range. Slots in the shifted region that
            // receive no value stay at 0 (default array init).
            for (int i = startIndex; i < count; i++)
            {
                int target = i + offset;
                if (target >= 0 && target < count)
                    result[target] = original[i];
            }

            Undo.RecordObject(_renderer, "Shift Blend Shape Weights");
            for (int i = 0; i < count; i++)
                _renderer.SetBlendShapeWeight(i, result[i]);
        }
    }
}
#endif
