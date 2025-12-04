using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using static HydraulicErosion;

public class TerrainGenerationPresenter : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private ErosionSettings _settings;
    [SerializeField] private ComputeShader _erosionShader;
    [SerializeField] private float _erodeStepTimeDelay;

    [SerializeField] private TerrainGenerationView _view;
    [SerializeField] private TerrainGenerator _terrainGenerator;
    [SerializeField] private HydraulicErosion _erosion;

    [SerializeField] private Material _terrainMaterial;

    private Coroutine _erosionRoutine;

    private void Start()
    {
        _erosion = new();

        _terrainGenerator.GenerateTerrain();
        Texture2D texture = _terrainGenerator.GenerateHeightMap();
        _terrainGenerator.OnHeightMapGenerated += OnHeightMapRegenerated;
        _settings.Size = texture.width;
        _erosion.Init(_erosionShader, _settings, texture);
        InitView();
        SetMaterialValues();
    }

    private void OnDestroy()
    {
        _erosion.ReleaseAll();
        _terrainGenerator.OnHeightMapGenerated -= OnHeightMapRegenerated;
        Unsubscribe();
    }

    private void InitView()
    {
        _view.Init(_erosion.HeightRT, _erosion.WaterRT, _erosion.SedimentRT, _erosion.OutflowRT, _erosion.VelocityRT);
        Subscribe();
    }

    private void SetMaterialValues()
    {
        _terrainMaterial.SetFloat("_Size", _terrainGenerator.GetAbsoluteTerrainSize().x);
        //_terrainMaterial.SetFloat("_Amplitude", _terrainGenerator.GetAmplitude());
        _terrainMaterial.SetTexture("_HeightMap", _erosion.HeightRT);
        _terrainMaterial.SetTexture("_WaterMap", _erosion.WaterRT);
        _terrainMaterial.SetTexture("_SedimentMap", _erosion.SedimentRT);
    }

    private void Subscribe()
    {
        _view.StartButton.onClick.AddListener(() => _erosionRoutine = StartCoroutine(StartErosionCycle()));
        _view.EndButton.onClick.AddListener(() => EndErosionCycle());
        _view.RefreshButton.onClick.AddListener(() => _erosion.Refresh());
    }

    private void Unsubscribe()
    {
        _view.StartButton.onClick.RemoveAllListeners();
        _view.EndButton.onClick.RemoveAllListeners();
        _view.RefreshButton.onClick.RemoveAllListeners();
    }

    private void OnHeightMapRegenerated(Texture2D heightMap)
    {
        _settings.Size = heightMap.width;
        _erosion.Init(_erosionShader, _settings, heightMap);
        _view.Init(_erosion.HeightRT);
        SetMaterialValues();
    }

    private IEnumerator StartErosionCycle()
    {
        EndErosionCycle();

        _erosion.UpdateShaderValues(_settings);

        WaitForSeconds wait = new(_erodeStepTimeDelay);

        while (true)
        {
            _erosion.SimulationStep();
            yield return wait;
        }
    }

    private void EndErosionCycle()
    {
        if (_erosionRoutine != null)
        {
            StopCoroutine(_erosionRoutine);
            _erosionRoutine = null;
        }
    }
}
