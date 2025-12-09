using System;
using UnityEngine;

public class ShadowComputer
{
    private ComputeShader _shadowShader;
    private RenderTexture _heightTexture;
    private ShadowSettings _settings;

    public RenderTexture ShadowTexture { get; private set; }

    private int _shadowCalculationKernel;
    private readonly string _shadowCalculationKernelName = "CalculateShadows";

    public void Init(ComputeShader shadowShader, RenderTexture heightTexture, ShadowSettings settings)
    {
        _shadowShader = shadowShader;
        _settings = settings;
        _heightTexture = heightTexture;

        InitTextures(settings, heightTexture);
        SetupShader();
    }

    private void InitTextures(ShadowSettings settings, RenderTexture heightTexture)
    {
        ShadowTexture ??= TextureExtensions.NewRenderTexture(Vector2Int.one * settings.Size, RenderTextureFormat.RFloat);
    }

    private void SetupShader()
    {
        _shadowCalculationKernel = _shadowShader.FindKernel(_shadowCalculationKernelName);
        UpdateShaderValues(_settings);
    }

    private void UpdateShaderValues(ShadowSettings settings)
    {
        _shadowShader.SetVector("terrainSize", settings.TerrainSize);
        UpdateDynamicValues(settings);
    }

    private void UpdateDynamicValues(ShadowSettings settings)
    {
        _shadowShader.SetVector("lightDirection", settings.LightDirection);
        _shadowShader.SetFloat("heightScale", settings.HeightScale);
        _shadowShader.SetFloat("maxDistance", settings.MaxDistance);
        _shadowShader.SetFloat("stepSize", settings.StepSize);
        _shadowShader.SetFloat("bias", settings.Bias);
        _shadowShader.SetFloat("softness", settings.Softness);
    }

    public void ComputeShadows()
    {
        int threadGroups = GetThreadGroups();

        UpdateShaderValues(_settings);

        _shadowShader.SetTexture(_shadowCalculationKernel, "HeightMap", _heightTexture);
        _shadowShader.SetTexture(_shadowCalculationKernel, "ShadowMap", ShadowTexture);

        _shadowShader.Dispatch(_shadowCalculationKernel, threadGroups, threadGroups, 1);
    }

    private int GetThreadGroups()
    {
        return Mathf.CeilToInt(_settings.Size / 8.0f);
    }
}
