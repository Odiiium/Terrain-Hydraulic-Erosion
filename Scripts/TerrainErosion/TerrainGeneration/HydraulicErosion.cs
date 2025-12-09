using Sirenix.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HydraulicErosion
{
    private ErosionSettings _settings;
    private List<RenderTexture> _textures = new();
    private ComputeShader _erosionShader;
    private Texture2D _initialHeightmap;

    private int _waterIncrementKernel;
    private int _outflowCalculationKernel;
    private int _velocityFieldCalculationKernel;
    private int _erosionAndDepositionKernel;
    private int _sedimentTransportKernel;
    private int _evaporationKernel;

    private readonly string _waterIncrementKernelName = "WaterIncrement";
    private readonly string _outflowCalculationKernelName = "OutflowCalculation";
    private readonly string _velocityFieldCalculationKernelName = "VelocityFieldCalculation";
    private readonly string _erosionAndDepositionKernelName = "ErosionAndDeposition";
    private readonly string _sedimentTransportKernelName = "SedimentTransport";
    private readonly string _evaporationKernelName = "Evaporation";

    public RenderTexture HeightRT {get; private set;}
    public RenderTexture WaterRT { get; private set; }
    public RenderTexture SedimentRT { get; private set; }
    public RenderTexture OutflowRT { get; private set; }
    public RenderTexture VelocityRT { get; private set; }

    private RenderTexture _writeHeightRT, _writeWaterRT, _writeSedimentRT, _writeOutflowRT;

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
        ClearAllTextures(except: HeightRT);
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

        Action<RenderTexture> onTextureCreated = (texture) => _textures.Add(texture);
        Vector2Int textureSize = Vector2Int.one * _settings.Size;

        HeightRT = TextureExtensions.NewRenderTexture(textureSize, RenderTextureFormat.RFloat, onTextureCreated);
        WaterRT = TextureExtensions.NewRenderTexture(textureSize, RenderTextureFormat.RFloat, onTextureCreated);
        SedimentRT = TextureExtensions.NewRenderTexture(textureSize, RenderTextureFormat.RFloat, onTextureCreated);
        VelocityRT = TextureExtensions.NewRenderTexture(textureSize, RenderTextureFormat.RGFloat, onTextureCreated);
        OutflowRT = TextureExtensions.NewRenderTexture(textureSize, RenderTextureFormat.ARGBFloat, onTextureCreated);

        _writeHeightRT = TextureExtensions.NewRenderTexture(textureSize, RenderTextureFormat.RFloat, onTextureCreated);
        _writeWaterRT = TextureExtensions.NewRenderTexture(textureSize, RenderTextureFormat.RFloat, onTextureCreated);
        _writeSedimentRT = TextureExtensions.NewRenderTexture(textureSize, RenderTextureFormat.RFloat, onTextureCreated);
        _writeOutflowRT = TextureExtensions.NewRenderTexture(textureSize, RenderTextureFormat.ARGBFloat, onTextureCreated);

        PopulateHeightMap();

        ClearAllTextures(except: HeightRT);

        //PopulateWaterTextureWithRandomDroplets();
    }

    private void PopulateHeightMap()
    {
        if (_initialHeightmap != null)
            Graphics.Blit(_initialHeightmap, HeightRT);
        else
        {
            RenderTexture.active = HeightRT;
            GL.Clear(false, true, Color.black);
            RenderTexture.active = null;
        }
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

    private void ClearAllTextures(RenderTexture except = null)
    {
        _textures.ForEach(texture =>
        {
            if (except != null && texture == except)
                return;

            TextureExtensions.ClearTexture(texture);
        });
    }

    private void SetupShader(ErosionSettings settings)
    {
        _waterIncrementKernel = _erosionShader.FindKernel(_waterIncrementKernelName);
        _outflowCalculationKernel = _erosionShader.FindKernel(_outflowCalculationKernelName);
        _velocityFieldCalculationKernel = _erosionShader.FindKernel(_velocityFieldCalculationKernelName);
        _erosionAndDepositionKernel = _erosionShader.FindKernel(_erosionAndDepositionKernelName);
        _sedimentTransportKernel = _erosionShader.FindKernel(_sedimentTransportKernelName);
        _evaporationKernel = _erosionShader.FindKernel(_evaporationKernelName);

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
        _erosionShader.SetFloat("deltaTime", settings.TimeStep);
        _erosionShader.SetFloat("gravity", settings.Gravity);
    }

    public void SimulationStep()
    {
        if (_erosionShader == null) 
            return;

        for (int i = 0; i < _settings.IterationsPerFrame; i++)
        {
            RainfallStep();
            OutflowCalculationStep();
            VelocityFieldCalculationStep();
            ErosionAndDepositStep();
            SedimentTransportStep();
            EvaporationStep();
        }
    }

    private void RainfallStep()
    {
        int threadGroups = GetThreadGroups();

        PopulateKernelWithTextures(_waterIncrementKernel, new() { ErosionMap.WaterW, ErosionMap.WaterR });

        _erosionShader.Dispatch(_waterIncrementKernel, threadGroups, threadGroups, 1);

        Graphics.Blit(_writeWaterRT, WaterRT);
    }

    private void OutflowCalculationStep()
    {
        int threadGroups = GetThreadGroups();

        PopulateKernelWithTextures(_outflowCalculationKernel, new()
            { ErosionMap.HeightR, ErosionMap.WaterR, ErosionMap.OutflowR, ErosionMap.OutflowW });

        _erosionShader.Dispatch(_outflowCalculationKernel, threadGroups, threadGroups, 1);

        Graphics.Blit(_writeOutflowRT, OutflowRT);
    }

    private void VelocityFieldCalculationStep()
    {
        int threadGroups = GetThreadGroups();

        PopulateKernelWithTextures(_velocityFieldCalculationKernel, new() 
            { ErosionMap.WaterR, ErosionMap.OutflowR, ErosionMap.VelocityRW, ErosionMap.WaterW });

        _erosionShader.Dispatch(_velocityFieldCalculationKernel, threadGroups, threadGroups, 1);

        Graphics.Blit(_writeWaterRT, WaterRT);
    }

    private void ErosionAndDepositStep()
    {
        int threadGroups = GetThreadGroups();

        PopulateKernelWithTextures(_erosionAndDepositionKernel, new()
            {ErosionMap.HeightR, ErosionMap.WaterR, ErosionMap.SedimentR, ErosionMap.OutflowR, ErosionMap.VelocityRW, ErosionMap.WaterW, ErosionMap.SedimentW, ErosionMap.HeightW});

        _erosionShader.Dispatch(_erosionAndDepositionKernel, threadGroups, threadGroups, 1);

        Graphics.Blit(_writeWaterRT, WaterRT);
        Graphics.Blit(_writeSedimentRT, SedimentRT);
        Graphics.Blit(_writeHeightRT, HeightRT);
    }

    private void SedimentTransportStep()
    {
        int threadGroups = GetThreadGroups();

        PopulateKernelWithTextures(_sedimentTransportKernel, new() { ErosionMap.SedimentR,  ErosionMap.VelocityRW, ErosionMap.SedimentW});

        _erosionShader.Dispatch(_sedimentTransportKernel, threadGroups, threadGroups, 1);

        Graphics.Blit(_writeSedimentRT, SedimentRT);
    }

    private void EvaporationStep()
    {
        int threadGroups = GetThreadGroups();

        PopulateKernelWithTextures(_evaporationKernel, new() { ErosionMap.WaterR,  ErosionMap.WaterW });

        _erosionShader.Dispatch(_evaporationKernel, threadGroups, threadGroups, 1);

        Graphics.Blit(_writeWaterRT, WaterRT);
    }

    private int GetThreadGroups()
    {
        return Mathf.CeilToInt(_settings.Size / 8.0f);
    }

    private void PopulateKernelWithTextures(int kernelId, List<ErosionMap> maps)
    {
        maps.Select(type => type switch
            {
                ErosionMap.HeightR => ("HeightMap", HeightRT),
                ErosionMap.WaterR => ("WaterMap", WaterRT),
                ErosionMap.SedimentR => ("SedimentMap", SedimentRT),
                ErosionMap.OutflowR => ("OutflowMap", OutflowRT),
                ErosionMap.HeightW => ("TempHeightMap", _writeHeightRT),
                ErosionMap.WaterW => ("TempWaterMap", _writeWaterRT),
                ErosionMap.SedimentW => ("TempSedimentMap", _writeSedimentRT),
                ErosionMap.OutflowW => ("TempOutflowMap", _writeOutflowRT),
                ErosionMap.VelocityRW => ("VelocityMap", VelocityRT),
            })
            .ForEach(tuple => _erosionShader.SetTexture(kernelId, tuple.Item1, tuple.Item2));
    }

    private enum ErosionMap
    {
        HeightR, WaterR, SedimentR, OutflowR, VelocityRW, HeightW, WaterW, SedimentW, OutflowW
    }
}