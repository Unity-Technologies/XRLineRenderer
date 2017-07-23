using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// A VR-Focused drop-in replacement for the Line Renderer
/// This renderer draws fixed-width lines with simulated volume and glow.
/// This has many of the advantages of the traditional Line Renderer, old-school system-level line rendering functions, 
/// and volumetric (a linked series of capsules or cubes) rendering
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
[ExecuteInEditMode]
public class XRLineRenderer : MonoBehaviour
{
    static readonly GradientColorKey k_DefaultStartColor = new GradientColorKey(Color.white, 0);
    static readonly GradientColorKey k_DefaultEndColor = new GradientColorKey(Color.white, 1);
    static readonly GradientAlphaKey k_DefaultStartAlpha = new GradientAlphaKey(1,0);
    static readonly GradientAlphaKey k_DefaultEndAlpha = new GradientAlphaKey(1,1);

    [SerializeField]
    [Tooltip("Materials to use when rendering.")]
    Material[] m_Materials;

    // Stored Line Data
    [SerializeField]
    Vector3[] m_Positions;

    [SerializeField]
    [FormerlySerializedAs("m_WorldSpaceData")]
    [Tooltip("Draw lines in worldspace (or local space) - driven via shader")]
    bool m_UseWorldSpace;

    [SerializeField]
    [Tooltip("Connect the first and last vertices, to create a loop.")]
    bool m_Loop;

    [SerializeField]
    [Tooltip("The multiplier applied to the curve, describing the width (in world space) along the line.")]
    float m_Width = 1.0f;

    [SerializeField]
    [Tooltip("The curve describing the width of the line at various points along its length.")]
    AnimationCurve m_WidthCurve = new AnimationCurve();

    [SerializeField]
    [Tooltip("The gradient describing color along the line.")]
    Gradient m_Color = new Gradient();

    // Cached Data
    XRMeshChain m_VrMeshData;
    bool m_MeshNeedsRefreshing;
    float m_StepSize = 1.0f;

    /// <summary>
    /// Draw lines in worldspace (or local space)
    /// </summary>
    public bool useWorldSpace
    {
        get
        {
            return m_UseWorldSpace;
        }
        set
        {
            m_UseWorldSpace = value;
        }
    }

    /// <summary>
    /// Set the width at the start of the line.
    /// </summary>
    public float widthStart
    {
        get { return m_WidthCurve.Evaluate(0) * m_Width; }
        set
        {
            m_WidthCurve.keys[0].value = value;
            UpdateWidth();
        }
    }

    /// <summary>
    /// Set the width at the end of the line.
    /// </summary>
    public float widthEnd
    {
        get { return m_WidthCurve.Evaluate(1) * m_Width; }
        set
        {
            var lastIndex = m_WidthCurve.keys.Length - 1;
            m_WidthCurve.keys[lastIndex].value = value;
            UpdateWidth();
        }
    }

    /// <summary>
    /// Set an overall multiplier that is applied to the LineRenderer.widthCurve to get the final width of the line.
    /// </summary>
    public float widthMultiplier
    {
        get { return m_Width; }
        set
        {
            m_Width = value;
            UpdateWidth();
        }
    }

    /// <summary>
    /// Set the curve describing the width of the line at various points along its length.
    /// </summary>
    public AnimationCurve widthCurve
    {
        get { return m_WidthCurve; }
        set 
        {
            m_WidthCurve = value ?? new AnimationCurve(new Keyframe(0,1.0f));
            UpdateWidth();
        }
    }

    /// <summary>
    /// Set the color gradient describing the color of the line at various points along its length.
    /// </summary>
    public Gradient colorGradient
    {
        get { return m_Color; }
        set
        {
            if (m_Color == value)
            {
                return;
            }
            m_Color = value ?? new Gradient { alphaKeys = new []{ k_DefaultStartAlpha, k_DefaultEndAlpha }, colorKeys = new []{ k_DefaultStartColor, k_DefaultEndColor }, mode = GradientMode.Blend };
            UpdateColors();
        }
    }

    /// <summary>
    /// Set the color at the start of the line.
    /// </summary>
    public Color colorStart
    {
        get { return m_Color.Evaluate(0); }
        set
        {
            var flatColor = value;
            flatColor.a = 1.0f;
            m_Color.colorKeys[0].color = flatColor;
            m_Color.alphaKeys[0].alpha = value.a;
            UpdateColors();
        }
    }

    /// <summary>
    /// Set the color at the end of the line.
    /// </summary>
    public Color colorEnd
    {
        get { return m_Color.Evaluate(1); }
        set
        {
            var lastColorIndex = m_Color.colorKeys.Length - 1;
            var lastAlphaIndex = m_Color.alphaKeys.Length - 1;
            var flatColor = value;
            flatColor.a = 1.0f;
            m_Color.colorKeys[lastColorIndex].color = flatColor;
            m_Color.alphaKeys[lastAlphaIndex].alpha = value.a;
            UpdateColors();
        }
    }

    /// <summary>
    /// Cleans up the visible interface of the meshrenderer by hiding unneeded components
    /// Also makes sure the animation curves are set up properly to defualts
    /// </summary>
    void OnValidate()
    {
        var meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.hideFlags = HideFlags.HideInInspector;
        var meshFilter = GetComponent<MeshFilter>();
        meshFilter.hideFlags = HideFlags.HideInInspector;

        if (m_Materials == null)
        {
            m_Materials = meshRenderer.sharedMaterials;
        }
        meshRenderer.sharedMaterials = m_Materials;

        if (m_WidthCurve == null || m_WidthCurve.keys == null || m_WidthCurve.keys.Length == 0)
        {
            m_WidthCurve = new AnimationCurve(new Keyframe(0, 1.0f));
        }
        
        m_Color = m_Color ?? new Gradient { alphaKeys = new[] { k_DefaultStartAlpha, k_DefaultEndAlpha }, colorKeys = new[] { k_DefaultStartColor, k_DefaultEndColor }, mode = GradientMode.Blend };

        #if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += EditorCheckForUpdate;
        #endif
    }

    void CopyWorldSpaceDataFromMaterial()
    {
        if (m_Materials != null && m_Materials.Length > 0)
        {
            var firstMaterial = m_Materials[0];
            if (firstMaterial.HasProperty("_WorldData"))
            {
                m_UseWorldSpace = !Mathf.Approximately(firstMaterial.GetFloat("_WorldData"), 0.0f);
            }
            else
            {
                m_UseWorldSpace = false;
            }
        }
    }

    /// <summary>
    /// Ensures the lines have all their data precached upon loading
    /// </summary>
    void Awake()
    {
        Initialize();
    }

    /// <summary>
    /// Does the actual internal mesh updating as late as possible so nothing ends up a frame behind
    /// </summary>
    void LateUpdate()
    {
        if (m_MeshNeedsRefreshing == true)
        {
            m_VrMeshData.RefreshMesh();
            m_MeshNeedsRefreshing = false;
        }
    }

    /// <summary>
    /// Allows the component to be referenced as a renderer, forwarding the MeshRenderer ahead
    /// </summary>
    public static implicit operator Renderer(XRLineRenderer lr)
    {
        return lr.GetComponent<MeshRenderer>();
    }

    //////////////////
    /// Editor Usage
    //////////////////
    public void EditorCheckForUpdate()
    {
        // If we did not initialize, refresh all the properties instead
        Initialize(true);
    }

    /// <summary>
    /// Sets the position of the vertex in the line.
    /// </summary>
    /// <param name="index">Which vertex to set</param>
    /// <param name="position">The new location in space of this vertex</param>
    public void SetPosition(int index, Vector3 position)
    {
        // Update internal data
        m_Positions[index] = position;

        // See if the data needs initializing
        if (Initialize())
        {
            return;
        }

        // Otherwise, do fast setting
        var prevIndex = (index - 1 + m_Positions.Length) % m_Positions.Length;
        var endIndex = (index + 1) % m_Positions.Length;

        if (index > 0 || m_Loop)
        {
            m_VrMeshData.SetElementPipe((index * 2) - 1, ref m_Positions[prevIndex], ref m_Positions[index]);
        }

        m_VrMeshData.SetElementPosition(index * 2, ref m_Positions[index]);
        if (index < (m_Positions.Length - 1) || m_Loop)
        {
            m_VrMeshData.SetElementPipe((index * 2) + 1, ref m_Positions[index], ref m_Positions[endIndex]);
        }
        m_VrMeshData.SetMeshDataDirty(XRMeshChain.MeshRefreshFlag.Positions);
        m_MeshNeedsRefreshing = true;
    }

    /// <summary>
    /// Sets all positions in the line. Cheaper than calling SetPosition repeatedly
    /// </summary>
    /// <param name="newPositions">All of the new endpoints of the line</param>
    /// <param name="knownSizeChange">Turn on to run a safety check to make sure the number of endpoints does not change (bad for garbage collection)</param>
    void SetPositions(Vector3[] newPositions, bool knownSizeChange = false)
    {
        // Update internal data
        if (m_Positions == null || newPositions.Length != m_Positions.Length)
        {
            if (knownSizeChange == false)
            {
                Debug.LogWarning("New positions does not match size of existing array.  Adjusting vertex count as well");
            }
            m_Positions = newPositions;
            Initialize(true);
            return;
        }
        m_Positions = newPositions;

        // See if the data needs initializing
        if (Initialize())
        {
            return;
        }

        // Otherwise, do fast setting
        var pointCounter = 0;
        var elementCounter = 0;
        m_VrMeshData.SetElementPosition(elementCounter, ref m_Positions[pointCounter]);
        elementCounter++;
        pointCounter++;
        while (pointCounter < m_Positions.Length)
        {
            m_VrMeshData.SetElementPipe(elementCounter, ref m_Positions[pointCounter - 1], ref m_Positions[pointCounter]);
            elementCounter++;
            m_VrMeshData.SetElementPosition(elementCounter, ref m_Positions[pointCounter]);

            elementCounter++;
            pointCounter++;
        }
        if (m_Loop)
        {
            m_VrMeshData.SetElementPipe(elementCounter, ref m_Positions[pointCounter - 1], ref m_Positions[0]);
        }

        // Dirty all the VRMeshChain flags so everything gets refreshed
        m_VrMeshData.SetMeshDataDirty(XRMeshChain.MeshRefreshFlag.Positions);
        m_MeshNeedsRefreshing = true;
    }

    /// <summary>
    /// Sets the number of billboard-line chains. This function regenerates the point list if the
    /// number of vertex points changes, so use it sparingly.
    /// </summary>
    /// <param name="count">The new number of vertices in the line</param>
    public void SetVertexCount(int count)
    {
        // See if anything needs updating
        if (m_Positions.Length == count)
        {
            return;
        }

        // Adjust this array
        var newPositions = new Vector3[count];
        var copyCount = Mathf.Min(m_Positions.Length, count);
        var copyIndex = 0;

        while (copyIndex < copyCount)
        {
            newPositions[copyIndex] = m_Positions[copyIndex];
            copyIndex++;
        }
        m_Positions = newPositions;

        // Do an initialization, this changes everything
        Initialize(true);
    }
    
    /// <summary>
    /// Get the number of billboard-line chains.
    /// <summary
    public int GetVertexCount()
    {
        return m_Positions.Length;
    }

    /// <summary>
    /// Updates the colors that make up the line
    /// </summary>
    void UpdateColors()
    {
        // See if the data needs initializing
        if (Initialize())
        {
            return;
        }

        // If it doesn't, go through each point and set the data
        var pointCounter = 0;
        var elementCounter = 0;
        var stepPercent = 0.0f;

        var lastColor = m_Color.Evaluate(stepPercent);
        m_VrMeshData.SetElementColor(elementCounter, ref lastColor);
        elementCounter++;
        pointCounter++;
        stepPercent += m_StepSize;

        while (pointCounter < m_Positions.Length)
        {
            var currentColor = m_Color.Evaluate(stepPercent);
            m_VrMeshData.SetElementColor(elementCounter, ref lastColor, ref currentColor);
            elementCounter++;

            m_VrMeshData.SetElementColor(elementCounter, ref currentColor);

            lastColor = currentColor;
            elementCounter++;
            pointCounter++;
            stepPercent += m_StepSize;
        }

        if (m_Loop)
        {
            lastColor = m_Color.Evaluate(stepPercent);
            m_VrMeshData.SetElementColor(elementCounter, ref lastColor);
        }

        // Dirty the color meshChain flags so the mesh gets new data
        m_VrMeshData.SetMeshDataDirty(XRMeshChain.MeshRefreshFlag.Colors);
        m_MeshNeedsRefreshing = true;
    }

    /// <summary>
    /// Sets the line width at the start and at the end.
    /// Note, varying line widths will have a segmented appearance vs. the smooth look one gets with the traditional linerenderer.
    /// </summary>
    void UpdateWidth()
    {
        // See if the data needs initializing
        if (Initialize())
        {
            return;
        }

        // Otherwise, do fast setting
        var pointCounter = 0;
        var elementCounter = 0;
        var stepPercent = 0.0f;
        
        // We go through the element list, much like initialization, but only update the width part of the variables
        var lastWidth = m_WidthCurve.Evaluate(stepPercent) * m_Width;
        m_VrMeshData.SetElementSize(elementCounter, lastWidth);
        elementCounter++;
        pointCounter++;

        stepPercent += m_StepSize;

        while (pointCounter < m_Positions.Length)
        {
            var currentWidth = m_WidthCurve.Evaluate(stepPercent) * m_Width;

            m_VrMeshData.SetElementSize(elementCounter, lastWidth, currentWidth);
            elementCounter++;
            m_VrMeshData.SetElementSize(elementCounter, currentWidth);
            lastWidth = currentWidth;
            elementCounter++;
            pointCounter++;
            stepPercent += m_StepSize;
        }

        if (m_Loop)
        {
            var currentWidth = m_WidthCurve.Evaluate(stepPercent) * m_Width;
            m_VrMeshData.SetElementSize(elementCounter, lastWidth, currentWidth);
        }

        // Dirty all the VRMeshChain flags so everything gets refreshed
        m_VrMeshData.SetMeshDataDirty(XRMeshChain.MeshRefreshFlag.Sizes);
        m_MeshNeedsRefreshing = true;
    }

    /// <summary>
    /// Ensures the mesh data for the renderer is created, and updates it if neccessary
    /// </summary>
    /// <param name="force">Whether or not to force a full rebuild of the mesh data</param>
    /// <returns>True if an initialization occurred, false if it was skipped</returns>
    protected bool Initialize(bool force = false)
    {
        CopyWorldSpaceDataFromMaterial();
        if (m_Positions == null)
        {
            return false;
        }
        var performFullInitialize = force;

        // For a line renderer we assume one big chain
        // We need a control point for each billboard and a control point for each pipe connecting them together
        // Except for the end, which must be capped with another billboard.  This gives us (positions * 2) - 1
        var neededPoints = Mathf.Max((m_Positions.Length * 2) - 1, 0);

        // If we're looping, then we do need one more pipe
        if (m_Loop)
        {
            neededPoints++;
        }

        if (m_VrMeshData == null)
        {
            m_VrMeshData = new XRMeshChain();
        }
        if (m_VrMeshData.reservedElements != neededPoints)
        {
            m_VrMeshData.worldSpaceData = useWorldSpace;
            m_VrMeshData.GenerateMesh(gameObject, true, neededPoints);
            performFullInitialize = true;
        }
        if (performFullInitialize == false)
        {
            return false;
        }
        if (neededPoints == 0)
        {
            m_StepSize = 1.0f;
            return true;
        }
        m_StepSize = 1.0f / Mathf.Max(m_Loop ? m_Positions.Length : m_Positions.Length - 1, 1.0f);

        var pointCounter = 0;
        var elementCounter = 0;
        var stepPercent = 0.0f;

        var lastColor = m_Color.Evaluate(stepPercent);
        var lastWidth = m_WidthCurve.Evaluate(stepPercent) * m_Width;

        // Initialize the single starting point
        m_VrMeshData.SetElementSize(elementCounter, lastWidth);
        m_VrMeshData.SetElementPosition(elementCounter, ref m_Positions[pointCounter]);
        m_VrMeshData.SetElementColor(elementCounter, ref lastColor);
        elementCounter++;
        pointCounter++;

        stepPercent += m_StepSize;
        

        // Now do the chain
        while (pointCounter < m_Positions.Length)
        {
            var currentWidth = m_WidthCurve.Evaluate(stepPercent) * m_Width;
            var currentColor = m_Color.Evaluate(stepPercent);

            // Create a pipe from the previous point to here
            m_VrMeshData.SetElementSize(elementCounter, lastWidth, currentWidth);
            m_VrMeshData.SetElementPipe(elementCounter, ref m_Positions[pointCounter - 1], ref m_Positions[pointCounter]);
            m_VrMeshData.SetElementColor(elementCounter, ref lastColor, ref currentColor);
            elementCounter++;

            // Now record our own point data
            m_VrMeshData.SetElementSize(elementCounter, currentWidth);
            m_VrMeshData.SetElementPosition(elementCounter, ref m_Positions[pointCounter]);
            m_VrMeshData.SetElementColor(elementCounter, ref currentColor);

            // Go onto the next point while retaining previous values we might need to lerp between
            lastWidth = currentWidth;
            lastColor = currentColor;
            elementCounter++;
            pointCounter++;
            stepPercent += m_StepSize;
        }

        if (m_Loop)
        {
            var currentWidth = m_WidthCurve.Evaluate(stepPercent) * m_Width;
            var currentColor = m_Color.Evaluate(stepPercent);
            m_VrMeshData.SetElementSize(elementCounter, lastWidth, currentWidth);
            m_VrMeshData.SetElementPipe(elementCounter, ref m_Positions[pointCounter - 1], ref m_Positions[0]);
            m_VrMeshData.SetElementColor(elementCounter, ref lastColor, ref currentColor);
        }

        // Dirty all the VRMeshChain flags so everything gets refreshed
        m_VrMeshData.SetMeshDataDirty(XRMeshChain.MeshRefreshFlag.All);
        m_MeshNeedsRefreshing = true;
        return true;
    }

    /// <summary>
    /// Enables the internal mesh representing the line
    /// </summary>
    void OnEnable()
    {
        GetComponent<MeshRenderer>().enabled = true;
    }

    /// <summary>
    /// Disables the internal mesh representing the line
    /// </summary>
    void OnDisable()
    {
        GetComponent<MeshRenderer>().enabled = false;
    }
}
