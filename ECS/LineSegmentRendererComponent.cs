using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Unity.Rendering
{
    /// <summary>
    /// Mimics the neccessary pieces of the renderer system for rendering XR Line Segments.
    /// Acts as a renderer without having to include the legacy components.
    /// Support for rendering in 'Classic' GameObject mode, or pure entity mode
    /// Tweaked version of the MeshInstanceRenderer from the ECS 
    /// </summary>
    [Serializable]
    public struct LineSegmentRenderer : ISharedComponentData
    {
        public Mesh                 mesh;
        public Material[]           materials;
        public BlittableBool visible;
    }

    [DisallowMultipleComponent]
    public class LineSegmentRendererComponent : SharedComponentDataWrapper<LineSegmentRenderer> { }
}
