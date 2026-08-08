Shader "PerspectivePuzzle/ToyLit"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _ShadowTint ("Shadow Tint", Range(0, 1)) = 0.68
        _Highlight ("Highlight", Range(0, 1)) = 0.16
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ToyForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _ShadowTint;
                half _Highlight;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                half3 lightDirection = normalize(half3(-0.45h, 0.82h, -0.35h));
                half diffuse = saturate(dot(normal, lightDirection));
                half stepped = smoothstep(0.18h, 0.72h, diffuse);
                half lighting = lerp(_ShadowTint, 1.0h, stepped);
                half topGlow = pow(saturate(normal.y), 5.0h) * _Highlight;
                return half4(_BaseColor.rgb * (lighting + topGlow), _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
