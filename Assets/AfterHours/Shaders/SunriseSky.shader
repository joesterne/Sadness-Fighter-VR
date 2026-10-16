// The rooftop's sunrise sky: a painted-looking gradient with a flat, glowing sun and a soft halo, all worked out per pixel from the view direction.
// There is no texture, so there is nothing to band, stream or seam, and the whole sky is one cheap pass. A whisper of noise breaks up the 8-bit steps.
Shader "After Hours/Sunrise sky"
{
    Properties
    {
        _Zenith("Straight up", Color) = (0.31, 0.49, 0.90, 1)
        _High("High sky", Color) = (0.53, 0.66, 0.94, 1)
        _Mid("Mid sky", Color) = (0.91, 0.70, 0.85, 1)
        _Low("Low sky", Color) = (1.0, 0.70, 0.60, 1)
        _Horizon("Horizon", Color) = (1.0, 0.85, 0.56, 1)
        _Ground("Below the horizon", Color) = (0.94, 0.72, 0.63, 1)
        _SunColor("Sun", Color) = (1.0, 0.97, 0.79, 1)
        _GlowColor("Sun glow", Color) = (1.0, 0.76, 0.43, 1)
        _SunDir("Direction to the sun", Vector) = (0.4, 0.2, 0.9, 0)
        _SunRadius("Sun radius (radians)", Float) = 0.06
    }
    SubShader
    {
        Tags { "RenderType" = "Background" "Queue" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        Pass
        {
            Name "Sunrise sky"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Zenith;
                half4 _High;
                half4 _Mid;
                half4 _Low;
                half4 _Horizon;
                half4 _Ground;
                half4 _SunColor;
                half4 _GlowColor;
                float4 _SunDir;
                float _SunRadius;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 view = normalize(input.direction);
                float up = saturate(view.y);
                float down = saturate(-view.y);

                // From the horizon up: gold, peach, pink, soft blue, morning blue.
                float3 color = lerp(_Horizon.rgb, _Low.rgb, smoothstep(0.0, 0.10, up));
                color = lerp(color, _Mid.rgb, smoothstep(0.10, 0.30, up));
                color = lerp(color, _High.rgb, smoothstep(0.30, 0.58, up));
                color = lerp(color, _Zenith.rgb, smoothstep(0.58, 1.0, up));
                color = lerp(color, _Ground.rgb, smoothstep(0.0, 0.25, down));

                // The dawn glow along the horizon, strongest toward the sun.
                float3 sunDir = normalize(_SunDir.xyz);
                float2 flatView = normalize(view.xz + 1e-5);
                float2 flatSun = normalize(sunDir.xz + 1e-5);
                float toward = saturate(dot(flatView, flatSun));
                float band = exp(-pow(abs(view.y) * 5.0, 2.0));
                color += _GlowColor.rgb * 0.38 * band * pow(toward, 3.0);

                // The sun: a flat disc with a slightly paler rim, a tight halo and a wide soft one.
                float angle = acos(clamp(dot(view, sunDir), -1.0, 1.0));
                color += _GlowColor.rgb * (0.30 * exp(-angle * angle / 0.045) + 0.26 * exp(-angle * angle / 0.55));
                float disc = 1.0 - smoothstep(_SunRadius * 0.93, _SunRadius, angle);
                float centre = 1.0 - smoothstep(0.0, _SunRadius, angle);
                float3 rim = lerp(_SunColor.rgb, _GlowColor.rgb, 0.7);
                float3 sun = lerp(rim, _SunColor.rgb, smoothstep(0.2, 0.9, centre));
                color = lerp(color, sun, disc);

                float2 pixel = input.positionCS.xy;
                float noise = frac(sin(dot(pixel, float2(12.9898, 78.233))) * 43758.5453);
                color += (noise - 0.5) / 255.0;
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
