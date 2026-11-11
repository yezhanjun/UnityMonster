using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace MTPSKIT
{
    // Install only on the renderer that owns the Parent FX camera. Does not mutate pipeline assets.
    public sealed class RainParentDistortionFeature : ScriptableRendererFeature
    {
        DistortionPass pass;

        public override void Create()
        {
            pass?.Release();
            pass = new DistortionPass { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.renderType != CameraRenderType.Base ||
                (renderingData.cameraData.cameraType != CameraType.Game && renderingData.cameraData.cameraType != CameraType.SceneView)) return;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            pass?.Release();
            pass = null;
        }

        sealed class DistortionPass : ScriptableRenderPass
        {
            static readonly int GrabId = Shader.PropertyToID("_GrabTexturePostProcess");
            static readonly ShaderTagId Tag = new ShaderTagId("RainParentDistortion");
            RTHandle grab;
            readonly ProfilingSampler sampler = new ProfilingSampler("Parent distortion capture/draw");

            public DistortionPass()
            {
                requiresIntermediateTexture = true;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public void Release() { grab?.Release(); grab = null; }

            [System.Obsolete("Compatibility path for the project's current URP configuration.")]
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;
                RenderingUtils.ReAllocateHandleIfNeeded(ref grab,descriptor,FilterMode.Bilinear,TextureWrapMode.Clamp,name:"_ParentDistortionGrab");
            }

            [System.Obsolete("Compatibility path for the project's current URP configuration.")]
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var renderer = renderingData.cameraData.renderer;
                var cmd = CommandBufferPool.Get();
                try
                {
                    using(new ProfilingScope(cmd,sampler))
                    {
                        Blitter.BlitCameraTexture(cmd,renderer.cameraColorTargetHandle,grab);
                        cmd.SetGlobalTexture(GrabId,grab);
                        CoreUtils.SetRenderTarget(cmd,renderer.cameraColorTargetHandle,renderer.cameraDepthTargetHandle);
                    }
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                    var drawing = CreateDrawingSettings(Tag,ref renderingData,SortingCriteria.CommonTransparent);
                    var filtering = new FilteringSettings(RenderQueueRange.transparent);
                    context.DrawRenderers(renderingData.cullResults,ref drawing,ref filtering);
                }
                finally { CommandBufferPool.Release(cmd); }
            }

            sealed class CopyData { public TextureHandle source; }
            sealed class DrawData { public RendererListHandle list; }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var rendering = frameData.Get<UniversalRenderingData>();
                var camera = frameData.Get<UniversalCameraData>();
                var light = frameData.Get<UniversalLightData>();
                var descriptor = graph.GetTextureDesc(resources.activeColorTexture);
                descriptor.name = "Parent distortion grab";
                descriptor.msaaSamples = MSAASamples.None;
                descriptor.clearBuffer = false;
                var captured = graph.CreateTexture(descriptor);
                using(var builder = graph.AddRasterRenderPass<CopyData>("Parent distortion capture",out var data))
                {
                    data.source = resources.activeColorTexture;
                    builder.UseTexture(data.source,AccessFlags.Read);
                    builder.SetRenderAttachment(captured,0);
                    builder.SetGlobalTextureAfterPass(captured,GrabId);
                    builder.SetRenderFunc(static (CopyData d,RasterGraphContext ctx) =>
                        Blitter.BlitTexture(ctx.cmd,d.source,new Vector4(1,1,0,0),0,false));
                }
                var settings = RenderingUtils.CreateDrawingSettings(Tag,rendering,camera,light,SortingCriteria.CommonTransparent);
                var filter = new FilteringSettings(RenderQueueRange.transparent);
                var list = graph.CreateRendererList(new RendererListParams(rendering.cullResults,settings,filter));
                using(var builder = graph.AddRasterRenderPass<DrawData>("Parent distortion draw",out var data))
                {
                    data.list = list;
                    builder.UseRendererList(list);
                    builder.UseTexture(captured,AccessFlags.Read);
                    if(resources.cameraDepthTexture.IsValid()) builder.UseTexture(resources.cameraDepthTexture,AccessFlags.Read);
                    builder.SetRenderAttachment(resources.activeColorTexture,0);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture,AccessFlags.Read);
                    builder.SetRenderFunc(static (DrawData d,RasterGraphContext ctx) => ctx.cmd.DrawRendererList(d.list));
                }
            }
        }
    }
}
