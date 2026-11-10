Shader "Universal Render Pipeline/TotalWarWater"
{
    Properties
    {
        [Header(Physically Based Water Color and Transparency)]
        _ShallowColor ("Shallow Color (얕은 물)", Color) = (0.40, 0.58, 0.60, 0.12)
        _DeepColor ("Deep Color (깊은 물)", Color) = (0.05, 0.18, 0.26, 0.75)
        _DepthDistance ("Depth Fade Distance (수심 페이드 거리)", Float) = 3.5

        [Header(Sky and Environment Reflection)]
        _ReflectionIntensity ("Reflection Intensity (하늘 반사 강도)", Range(0.0, 2.0)) = 1.0
        _SkyReflectionTint ("Sky Reflection Tint (하늘 반사 틴트)", Color) = (0.45, 0.65, 0.85, 1.0)
        _FresnelPower ("Fresnel Power (프레넬 반사 지수)", Range(1.0, 8.0)) = 4.5
        _Smoothness ("Smoothness (표면 매끄러움)", Range(0.0, 1.0)) = 0.94

        [Header(Waves and Flow)]
        _BumpMap ("Normal Map (물결 노멀맵)", 2D) = "bump" {}
        _WaveSpeed ("Wave Speed (XY: Wave1, ZW: Wave2)", Vector) = (0.015, 0.01, -0.01, 0.015)
        _WaveScale ("Wave Scale (물결 크기)", Float) = 0.20
        _WaveStrength ("Wave Normal Strength (노멀 강도)", Range(0.0, 2.0)) = 0.45

        [Header(Shoreline and Edge Foam)]
        _FoamColor ("Foam Color (거품 색상)", Color) = (0.95, 0.98, 1.0, 0.75)
        _FoamDistance ("Foam Distance (연안 포말 거리)", Range(0.01, 0.8)) = 0.25
        _FoamIntensity ("Foam Intensity (거품 강도)", Range(0.0, 1.0)) = 0.50
        _FoamNoiseScale ("Foam Noise Scale", Float) = 5.0
        _EdgeSoftness ("Edge Softness (연안 경계선 부드러움)", Range(0.01, 1.0)) = 0.30
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardLitWater"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : NORMAL;
                float4 screenPos    : TEXCOORD3;
                float fogCoord      : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float _DepthDistance;

                float _ReflectionIntensity;
                float4 _SkyReflectionTint;
                float _FresnelPower;
                float _Smoothness;

                float4 _BumpMap_ST;
                float4 _WaveSpeed;
                float _WaveScale;
                float _WaveStrength;

                float4 _FoamColor;
                float _FoamDistance;
                float _FoamIntensity;
                float _FoamNoiseScale;
                float _EdgeSoftness;
            CBUFFER_END

            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float PerlinNoise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i + float2(0.0, 0.0));
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                VertexPositionInputs vertexInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInputs.positionCS;
                output.positionWS = vertexInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BumpMap);
                output.screenPos = ComputeScreenPos(vertexInputs.positionCS);
                output.fogCoord = ComputeFogFactor(vertexInputs.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. 카메라 시선 벡터 및 노멀 벡터 계산
                half3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);

                // 2. 2단 물결 스크롤 및 잔잔한 파동 합성
                float time = _Time.y;
                float2 uv1 = input.uv * _WaveScale + _WaveSpeed.xy * time;
                float2 uv2 = input.uv * _WaveScale * 1.4 + _WaveSpeed.zw * time;

                half3 n1 = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv1));
                half3 n2 = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv2));
                half3 blendedNormalTS = normalize(half3(n1.xy + n2.xy, n1.z * n2.z));

                // 절차적 잔물결 (자연스러운 수면 유동)
                float waveSway1 = sin(input.positionWS.x * 0.3 + time * 0.8) * cos(input.positionWS.z * 0.3 + time * 0.7);
                float waveSway2 = sin(input.positionWS.x * 0.6 - time * 1.1) * cos(input.positionWS.z * 0.6 + time * 0.9);
                blendedNormalTS.xy += float2(waveSway1 + waveSway2 * 0.4, -waveSway1 * 0.8) * 0.08;
                blendedNormalTS = normalize(lerp(half3(0, 0, 1), blendedNormalTS, _WaveStrength));

                half3 normalWS = normalize(input.normalWS + half3(blendedNormalTS.x, 0, blendedNormalTS.y));

                // 3. 실제 수심 계산 (바닥 투과도 연산)
                float2 screenUV = input.screenPos.xy / max(0.0001, input.screenPos.w);
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneLinearDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float surfaceLinearDepth = LinearEyeDepth(input.positionCS.z, _ZBufferParams);

                float rawDiff = sceneLinearDepth - surfaceLinearDepth;
                bool isDepthValid = (rawDepth > 0.0001) && (rawDepth < 0.9999) && (rawDiff >= 0.0);
                float waterDepth = isDepthValid ? rawDiff : 0.25;

                // 4. 투명 물 색상 페이드 (얕은 곳은 바닥 흙/돌/풀이 맑고 투명하게 들여다보임)
                float depthFactor = saturate(waterDepth / max(0.01, _DepthDistance));
                half4 waterBodyColor = lerp(_ShallowColor, _DeepColor, depthFactor);

                // 5. 🌤️ [삼국지 토탈워 하늘 반사] - 하늘의 큐브맵/스카이박스 환경광 실시간 반사
                half3 reflectVector = reflect(-viewDirWS, normalWS);
                half roughness = 1.0 - _Smoothness;
                half3 envProbe = GlossyEnvironmentReflection(reflectVector, roughness, 1.0);

                // 하늘 반구 그라데이션 (스카이박스 유무와 상관없이 대기 원근감 부여)
                half skyGradient = saturate(reflectVector.y * 0.7 + 0.3);
                half3 skyAtmosphere = lerp(_SkyReflectionTint.rgb * 0.8, _SkyReflectionTint.rgb * 1.25, skyGradient);
                half envLuma = dot(envProbe, half3(0.299, 0.587, 0.114));
                half3 skyReflection = lerp(skyAtmosphere, envProbe, saturate(envLuma * 2.0)) * _ReflectionIntensity;

                // 6. 🌊 물리 기반 프레넬 (Schlick's Fresnel)
                // 위에서 내려다보면 물은 맑고 투명하여 바닥(돌, 흙, 잔디)이 훤히 비치고,
                // 시선이 비스듬해질수록 하늘(Skybox)의 푸른빛이 거울처럼 반사됨!
                half NdotV = saturate(dot(normalWS, viewDirWS));
                half fresnel = pow(1.0 - NdotV, _FresnelPower);
                fresnel = lerp(0.02, 0.98, fresnel); // 물의 물리 굴절률(F0=0.02) 기준 반사 블렌딩

                // 물 자체 색상 위에 하늘 반사광 합성
                half3 finalRGB = lerp(waterBodyColor.rgb, skyReflection, fresnel);
                // 위에서 볼 때는 바닥이 맑게 투과(낮은 알파), 비스듬히 볼 때는 하늘 반사광으로 거울 수면 형성
                half finalAlpha = saturate(lerp(waterBodyColor.a, 0.92, fresnel));

                // 7. 메인 라이트 태양광 스펙큘러 하이라이트
                Light mainLight = GetMainLight();
                half3 halfDir = normalize(mainLight.direction + viewDirWS);
                half NdotH = saturate(dot(normalWS, halfDir));
                half specIntensity = pow(NdotH, _Smoothness * 256.0) * _Smoothness;
                finalRGB += mainLight.color * specIntensity * 0.9;

                // 8. 잔잔한 물가 연안 포말 (Edge Foam)
                if (isDepthValid && waterDepth <= _FoamDistance)
                {
                    float foamNoise = PerlinNoise2D(input.positionWS.xz * _FoamNoiseScale + time * 0.3);
                    float foamFactor = 1.0 - saturate(waterDepth / max(0.001, _FoamDistance * (0.8 + 0.4 * foamNoise)));
                    foamFactor = smoothstep(0.2, 0.95, foamFactor) * _FoamIntensity;
                    finalRGB = lerp(finalRGB, _FoamColor.rgb, foamFactor);
                    finalAlpha = saturate(finalAlpha + foamFactor * 0.4);
                }

                // 9. 🌊 [삼국지 토탈워 핵심] 연안 소프트 블렌딩 (Soft Shoreline Intersection)
                // 수면 메쉬와 지형이 만나는 접점(waterDepth -> 0)에서 칼자국(Hard Edge)을 완전히 제거!
                // 물이 지형 속으로 솜사탕처럼 부드럽게 스며들도록 알파를 페이드 처리하여 메쉬 경계면을 100% 자연화
                if (isDepthValid)
                {
                    half softEdge = smoothstep(0.002, max(0.01, _EdgeSoftness), waterDepth);
                    finalAlpha *= softEdge;
                }

                // 10. 안개(Fog) 적용
                finalRGB = MixFog(finalRGB, input.fogCoord);

                return half4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
