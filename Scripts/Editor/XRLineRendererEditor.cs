using UnityEngine;

namespace UnityEditor
{
    [CustomEditor(typeof(XRLineRenderer))]
    [CanEditMultipleObjects]
    public class XRLineRendererEditor : Editor
    {
        SerializedProperty m_Materials;
        SerializedProperty m_Positions;
        SerializedProperty m_UseWorldSpace;
        SerializedProperty m_Loop;
        SerializedProperty m_Width;
        SerializedProperty m_WidthCurve;
        SerializedProperty m_Color;

        void OnEnable()
        {
            m_Materials = serializedObject.FindProperty("m_Materials");
            m_Positions = serializedObject.FindProperty("m_Positions");
            m_UseWorldSpace = serializedObject.FindProperty("m_UseWorldSpace");
            m_Loop = serializedObject.FindProperty("m_Loop");
            m_Width = serializedObject.FindProperty("m_Width");
            m_WidthCurve = serializedObject.FindProperty("m_WidthCurve");
            m_Color = serializedObject.FindProperty("m_Color");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(m_Materials, true);
            EditorGUILayout.PropertyField(m_Positions, true);

            using (new EditorGUI.DisabledGroupScope(true))
            {
                EditorGUILayout.PropertyField(m_UseWorldSpace, true);
            }
            
            EditorGUILayout.PropertyField(m_Loop, true);
            EditorGUILayout.PropertyField(m_Width, true);
            EditorGUILayout.CurveField(m_WidthCurve, Color.red, new Rect(0, 0, 1, 1));
            EditorGUILayout.PropertyField(m_Color, true);
            serializedObject.ApplyModifiedProperties();
        }
    }
}