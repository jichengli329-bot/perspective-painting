Shader "PerspectivePuzzle/CeladonLit"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.31, 0.58, 0.50, 1)
        _ShadowColor ("Shadow Color", Color) = (0.12, 0.25, 0.25, 1)
        _HighlightColor ("Glaze Highlight", Color) = (0.72, 0.88, 0.76, 1)
        _Smoothness ("Glaze Smoothness", Range(0, 1)) = 0.42
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.10
        _TopLight ("Top Light", Range(0, 0.5)) = 0.12
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "CeladonForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShadowColor;
                half4 _HighlightColor;
                half _Smoothness;
                half _RimStrength;
                half _TopLight;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                half3 viewDirection = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half ndl = saturate(dot(normal, mainLight.direction));
                half porcelainBand = smoothstep(0.12h, 0.72h, ndl);
                half shadow = lerp(0.56h, 1.0h, mainLight.shadowAttenuation);
                half3 diffuse = lerp(_ShadowColor.rgb, _BaseColor.rgb, porcelainBand) * shadow;

                half3 halfDirection = SafeNormalize(mainLight.direction + viewDirection);
                half specPower = lerp(18.0h, 96.0h, _Smoothness);
                half specular = pow(saturate(dot(normal, halfDirection)), specPower)
                    * lerp(0.04h, 0.30h, _Smoothness);
                half rim = pow(1.0h - saturate(dot(normal, viewDirection)), 3.5h) * _RimStrength;
                half top = pow(saturate(normal.y), 4.0h) * _TopLight;
                half3 ambient = SampleSH(normal) * _BaseColor.rgb * 0.32h;

                half3 color = diffuse * mainLight.color + ambient
                    + _HighlightColor.rgb * (specular + rim + top);
                color = MixFog(color, input.fogFactor);
                return half4(color, _BaseColor.a);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
