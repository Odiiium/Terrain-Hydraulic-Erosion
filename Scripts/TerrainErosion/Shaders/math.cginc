
float4 remap(float4 value, float4 inMin, float4 inMax, float4 outMin, float4 outMax)
{
    return (value - inMin) / (inMax - inMin) * (outMax - outMin) + outMin;
}

float remap(float value, float inMin, float inMax, float outMin, float outMax)
{
    return (value - inMin) / (inMax - inMin) * (outMax - outMin) + outMin;
}

float vectorContractionByOne(float4 vec)
{
    return dot(vec, float4(1, 1, 1, 1));
}

float vectorContractionByOne(float3 vec)
{
    return dot(vec, float3(1, 1, 1));
}