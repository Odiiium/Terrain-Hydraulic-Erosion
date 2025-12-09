using System.Collections;
using UnityEngine;

public class TerrainGenerationPresenter : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private ErosionSettings _settings;
    [SerializeField] private ShadowSettings _shadowSettings;
    [SerializeField] private float _erodeStepTimeDelay;

    [Header("References")]
    [SerializeField] private ComputeShader _erosionShader;
    [SerializeField] private ComputeShader _shadowShader;
    [SerializeField] private TerrainGenerationView _view;
    [SerializeField] private TerrainGenerator _terrainGenerator;
    [SerializeField] private Material _terrainMaterial;

    private Coroutine _erosionRoutine;
    private HydraulicErosion _erosion;
    private ShadowComputer _shadowComputer;

    private void Start()
    {
        _erosion = new();
        _shadowComputer = new();

        _terrainGenerator.GenerateTerrain();
        Texture2D texture = _terrainGenerator.GenerateHeightMap();
        _terrainGenerator.OnHeightMapGenerated += OnHeightMapRegenerated;

        SetupSettings(texture.width, _terrainGenerator.GetAbsoluteTerrainSize());

        _erosion.Init(_erosionShader, _settings, texture);
        _shadowComputer.Init(_shadowShader, _erosion.HeightRT, _shadowSettings);

        InitView();
        SetMaterialValues();
        RegenerateShadows();
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

    private void SetupSettings(int textureSize, Vector2 terrainSize)
    {
        _settings.Size = textureSize;
        _shadowSettings.Size = textureSize;
        _shadowSettings.TerrainSize = terrainSize;
        _shadowSettings.LightDirection = RenderSettings.sun.transform.forward;
    }

    private void SetMaterialValues()
    {
        _terrainMaterial.SetFloat("_Size", _terrainGenerator.GetAbsoluteTerrainSize().x);
        //_terrainMaterial.SetFloat("_Amplitude", _terrainGenerator.GetAmplitude());
        _terrainMaterial.SetTexture("_HeightMap", _erosion.HeightRT);
        _terrainMaterial.SetTexture("_WaterMap", _erosion.WaterRT);
        _terrainMaterial.SetTexture("_SedimentMap", _erosion.SedimentRT);
        _terrainMaterial.SetTexture("_ShadowTexture", _shadowComputer.ShadowTexture);
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
        _shadowComputer.Init(_shadowShader, _erosion.HeightRT, _shadowSettings);

        _view.Init(_erosion.HeightRT);
        SetMaterialValues();
    }

    private IEnumerator StartErosionCycle()
    {
        EndErosionCycle();

        _erosion.UpdateShaderValues(_settings);

        WaitForSeconds wait = new(_erodeStepTimeDelay);

        int iter = 0;

        while (true)
        {
            _erosion.SimulationStep();
            iter++;

            if (iter > 100)
            {
                RegenerateShadows();
                iter = 0;
            }

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

    [ButtonEditor.Button(nameof(RegenerateShadows))]
    private void RegenerateShadows()
    {
        _shadowSettings.LightDirection = RenderSettings.sun.transform.forward;

        _shadowComputer.ComputeShadows();
    }
}
