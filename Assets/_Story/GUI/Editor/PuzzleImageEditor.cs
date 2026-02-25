using UnityEditor;
using UnityEditor.UI;

namespace ReadingBuddy.UI
{
    [CustomEditor(typeof(PuzzleImage))]
    [CanEditMultipleObjects]
    public class PuzzleImageEditor : ImageEditor
    {
        private SerializedProperty _isPuzzled;
        private SerializedProperty _rows;
        private SerializedProperty _columns;
        private SerializedProperty _gap;

        protected override void OnEnable()
        {
            base.OnEnable();
            _isPuzzled = serializedObject.FindProperty("_isPuzzled");
            _rows      = serializedObject.FindProperty("_rows");
            _columns   = serializedObject.FindProperty("_columns");
            _gap       = serializedObject.FindProperty("_gap");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Puzzle", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_isPuzzled, new UnityEngine.GUIContent("Is Puzzled"));
            EditorGUILayout.PropertyField(_rows,      new UnityEngine.GUIContent("Rows"));
            EditorGUILayout.PropertyField(_columns,   new UnityEngine.GUIContent("Columns"));
            EditorGUILayout.PropertyField(_gap,       new UnityEngine.GUIContent("Gap"));

            serializedObject.ApplyModifiedProperties();
        }
    }
}