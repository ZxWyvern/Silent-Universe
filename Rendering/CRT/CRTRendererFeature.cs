using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class CRTRendererFeature : ScriptableRendererFeature
{
    class CRTPass : ScriptableRenderPass
    {
        public Material material;

        // Wadah penampung data untuk dikirim ke dalam eksekusi Render Graph
        private class PassData
        {
            public TextureHandle source;
            public Material material;
        }

        // FUNGSI BARU UNITY 6: Menggantikan OnCameraSetup dan Execute
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null) return;
            material.SetFloat("_Time_Custom", Time.time);

            // Ambil data rendering frame saat ini
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            // Jangan jalankan efek ini di layar preview material editor
            if (cameraData.cameraType == CameraType.Preview || cameraData.cameraType == CameraType.Reflection)
                return;

            // Ambil output gambar dari kamera (berlaku untuk Full Screen maupun Render Texture)
            TextureHandle source = resourceData.activeColorTexture;
            if (!source.IsValid()) return;

            // Buat kanvas/tekstur sementara
            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0; // Kita hanya memanipulasi warna, tidak butuh depth

            TextureHandle tempTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_TempCRTTexture", false);

            // LANGKAH 1: Salin gambar kamera ke TempTexture sambil menyuntikkan Material CRT
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("CRT Blit Pass", out var passData))
            {
                passData.source = source;
                passData.material = material;

                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(tempTexture, 0);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            // LANGKAH 2: Salin kembali hasil gambar dari TempTexture ke output kamera asli
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("CRT Copy Back Pass", out var passData))
            {
                passData.source = tempTexture;

                builder.UseTexture(tempTexture, AccessFlags.Read);
                builder.SetRenderAttachment(source, 0);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    // Copy murni tanpa material
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0.0f, false);
                });
            }
        }
    }

    [Header("Settings")]
    public Material crtMaterial;
    
    // Pastikan ini tetap BeforeRenderingPostProcessing agar aman di Render Texture
    public RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingPostProcessing; 

    private CRTPass customPass;

    public override void Create()
    {
        customPass = new CRTPass
        {
            material = crtMaterial,
            renderPassEvent = passEvent
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(customPass);
    }
}