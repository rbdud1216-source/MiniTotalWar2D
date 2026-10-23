Shader "Universal Render Pipeline/TotalWarFoliageWind"
{
    Properties
    {
        [Header(Base Texture)]
        _BaseMap ("Albedo Map", 2D) = "white" {}
        [HideInInspector] _MainTex ("BaseMap (RGB)", 2D) = "white" {}
        _BaseColor ("Base Color Tint", Color) = (1, 1, 1, 1)

        [Header(Alpha Cutout)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Enable Alpha Cutout", Float) = 1.0
        _Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.40

        [Header(Normal Map)]
        [Toggle(_NORMALMAP)] _EnableNormalMap ("Enable Normal Map", Float) = 1.0
        [NoScaleOffset] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(0.0, 2.0)) = 1.2

        [Header(Surface Properties)]
        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.20
        _Metallic ("Metallic", Range(0.0, 1.0)) = 0.0
        _Cull ("Culling Mode (0=Both, 2=Back)", Float) = 0.0

        [Header(Wind Wave Animation)]
        _WindMultiplier ("Wind Sway Multiplier", Range(0.0, 3.0)) = 1.0
        _LeafFlutter ("Leaf Flutter Strength", Range(0.0, 2.0)) = 0.8

        [Header(Fire and Charred Burning)]
        _BurnProgress ("Burn Progress (0=Normal, 1=Charred Ash)", Range(0.0, 1.0)) = 0.0
        _CharredMap ("Charred Ash Texture", 2D) = "gray" {}
        _CharredTile ("Charred Texture Tiling", Float) = 3.0
        _CharredColor ("Charred Ash Color", Color) = (0.35, 0.32, 0.30, 1.0)
        _EmberGlowColor ("Ember Glow Color", Color) = (3.5, 0.9, 0.15, 1.0)

        [Header(Winter Snow Accumulation)]
        _SnowAmount ("Snow Accumulation (0=None, 1=Heavy)", Range(0.0, 1.0)) = 0.0
        _LeafSnowRatio ("Leaf Snow Ratio (0=None, 1=All)", Range(0.0, 1.0)) = 0.55
        _SnowThreshold ("Snow Angle Threshold", Range(-1.0, 1.0)) = -0.15
        _SnowColor ("Snow Color", Color) = (0.95, 0.98, 1.0, 1.0)
        _IsBamboo ("Is Bamboo Foliage (0=Pine/Others, 1=Bamboo)", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="TransparentCutout"
            "Queue"="AlphaTest"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
        }

        Cull [_Cull]

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _NORMALMAP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                nointerpolation float leafPhaseNoise : TEXCOORD5;
                float heightNorm : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
                half _BumpScale;
                half _Smoothness;
                half _Metallic;
                float _WindMultiplier;
                float _LeafFlutter;
                float _CharredTile;
                half4 _CharredColor;
                half4 _EmberGlowColor;
                float _Cull;
                float _SnowAmount;
                float _LeafSnowRatio;
                float _SnowThreshold;
                half4 _SnowColor;
                float _IsBamboo;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _BurnProgress)
            UNITY_INSTANCING_BUFFER_END(Props)

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            TEXTURE2D(_CharredMap);
            SAMPLER(sampler_CharredMap);

            // 전역 바람 변수 (TotalWarWindManager에서 실시간 공급)
            float4 _TotalWarWindVector; // (DirX * Strength, DirZ * Strength, Speed, Time)
            float4 _TotalWarWindParams; // (Strength, GustFactor, 0, 0)

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);

                // 삼국지 토탈워 바람 물리 시뮬레이션:
                // 1. 바람 방향 및 세기 정규화
                float2 windDir2D = _TotalWarWindVector.xy;
                float windStrength = length(windDir2D);
                float2 normWindDir = (windStrength > 0.001) ? (windDir2D / windStrength) : float2(1.0, 0.0);
                float windTime = _TotalWarWindVector.w;

                // 2. 밑동 완전 고정 및 외팔보(Cantilever Beam) 2차 굽힘 공식
                // 밑동(Y <= 2.0m)은 변위 0.0000으로 절대 지면에서 미끄러지거나 걷지 않음
                float rawY = max(0.0, input.positionOS.y - 2.0);
                float normY = saturate(rawY / 30.0); // 0(밑동) ~ 1(수관부 상단)
                float stemBendFactor = normY * normY; // 2차 포물선: 밑동은 고정, 상단 가지만 부드럽게 휨

                // 3. 줄기 메인 스웨이 (완만한 거대 파동, 월드 진폭 5~10cm 제어)
                float stemPhase = windTime * 0.9 + dot(worldPos.xz, float2(0.03, 0.025));
                float stemWave = sin(stemPhase) * (0.06 * windStrength);

                // 4. 나뭇잎/잔가지 바람결 진행 파동 (Traveling Wave & Directional Flutter)
                // - 원점 중심 구형 좌표 대신 월드 바람 진행축(windTravelAxis)을 사용하여 바람이 파도처럼 숲을 훑고 지나가도록 구현
                // - 노멀(Normal) 방향 팽창을 원천 배제하여 '원형 동심원 팽창' 및 '그림자 경계 교차로 인한 밝아짐/어두워짐 점멸' 완전 해결
                float windTravelAxis = dot(worldPos.xz, normWindDir);
                float leafNoise = dot(input.positionOS.xyz, float3(1.2, 2.4, 1.6));
                float leafPhase = windTime * 3.8 + windTravelAxis * 0.65 + leafNoise;
                float leafWave = sin(leafPhase) * (0.040 * _LeafFlutter);

                // 5. 오브젝트 로컬 공간 변위 합성 (바람 진행 벡터 및 수직 횡방향 탄성 진동)
                float3 worldWindDir = float3(normWindDir.x, 0.0, normWindDir.y);
                float3 objWindDir = normalize(TransformWorldToObjectDir(worldWindDir));
                float3 objWindCross = normalize(float3(-objWindDir.z, 0.0, objWindDir.x));

                // 줄기 굽힘 변위 (Stem Sway)
                float3 stemDisp = objWindDir * (stemWave * stemBendFactor * _WindMultiplier);

                // 나뭇잎 바람결 변위: 바람 진행 방향(70%) + 횡방향 탄성 살랑임(25%) + 풍압 하강(5%)
                float3 leafDir = objWindDir * 0.70 + objWindCross * (cos(leafPhase * 0.75) * 0.25) - float3(0.0, 0.06, 0.0);
                float3 leafDisp = leafDir * (leafWave * saturate(normY + 0.12) * _WindMultiplier);

                float3 totalDisp = stemDisp + leafDisp;

                // 과도한 왜곡 방지 안전 클램프 (최대 0.18m 이내)
                float dispLen = length(totalDisp);
                if (dispLen > 0.18)
                {
                    totalDisp = totalDisp * (0.18 / dispLen);
                }

                float3 displacedPosOS = input.positionOS.xyz + totalDisp;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(displacedPosOS);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                // 나뭇잎 카드(클러스터)별 고유 시드 계산
                // 로컬 좌표를 3.5m 단위로 퀀타이즈하여 같은 잎 카드 내 버텍스는 동일한 해시 시드를 공유
                float3 leafCell = floor(input.positionOS.xyz * 3.5);
                output.leafPhaseNoise = frac(sin(dot(leafCell, float3(12.9898, 78.233, 45.164))) * 43758.5453);

                // 식생 나뭇잎 실제 높이 범위(12.0m 최하단 가지 ~ 46.0m 최상단 수관 꼭대기) 0.0~1.0 완전 선형 매핑
                // 이를 통해 나뭇잎 높이에 따른 점진적 적설/해빙 그라데이션이 나무 전체 수관에 완벽하게 펼쳐짐
                float leafNormY = saturate((input.positionOS.y - 12.0) / 34.0);
                output.heightNorm = leafNormY;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                float burnProg = UNITY_ACCESS_INSTANCED_PROP(Props, _BurnProgress);
                float3 normalWS = normalize(input.normalWS);

            #if defined(_NORMALMAP)
                half4 nSample = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv);
                half3 normalTS = UnpackNormalScale(nSample, _BumpScale);
                normalWS = normalize(normalWS);
            #endif

                // =========================================================================
                // ❄️ [1단계: 삼국지 토탈워 본편 공식 겨울 적설 시스템 (Winter Snow Accumulation)]
                // =========================================================================
                // 1) 화재 열기에 의한 눈 융해(Melting): 불이 붙으면 고온 열기로 눈이 순식간에 녹아 증발
                // 2) 줄기(Trunk):
                //    - 수평 가지 상단(Y > 0.65)에 소복이 얹히는 눈선
                //    - 🌬️ 바람받이 면(Windward Side): 바람이 불어오는 방향(-windDir)을 마주보는 기둥 표면에 세로로 얇게 스쳐 묻은 눈선!
                //    - 바람 그늘 및 수직 배면은 원래의 짙은 고동색/흑갈색 수피 100% 보존
                // 3) 나뭇잎(Leaves):
                //    - 윗면(Top Surface) 집중: 하늘(normalWS.y > 0)을 향하는 표면에 소복이 눈이 쌓이고, 아랫면은 어두운 침엽수 녹색 음영 보존!
                //    - _LeafSnowRatio = 1.0 (최대): 나무 전체(위/아래 모든 나뭇잎)에 빈틈없이 100% 소복하게 눈 적설!
                //    - _LeafSnowRatio가 줄어들수록: 나무 아랫쪽 잎사귀부터 서서히 푸르러지며(녹색 복원), 가지별 랜덤 노이즈와 결합되어 유기적으로 녹색 전환!
                half snowCoverage = 0.0;
                if (_SnowAmount > 0.001)
                {
                    half snowMelt = saturate(1.0 - burnProg * 4.0);
                    half effectiveSnow = _SnowAmount * snowMelt;

                    if (effectiveSnow > 0.001)
                    {
                        half geomUp = normalWS.y;

                    #if defined(_ALPHATEST_ON)
                        if (_IsBamboo > 0.5)
                        {
                            // 🎋 [대나무 전용: 삼각형 절단선 원천 제거 및 유기적 설죽(雪竹) 시스템]
                            float3 pos = input.positionWS.xyz;
                            float wave1 = sin(pos.x * 0.85 + pos.y * 0.65) * cos(pos.z * 0.85 + pos.y * 0.50);
                            float wave2 = sin(pos.x * 1.70 - pos.z * 1.40 + pos.y * 0.80) * 0.5;
                            float organicClump = (wave1 + wave2) * 0.38 + 0.50; // 0.0 ~ 1.0 부드러운 구름형 노이즈

                            half ratioMask = saturate((_LeafSnowRatio - organicClump + 0.25) / 0.50);
                            if (_LeafSnowRatio >= 0.95) ratioMask = 1.0;
                            if (_LeafSnowRatio <= 0.01) ratioMask = 0.0;

                            half leafTopFactor = saturate(normalWS.y * 1.1 + 0.40);
                            half leafSnowWeight = lerp(leafTopFactor, 1.0, saturate(_LeafSnowRatio * 1.15));

                            snowCoverage = ratioMask * leafSnowWeight * effectiveSnow;

                            // 순백의 밝고 선명한 눈 스파클
                            half snowSparkle = frac(sin(dot(input.positionWS.xz * 12.0, float2(12.9898, 78.233))) * 43758.5453) * 0.030;
                            half3 finalSnow = _SnowColor.rgb + snowSparkle;

                            albedo.rgb = lerp(albedo.rgb, finalSnow, snowCoverage);
                        }
                        else
                        {
                            // 🌲 [일반 나무 (소나무, 황산소나무, 활엽수 등) 이전의 풍성하고 소복한 순백 설화 시스템 100% 복원]
                            float3 clumpCell = floor(input.positionWS.xyz * 1.5);
                            float clumpNoise = frac(sin(dot(clumpCell, float3(12.9898, 78.233, 45.164))) * 43758.5453);
                            float leafSeed = frac(input.leafPhaseNoise * 0.65 + clumpNoise * 0.35);

                            half ratioDelta = _LeafSnowRatio - leafSeed;
                            half ratioMask = saturate(ratioDelta / 0.18 + 0.50);

                            if (_LeafSnowRatio >= 0.95) ratioMask = 1.0;
                            if (_LeafSnowRatio <= 0.02) ratioMask = 0.0;

                            half leafTopFactor = saturate(normalWS.y * 1.2 + 0.35);
                            half leafSnowWeight = lerp(leafTopFactor, 1.0, saturate(_LeafSnowRatio * 1.15));

                            snowCoverage = ratioMask * leafSnowWeight * effectiveSnow;

                            half snowSparkle = frac(sin(dot(input.positionWS.xz * 12.0, float2(12.9898, 78.233))) * 43758.5453) * 0.035;
                            half3 finalSnow = _SnowColor.rgb + snowSparkle;

                            albedo.rgb = lerp(albedo.rgb, finalSnow, snowCoverage);
                        }
                    #else
                        // [줄기/기둥(Trunk) 적설]:
                        // 1) 완만한 수평 나뭇가지 상단 윗면 적설
                        half branchTopSnow = saturate((geomUp - 0.65) / 0.30);
                        branchTopSnow = branchTopSnow * branchTopSnow * 0.65;

                        // 2) 🌬️ 바람 부는 쪽 기둥 표면 눈 묻음 (Windward Trunk Snow Accumulation):
                        // 삼탈워 본편 인게임 실제 관찰 결과 1:1 재현:
                        // - 바람/눈보라가 불어오는 방향(-windDir)을 마주하는 기둥 표면(Windward Side)에 세로 서리 눈선 안착
                        // - 바람 그늘(배면)은 짙은 흑갈색 수피 100% 보존
                        float windLen = length(_TotalWarWindVector.xy);
                        float2 normWind = (windLen > 0.001) ? (_TotalWarWindVector.xy / windLen) : float2(1.0, 0.0);
                        float2 windFrom = -normWind; // 바람이 불어오는 방향
                        float2 trunkNorm2D = (length(normalWS.xz) > 0.001) ? normalize(normalWS.xz) : float2(0.0, 1.0);
                        half windFacing = dot(trunkNorm2D, windFrom); // 바람받이 정면 마주침 내적 (-1.0 ~ +1.0)

                        // 바람을 마주보는 측면(내적 > 0.10)에 세로 서리 눈선 형성
                        half windSnowStreak = saturate((windFacing - 0.10) / 0.70);
                        windSnowStreak = windSnowStreak * windSnowStreak; // 포물선 부드러운 감쇄

                        // 거친 나무껍질 수피 틈새 세로 질감 노이즈
                        half barkStreakNoise = frac(sin(dot(input.positionWS.xy * float2(3.0, 12.0), float2(12.9898, 78.233))) * 43758.5453) * 0.35 + 0.65;
                        // 바람 세기에 따른 착설 강도 보정 (바람이 강할수록 착설이 선명해짐)
                        half windStrengthFactor = saturate(windLen * 0.5 + 0.4);
                        half windTrunkSnow = windSnowStreak * 0.45 * barkStreakNoise * windStrengthFactor;

                        // 수평 가지 상단 눈 + 바람받이 측면 눈 합성
                        half combinedTrunkSnow = max(branchTopSnow, windTrunkSnow);
                        snowCoverage = combinedTrunkSnow * effectiveSnow;

                        albedo.rgb = lerp(albedo.rgb, _SnowColor.rgb, snowCoverage);
                    #endif
                    }
                }

                // =========================================================================
                // 🔥 [2단계: 삼국지 토탈워 본편 화재 & 숯 탄화 시스템 (Combustion & Charring)]
                // =========================================================================
            #if defined(_ALPHATEST_ON)
                // [나뭇잎 점진적 유기적 연소 시스템 (Organic Per-Leaf Combustion System)]
                half dynamicCutoff = _Cutoff;

                if (burnProg > 0.001)
                {
                    // 3차원 공간 가지 뭉치(Branch Clump) 노이즈:
                    // X, Y, Z 입체 좌표를 결합하여 나뭇가지 덩어리별로 불규칙하게 시차 연소
                    float branchClump = sin(input.positionWS.x * 0.95 + input.positionWS.z * 0.75) * cos(input.positionWS.y * 0.85 + input.positionWS.x * 0.40);
                    float leafPhaseOffset = (input.leafPhaseNoise - 0.5) * 0.28 + branchClump * 0.16;

                    // 불이 실제로 붙기 시작할 때부터 서서히 시차가 발현되도록 오프셋 가중치 적용
                    float offsetWeight = saturate(burnProg * 2.5);
                    float effectiveOffset = leafPhaseOffset * offsetWeight;

                    // 개별 나뭇잎의 로컬 연소 진행도 (0.0: 완전 정상 녹색잎 -> 1.0: 완전 전소 소멸)
                    float localProgress = saturate((burnProg + effectiveOffset - 0.03) / 0.52);

                    if (localProgress >= 0.96)
                    {
                        clip(-1.0); // 완전히 타서 재가 된 잎 소멸
                    }

                    // --- 1단계: 고온 열기 화선 (Combustion Wave Front - 주황/앰버 달아오름) ---
                    half heatFactor = smoothstep(0.04, 0.32, localProgress) * (1.0 - smoothstep(0.35, 0.65, localProgress));
                    half3 heatColor = albedo.rgb * half3(1.6, 0.85, 0.22) + half3(0.42, 0.18, 0.02);
                    albedo.rgb = lerp(albedo.rgb, heatColor, heatFactor * 0.85);

                    // --- 2단계: 흑색 숯 및 재 탄화 (Charcoal & Ash Carbonization) ---
                    half charProgress = smoothstep(0.22, 0.68, localProgress);
                    half4 charredSample = SAMPLE_TEXTURE2D(_CharredMap, sampler_CharredMap, input.uv * _CharredTile);
                    half sampleLuma = dot(charredSample.rgb, half3(0.299, 0.587, 0.114));
                    if (charProgress > 0.001)
                    {
                        half3 leafCharredAlbedo;
                        if (sampleLuma > 0.02)
                        {
                            leafCharredAlbedo = charredSample.rgb * half3(0.12, 0.11, 0.11);
                        }
                        else
                        {
                            leafCharredAlbedo = albedo.rgb * half3(0.05, 0.05, 0.05);
                        }
                        albedo.rgb = lerp(albedo.rgb, leafCharredAlbedo, charProgress);
                    }

                    // --- 3단계: 구멍 뚫림 및 침식 디졸브 (Perforated Ash Dissolve) ---
                    half shrinkFactor = smoothstep(0.48, 0.95, localProgress);
                    half crackNoise = (sampleLuma > 0.02) ? (charredSample.r - 0.5) * 0.35 : 0.0;
                    half pinholeNoise = sin(input.uv.x * 45.0) * cos(input.uv.y * 45.0) * 0.14;
                    dynamicCutoff = lerp(_Cutoff, 0.99, shrinkFactor) + (crackNoise + pinholeNoise) * shrinkFactor;
                }

                half currentAlpha = albedo.a * _BaseColor.a;
                clip(currentAlpha - dynamicCutoff);
            #else
                // [줄기/기둥(Trunk) 점진적 숯 탄화 (Gradual Carbonization / Charring)]
                // 1단계: 착화 직후(0.04 ~ 0.35) 고온 열기에 줄기 수피가 붉게 달아오르는 앰버 화선(Stem Heat Wave)
                // 2단계: 맹렬 연소기(0.10 ~ 0.65) 삼탈워 원본 숯 텍스처(_CharredMap)와 깊은 흑색 숯(Charcoal Black)으로 완벽 탄화
                // 3단계: 전소 완료(0.70 ~ 1.0) 칠흑 같은 숯 골격과 회백색 잿가루 균열(Ash Cracks)로 완성
                if (burnProg > 0.001)
                {
                    // 고온 열기로 줄기 표면이 붉게 달아오름
                    half stemHeatFactor = smoothstep(0.04, 0.22, burnProg) * (1.0 - smoothstep(0.28, 0.55, burnProg));
                    half3 stemHeatColor = albedo.rgb * half3(1.4, 0.70, 0.18) + half3(0.35, 0.12, 0.02);
                    albedo.rgb = lerp(albedo.rgb, stemHeatColor, stemHeatFactor * 0.75);

                    // 숯 탄화 진행도 (smoothstep)
                    half trunkCharFactor = saturate((burnProg - 0.08) / 0.58);
                    trunkCharFactor = trunkCharFactor * trunkCharFactor * (3.0 - 2.0 * trunkCharFactor);

                    if (trunkCharFactor > 0.001)
                    {
                        half4 charredSample = SAMPLE_TEXTURE2D(_CharredMap, sampler_CharredMap, input.uv * _CharredTile);
                        half sampleLuma = dot(charredSample.rgb, half3(0.299, 0.587, 0.114));
                        half3 charredAlbedo;
                        if (sampleLuma > 0.02)
                        {
                            // 삼탈워 원본 숯 텍스처를 활용한 짙은 흑색 숯(Charcoal) + 회백색 재(Ash) 질감
                            charredAlbedo = charredSample.rgb * half3(0.11, 0.105, 0.105);
                        }
                        else
                        {
                            charredAlbedo = albedo.rgb * half3(0.06, 0.055, 0.055);
                        }
                        albedo.rgb = lerp(albedo.rgb, charredAlbedo, trunkCharFactor);
                    }
                }
            #endif

                // =========================================================================
                // ☀️ [3단계: URP 메인 라이트 조명 연산]
                // =========================================================================
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half NdotL = saturate(dot(normalWS, mainLight.direction));

            #if defined(_ALPHATEST_ON)
                // [삼탈워 실사 나뭇잎: 반투명 햇살 투광 (Subsurface Translucency)]
                half transmission = saturate(dot(-normalWS, mainLight.direction) * 0.75 + 0.25) * 0.50;
                half directLight = (NdotL + transmission) * mainLight.shadowAttenuation;
                half3 diffuse = albedo.rgb * (mainLight.color * directLight);
                half3 ambient = max(SampleSH(normalWS), SampleSH(float3(0, 1, 0)) * 0.42) * albedo.rgb;
                half3 snowScattering = _SnowColor.rgb * (snowCoverage * 0.06);
                half3 finalColor = diffuse + ambient + snowScattering;
            #else
                half3 diffuse = albedo.rgb * (mainLight.color * (NdotL * mainLight.shadowAttenuation));
                half3 ambient = max(SampleSH(normalWS), SampleSH(float3(0, 1, 0)) * 0.35) * albedo.rgb;
                half3 snowScattering = _SnowColor.rgb * (snowCoverage * 0.05);
                half3 finalColor = diffuse + ambient + snowScattering;
            #endif

                // 포그 적용
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, albedo.a);
            }
            ENDHLSL
        }

        // 실시간 그림자 투사 패스 (ShadowCaster)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                nointerpolation float leafPhaseNoise : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
                half _BumpScale;
                half _Smoothness;
                half _Metallic;
                float _WindMultiplier;
                float _LeafFlutter;
                float _CharredTile;
                half4 _CharredColor;
                half4 _EmberGlowColor;
                float _Cull;
                float _SnowAmount;
                float _LeafSnowRatio;
                float _SnowThreshold;
                half4 _SnowColor;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _BurnProgress)
            UNITY_INSTANCING_BUFFER_END(Props)

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float4 _TotalWarWindVector;

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);

                float2 windDir2D = _TotalWarWindVector.xy;
                float windStrength = length(windDir2D);
                float2 normWindDir = (windStrength > 0.001) ? (windDir2D / windStrength) : float2(1.0, 0.0);
                float windTime = _TotalWarWindVector.w;

                float rawY = max(0.0, input.positionOS.y - 2.0);
                float normY = saturate(rawY / 30.0);
                float stemBendFactor = normY * normY;

                float stemPhase = windTime * 0.9 + dot(worldPos.xz, float2(0.03, 0.025));
                float stemWave = sin(stemPhase) * (0.06 * windStrength);

                float windTravelAxis = dot(worldPos.xz, normWindDir);
                float leafNoise = dot(input.positionOS.xyz, float3(1.2, 2.4, 1.6));
                float leafPhase = windTime * 3.8 + windTravelAxis * 0.65 + leafNoise;
                float leafWave = sin(leafPhase) * (0.040 * _LeafFlutter);

                float3 worldWindDir = float3(normWindDir.x, 0.0, normWindDir.y);
                float3 objWindDir = normalize(TransformWorldToObjectDir(worldWindDir));
                float3 objWindCross = normalize(float3(-objWindDir.z, 0.0, objWindDir.x));

                float3 stemDisp = objWindDir * (stemWave * stemBendFactor * _WindMultiplier);
                float3 leafDir = objWindDir * 0.70 + objWindCross * (cos(leafPhase * 0.75) * 0.25) - float3(0.0, 0.06, 0.0);
                float3 leafDisp = leafDir * (leafWave * saturate(normY + 0.12) * _WindMultiplier);

                float3 totalDisp = stemDisp + leafDisp;
                float dispLen = length(totalDisp);
                if (dispLen > 0.18)
                {
                    totalDisp = totalDisp * (0.18 / dispLen);
                }

                float3 displacedPosOS = input.positionOS.xyz + totalDisp;
                float3 positionWS = TransformObjectToWorld(displacedPosOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _MainLightPosition.xyz));
                output.positionWS = positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                float3 leafCell = floor(input.positionOS.xyz * 3.5);
                output.leafPhaseNoise = frac(sin(dot(leafCell, float3(12.9898, 78.233, 45.164))) * 43758.5453);

                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

            #if defined(_ALPHATEST_ON)
                float burnProg = UNITY_ACCESS_INSTANCED_PROP(Props, _BurnProgress);
                half dynamicCutoff = _Cutoff;

                if (burnProg > 0.001)
                {
                    float branchClump = sin(input.positionWS.x * 0.95 + input.positionWS.z * 0.75) * cos(input.positionWS.y * 0.85 + input.positionWS.x * 0.40);
                    float leafPhaseOffset = (input.leafPhaseNoise - 0.5) * 0.28 + branchClump * 0.16;
                    float offsetWeight = saturate(burnProg * 2.5);
                    float effectiveOffset = leafPhaseOffset * offsetWeight;
                    float localProgress = saturate((burnProg + effectiveOffset - 0.03) / 0.52);

                    if (localProgress >= 0.96)
                    {
                        clip(-1.0);
                    }

                    half shrinkFactor = smoothstep(0.48, 0.95, localProgress);
                    half pinholeNoise = sin(input.uv.x * 45.0) * cos(input.uv.y * 45.0) * 0.14;
                    dynamicCutoff = lerp(_Cutoff, 0.99, shrinkFactor) + pinholeNoise * shrinkFactor;
                }

                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                clip(alpha - dynamicCutoff);
            #endif

                return 0;
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}
