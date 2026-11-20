Shader "Universal Render Pipeline/TotalWarWater"
{
    Properties
    {
        [Header(Water_Type_And_Flow)]
        _FlowMode ("수계 모드 (0: 바다/호수, 1: 강물 유속, 2: 늪지)", Range(0.0, 2.0)) = 0.0
        _FlowSpeed ("강물 유속 속도", Range(0.0, 3.0)) = 0.55

        [Header(Physically_Based_Water_Color_And_Absorption)]
        _ShallowColor ("얕은 물 맑은 색조 (Shallow Color)", Color) = (0.22, 0.50, 0.58, 0.35)
        _DeepColor ("깊은 대양 색조 (Deep Color)", Color) = (0.04, 0.22, 0.42, 1.0)
        _DepthDistance ("수심 색상 전환 거리 (m)", Float) = 5.5
        _Clarity ("수질 투명도 (높을수록 얕은 물이 맑음)", Range(0.1, 12.0)) = 5.5
        _AbsorptionRGB ("수심 RGB 광흡수율 (Beer-Lambert)", Vector) = (0.48, 0.19, 0.10, 1.0)
        _RefractionStrength ("수중 굴절 왜곡 강도", Range(0.0, 0.10)) = 0.014
        _WetBankDarken ("물가 젖은 바닥 어두움", Range(0.0, 0.5)) = 0.18

        [Header(Waves_And_Surface_Normals)]
        _BumpMap ("추가 물결 노멀맵 (선택 사항)", 2D) = "bump" {}
        _WaveSpeed ("물결 스크롤 속도 (XY: 1층, ZW: 2층)", Vector) = (0.02, 0.015, -0.015, 0.02)
        _WaveScale ("물결 밀도 스케일", Float) = 0.22
        _WaveStrength ("물결 굴곡 노멀 강도", Range(0.0, 1.5)) = 0.28
        _GerstnerAmp ("입체 파도 기복 높이 (Gerstner)", Range(0.0, 1.0)) = 0.09
        _WaveChoppiness ("파도 거칠기 (Choppiness)", Range(0.0, 1.0)) = 0.32

        [Header(Sky_Reflection_Fresnel_And_Specular)]
        _ReflectionIntensity ("하늘·환경 반사 강도", Range(0.0, 2.0)) = 0.95
        _SkyReflectionTint ("하늘 반사 기본 틴트", Color) = (0.48, 0.72, 0.95, 1.0)
        _FresnelPower ("프레넬 반사 지수", Range(1.0, 8.0)) = 4.6
        _Smoothness ("표면 매끄러움 (광택)", Range(0.0, 1.0)) = 0.95
        _SunSpecularPower ("태양 반짝임 선명도", Range(32.0, 1024.0)) = 520.0
        _SunSpecularIntensity ("태양 반짝임 강도", Range(0.0, 5.0)) = 1.85
        _SubsurfaceColor ("파도 햇살 투과색 (SSS)", Color) = (0.08, 0.42, 0.48, 1.0)
        _SubsurfaceStrength ("파도 햇살 투과 세기", Range(0.0, 2.0)) = 0.32

        [Header(Caustics_And_Shoreline_Foam)]
        _CausticsStrength ("수중 미세 빛결 세기 (기본 0)", Range(0.0, 1.0)) = 0.0
        _CausticsScale ("빛결 무늬 크기", Float) = 0.9
        _FoamColor ("물가 포말 색상 (Foam Color)", Color) = (0.94, 0.97, 1.0, 0.75)
        _FoamDistance ("물가 얇은 거품 폭 (m)", Range(0.01, 1.0)) = 0.14
        _FoamIntensity ("물가 거품 강도", Range(0.0, 1.0)) = 0.22
        _FoamNoiseScale ("거품 결 밀도", Float) = 3.2
        _WhitecapThreshold ("강풍 흰파도(Whitecap) 임계값", Range(0.2, 1.0)) = 0.72
        _EdgeSoftness ("물가 경계 부드러움 (m)", Range(0.01, 1.0)) = 0.18
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

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

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
                float3 normalWS     : TEXCOORD2;
                float4 screenPos    : TEXCOORD3;
                float fogCoord      : TEXCOORD4;
                float waveCrest     : TEXCOORD5;
                float windFactor    : TEXCOORD6;
            };

            CBUFFER_START(UnityPerMaterial)
                float _FlowMode;
                float _FlowSpeed;

                float4 _ShallowColor;
                float4 _DeepColor;
                float _DepthDistance;
                float _Clarity;
                float4 _AbsorptionRGB;
                float _RefractionStrength;
                float _WetBankDarken;

                float4 _BumpMap_ST;
                float4 _WaveSpeed;
                float _WaveScale;
                float _WaveStrength;
                float _GerstnerAmp;
                float _WaveChoppiness;

                float _ReflectionIntensity;
                float4 _SkyReflectionTint;
                float _FresnelPower;
                float _Smoothness;
                float _SunSpecularPower;
                float _SunSpecularIntensity;
                float4 _SubsurfaceColor;
                float _SubsurfaceStrength;

                float _CausticsStrength;
                float _CausticsScale;
                float4 _FoamColor;
                float _FoamDistance;
                float _FoamIntensity;
                float _FoamNoiseScale;
                float _WhitecapThreshold;
                float _EdgeSoftness;
            CBUFFER_END

            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            // 전역 바람 및 계절 브로드캐스트 (TotalWarWindManager / TotalWarTerrainManager)
            float4 _TotalWarWindVector;     // (DirX * Strength, DirZ * Strength, WindSpeed, Time * Speed)
            float4 _TotalWarWindParams;     // (Strength, GustFactor, WindSpeed, ActiveFlag)
            float _TotalWarTerrainSnowAmount;

            // 🌊 [비단결 실사 파동 스펙트럼]: 격자 돌기/물방울 없이 무리수 파장·방향 8파 중첩 해석적 기울기 산출
            void AccumulateSineWaveGrad(float2 pos, float2 dir, float freq, float amp, float speed, float t, inout float2 grad, inout float heightSum)
            {
                float phase = dot(pos, dir) * freq - t * speed;
                float s = sin(phase);
                float c = cos(phase);
                // Trochoidal(약간 뾰족한 파도 마루) 형태의 자연스러운 기울기
                grad += dir * (c * freq * amp);
                heightSum += s * amp;
            }

            float3 ComputeSilkyWaveNormal(float2 pos, float2 windDir, float t, float strength, float isSwamp, out float waveHeightNorm)
            {
                float2 perp = float2(-windDir.y, windDir.x);
                float2 d1 = windDir;
                float2 d2 = normalize(windDir * 0.86 + perp * 0.51);
                float2 d3 = normalize(windDir * 0.74 - perp * 0.67);
                float2 d4 = normalize(windDir * 0.42 + perp * 0.91);
                float2 d5 = normalize(windDir * 0.31 - perp * 0.95);
                float2 d6 = normalize(windDir * 0.95 - perp * 0.31);
                float2 d7 = normalize(windDir * 0.58 + perp * 0.81);
                float2 d8 = normalize(windDir * 0.18 - perp * 0.98);

                float scale = max(0.05, _WaveScale * 3.8);
                float2 grad = float2(0.0, 0.0);
                float hSum = 0.0;

                // 광역 대양 너울 -> 중거리 파동 -> 미세 바람 잔물결까지 8단 스펙트럼 합성
                AccumulateSineWaveGrad(pos, d1, 0.23 * scale, 0.16,  1.15, t, grad, hSum);
                AccumulateSineWaveGrad(pos, d2, 0.37 * scale, 0.12,  1.45, t, grad, hSum);
                AccumulateSineWaveGrad(pos, d3, 0.59 * scale, 0.09,  1.75, t, grad, hSum);
                AccumulateSineWaveGrad(pos, d4, 0.93 * scale, 0.06,  2.10, t, grad, hSum);
                AccumulateSineWaveGrad(pos, d5, 1.47 * scale, 0.04,  2.55, t, grad, hSum);
                AccumulateSineWaveGrad(pos, d6, 2.31 * scale, 0.025, 3.05, t, grad, hSum);
                AccumulateSineWaveGrad(pos, d7, 3.67 * scale, 0.015, 3.60, t, grad, hSum);
                AccumulateSineWaveGrad(pos, d8, 5.83 * scale, 0.009, 4.20, t, grad, hSum);

                float swampAtten = lerp(1.0, 0.20, isSwamp);
                grad *= (strength * swampAtten);
                waveHeightNorm = saturate(hSum * 1.4 + 0.5);

                return normalize(float3(-grad.x, 1.0, -grad.y));
            }

            // 4파 Gerstner 입체 버텍스 파도 (대양/큰 호수 전용)
            void EvaluateGerstnerWave(
                float2 dir,
                float steepness,
                float wavelength,
                float speedMult,
                float3 posWS,
                float time,
                inout float3 offset,
                inout float3 tangentX,
                inout float3 tangentZ,
                inout float crest)
            {
                float k = 6.2831853 / max(0.5, wavelength);
                float c = sqrt(9.8 / k) * speedMult;
                float2 d = normalize(dir);
                float f = k * (dot(d, posWS.xz) - c * time);
                float a = steepness / k;

                float cosF = cos(f);
                float sinF = sin(f);

                offset.x += d.x * (a * cosF);
                offset.y += a * sinF;
                offset.z += d.y * (a * cosF);

                tangentX += float3(-d.x * d.x * (steepness * sinF), d.x * (steepness * cosF), -d.x * d.y * (steepness * sinF));
                tangentZ += float3(-d.x * d.y * (steepness * sinF), d.y * (steepness * cosF), -d.y * d.y * (steepness * sinF));

                crest += saturate(sinF) * steepness;
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                float3 basePosWS = TransformObjectToWorld(input.positionOS.xyz);

                bool hasWindMgr = (_TotalWarWindParams.w > 0.5);
                float2 rawWindVec = _TotalWarWindVector.xy;
                float rawWindLen = length(rawWindVec);
                float2 windDir = (rawWindLen > 0.001) ? (rawWindVec / rawWindLen) : normalize(float2(0.76, 0.65));
                float windMag = hasWindMgr ? clamp(rawWindLen, 0.0, 10.0) : 0.45;
                float windWaveScale = hasWindMgr ? lerp(0.18, 2.20, saturate(windMag / 6.0)) : 0.60;
                float waveTime = _Time.y * (0.75 + saturate(windMag / 8.0) * 1.15);

                // 강물/늪지에서는 버텍스 기복 억제
                float openWaterMask = 1.0 - saturate(_FlowMode);
                float effAmp = _GerstnerAmp * windWaveScale * openWaterMask;

                float3 waveOffset = float3(0.0, 0.0, 0.0);
                float3 tangentX = float3(1.0, 0.0, 0.0);
                float3 tangentZ = float3(0.0, 0.0, 1.0);
                float crest = 0.0;

                if (effAmp > 0.002)
                {
                    float2 perp = float2(-windDir.y, windDir.x);
                    float chop = lerp(0.03, 0.18, _WaveChoppiness) * saturate(effAmp * 3.0);

                    EvaluateGerstnerWave(windDir,                             chop * 1.00, 22.0, 0.90, basePosWS, waveTime, waveOffset, tangentX, tangentZ, crest);
                    EvaluateGerstnerWave(normalize(windDir * 0.8 + perp * 0.6), chop * 0.70, 14.5, 1.00, basePosWS, waveTime, waveOffset, tangentX, tangentZ, crest);
                    EvaluateGerstnerWave(normalize(windDir * 0.7 - perp * 0.7), chop * 0.48,  8.7, 1.12, basePosWS, waveTime, waveOffset, tangentX, tangentZ, crest);
                    EvaluateGerstnerWave(normalize(windDir * 0.3 + perp * 0.95),chop * 0.30,  5.3, 1.22, basePosWS, waveTime, waveOffset, tangentX, tangentZ, crest);

                    waveOffset.y *= (effAmp * 2.0);
                    waveOffset.xz *= (effAmp * 0.9);
                }

                float3 displacedWS = basePosWS + waveOffset;
                float3 gerstnerNormal = normalize(cross(tangentZ, tangentX));
                if (gerstnerNormal.y < 0.0) gerstnerNormal = -gerstnerNormal;

                output.positionWS = displacedWS;
                output.positionCS = TransformWorldToHClip(displacedWS);
                output.normalWS = normalize(lerp(float3(0.0, 1.0, 0.0), gerstnerNormal, saturate(effAmp * 3.5)));
                output.uv = TRANSFORM_TEX(input.uv, _BumpMap);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                output.waveCrest = saturate(crest * 1.4);
                output.windFactor = saturate(windMag / 6.0);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float time = _Time.y;
                half3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);

                float isRiver = saturate(1.0 - abs(_FlowMode - 1.0));
                float isSwamp = saturate(_FlowMode - 1.0);

                bool hasWindMgr = (_TotalWarWindParams.w > 0.5);
                float2 rawWindVec = _TotalWarWindVector.xy;
                float rawWindLen = length(rawWindVec);
                float2 windDir = (rawWindLen > 0.001) ? (rawWindVec / rawWindLen) : normalize(float2(0.76, 0.65));
                float windMag = hasWindMgr ? clamp(rawWindLen, 0.0, 10.0) : 0.45;
                float rippleTime = time * (0.85 + saturate(windMag / 7.0) * 1.25);
                float windBoost = hasWindMgr ? lerp(0.40, 1.80, saturate(windMag / 5.0)) : 0.85;

                // 1. 비단결 8파 실사 수면 노멀 (격자 돌기/물방울 패턴 0%)
                float waveHeightNorm = 0.5;
                float3 procNormal;
                if (isRiver > 0.5)
                {
                    // 강물 모드: 강줄기 리본 UV V축을 따라 부드럽게 하류로 흐르는 물결
                    float2 riverCoord = float2(input.uv.x * 14.0, input.uv.y * 14.0 - time * (_FlowSpeed * 2.2));
                    procNormal = ComputeSilkyWaveNormal(riverCoord, float2(0.0, 1.0), time * 0.9, _WaveStrength * windBoost, 0.0, waveHeightNorm);
                }
                else
                {
                    procNormal = ComputeSilkyWaveNormal(input.positionWS.xz, windDir, rippleTime, _WaveStrength * windBoost, isSwamp, waveHeightNorm);
                }

                // 선택적 _BumpMap 연결 시 미세 합성
                float2 uv1 = input.positionWS.xz * (_WaveScale * 0.35) + _WaveSpeed.xy * time;
                float2 uv2 = input.positionWS.xz * (_WaveScale * 0.52) + _WaveSpeed.zw * time;
                half3 b1 = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv1));
                half3 b2 = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv2));
                half2 extraBump = (b1.xy + b2.xy) * (0.25 * _WaveStrength);

                half3 normalWS = normalize(input.normalWS + half3(procNormal.x + extraBump.x, 0.0, procNormal.z + extraBump.y));

                // 원거리 노멀 지글거림(에일리어싱) 부드럽게 감쇠 -> 대양의 매끈한 실루엣 유지
                float camDist = length(GetCameraPositionWS() - input.positionWS);
                float distNormalFade = saturate(1.0 - (camDist - 35.0) / 220.0);
                normalWS = normalize(lerp(float3(0.0, 1.0, 0.0), normalWS, lerp(0.28, 1.0, distNormalFade)));

                // 2. 실제 수심 계산 (맵 밖 대양 = rawDepth ~ 0 -> 완전 불투명 심해 대양으로 자동 판별!)
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float surfaceEyeDepth = input.screenPos.w;

                float eyeDepthDiff = sceneEyeDepth - surfaceEyeDepth;
                // 원경 스카이박스(rawDepth == 0 or 1)는 지형이 없는 대양(Open Ocean)으로 취급
                bool hasTerrainBelow = (rawDepth > 0.0001) && (rawDepth < 0.9999) && (eyeDepthDiff >= -0.05) && (sceneEyeDepth < 2500.0);
                float rayDepth = hasTerrainBelow ? max(0.0, eyeDepthDiff) : 40.0;
                float verticalDepth = hasTerrainBelow ? (rayDepth * max(0.25, abs(viewDirWS.y))) : 40.0;

                // 3. 얕은 물 부드러운 수중 굴절 (Screen-Space Refraction)
                float refractFade = hasTerrainBelow ? (saturate(verticalDepth * 1.4) * saturate(1.0 - camDist / 180.0)) : 0.0;
                float2 refractOffset = normalWS.xz * (_RefractionStrength * refractFade);
                float2 refractUV = saturate(screenUV + refractOffset);

                float refractRawDepth = SampleSceneDepth(refractUV);
                float refractEyeDepth = LinearEyeDepth(refractRawDepth, _ZBufferParams);
                float refractDiff = refractEyeDepth - surfaceEyeDepth;
                if (!hasTerrainBelow || refractDiff < 0.0)
                {
                    refractUV = screenUV;
                    refractDiff = rayDepth;
                }
                float refractVertDepth = hasTerrainBelow ? (max(0.0, refractDiff) * max(0.25, abs(viewDirWS.y))) : 40.0;

                half3 underwaterScene = hasTerrainBelow ? SampleSceneColor(refractUV) : _DeepColor.rgb;
                bool hasOpaqueScene = hasTerrainBelow && (dot(underwaterScene, underwaterScene) > 0.00001);

                // 물가 젖은 바닥 미세 습윤감
                if (hasOpaqueScene)
                {
                    half wetMask = saturate(1.0 - refractVertDepth / 1.8);
                    underwaterScene *= lerp(1.0, 1.0 - _WetBankDarken, wetMask * 0.7);
                }

                // 4. URP 주 광원 & 실시간 그림자
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 lightDir = mainLight.direction;
                if (dot(lightDir, lightDir) < 0.001) lightDir = normalize(half3(0.35, 0.85, 0.35));
                half3 lightCol = mainLight.color;
                if (dot(lightCol, lightCol) < 0.001) lightCol = half3(1.0, 0.96, 0.90);
                half lightAtten = mainLight.distanceAttenuation * mainLight.shadowAttenuation;

                // 5. 선택적 미세 햇살 빛결 (Voronoi 물방울 제거, 기본 0.0 -> 원할 때만 근거리 얕은 물에 은은하게)
                if (_CausticsStrength > 0.005 && hasOpaqueScene && isSwamp < 0.5 && camDist < 55.0)
                {
                    float2 cUV = input.positionWS.xz * _CausticsScale;
                    float wA = sin(cUV.x * 2.1 + time * 1.4 + sin(cUV.y * 1.7 - time * 1.1));
                    float wB = sin(cUV.y * 2.3 - time * 1.2 + sin(cUV.x * 1.9 + time * 1.3));
                    float filament = pow(max(0.0001, abs(wA * wB)), 4.0);
                    float cMask = smoothstep(0.10, 0.45, refractVertDepth) * saturate(1.0 - refractVertDepth / 2.2) * saturate(1.0 - camDist / 55.0);
                    underwaterScene += lightCol * (filament * _CausticsStrength * cMask * lightAtten * 0.45);
                }

                // 6. Beer-Lambert 수심 광흡수: 얕은 물(투명하게 바닥 투과) -> 깊은 바다/맵 밖 대양(웅장한 푸른 불투명)
                half3 deepCol = _DeepColor.rgb;
                half3 shallowCol = _ShallowColor.rgb;

                if (isSwamp > 0.01)
                {
                    shallowCol = lerp(shallowCol, half3(0.16, 0.21, 0.12), isSwamp);
                    deepCol = lerp(deepCol, half3(0.05, 0.09, 0.05), isSwamp);
                }

                if (_TotalWarTerrainSnowAmount > 0.05)
                {
                    shallowCol = lerp(shallowCol, half3(0.24, 0.42, 0.50), _TotalWarTerrainSnowAmount * 0.50);
                    deepCol = lerp(deepCol, half3(0.03, 0.14, 0.28), _TotalWarTerrainSnowAmount * 0.55);
                }

                float clarityScale = lerp(max(0.2, _Clarity), 0.5, isSwamp);
                half3 transmittance = hasTerrainBelow
                    ? exp(-refractVertDepth * _AbsorptionRGB.rgb * (2.2 / clarityScale))
                    : half3(0.0, 0.0, 0.0);

                float depthRatio = saturate(refractVertDepth / max(0.2, _DepthDistance));
                half3 waterBody = lerp(shallowCol, deepCol, pow(max(0.0001, depthRatio), 0.72));
                // 대양에서도 칙칙한 회색이 되지 않도록 주광+환경광 균형 유지
                half3 litWaterBody = waterBody * (0.58 + 0.42 * lightAtten);

                half3 waterVolumeColor = hasOpaqueScene
                    ? (underwaterScene * transmittance + litWaterBody * (1.0 - transmittance))
                    : litWaterBody;

                // 7. 파도 햇살 투과광 (Wave SSS - 은은한 청록빛)
                float sssAngle = pow(max(0.0001, saturate(dot(viewDirWS, -lightDir + normalWS * 0.4))), 3.5);
                float sssMask = saturate(input.waveCrest * 0.9 + (1.0 - depthRatio) * 0.35) * (1.0 - isSwamp);
                waterVolumeColor += _SubsurfaceColor.rgb * lightCol * (sssAngle * sssMask * _SubsurfaceStrength * lightAtten);

                // 8. 물리 기반 하늘 반사 & Schlick 프레넬 (얕은 물은 바닥이 잘 보이게, 대양은 거울처럼 장엄하게)
                half3 reflectVec = reflect(-viewDirWS, normalWS);
                reflectVec.y = max(0.03, reflectVec.y);
                half roughness = saturate(1.0 - _Smoothness + isSwamp * 0.35 + input.windFactor * 0.06);
                half3 envProbe = GlossyEnvironmentReflection(reflectVec, roughness, 1.0);

                half horizonFactor = pow(max(0.0001, 1.0 - saturate(reflectVec.y)), 2.8);
                half3 zenithCol = _SkyReflectionTint.rgb * 0.80;
                half3 horizonCol = lerp(_SkyReflectionTint.rgb * 1.18, unity_FogColor.rgb, 0.35);
                half3 fallbackSky = lerp(zenithCol, horizonCol, horizonFactor);
                half envLuma = dot(envProbe, half3(0.299, 0.587, 0.114));
                half3 skyReflection = lerp(fallbackSky, envProbe, saturate(envLuma * 1.6)) * (_ReflectionIntensity * (1.0 - isSwamp * 0.5));
                skyReflection *= (0.76 + 0.24 * lightAtten);

                half NdotV = saturate(dot(normalWS, viewDirWS));
                half fresnel = 0.02 + 0.98 * pow(max(0.0001, 1.0 - NdotV), _FresnelPower);
                // 얕은 물가(0~2.8m)에서는 반사율을 낮춰 물속이 맑게 보이고, 깊은 대양에서는 풀 프레넬 적용
                half depthReflectScale = hasTerrainBelow ? lerp(0.14, 0.95, smoothstep(0.0, 2.8, refractVertDepth)) : 1.0;
                fresnel *= depthReflectScale * (1.0 - isSwamp * 0.45);

                half3 finalRGB = lerp(waterVolumeColor, skyReflection, fresnel);

                // 9. 태양광 스펙큘러 (깔끔한 햇빛 반짝임 Sun Glint)
                half3 halfDir = normalize(lightDir + viewDirWS);
                half NdotH = saturate(dot(normalWS, halfDir));
                half sharpGlint = pow(max(0.0001, NdotH), _SunSpecularPower) * _SunSpecularIntensity;
                half broadSheen = pow(max(0.0001, NdotH), 64.0) * 0.08;
                finalRGB += lightCol * ((sharpGlint + broadSheen) * lightAtten * (1.0 - isSwamp * 0.75));

                // 10. 정제된 초미세 물가선 (얼룩말 줄무늬·거품 잡티 제거 -> 10~15cm 얇고 깔끔한 수면선만)
                half totalFoam = 0.0;
                if (hasTerrainBelow && _FoamIntensity > 0.005 && verticalDepth < _FoamDistance)
                {
                    float shoreT = saturate(verticalDepth / max(0.01, _FoamDistance));
                    float wisp = sin(input.positionWS.x * _FoamNoiseScale + time * 1.3) * cos(input.positionWS.z * _FoamNoiseScale * 1.13 - time * 1.1);
                    float wispMask = smoothstep(-0.2, 0.75, wisp);
                    // 물과 땅이 닿는 아주 얇은 가장자리만 은은하게
                    totalFoam = (1.0 - smoothstep(0.0, 0.85, shoreT)) * smoothstep(0.0, 0.22, shoreT) * wispMask * _FoamIntensity * (1.0 - isSwamp);
                }

                // 진짜 강풍/태풍(windMag > 3.5)일 때 대양 파도 마루에만 미세한 흰파도 허용
                if (!hasTerrainBelow || verticalDepth > 4.0)
                {
                    float stormMask = saturate((windMag - 3.5) / 4.5);
                    float crestFoam = smoothstep(_WhitecapThreshold, 1.0, input.waveCrest * 0.8 + waveHeightNorm * 0.35) * stormMask * 0.55;
                    totalFoam = saturate(totalFoam + crestFoam);
                }

                half3 litFoamColor = _FoamColor.rgb * (0.65 + 0.35 * lightAtten);
                finalRGB = lerp(finalRGB, litFoamColor, totalFoam * _FoamColor.a);

                // 11. 얕은 물가 경계 소프트 페이드 & 대양 완전 불투명
                half finalAlpha = 1.0;
                if (hasTerrainBelow)
                {
                    half edgeAlpha = smoothstep(0.002, max(0.01, _EdgeSoftness), verticalDepth);
                    if (hasOpaqueScene)
                    {
                        finalAlpha = edgeAlpha;
                    }
                    else
                    {
                        // OpaqueTexture 미지원 카메라에서도 얕은 곳 투명 -> 깊은 곳 불투명 유지
                        half depthOpacity = saturate(1.0 - dot(transmittance, half3(0.333, 0.333, 0.334)));
                        finalAlpha = edgeAlpha * saturate(max(depthOpacity, max(fresnel * 0.85, totalFoam)));
                    }
                }
                else
                {
                    // 맵 밖 대양: 100% 불투명하고 장엄한 푸른 바다!
                    finalAlpha = 1.0;
                }

                // 12. 대기 안개 (Fog) 합성
                finalRGB = MixFog(finalRGB, input.fogCoord);

                return half4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "MiniTotalWar.Editor.TotalWarWaterShaderGUI"
}
