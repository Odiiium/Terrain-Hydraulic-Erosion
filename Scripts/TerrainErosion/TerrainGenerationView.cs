using UnityEngine;
using UnityEngine.UI;

public class TerrainGenerationView : MonoBehaviour
{
    [SerializeField] private RawImage _height;
    [SerializeField] private RawImage _water;
    [SerializeField] private RawImage _sediment;
    [SerializeField] private RawImage _outFlow;
    [SerializeField] private RawImage _velocity;

    public Button StartButton;
    public Button RefreshButton;
    public Button EndButton;

    public void Init(RenderTexture height, RenderTexture water, RenderTexture sediment, RenderTexture outflow, RenderTexture velocity)
    {
        _height.texture = height;
        _water.texture = water;
        _sediment.texture = sediment;
        _outFlow.texture = outflow;
        _velocity.texture = velocity;
    }

    public void Init(RenderTexture height)
    {
        _height.texture = height;
    }
}
