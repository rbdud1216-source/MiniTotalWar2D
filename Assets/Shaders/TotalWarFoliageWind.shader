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
                float _BurnProgress;
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

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                float burnProg = UNITY_ACCESS_INSTANCED_PROP(Props, _BurnProgress);

            #if defined(_ALPHATEST_ON)
                // [나뭇잎 점진적 유기적 연소 시스템 (Organic Per-Leaf Combustion System)]
                // 1) 착화 전(burnProg <= 0.001)에는 단풍/탄화 로직을 건너뛰어 원본 녹색 잎 100% 보존
                // 2) 3차원 공간 가지 뭉치(Branch Clump) 노이즈: 위에서 아래로 스캔하듯 사라지는 선형성 완전 제거
                // 3) 고온 열기에 달아오르는 주황빛 앰버 화선(Combustion Wave Front) 선행
                // 4) 삼탈워 원본 숯 텍스처(_CharredMap)를 나뭇잎에 직접 매핑하여 깊은 흑색 숯(Charcoal Black)과 회백색 재(Ash)로 완벽 탄화
                // 5) 숯 결(Cracks)과 엽맥 고주파 노이즈를 타고 구멍이 숭숭 뚫리며 바스러지는 침식 디졸브(Perforated Ash Dissolve)

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
                    // 탄화 직전(localProgress 0.04 ~ 0.35), 고온 열기로 잎사귀 수분이 증발하며
                    // 잎사귀 면 전체가 바싹 마른 단풍 앰버 및 이글거리는 숯불 주황/골드 톤으로 서서히 달아오름
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
                half trunkCharFactor = (burnProg > 0.001) ? saturate((burnProg - 0.10) / 0.55) : 0.0;
                trunkCharFactor = trunkCharFactor * trunkCharFactor * (3.0 - 2.0 * trunkCharFactor);

                if (trunkCharFactor > 0.001)
                {
                    half4 charredSample = SAMPLE_TEXTURE2D(_CharredMap, sampler_CharredMap, input.uv * _CharredTile);
                    half sampleLuma = dot(charredSample.rgb, half3(0.299, 0.587, 0.114));
                    half3 charredAlbedo;
                    if (sampleLuma > 0.02)
                    {
                        charredAlbedo = charredSample.rgb * half3(0.14, 0.13, 0.13);
                    }
                    else
                    {
                        charredAlbedo = albedo.rgb * half3(0.08, 0.075, 0.075);
                    }
                    albedo.rgb = lerp(albedo.rgb, charredAlbedo, trunkCharFactor);
                }
            #endif

                float3 normalWS = normalize(input.normalWS);
            #if defined(_NORMALMAP)
                half4 nSample = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv);
                half3 normalTS = UnpackNormalScale(nSample, _BumpScale);
                normalWS = normalize(normalWS);
            #endif

                // URP 메인 라이트 (태양광) 조명 연산
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half NdotL = saturate(dot(normalWS, mainLight.direction));

            #if defined(_ALPHATEST_ON)
                // [삼탈워 실사 나뭇잎: 반투명 햇살 투광 (Subsurface Translucency)]
                // 햇빛을 등진 반대편 나뭇잎에도 나뭇잎 세포 조직을 뚫고 나오는 화사한 투광 적용 (역광 암부 떡짐 방지)
                half transmission = saturate(dot(-normalWS, mainLight.direction) * 0.75 + 0.25) * 0.50;
                half directLight = (NdotL + transmission) * mainLight.shadowAttenuation;
                half3 diffuse = albedo.rgb * (mainLight.color * directLight);

                // 그림자 진 영역에서도 디테일이 뭉개지지 않도록 부드러운 스카이라이트 SH 바운스 확보
                half3 ambient = max(SampleSH(normalWS), SampleSH(float3(0, 1, 0)) * 0.42) * albedo.rgb;
                half3 finalColor = diffuse + ambient;
            #else
                half3 diffuse = albedo.rgb * (mainLight.color * (NdotL * mainLight.shadowAttenuation));
                half3 ambient = max(SampleSH(normalWS), SampleSH(float3(0, 1, 0)) * 0.35) * albedo.rgb;
                half3 finalColor = diffuse + ambient;
            #endif


                // [기둥/줄기는 스스로 빛나는 네온사인이 아님]
                // 기둥 본체는 검은 숯(Charred Bark)으로 그을릴 뿐이며, 빛은 화염 파티클의 실제 불길과 포인트 라이트가 비춤!

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
                float _BurnProgress;
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
