
float4 remap(float4 value, float4 inMin, float4 inMax, float4 outMin, float4 outMax)
{
    return (value - inMin) / (inMax - inMin) * (outMax - outMin) + outMin;
}

float remap(float value, float inMin, float inMax, float outMin, float outMax)
{
    return (value - inMin) / (inMax - inMin) * (outMax - outMin) + outMin;
}

float VectorContractionByOne(float4 vec)
{
    return dot(vec, float4(1, 1, 1, 1));
}

float VectorContractionByOne(float3 vec)
{
    return dot(vec, float3(1, 1, 1));
}

int clampInt(int v, int a, int b)
{
    return max(a, min(v, b));
}

float CosSlope(int2 pixel, float2 dh)
{
    return 1 / sqrt(1 + dh.x * dh.x + dh.y * dh.y);
}

inline float SinSlope(int2 pixel, float2 dh)
{
    float cosSlope = CosSlope(pixel, dh);
    return sqrt(1 - cosSlope * cosSlope);

}

float SampleBilinear(RWTexture2D<float> tex, float2 srcUV, int2 texSize)
{
    float2 pos = srcUV * (float2) texSize - 0.5;

    int2 iPos = int2(floor(pos));
    float2 f = pos - (float2) iPos;

    int2 i00 = int2(clampInt(iPos.x, 0, texSize.x - 1), clampInt(iPos.y, 0, texSize.y - 1));
    int2 i10 = int2(clampInt(iPos.x + 1, 0, texSize.x - 1), clampInt(iPos.y, 0, texSize.y - 1));
    int2 i01 = int2(clampInt(iPos.x, 0, texSize.x - 1), clampInt(iPos.y + 1, 0, texSize.y - 1));
    int2 i11 = int2(clampInt(iPos.x + 1, 0, texSize.x - 1), clampInt(iPos.y + 1, 0, texSize.y - 1));

    float v00 = tex.Load(int3(i00, 0));
    float v10 = tex.Load(int3(i10, 0));
    float v01 = tex.Load(int3(i01, 0));
    float v11 = tex.Load(int3(i11, 0));

    float vx0 = lerp(v00, v10, f.x);
    float vx1 = lerp(v01, v11, f.x);
    float vxy = lerp(vx0, vx1, f.y);

    return vxy;
}

float2 CalculateTextureDerivative(RWTexture2D<float> tex, int2 pixel, int dx, int dy)
{
    uint width, height;
    tex.GetDimensions(width, height);
    
    float xLeft = clampInt(pixel.x - dx, 0, width - 1);
    float xRight = clampInt(pixel.x + dx, 0, width - 1);
    float yBottom = clampInt(pixel.y - dy, 0, height - 1);
    float yTop = clampInt(pixel.y + dy, 0, height - 1);

    float hLeft = tex[float2(xLeft, pixel.y)];
    float hRight = tex[float2(xRight, pixel.y)];
    float hBottom = tex[float2(pixel.x, yBottom)];
    float hTop = tex[float2(pixel.x, yTop)];
    
    float dhdx = (hRight - hLeft) * .5 * dx;
    float dhdy = (hTop - hBottom) * .5 * dy;

    return (dhdx, dhdy);
}