using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Experimental.PlayerLoop;

namespace Unity.Rendering
{
    [UpdateAfter(typeof(PreLateUpdate.ParticleSystemBeginUpdateAll))]
    [UnityEngine.ExecuteInEditMode]
    public class LineSegmentRendererSystem : ComponentSystem
    {
        List<LineSegmentRenderer>  m_CachedSegmentRenderers = new List<LineSegmentRenderer>(10);
        ComponentGroup m_PureRendererGroup;
        ComponentGroup m_HybridRendererGroup;

        protected override void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);
            m_PureRendererGroup = GetComponentGroup(typeof(LineSegmentRenderer), typeof(TransformMatrix), typeof(RenderLayer));
            m_HybridRendererGroup = GetComponentGroup(typeof(LineSegmentRenderer), typeof(Transform));
        }

        protected override void OnUpdate()
        {
            // Each segment renderer is _probably_ unique here.  But we're more interested
            // in doing optimized/parallel operations on the data inside the mesh, rather than
            // the meshes themselves.
            EntityManager.GetAllUniqueSharedComponentDatas(m_CachedSegmentRenderers);
            for (var i = 0; i != m_CachedSegmentRenderers.Count;i++)
            {
                var renderer = m_CachedSegmentRenderers[i];
                m_PureRendererGroup.SetFilter(renderer);
                var transforms = m_PureRendererGroup.GetComponentDataArray<TransformMatrix>();
                var renderLayers = m_PureRendererGroup.GetComponentDataArray<RenderLayer>();

                for(var j = 0; j < transforms.Length; j++)
                {
                    if (!renderer.visible.Value || renderer.mesh == null)
                    {
                        continue;
                    }

                    var matrix = new Matrix4x4(transforms[j].Value.m0,
                                                transforms[j].Value.m1,
                                                transforms[j].Value.m2,
                                                transforms[j].Value.m3);

                    foreach (var mat in renderer.materials)
                    {
                        Graphics.DrawMesh(renderer.mesh, matrix, mat, renderLayers[j].value);
                    }
                }
            }

            for (var i = 0; i != m_CachedSegmentRenderers.Count;i++)
            {
                var renderer = m_CachedSegmentRenderers[i];
                m_HybridRendererGroup.SetFilter(renderer);
                var transforms = m_HybridRendererGroup.GetComponentArray<Transform>();

                for(var j = 0; j < transforms.Length; j++)
                {
                    if (!renderer.visible.Value || renderer.mesh == null)
                    {
                        continue;
                    }

                    var matrix = transforms[j].localToWorldMatrix;
                    var renderLayer = transforms[j].gameObject.layer;

                    foreach (var mat in renderer.materials)
                    {
                        Graphics.DrawMesh(renderer.mesh, matrix, mat, renderLayer);
                    }
                }
            }
            m_CachedSegmentRenderers.Clear();
        }
    }
}
