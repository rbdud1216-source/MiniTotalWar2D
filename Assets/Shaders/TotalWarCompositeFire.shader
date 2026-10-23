Shader "TotalWar/VFX/CompositeFlames"
{
    Properties
    {
        [Header(Three Kingdoms Fire Flipbook Textures)]
        _MainTex ("Core Flame A (8x8 Flipbook)", 2D) = "white" {}
        _OuterTex ("Outer Flame B (8x8 Flipbook)", 2D) = "white" {}
        _TipsTex ("Tips Flame H (8x8 Flipbook)", 2D) = "white" {}

        [Header(HDR Color and Multipliers)]
        [HDR] _CoreColor ("Core Flame A Color (HDR)", Color) = (2.0, 1.45, 0.90, 0.95)
        [HDR] _OuterColor ("Outer Flame B Color (HDR)", Color) = (1.50, 0.75, 0.22, 0.85)
        [HDR] _TipsColor ("Tips Flame H Color (HDR)", Color) = (1.15, 0.50, 0.14, 0.75)
        _GlowIntensity ("Glow Intensity", Range(0.5, 3.0)) = 0.90
        _SoftFalloff ("Atmospheric Soft Falloff", Range(0.5, 2.5)) = 1.25

        [Header(Blending)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1 // One
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10 // OneMinusSrcAlpha
    }

    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "RenderType"="Transparent" 
            "RenderPipeline"="UniversalPipeline" 
            "IgnoreProjector"="True" 
            "PreviewType"="Plane"
        }

        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 uv2AndBlend : TEXCOORD1; // xy = next frame UV, z = blend factor
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 blendData : TEXCOORD1; // xy = uv2, z = blend factor
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_OuterTex);
            SAMPLER(sampler_OuterTex);
            TEXTURE2D(_TipsTex);
            SAMPLER(sampler_TipsTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _OuterTex_ST;
                float4 _TipsTex_ST;
                half4 _CoreColor;
                half4 _OuterColor;
                half4 _TipsColor;
                half _GlowIntensity;
                half _SoftFalloff;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.blendData = float3(input.uv2AndBlend.xy, input.uv2AndBlend.z);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // [삼국지 토탈워 공식 3종 화염(A, B, H) 1:1 동시 포개어짐 합성]
                // 8x8 플립북 파티클 UV 좌표를 공유하여 세 텍스처를 정확히 동일한 프레임에서 동시 샘플링
                half4 colA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 colB = SAMPLE_TEXTURE2D(_OuterTex, sampler_OuterTex, input.uv);
                half4 colH = SAMPLE_TEXTURE2D(_TipsTex, sampler_TipsTex, input.uv);

                // 프레임 간 크로스페이드 보간 (Flipbook Smooth Blending)
                half blend = input.blendData.z;
                if (blend > 0.001)
                {
                    half4 colA2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.blendData.xy);
                    half4 colB2 = SAMPLE_TEXTURE2D(_OuterTex, sampler_OuterTex, input.blendData.xy);
                    half4 colH2 = SAMPLE_TEXTURE2D(_TipsTex, sampler_TipsTex, input.blendData.xy);
                    colA = lerp(colA, colA2, blend);
                    colB = lerp(colB, colB2, blend);
                    colH = lerp(colH, colH2, blend);
                }

                // 1. 코어(A): 밝은 백황색 고휘도 중심 화염
                half3 rgbA = colA.rgb * _CoreColor.rgb;
                half alphaA = colA.a * _CoreColor.a;

                // 2. 외곽 너울(B): 화염의 풍성한 주황빛 일렁임
                half3 rgbB = colB.rgb * _OuterColor.rgb;
                half alphaB = colB.a * _OuterColor.a;

                // 3. 상단 팁(H): 하늘로 치솟으며 날름거리는 끝단 불꽃
                half3 rgbH = colH.rgb * _TipsColor.rgb;
                half alphaH = colH.a * _TipsColor.a;

                // [3중 복합 가산 및 볼륨 합성]
                // 코어 중심부는 Additive 효과로 눈부시게 타오르고, 외곽은 볼륨감 있게 감싸줌
                half3 finalRgb = (rgbA * alphaA * 1.35) + (rgbB * alphaB * 1.0) + (rgbH * alphaH * 0.75);
                finalRgb *= input.color.rgb * _GlowIntensity;

                half finalAlpha = saturate(alphaA * 1.2 + alphaB * 0.9 + alphaH * 0.7) * input.color.a;

                // 부드러운 대기 감쇄 (Atmospheric Soft Diffusion)
                // 칼날처럼 쨍하게 잘리는 경계를 연기처럼 부드럽게 감쇄시켜 현실적인 삼탈워 대기 화염 완성
                finalAlpha = pow(finalAlpha, _SoftFalloff);

                // Pre-multiplied Alpha 출력: 밝은 코어는 은은하게 빛나고 외곽은 연기처럼 자연스럽게 블렌딩
                return half4(finalRgb * finalAlpha, finalAlpha);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Particles/Unlit"
}
