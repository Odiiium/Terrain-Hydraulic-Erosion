using System;
using System.Collections.Generic;
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
        ClearAllTextures(except: HeightRT);
        //PopulateWaterTextureWithRandomDroplets();
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
        VelocityRT = NewRT(RenderTextureFormat.RGFloat);
        OutflowRT = NewRT(RenderTextureFormat.ARGBFloat);

        _tempHeight = NewRT(RenderTextureFormat.ARGBFloat);
        _tempWater = NewRT(RenderTextureFormat.ARGBFloat);
        _tempSediment = NewRT(RenderTextureFormat.ARGBFloat);

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
            ClearTexture(texture);
        });
    }

    private void ClearTexture(RenderTexture texture)
    {
        Graphics.Blit(Texture2D.blackTexture, texture);
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

        _erosionShader.SetTexture(_waterIncrementKernel, "WaterMap", WaterRT);

        _erosionShader.Dispatch(_waterIncrementKernel, threadGroups, threadGroups, 1);
    }

    private void OutflowCalculationStep()
    {
        int threadGroups = GetThreadGroups();

        _erosionShader.SetTexture(_outflowCalculationKernel, "HeightMap", HeightRT);
        _erosionShader.SetTexture(_outflowCalculationKernel, "WaterMap", WaterRT);
        _erosionShader.SetTexture(_outflowCalculationKernel, "OutflowMap", OutflowRT);

        _erosionShader.Dispatch(_outflowCalculationKernel, threadGroups, threadGroups, 1);
    }

    private void VelocityFieldCalculationStep()
    {
        int threadGroups = GetThreadGroups();

        _erosionShader.SetTexture(_velocityFieldCalculationKernel, "WaterMap", WaterRT);
        _erosionShader.SetTexture(_velocityFieldCalculationKernel, "OutflowMap", OutflowRT);
        _erosionShader.SetTexture(_velocityFieldCalculationKernel, "VelocityMap", VelocityRT);

        _erosionShader.Dispatch(_velocityFieldCalculationKernel, threadGroups, threadGroups, 1);
    }

    private void ErosionAndDepositStep()
    {
        int threadGroups = GetThreadGroups();

        _erosionShader.SetTexture(_erosionAndDepositionKernel, "HeightMap", HeightRT);
        _erosionShader.SetTexture(_erosionAndDepositionKernel, "WaterMap", WaterRT);
        _erosionShader.SetTexture(_erosionAndDepositionKernel, "OutflowMap", OutflowRT);
        _erosionShader.SetTexture(_erosionAndDepositionKernel, "VelocityMap", VelocityRT);
        _erosionShader.SetTexture(_erosionAndDepositionKernel, "SedimentMap", SedimentRT);

        _erosionShader.Dispatch(_erosionAndDepositionKernel, threadGroups, threadGroups, 1);
    }

    private void SedimentTransportStep()
    {
        int threadGroups = GetThreadGroups();

        _erosionShader.SetTexture(_sedimentTransportKernel, "VelocityMap", VelocityRT);
        _erosionShader.SetTexture(_sedimentTransportKernel, "SedimentMap", SedimentRT);

        _erosionShader.Dispatch(_sedimentTransportKernel, threadGroups, threadGroups, 1);
    }

    private void EvaporationStep()
    {
        int threadGroups = GetThreadGroups();

        _erosionShader.SetTexture(_evaporationKernel, "WaterMap", WaterRT);
        _erosionShader.SetTexture(_evaporationKernel, "VelocityMap", VelocityRT);
        _erosionShader.SetTexture(_evaporationKernel, "OutflowMap", OutflowRT);

        _erosionShader.Dispatch(_evaporationKernel, threadGroups, threadGroups, 1);
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