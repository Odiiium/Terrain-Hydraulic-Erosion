using UnityEngine;

[CreateAssetMenu(fileName = "Erosion Settings", menuName = "Settings/Erosion Settings", order = 0)]
public class ErosionSettings : ScriptableObject
{
    public int Size;
    public float CellSize;
    public int IterationsPerFrame;
    [Range(0f, 1f)] public float Evaporation;
    [Range(0f, 1f)] public float Rainfall;
    public float CapacityFactor;
    public float MinSlope;
    public float DepositSpeed;
    public float ErodeSpeed;
    public float Gravity;
    public float TimeStep;
    public int RainDensity;
}