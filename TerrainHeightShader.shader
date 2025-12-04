Shader "Unlit/TerrainLit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Amplitude("Amplitude", Float) = 1
        _Size ("Size", Float) = 1

        _SpecularColor("Specular Color", Color) = (1,1,1,1)
        _Shininess("Shininess", Range(1,200)) = 50
        _Ambient("Ambient", Range(0,1)) = 0.2

        _HeightMap("Height", 2D) = "white" {}
        _SedimentMap("Sediment", 2D) = "white" {}
        _WaterMap("Water", 2D) = "white"{}

        _HeightColor("Height Color", Color) = (1,1,1,1)
        _SedimentColor("Sediment Color", Color) = (1,1,1,1)
        _WaterColor("Water Color", Color) = (1,1,1,1)
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

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            struct appdata
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
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            sampler2D _HeightMap;
            sampler2D _SedimentMap;
            sampler2D _WaterMap;

            float4 _HeightColor;
            float4 _SedimentColor;
            float4 _WaterColor;

            float _Size;
            float _Amplitude;

            float4 _SpecularColor;
            float  _Shininess;
            float  _Ambient;

            float4 remap(float4 value, float4 inMin, float4 inMax, float4 outMin, float4 outMax)
            {
                return (value - inMin) / (inMax - inMin) * (outMax - outMin) + outMin;
            }

            v2f vert (appdata v)
            {
                v2f o;
                
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                float2 pos = (v.vertex.xz + _Size / 2) / _Size;
                float h = tex2Dlod(_HeightMap, float4(pos,0,0)).r;

                float3 displaced = float3(v.vertex.x, h * _Amplitude, v.vertex.z);

                o.vertex = UnityObjectToClipPos(float4(displaced,1));
                o.worldPos = mul(unity_ObjectToWorld, float4(displaced,1)).xyz;

                o.worldNormal = UnityObjectToWorldNormal(v.normal);

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float4 water = tex2D(_WaterMap, i.uv);
                float4 height = tex2D(_HeightMap, i.uv);
                float4 sediment = tex2D(_SedimentMap, i.uv);

                float sedimentFactor = log10(sediment.x);
                float waterFactor = log10(water.x);

                sedimentFactor = remap(sedimentFactor, 0, -4, 1, 0);
                waterFactor = remap(waterFactor, 0, -4, 1, 0);

                float4 terrainColor = lerp(_HeightColor * height.x, _SedimentColor, saturate(sedimentFactor));
                
                terrainColor += waterFactor > 0 ? _WaterColor * _WaterColor.a : terrainColor;

                float3 N = normalize(i.worldNormal);

                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 H = normalize(L + V);

                float diff = max(dot(N, L), 0);

                float spec = pow(max(dot(N, H), 0), _Shininess);

                float3 color =
                    terrainColor * (diff + _Ambient)
                    ;//+ _SpecularColor.rgb * spec * float4(1,1,1,1);

                return float4(color, 1);
            }
            ENDCG
        }
    }
}
