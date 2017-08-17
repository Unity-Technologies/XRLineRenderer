using UnityEngine;

namespace UnityEditor
{
    [CustomEditor(typeof(XRTrailRenderer))]
    [CanEditMultipleObjects]
    public class XRTrailRendererEditor : Editor
    {
        SerializedProperty m_Materials;
        SerializedProperty m_Time;
        SerializedProperty m_MinVertexDistance;
        SerializedProperty m_Autodestruct;
        SerializedProperty m_Width;
        SerializedProperty m_WidthCurve;
        SerializedProperty m_Color;
        SerializedProperty m_MaxTrailPoints;
        SerializedProperty m_StealLastPointWhenEmpty;
        SerializedProperty m_SmoothInterpolation;

        void OnEnable()
        {
            m_Materials = serializedObject.FindProperty("m_Materials");
            m_Time = serializedObject.FindProperty("m_Time");
            m_MinVertexDistance = serializedObject.FindProperty("m_MinVertexDistance");
            m_Autodestruct = serializedObject.FindProperty("m_Autodestruct");
            m_Width = serializedObject.FindProperty("m_Width");
            m_WidthCurve = serializedObject.FindProperty("m_WidthCurve");
            m_Color = serializedObject.FindProperty("m_Color");
            m_MaxTrailPoints = serializedObject.FindProperty("m_MaxTrailPoints");
            m_StealLastPointWhenEmpty = serializedObject.FindProperty("m_StealLastPointWhenEmpty");
            m_SmoothInterpolation = serializedObject.FindProperty("m_SmoothInterpolation");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(m_Materials, true);
            EditorGUILayout.PropertyField(m_Time, true);
            EditorGUILayout.PropertyField(m_MinVertexDistance, true);
            EditorGUILayout.PropertyField(m_Autodestruct, true);
            EditorGUILayout.PropertyField(m_Width, true);
            EditorGUILayout.CurveField(m_WidthCurve, Color.red, new Rect(0, 0, 1, 1));
            EditorGUILayout.PropertyField(m_Color, true);
            EditorGUILayout.PropertyField(m_MaxTrailPoints, true);
            EditorGUILayout.PropertyField(m_StealLastPointWhenEmpty, true);
            EditorGUILayout.PropertyField(m_SmoothInterpolation, true);
            serializedObject.ApplyModifiedProperties();
        }
    }
}