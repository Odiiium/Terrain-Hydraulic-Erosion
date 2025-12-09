using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class BlurRenderFeature : FullScreenPassRendererFeature
{
    [SerializeField] private BlurSettings _settings;
    [SerializeField] private Shader _shader;

    private Material _material;
    private BlurRenderPass _pass;

    public override void Create()
    {
        if (_shader == null)
            return;

        _material = new(_shader);
        _pass = new(_material, _settings);

        _pass.renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_pass == null)
            return;

        if (renderingData.cameraData.cameraType == CameraType.Game)
            renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        if (Application.isPlaying)
            Destroy(_material);
        else
            DestroyImmediate(_material);
    }

}

public class BlurRenderPass : ScriptableRenderPass
{
    private BlurSettings _settings;
    private Material _material;
    private TextureDesc blurTextureDescriptor;

    private static readonly int horizontalBlurId = Shader.PropertyToID("_HorizontalBlur");
    private static readonly int verticalBlurId = Shader.PropertyToID("_VerticalBlur");
    private const string k_BlurTextureName = "_BlurTexture";
    private const string k_VerticalPassName = "VerticalBlurRenderPass";
    private const string k_HorizontalPassName = "HorizontalBlurRenderPass";

    public BlurRenderPass(Material material, BlurSettings settings)
    {
        _settings = settings;
        _material = material;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        base.Execute(context, ref renderingData);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

        TextureHandle cameraColor = resourceData.activeColorTexture;

        blurTextureDescriptor = cameraColor.GetDescriptor(renderGraph);
        blurTextureDescriptor.name = k_BlurTextureName;
        blurTextureDescriptor.depthBufferBits = 0;

        TextureHandle destination = renderGraph.CreateTexture(blurTextureDescriptor);

        if (resourceData.isActiveTargetBackBuffer)
            return;

        UpdateBlurSettings();

        if (!cameraColor.IsValid() || !destination.IsValid())
            return;

        RenderGraphUtils.BlitMaterialParameters paraVertical = new(cameraColor, destination, _material, 0);
        renderGraph.AddBlitPass(paraVertical, k_VerticalPassName);

        RenderGraphUtils.BlitMaterialParameters paraHorizontal = new(destination, cameraColor, _material, 1);
        renderGraph.AddBlitPass(paraHorizontal, k_HorizontalPassName);


    }

    private void UpdateBlurSettings()
    {
        if (_material == null) return;

        _material.SetFloat(horizontalBlurId, _settings.horizontalBlur);
        _material.SetFloat(verticalBlurId, _settings.verticalBlur);
    }
}

[Serializable]
public class BlurSettings
{
    [Range(0, 0.05f)] public float horizontalBlur;
    [Range(0, 0.05f)] public float verticalBlur;
}
