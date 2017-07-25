using System;
using UnityEngine;

/// <summary>
/// A unified base class for the XR Line Renderer and XR Trail Renderer
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
[ExecuteInEditMode]
public abstract class XRLineRendererBase : MonoBehaviour
{
    static readonly GradientColorKey k_DefaultStartColor = new GradientColorKey(Color.white, 0);
    static readonly GradientColorKey k_DefaultEndColor = new GradientColorKey(Color.white, 1);
    static readonly GradientAlphaKey k_DefaultStartAlpha = new GradientAlphaKey(1,0);
    static readonly GradientAlphaKey k_DefaultEndAlpha = new GradientAlphaKey(1,1);

    [SerializeField]
    [Tooltip("Materials to use when rendering.")]
    protected Material[] m_Materials;

    [SerializeField]
    [Tooltip("The multiplier applied to the curve, describing the width (in world space) along the line.")]
    protected float m_Width = 1.0f;

    [SerializeField]
    [Tooltip("The curve describing the width of the line at various points along its length.")]
    protected AnimationCurve m_WidthCurve = new AnimationCurve();

    [SerializeField]
    [Tooltip("The gradient describing color along the line.")]
    protected Gradient m_Color = new Gradient();

    [SerializeField]
    [HideInInspector]
    protected MeshRenderer m_MeshRenderer;

    // Cached Data
    protected XRMeshChain m_XRMeshData;
    protected bool m_MeshNeedsRefreshing;
    protected float m_StepSize = 1.0f;

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

    void OnValidate()
    {
        SetupMeshBackend();
        if (NeedsReinitialize())
        {
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.delayCall += EditorCheckForUpdate;
            #endif
        }
        else
        {
            EditorCheckForUpdate();
        }
    }

    /// <summary>
    /// Cleans up the visible interface of the meshrenderer by hiding unneeded components
    /// Also makes sure the animation curves are set up properly to defualts
    /// </summary>
    void SetupMeshBackend()
    {
        m_MeshRenderer = GetComponent<MeshRenderer>();
        m_MeshRenderer.hideFlags = HideFlags.HideInInspector;
        var meshFilter = GetComponent<MeshFilter>();
        meshFilter.hideFlags = HideFlags.HideInInspector;

        if (m_Materials == null)
        {
            m_Materials = m_MeshRenderer.sharedMaterials;
        }
        m_MeshRenderer.sharedMaterials = m_Materials;

        if (m_WidthCurve == null || m_WidthCurve.keys == null || m_WidthCurve.keys.Length == 0)
        {
            m_WidthCurve = new AnimationCurve(new Keyframe(0, 1.0f));
        }
        m_Color = m_Color ?? new Gradient { alphaKeys = new[] { k_DefaultStartAlpha, k_DefaultEndAlpha }, colorKeys = new[] { k_DefaultStartColor, k_DefaultEndColor }, mode = GradientMode.Blend };
    }

    /// <summary>
    /// Makes the sure mesh renderer reference is initialized before any functions try to access it
    /// </summary>
    protected virtual void Awake()
    {
        SetupMeshBackend();
        Initialize(true);
    }

    /// <summary>
    /// Ensures the lines have all their data precached upon loading
    /// </summary>
    void Start()
    {
        Initialize();
    }

    /// <summary>
    /// Does the actual internal mesh updating as late as possible so nothing ends up a frame behind
    /// </summary>
    protected virtual void LateUpdate()
    {
        if (m_MeshNeedsRefreshing == true)
        {
            m_XRMeshData.RefreshMesh();
            m_MeshNeedsRefreshing = false;
        }
    }

    /// <summary>
    /// Allows the component to be referenced as a renderer, forwarding the MeshRenderer ahead
    /// </summary>
    public static implicit operator Renderer(XRLineRendererBase lr)
    {
        return lr.GetComponent<MeshRenderer>();
    }


    /// <summary>
    /// Triggered by validation - forced initialization to make sure data changed
    /// in the editor is reflected immediately to the user.
    /// </summary>
    void EditorCheckForUpdate()
    {
        // Because this gets delay-called, it can be referring to a destroyed component when a scene starts
        if (this == null)
        {
            return;
        }
        // If we did not initialize, refresh all the properties instead
        Initialize(true);
    }

    /// <summary>
    /// Updates any internal variables to represent the new color that has been applied
    /// </summary>
    protected abstract void UpdateColors();
    
    /// <summary>
    /// Updates any internal variables to represent the new width that has been applied
    /// </summary>
    protected abstract void UpdateWidth();
    
    /// <summary>
    /// Ensures the mesh data for the renderer is created, and updates it if neccessary
    /// </summary>
    /// <param name="force">Whether or not to force a full rebuild of the mesh data</param>
    /// <returns>True if an initialization occurred, false if it was skipped</returns>
    protected virtual bool Initialize(bool force = false)
    {
        return force;
    }

    /// <summary>
    /// Tests if the mesh data for the renderer needs to be created or rebuilt.  Used
    /// to delay updates if neccessary in the OnValidate function
    /// </summary>
    /// <returns></returns>
    protected virtual bool NeedsReinitialize()
    {
        return true;
    }

    /// <summary>
    /// Enables the internal mesh representing the line
    /// </summary>
    protected virtual void OnEnable()
    {
        m_MeshRenderer.enabled = true;
    }

    /// <summary>
    /// Disables the internal mesh representing the line
    /// </summary>
    protected virtual void OnDisable()
    {
        m_MeshRenderer.enabled = false;
    }
}
