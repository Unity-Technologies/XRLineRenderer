using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Unity.Rendering
{
    /// <summary>
    /// Contains which drawing layer an object should be rendered on
    /// when being rendered through a componentsystem
    /// </summary>
    [Serializable]
    public struct RenderLayer : IComponentData
    {
        public int                 value;
    }

    public class RenderLayerComponent : ComponentDataWrapper<RenderLayer> { }
}
