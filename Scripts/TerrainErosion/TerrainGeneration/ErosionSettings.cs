using System;
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
    public ErosionStep ErosionFlags;

    [Flags]
    public enum ErosionStep
    {
        Rainfall = 0x1, OutflowCalculation = 0x2, VelocityCalculation = 0x4, Erosion = 0x8, SedimentTransport = 0x16, Evaporation = 0x32
    }
}