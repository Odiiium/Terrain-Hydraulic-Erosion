Shader "Unlit/TerrainLit"
{
    Properties
    {
        [Header(Terrain Data)][Space(5)]
        _Amplitude("Amplitude", Float) = 1
        _Size ("Size", Float) = 1

        [Header(Textures Data)][Space(5)]
        _HeightMap("Height", 2D) = "white" {}
        _SedimentMap("Sediment", 2D) = "white" {}
        _WaterMap("Water", 2D) = "white"{}

        [Header(Visual Texturing)][Space(5)]
        _SedimentTexture("Sediment Texture", 2D) = "white" {}
        _GroundTexture("Ground Texture", 2D) = "white" {}
        _NoiseTexture ("Noise Texture", 2D) = "white" {}
        _SnowTexture ("Snow texture", 2D) = "white" {}

        [Header(Shadows)][Space(5)]
        _ShadowTexture("Shadow Texture", 2D) = "white" {}

        [Header(Blend settings)][Space(5)]
        _SlopeThreshold ("Grass Slope Threshold", Range(0, 1) ) = .5
        _BlendAmount("Blend Amount", Range(0, .5)) = .02
        _SnowThreshold ("Snow Slope Threshold", Range(0, 1)) = .5
        _SnowBlendAmount("Snow Blend Amount", Range(0, 1)) = .02
        _SnowHeightCoefficient ("Snow Height Coefficient", Range(0, 2)) = 1.3

        [Header(Color Data)][Space(5)]
        _HeightColor("Height Color", Color) = (1,1,1,1)
        _SedimentColor("Sediment Color", Color) = (1,1,1,1)
        _WaterColor("Water Color", Color) = (1,1,1,1)

        [Header(Lighting)][Space(5)]
        _SpecularColor("Specular Color", Color) = (1,1,1,1)
        _Shininess("Shininess", Range(1,200)) = 50
        _Ambient("Ambient", Range(0,1)) = 0.2

        [Toggle(USE_WATER)] _UseWater ("Use Water", Float) = 0
        [Toggle(USE_SIMPLE_COLORING)] _UseSimpleColoring ("Use Simple Color Model", Float) = 0

        [Header(Normal Smoothing)][Space(5)]
        [Toggle(USE_NORMAL_SMOOTHING)] _UseNormalSmooth ("Use Normal Smoothing", Float) = 0
        _NormalSmooth ("Normal smooth", Range(0,1)) = .5

        _DisplacementMap ("Displacement Map", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "white" {}

        [Header(Curvature)][Space(5)]
        _CurvatureCapacity ("Curvature capacity", Range(0, 1)) = .8
        _CurvaturePower("Curvature Power", Range(0, 150)) = 10
        _CurvatureAddition("Curvature Change", Range(0, 1)) = .15
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull OFf

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_local _ USE_WATER
            #pragma multi_compile_local _ USE_NORMAL_SMOOTHING
            #pragma multi_compile_local _ USE_SIMPLE_COLORING

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            #include "math.cginc"

            struct Input
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                float4 vertex : SV_POSITION;

                float curvature : TEXCOORD3;
            };

            float4 _HeightMap_ST;
            float4 _HeightMap_TexelSize;

            sampler2D _HeightMap;
            sampler2D _SedimentMap;
            sampler2D _WaterMap;
            sampler2D _ShadowTexture;

            sampler2D _DisplacementMap;
            sampler2D _NormalMap;

            sampler2D _SedimentTexture;
            sampler2D _GroundTexture;
            sampler2D _NoiseTexture;
            sampler2D _SnowTexture;

            float4 _GroundTexture_ST;
            float4 _SedimentTexture_ST;
            float4 _SnowTexture_ST;
            float4 _DisplacementMap_ST;

            float4 _HeightColor;
            float4 _SedimentColor;
            float4 _WaterColor;

            float _Size;
            float _Amplitude;
            float _SnowHeightCoefficient;

            float _SlopeThreshold;
            float _BlendAmount;
            float _SnowThreshold;
            float _SnowBlendAmount;

            float4 _SpecularColor;
            float  _Shininess;
            float  _Ambient;

            float _NormalSmooth;

            float _CurvatureCapacity;
            float _CurvaturePower;
            float _CurvatureAddition;

            float sampleHeight(float2 uv)
            {
                return tex2Dlod(_HeightMap, float4(uv, 0, 0)).r * _Amplitude;
            }

            float3 calculateNormal(float2 vertexPos)
            {
                float2 texel = 1.0 / _HeightMap_TexelSize.w;
                float cellSize = _Size / _HeightMap_TexelSize.w;

                float2 uv = (vertexPos + _Size * .5) / _Size;

                float h = sampleHeight(uv);

                float hL = sampleHeight(uv + float2(-texel.x, 0));
                float hR = sampleHeight(uv + float2(texel.x, 0));
                float hD = sampleHeight(uv + float2(0, -texel.y));
                float hU = sampleHeight(uv + float2(0, texel.y));

                float dX = hR - hL;
                float dZ = hU - hD;

                #ifdef USE_NORMAL_SMOOTHING
                    float hLL = sampleHeight(uv + float2(texel.x * -2, 0));
                    float hRR = sampleHeight(uv + float2(texel.x * 2, 0));
                    float hDD = sampleHeight(uv + float2(0, texel.y * -2));
                    float hUU = sampleHeight(uv + float2(0, texel.y * 2));

                    float dX2 = (hRR - hLL) * 0.25;
                    float dZ2 = (hUU - hDD) * 0.25;

                    dX = lerp(dX, dX2, _NormalSmooth);
                    dZ = lerp(dZ, dZ2, _NormalSmooth);
                #endif

                return float3(-dX, 2 * cellSize, -dZ);
            }

            v2f vert (Input IN)
            {
                v2f o;
                
                o.uv = TRANSFORM_TEX(IN.uv, _HeightMap);

                float2 pos = (IN.vertex.xz + _Size * .5) / _Size;
                float h = tex2Dlod(_HeightMap, float4(pos,0,0)).r;

                float3 displaced = float3(IN.vertex.x, h * _Amplitude, IN.vertex.z);

                o.vertex = UnityObjectToClipPos(float4(displaced, 1) );
                o.worldPos = mul(unity_ObjectToWorld, float4(displaced,1)).xyz;

                o.worldNormal = calculateNormal(IN.vertex.xz);

                float3 upNormal = calculateNormal(IN.vertex.xz + float2(0, _Size /_HeightMap_TexelSize.w));
                float3 rightNormal = calculateNormal(IN.vertex.xz + float2(_Size /_HeightMap_TexelSize.w, 0));

                o.curvature = (dot(o.worldNormal, rightNormal) + dot(o.worldNormal, upNormal)) * 0.5;

                return o;
            }

            float4 calculateWaterColor(float4 water, float4 height, float3 N, float3 V, float spec, float4 terrainColor)
            {
                float terrainHeight = height.x;
                float waterHeight = water.x;

                float depth = max(waterHeight - terrainHeight, -.2); 

                float maxDepth = .1;
                float waterAlpha = saturate(1 - depth / maxDepth);

                float absorption = exp(-depth * 8);

                float transparency = pow(saturate(water * 12), 1.5);   

                float fresnel = pow(1 - saturate(dot(N, V)), 4);

                float3 waterBase = _WaterColor.rgb;
                float3 waterSpec = fresnel * spec * _SpecularColor.rgb * 5;

                float3 finalWaterColor = waterBase * absorption + waterSpec;

                float3 blended = lerp(terrainColor.rgb, finalWaterColor, transparency);

                return float4(blended, 1);
            }

            float4 calculateTerrainColor(v2f i, float4 height)
            {   
                float curvature = i.curvature;

                curvature = (curvature - _CurvatureCapacity) * _CurvaturePower;

                float slope = 1 - length(i.worldNormal);

                float4 noise = tex2D(_NoiseTexture, i.uv); 

                #ifdef USE_SIMPLE_COLORING
                    float4 sedimentColor = _SedimentColor;
                    float4 groundColor = _HeightColor;
                    float4 snowColor = float4(1,1,1,1);
                #else 
                    float2 sedUv = TRANSFORM_TEX(i.uv, _SedimentTexture);
                    float2 groundUv = TRANSFORM_TEX(i.uv, _GroundTexture);
                    float2 snowUv = TRANSFORM_TEX(i.uv, _SnowTexture);

                    float4 sedimentColor = tex2D(_SedimentTexture, sedUv);
                    float4 groundColor = tex2D(_GroundTexture, groundUv);
                    float4 snowColor = tex2D(_SnowTexture, snowUv);
                #endif

                float grassBlendAmount = _SlopeThreshold * (1 - _BlendAmount);
                float grassWeight = 1 - saturate((slope - grassBlendAmount) / (_SlopeThreshold  - grassBlendAmount));
                float4 color = groundColor * (1 - grassWeight) + sedimentColor * grassWeight * slope;

                float snowBlendAmount = _SnowThreshold * (1 - _SnowBlendAmount);
                float snowWeight = 1 - saturate((slope * 1.01 - snowBlendAmount) / (_SnowThreshold  - snowBlendAmount));
                float snowBlended = snowColor * (1 - snowWeight) + color * (snowWeight);

                color = height.x > _SnowHeightCoefficient * (1 - noise.r) ? snowBlended : color;

                return color;
            }

            float2 getDisplacementUv(v2f i, float displacementFactor)
            {
                float2 displacementUv = TRANSFORM_TEX(i.uv,_DisplacementMap);
                float3 normal = tex2D(_DisplacementMap, displacementUv).xyz * 2 - 1;
                normal = normalize(normal);

                return i.uv + normal.xz * displacementFactor;
            }

            float getShadow(v2f i)
            {
                float remapLow = .12;
                float shadow = tex2D(_ShadowTexture, i.uv);
                return remap(shadow, 0, 1, remapLow, 1 - remapLow);
            }

            float3 RRTAndODTFit(float3 v)
            {
                float3 a = v * (v + 0.0245786) - 0.000090537;
                float3 b = v * (0.983729 * v + 0.4329510) + 0.238081;
                return a / b / .85;
            }
            
            float3 tonemapACESFilm(float3 color)
            {
                color = RRTAndODTFit(color);
            
                return saturate(color);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float curvature = i.curvature;

                curvature = (curvature - .65) *5; // _CurvaturePower;

                float4 water = tex2D(_WaterMap, i.uv);
                float4 height = tex2D(_HeightMap, i.uv);

                float waterFactor = log10(water.x);
                waterFactor = remap(waterFactor, 0, -4, 1, 0);

                float4 terrainColor = calculateTerrainColor(i, height);

                float3 N = normalize(i.worldNormal);
                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float3 H = normalize(L + V);

                float diffuse = max(dot(N, L), 0);
                float spec = pow(max(dot(N, H), 0), _Shininess);

                float4 w = float4(0,0,0,0);

                #ifdef USE_WATER
                    if (waterFactor > .1e-3)
                    {
                        w = calculateWaterColor(water, height, N, V, spec, terrainColor);
                        return  w;
                    }
                #endif

                float3 color = w.w > 0 ? w : terrainColor; 

                color *= (diffuse + _Ambient);//+ _SpecularColor.rgb * spec * float4(1,1,1,1);
                color += curvature * _CurvatureAddition;
                color = tonemapACESFilm(color);
                color *= getShadow(i);

                return float4(color, 1);
            }

            ENDCG
        }
    }
}
