Shader "PerspectivePuzzle/PaintingObjectId"
{
    Properties
    {
        _ObjectIdColor ("Object ID Color", Color) = (1, 1, 1, 1)
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 100

        // Minimal opaque unlit pass for the machine-readable piece-ID render
        // target. Fragment writes the exact material _BaseColor bytes with no
        // lighting, textures, fog, blending or color effects; projection
        // comes from URP Core's TransformObjectToHClip, which follows the
        // view/projection matrices set by the evaluator's CommandBuffer.
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Cull Back
            ZTest [_ZTest]
            ZWrite On
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ObjectIdColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                return _ObjectIdColor;
            }
            ENDHLSL
        }
    }
}
