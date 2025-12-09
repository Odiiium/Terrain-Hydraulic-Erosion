using UnityEngine;
using System;

[CreateAssetMenu(fileName = "Shadow Settings", menuName = "Settings/Shadow Settings", order = 0)]
public class ShadowSettings : ScriptableObject
{
    [HideInInspector] public int Size;
    [HideInInspector] public Vector3 LightDirection;
    [HideInInspector] public Vector2 TerrainSize;

    public float HeightScale;
    [Range(1e-4f, .1f)] public float StepSize;
    [Range(0, 1e3f)] public float MaxDistance;
    public float Bias;
    public float Softness;
}