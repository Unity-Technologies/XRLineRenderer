using System;
using UnityEngine;

/// <summary>
/// An XR-Focused drop-in replacement for the Trail Renderer
/// This renderer draws fixed-width lines with simulated volume and glow.
/// This has many of the advantages of the traditional Line Renderer, old-school system-level line rendering functions, 
/// and volumetric (a linked series of capsules or cubes) rendering
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
[ExecuteInEditMode]
public class XRTrailRenderer : MeshChainRenderer
{
    const float k_AbsoluteMinVertexDistance = 0.01f;

    // Stored Trail Data
    [SerializeField]
    [Tooltip("How many points to store for tracing.")]
    int m_MaxTrailPoints = 20;

    [SerializeField]
    [Tooltip("Whether to use the last point or the first point of the trail when more are needed and none are available.")]
    bool m_StealLastPointWhenEmpty = true;

    [SerializeField]
    [Tooltip("How long the tail should be (second) [ 0, infinity ].")]
    float m_Time = 5.0f;

    [SerializeField]
    [Tooltip("The minimum distance to spawn a new point on the trail [ 0, infinity ].")]
    float m_MinVertexDistance = 0.1f;

    [SerializeField]
    [Tooltip("Destroy GameObject when there is no trail?")]
    bool m_Autodestruct = false;

    [SerializeField]
    [Tooltip("With this enabled, the last point will smooth lerp between the last recorded anchor point and the one after it")]
    bool m_SmoothInterpolation = false; 

    // Circular array support for trail point recording
    Vector3[] m_Points;
    float[] m_PointTimes;
    int m_PointIndexStart = 0;
    int m_PointIndexEnd = 0;

    // Cached Data
    Vector3 m_LastRecordedPoint = Vector3.zero;
    float m_LastPointTime;

    float m_EditorDeltaHelper;  // This lets us have access to a time data while not in play mode


    /// <summary>
    /// How long does the trail take to fade out.
    /// </summary>
    public float time
    {
        get { return m_Time; }
        set { m_Time = Mathf.Max(value, 0); }
    }

    /// <summary>
    /// Set the minimum distance the trail can travel before a new vertex is added to it.
    /// </summary>
    public float minVertexDistance
    {
        get { return m_MinVertexDistance; }
        set { m_MinVertexDistance = Mathf.Max(value, k_AbsoluteMinVertexDistance); }
    }

    /// <summary>
    /// Get the number of line segments in the trail
    /// </summary>
    public int positionCount { get; private set; }

    /// <summary>
    /// Destroy GameObject when there is no trail?
    /// </summary>
    public bool autodestruct
    {
        get { return m_Autodestruct; }
        set { m_Autodestruct = value; }
    }

    /// <summary>
    /// Set if the last point will smooth lerp between the last recorded anchor point and the one after it
    /// </summary>
    public bool smoothInterpolation
    {
        get { return m_SmoothInterpolation; }
        set { m_SmoothInterpolation = value; }
    }

    /// <summary>
    /// Updates the built-in mesh data for each control point of the trail
    /// </summary>
    protected override void LateUpdate()
    {
        // We do  the actual internal mesh updating as late as possible so nothing ends up a frame behind
        var deltaTime = Time.deltaTime;

        // We give the editor a little help with handling delta time in edit mode
        if (Application.isPlaying == false)
        {
            deltaTime = Time.realtimeSinceStartup - m_EditorDeltaHelper;
            m_EditorDeltaHelper = Time.realtimeSinceStartup;
        }
        
        // Get the current position of the renderer
        var currentPoint = transform.position;
        var pointDistance = (currentPoint - m_LastRecordedPoint).sqrMagnitude;
        var shrunkThisFrame = false;

        // Is it more than minVertexDistance from the last position?
        if (pointDistance > (m_MinVertexDistance*m_MinVertexDistance))
        {
            // In the situation we have no points, we need to record the start point as well
            if (m_PointIndexStart == m_PointIndexEnd)
            {
                m_Points[m_PointIndexStart] = m_LastRecordedPoint;
                m_PointTimes[m_PointIndexStart] = m_Time;
            }

            // Make space for a new point
            var newEndIndex = (m_PointIndexEnd + 1) % m_MaxTrailPoints;
            
            // In the situation that we are rendering all available vertices
            // We can either keep using the current point, or take the last point, depending on the user's preference
            if (newEndIndex != m_PointIndexStart)
            {
                m_PointIndexEnd = newEndIndex;
                m_PointTimes[m_PointIndexEnd] = 0;
                positionCount++;
            }
            else
            {
                if (m_StealLastPointWhenEmpty)
                {
                    m_XRMeshData.SetElementSize(m_PointIndexStart * 2, 0);
                    m_XRMeshData.SetElementSize((m_PointIndexStart * 2) + 1, 0);
                    m_PointIndexStart = (m_PointIndexStart + 1) % m_MaxTrailPoints;
                    m_PointIndexEnd = newEndIndex;
                    m_PointTimes[m_PointIndexEnd] = 0;
                    m_LastPointTime = m_PointTimes[m_PointIndexStart];
                }
            }
            m_Points[m_PointIndexEnd] = currentPoint;

            // Update the last recorded point
            m_LastRecordedPoint = currentPoint;
        }
        // Do time processing
        // The end point counts up to a maximum of 'time'
        m_PointTimes[m_PointIndexEnd] = Mathf.Min(m_PointTimes[m_PointIndexEnd] + deltaTime, m_Time);

        if (m_PointIndexStart != m_PointIndexEnd)
        {
            // Run down the counter on the start point
            m_PointTimes[m_PointIndexStart] -= deltaTime;
            
            // If we've hit 0, this point is done for
            if (m_PointTimes[m_PointIndexStart] <= 0.0f)
            {
                m_XRMeshData.SetElementSize(m_PointIndexStart * 2, 0);
                m_XRMeshData.SetElementSize((m_PointIndexStart * 2) + 1, 0);
                m_PointIndexStart = (m_PointIndexStart + 1) % m_MaxTrailPoints;
                m_LastPointTime = m_PointTimes[m_PointIndexStart];
                positionCount--;
                shrunkThisFrame = true;
            }
        }
        
        if (m_PointIndexStart != m_PointIndexEnd)
        {
            m_MeshNeedsRefreshing = true;
            m_MeshRenderer.enabled = true;
        }
        else
        {
            m_MeshNeedsRefreshing = false;
            m_MeshRenderer.enabled = false;
            if (m_Autodestruct && Application.isPlaying && shrunkThisFrame)
            {
                Destroy(gameObject);
            }
        }

        if (m_MeshNeedsRefreshing)
        {
            m_MeshRenderer.enabled = true;

            // Update first and last points position-wise
            var nextIndex = (m_PointIndexStart + 1) % m_MaxTrailPoints;
            if (m_SmoothInterpolation)
            {
                var toNextPoint = 1.0f - (m_PointTimes[m_PointIndexStart] / m_LastPointTime);
                var lerpPoint = Vector3.Lerp(m_Points[m_PointIndexStart], m_Points[nextIndex], toNextPoint);
                m_XRMeshData.SetElementPosition((m_PointIndexStart * 2), ref lerpPoint);
                m_XRMeshData.SetElementPipe((m_PointIndexStart * 2) + 1, ref lerpPoint, ref m_Points[nextIndex]);
            }
            else
            {
                m_XRMeshData.SetElementPosition((m_PointIndexStart * 2), ref m_Points[m_PointIndexStart]);
                m_XRMeshData.SetElementPipe((m_PointIndexStart * 2) + 1, ref m_Points[m_PointIndexStart], ref m_Points[nextIndex]);
            }
            
            var prevIndex = m_PointIndexEnd - 1;
            if (prevIndex < 0)
            {
                prevIndex = m_MaxTrailPoints - 1;
            }
            m_XRMeshData.SetElementPipe((prevIndex * 2) + 1, ref m_Points[prevIndex], ref m_Points[m_PointIndexEnd]);
            m_XRMeshData.SetElementPosition((m_PointIndexEnd * 2), ref m_Points[m_PointIndexEnd]);
            

            // Go through all points and update size and color
            var pointUpdateCounter = m_PointIndexStart;
            var pointCount = 0;
            m_StepSize = (positionCount > 0) ? (1.0f / positionCount) : 1.0f;

            var percent = 0.0f;
            var lastWidth = m_WidthCurve.Evaluate(percent) * m_Width;
            var lastColor = m_Color.Evaluate(percent);
            percent += m_StepSize;

            while (pointUpdateCounter != m_PointIndexEnd)
            {
                var nextWidth = m_WidthCurve.Evaluate(percent) * m_Width;
                m_XRMeshData.SetElementSize(pointUpdateCounter * 2, lastWidth);
                m_XRMeshData.SetElementSize((pointUpdateCounter * 2) + 1, lastWidth, nextWidth);
                lastWidth = nextWidth;

                var nextColor = m_Color.Evaluate(percent);
                m_XRMeshData.SetElementColor(pointUpdateCounter * 2, ref lastColor);
                m_XRMeshData.SetElementColor((pointUpdateCounter * 2) + 1, ref lastColor, ref nextColor);
                lastColor = nextColor;

                pointUpdateCounter = (pointUpdateCounter + 1) % m_MaxTrailPoints;
                pointCount++;
                percent += m_StepSize;
            }
            lastWidth = m_WidthCurve.Evaluate(1) * m_Width;
            m_XRMeshData.SetElementSize((m_PointIndexEnd * 2), lastWidth);
            lastColor = m_Color.Evaluate(1);
            m_XRMeshData.SetElementColor((m_PointIndexEnd * 2), ref lastColor);
            m_XRMeshData.SetMeshDataDirty(XRMeshChain.MeshRefreshFlag.All);
            m_XRMeshData.RefreshMesh();
        }
    }

    /// <summary>
    /// Editor helper function to ensure changes are reflected in edit-mode
    /// </summary>
    public void EditorCheckForUpdate()
    {
        // If we did not initialize, refresh all the properties instead
        Initialize();
    }

    /// <summary>
    /// Removes all points from the TrailRenderer. Useful for restarting a trail from a new position.
    /// </summary>
    public void Clear()
    {
        var zeroVec = Vector3.zero;
        var zeroColor = Color.clear;

        var elementCounter = 0;
        var pointCounter = 0;
        while (pointCounter < m_Points.Length)
        {
            // Start point
            m_XRMeshData.SetElementSize(elementCounter, 0);
            m_XRMeshData.SetElementPosition(elementCounter, ref zeroVec);
            m_XRMeshData.SetElementColor(elementCounter, ref zeroColor);
            elementCounter++;
            
            // Pipe to the next point
            m_XRMeshData.SetElementSize(elementCounter, 0);
            m_XRMeshData.SetElementPipe(elementCounter, ref zeroVec, ref zeroVec);
            m_XRMeshData.SetElementColor(elementCounter, ref zeroColor);

            // Go onto the next point while retaining previous values we might need to lerp between
            elementCounter++;
            pointCounter++;
        }

        m_PointIndexStart = 0;
        m_PointIndexEnd = 0;
        positionCount = 0;
        m_LastRecordedPoint = transform.position;
    }

    protected override void  Initialize()
    {
        base.Initialize();

        m_MaxTrailPoints = Mathf.Max(m_MaxTrailPoints, 3);

        // If we already have the right amount of points and mesh, then we can get away with just clearing the curve out
        if (m_Points != null && m_MaxTrailPoints == m_Points.Length && m_XRMeshData != null)
        {
            Clear();
            return;
        }
        
        m_Points = new Vector3[m_MaxTrailPoints];
        m_PointTimes = new float[m_MaxTrailPoints];

        // For a trail renderer we assume one big chain
        // We need a control point for each billboard and a control point for each pipe connecting them together
        // We make this a circular trail so the update logic is easier.  This gives us (position * 2)
        var neededPoints = Mathf.Max((m_MaxTrailPoints * 2), 0);

        if (m_XRMeshData == null)
        {
            m_XRMeshData = new XRMeshChain();
        }
        if (m_XRMeshData.reservedElements != neededPoints)
        {
            m_XRMeshData.worldSpaceData = true;
            m_XRMeshData.centerAtRoot = true;
            m_XRMeshData.GenerateMesh(gameObject, true, neededPoints);

            if (neededPoints == 0)
            {
                return;
            }
            // Dirty all the VRMeshChain flags so everything gets refreshed
            m_MeshRenderer.enabled = false;
            m_XRMeshData.SetMeshDataDirty(XRMeshChain.MeshRefreshFlag.All);
            m_MeshNeedsRefreshing = true;
        }
        Clear();
    }

    protected override bool NeedsReinitialize()
    {
        // No mesh data means we definately need to reinitialize
        if (m_XRMeshData == null)
        {
            return true;
        }
        // Mismatched point data means we definately need to reinitialize
        if (m_Points == null || m_MaxTrailPoints != m_Points.Length)
        {
            return true;
        }
        m_MaxTrailPoints = Mathf.Max(m_MaxTrailPoints, 3);
        var neededPoints = Mathf.Max((m_MaxTrailPoints * 2), 0);

        return (m_XRMeshData.reservedElements != neededPoints);
    }
    
    /// <summary>
    /// Enables the internal mesh representing the line
    /// </summary>
    protected override void OnEnable()
    { 
        m_MeshRenderer.enabled = (m_PointIndexStart != m_PointIndexEnd);
    }
}
