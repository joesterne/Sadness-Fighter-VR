// Comfort vignette for stick walking. Drawn on a quad just ahead of the eyes, on top of everything else.
// The fade is measured as the angle from each eye's own view direction, so both eyes see the soft edge in the same place
// and the clear centre does not shift with the headset's lens offsets or asymmetric field of view.
Shader "After Hours/Comfort vignette"
{
    Properties
    {
        _Color("Color", Color) = (0.025, 0.04, 0.07, 1)
        _Strength("Strength", Range(0, 1)) = 1
        _Inner("Clear view (degrees from centre)", Range(5, 80)) = 30
        _Outer("Full vignette (degrees from centre)", Range(10, 90)) = 58
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Overlay" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Comfort vignette"
            Tags { "LightMode" = "UniversalForward" }
            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Strength;
                float _Inner;
                float _Outer;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 view = normalize(input.positionWS - _WorldSpaceCameraPos.xyz);
                // Row 2 of the view matrix is the eye's backward axis in world space.
                float3 forward = -UNITY_MATRIX_V[2].xyz;
                float facing = dot(view, forward);
                float edge = 1.0 - smoothstep(cos(radians(_Outer)), cos(radians(_Inner)), facing);
                return half4(_Color.rgb, edge * _Strength * _Color.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
