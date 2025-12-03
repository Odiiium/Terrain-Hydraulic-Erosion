using System;
using System.Collections.Generic;
using UnityEngine;

public class HydraulicErosion
{
    private ErosionSettings _settings;
    private List<RenderTexture> _textures = new();
    private ComputeShader _erosionShader;
    private Texture2D _initialHeightmap;

    private int _outflowCalculationKernel;
    private int _erosionStepKernel;
    private int _applyKernel;

    private readonly string _outflowCalculationStepKernelName = "ComputeOutflow";
    private readonly string _erosionStepKernelName = "UpdateWaterAndErosion";
    private readonly string _applyResultsKernelName = "ApplyResults";

    public RenderTexture HeightRT {get; private set;}
    public RenderTexture WaterRT { get; private set; }
    public RenderTexture SedimentRT { get; private set; }
    public RenderTexture OutflowRT { get; private set; }

    private RenderTexture _tempHeight, _tempWater, _tempSediment;

    public void Init(ComputeShader erosionCompute, ErosionSettings settings, Texture2D heightMap)
    {
        _erosionShader = erosionCompute;
        _initialHeightmap = heightMap;
        _settings = settings;

        InitTextures();
        SetupShader(_settings);
    }

    public void Refresh()
    {
        Graphics.Blit(_initialHeightmap, HeightRT);

        ClearTexture(WaterRT);
        ClearTexture(SedimentRT);
        ClearTexture(OutflowRT);
        ClearTexture(_tempHeight);
        ClearTexture(_tempWater);
        ClearTexture(_tempSediment);

        PopulateWaterTextureWithRandomDroplets();
    }

    public void ReleaseAll()
    {
        foreach (var rt in _textures)
        {
            rt.Release();
            MonoBehaviour.DestroyImmediate(rt);
        }

        _textures.Clear();
    }

    private void InitTextures()
    {
        ReleaseAll();

        HeightRT = NewRT(RenderTextureFormat.RFloat);
        WaterRT = NewRT(RenderTextureFormat.RFloat);
        SedimentRT = NewRT(RenderTextureFormat.RFloat);
        OutflowRT = NewRT(RenderTextureFormat.ARGBFloat);

        _tempHeight = NewRT(RenderTextureFormat.ARGBFloat);
        _tempWater = NewRT(RenderTextureFormat.ARGBFloat);
        _tempSediment = NewRT(RenderTextureFormat.ARGBFloat);

        if (_initialHeightmap != null)
            Graphics.Blit(_initialHeightmap, HeightRT);
        else
        {
            RenderTexture.active = HeightRT;
            GL.Clear(false, true, Color.black);
            RenderTexture.active = null;
        }

        ClearTexture(WaterRT);
        ClearTexture(SedimentRT);
        ClearTexture(_tempHeight);
        ClearTexture(_tempWater);
        ClearTexture(_tempSediment);

        PopulateWaterTextureWithRandomDroplets();
    }

    private void PopulateWaterTextureWithRandomDroplets()
    {
        Color[] colors = new Color[WaterRT.width * WaterRT.height];

        for (int iter = 0, y = 0; y < WaterRT.width; y++)
            for (int x = 0; x < WaterRT.height; x++)
            {
                Color color = new Color(UnityEngine.Random.Range(0, 256) < 4 ? 1 : 0, 0, 0, 1);
                colors[iter++] = color;
            }

        Texture2D texture = new(WaterRT.width, WaterRT.height);

        texture.SetPixels(colors);
        texture.Apply();

        Graphics.Blit(texture, WaterRT);
    }

    private void ClearTexture(RenderTexture texture)
    {
        Graphics.Blit(Texture2D.blackTexture, texture);
    }

    private void SetupShader(ErosionSettings settings)
    {
        _outflowCalculationKernel = _erosionShader.FindKernel(_outflowCalculationStepKernelName);
        _erosionStepKernel = _erosionShader.FindKernel(_erosionStepKernelName);
        _applyKernel = _erosionShader.FindKernel(_applyResultsKernelName);

        UpdateShaderValues(settings);
    }

    public void UpdateShaderValues(ErosionSettings settings)
    {
        _erosionShader.SetInt("width", settings.Size);
        _erosionShader.SetInt("height", settings.Size);
        _erosionShader.SetFloat("cellSize", settings.CellSize);
        _erosionShader.SetFloat("rainfall", settings.Rainfall);
        _erosionShader.SetFloat("evaporation", settings.Evaporation);
        _erosionShader.SetFloat("capacityFactor", settings.CapacityFactor);
        _erosionShader.SetFloat("minSlope", settings.MinSlope);
        _erosionShader.SetFloat("depositSpeed", settings.DepositSpeed);
        _erosionShader.SetFloat("erodeSpeed", settings.ErodeSpeed);
        _erosionShader.SetFloat("timeStep", settings.TimeStep);
        _erosionShader.SetFloat("gravity", settings.Gravity);
    }

    public void SimulationStep()
    {
        if (_erosionShader == null) 
            return;

        for (int i = 0; i < _settings.IterationsPerFrame; i++)
        {
            ComputeOutflowStep();
            ErosionStep();
            ApplyStep();
        }
    }

    private void ComputeOutflowStep()
    {
        int threadGroups = GetThreadGroups();

        _erosionShader.SetTexture(_outflowCalculationKernel, "HeightMap", HeightRT);
        _erosionShader.SetTexture(_outflowCalculationKernel, "WaterMap", WaterRT);
        _erosionShader.SetTexture(_outflowCalculationKernel, "OutflowMap", OutflowRT);

        _erosionShader.Dispatch(_outflowCalculationKernel, threadGroups, threadGroups, 1);
    }

    private void ErosionStep()
    {
        int threadGroups = GetThreadGroups();

        _erosionShader.SetTexture(_erosionStepKernel, "HeightMap", HeightRT);
        _erosionShader.SetTexture(_erosionStepKernel, "WaterMap", WaterRT);
        _erosionShader.SetTexture(_erosionStepKernel, "SedimentMap", SedimentRT);

        _erosionShader.SetTexture(_erosionStepKernel, "OutflowMap", OutflowRT);

        _erosionShader.SetTexture(_erosionStepKernel, "TempWater", _tempWater);
        _erosionShader.SetTexture(_erosionStepKernel, "TempHeight", _tempHeight);
        _erosionShader.SetTexture(_erosionStepKernel, "TempSediment", _tempSediment);

        _erosionShader.Dispatch(_erosionStepKernel, threadGroups, threadGroups, 1);
    }

    private void ApplyStep()
    {
        int threadGroups = GetThreadGroups();

        _erosionShader.SetTexture(_applyKernel, "HeightMap", HeightRT);
        _erosionShader.SetTexture(_applyKernel, "WaterMap", WaterRT);
        _erosionShader.SetTexture(_applyKernel, "SedimentMap", SedimentRT);

        _erosionShader.SetTexture(_applyKernel, "OutflowMap", OutflowRT);

        _erosionShader.SetTexture(_applyKernel, "TempWater", _tempWater);
        _erosionShader.SetTexture(_applyKernel, "TempHeight", _tempHeight);
        _erosionShader.SetTexture(_applyKernel, "TempSediment", _tempSediment);

        _erosionShader.Dispatch(_applyKernel, threadGroups, threadGroups, 1);
    }

    private int GetThreadGroups()
    {
        return Mathf.CeilToInt(_settings.Size / 8.0f);
    }

    private RenderTexture NewRT(RenderTextureFormat format)
    {
        RenderTexture rt = new RenderTexture(_settings.Size, _settings.Size, 0, format) { enableRandomWrite = true };
        rt.filterMode = FilterMode.Bilinear;
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.Create();

        _textures.Add(rt);

        return rt;
    }

    [Serializable]
    public class ErosionSettings
    {
        public int Size;
        public float CellSize;
        public int IterationsPerFrame;
        [Range(0f, 1f)] public float Evaporation;
        [Range(0f,1f)] public float Rainfall;
        public float CapacityFactor;
        public float MinSlope;
        public float DepositSpeed;
        public float ErodeSpeed;
        public float Gravity;
        public float TimeStep;
    }   
}