Shader "Universal Render Pipeline/TotalWarRadialGrassLOD"
{
    Properties
    {
        [Header(Base_Texture)]
        _BaseMap ("기본 알베도 텍스처", 2D) = "white" {}
        [HideInInspector] _MainTex ("기본 텍스처 (RGB)", 2D) = "white" {}
        _BumpMap ("법선 굴곡 텍스처 (노멀)", 2D) = "bump" {}
        _BaseColor ("기본 색상 조절 (틴트)", Color) = (1, 1, 1, 1)

        [Header(Alpha_Cutout)]
        _Cutoff ("알파 컷오프 임계값", Range(0.0, 1.0)) = 0.25
        _Cull ("렌더링 면 모드 (0: 양면, 2: 단면)", Float) = 0.0

        [Header(Distance_LOD)]
        _FadeStartDistance ("페이드 시작 거리 (미터)", Float) = 150.0
        _FadeEndDistance ("완전 소멸 거리 (미터)", Float) = 450.0

        [Header(Wind_Animation)]
        _WindMultiplier ("바람 흔들림 강도 배율", Range(0.0, 2.0)) = 0.35
        _WindFrequency ("바람 파동 주기 주파수", Range(0.1, 5.0)) = 1.6
        _LeafFlutter ("풀잎 끝 펄럭임 세기", Range(0.0, 1.0)) = 0.25

        [Header(Surface_Properties)]
        _Smoothness ("표면 매끄러움 (광택)", Range(0.0, 1.0)) = 0.05
        _Metallic ("금속성 (메탈릭)", Range(0.0, 1.0)) = 0.0

        [Header(Tip_And_Edge_Feathering)]
        _TipFadeStart ("풀잎 끝 감쇄 시작점 (V)", Range(0.85, 1.0)) = 0.940
        _TipFadeEnd ("풀잎 끝 완전 소멸점 (V)", Range(0.90, 1.0)) = 0.985
        _EdgeBleedCrop ("외곽 파편 차단 여백 (U)", Range(0.0, 0.03)) = 0.006
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "TerrainCompatible" = "True"
        }

        Cull [_Cull]

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float distanceToCam : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                float heightNorm : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
                float _Cull;
                float _FadeStartDistance;
                float _FadeEndDistance;
                float _WindMultiplier;
                float _WindFrequency;
                float _LeafFlutter;
                half _Smoothness;
                half _Metallic;
                half _TipFadeStart;
                half _TipFadeEnd;
                half _EdgeBleedCrop;
            CBUFFER_END

            // 4x4 베이어 디더링 매트릭스 (부드러운 거리 페이드용)
            static const float DitherMatrix[16] =
            {
                1.0 / 17.0,  9.0 / 17.0,  3.0 / 17.0, 11.0 / 17.0,
                13.0 / 17.0,  5.0 / 17.0, 15.0 / 17.0,  7.0 / 17.0,
                4.0 / 17.0, 12.0 / 17.0,  2.0 / 17.0, 10.0 / 17.0,
                16.0 / 17.0,  8.0 / 17.0, 14.0 / 17.0,  6.0 / 17.0
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            // 🍂❄️ 전역 사계절 셰이더 프로퍼티 (TotalWarTerrainManager 브로드캐스트)
            float _TotalWarTerrainAutumnTint;
            float _TotalWarTerrainSnowAmount;

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                float heightNorm = saturate(input.positionOS.y / 0.85);

                // 🌿 미풍 바람 애니메이션 (뿌리 Y=0은 고정, 잎사귀 상단만 부드럽게 살랑거림)
                float windTime = _Time.y * _WindFrequency;
                float wave = sin(windTime + worldPos.x * 1.5 + worldPos.z * 1.2);
                float flutter = sin(windTime * 2.8 + worldPos.x * 3.4) * _LeafFlutter;

                float3 windDisp = float3(wave * 0.08 + flutter * 0.03, 0.0, wave * 0.06) * (heightNorm * _WindMultiplier);
                float3 finalWorldPos = worldPos + windDisp;

                output.positionCS = TransformWorldToHClip(finalWorldPos);
                output.positionWS = finalWorldPos;

                // 노멀 보존 (버텍스 노멀이 없거나 비정상일 때 안전 폴백)
                float3 nWS = TransformObjectToWorldNormal(input.normalOS);
                if (dot(nWS, nWS) < 0.1) nWS = float3(0.0, 1.0, 0.0);
                output.normalWS = nWS;

                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                // 카메라 거리 및 뿌리 차폐(AO)용 높이 정규화 계산
                float3 camPos = GetCameraPositionWS();
                output.distanceToCam = distance(finalWorldPos, camPos);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.heightNorm = heightNorm;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // 1. 삼탈워 공식 고해상도 아틀라스 텍스처 샘플링 (버텍스 컬러 0 오염 완전 원천 차단)
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                // 🌿 [칼자국 및 공중 파편 완전 소멸: 아틀라스 UV 경계 소프트 테이퍼링 (Tip & Edge Feathering)]
                // 1) 상단 수평 칼자국(V = 1.000 경계 절단) 방지: 텍스처 윗변 모서리에 닿기 전(0.940 ~ 0.985) 알파를 부드럽게 감쇄시켜 잎끝을 뾰족하게 마감
                half topFade = saturate((_TipFadeEnd - input.uv.y) / max(0.001, _TipFadeEnd - _TipFadeStart));
                topFade = topFade * topFade * (3.0 - 2.0 * topFade);

                // 2) 외곽 공중 파편 부스러기(U = 0.000 / 1.000 경계 누수 텍셀) 방지:
                half uFade = saturate((input.uv.x - _EdgeBleedCrop) / max(0.001, _EdgeBleedCrop * 2.5)) * saturate(((1.0 - _EdgeBleedCrop) - input.uv.x) / max(0.001, _EdgeBleedCrop * 2.5));
                uFade = uFade * uFade * (3.0 - 2.0 * uFade);

                half vBottomFade = saturate((input.uv.y - 0.004) / 0.012);
                vBottomFade = vBottomFade * vBottomFade * (3.0 - 2.0 * vBottomFade);

                half edgeMask = topFade * uFade * vBottomFade;
                half currentAlpha = albedo.a * edgeMask;
                half effectiveCutoff = max(_Cutoff, 0.12);

                // 2. 기본 알파 컷아웃 (풀잎 잎사귀 실루엣 추출)
                clip(currentAlpha - effectiveCutoff);

                // 🍂❄️ 전역 사계절 색조 실시간 블렌딩 (가을 황금빛 및 겨울 적설 서리)
                if (_TotalWarTerrainAutumnTint > 0.01)
                {
                    half3 autumnWarmth = albedo.rgb * half3(1.18, 0.90, 0.55);
                    albedo.rgb = lerp(albedo.rgb, autumnWarmth, saturate(_TotalWarTerrainAutumnTint * 0.85));
                }

                if (_TotalWarTerrainSnowAmount > 0.02)
                {
                    float snowCap = saturate(input.normalWS.y * 1.25 - 0.1) * _TotalWarTerrainSnowAmount;
                    half3 frostWhite = half3(0.92, 0.95, 1.05);
                    albedo.rgb = lerp(albedo.rgb, frostWhite, snowCap * 0.85);
                }

                // 3. 👁️ [핵심 고증: 더 위에 있으면 거의 안보임] 거리 기반 점진적 페이드아웃 (Distance Fade)
                float dist = input.distanceToCam;
                if (dist > _FadeStartDistance)
                {
                    if (dist >= _FadeEndDistance)
                    {
                        // 220m 이상 초고고도 전략 뷰: 100% 완전 소멸 (지형 PBR 잔디 텍스처가 100% 이어받음)
                        clip(-1.0);
                    }
                    else
                    {
                        // 70m ~ 220m: 4x4 스크린 도트 디더링으로 부드러운 유기적 디졸브 페이드
                        float fadeFactor = saturate((_FadeEndDistance - dist) / max(0.001, _FadeEndDistance - _FadeStartDistance));
                        float2 screenPos = input.positionCS.xy;
                        int ditherIdx = (int(fmod(screenPos.x, 4.0)) << 2) + int(fmod(screenPos.y, 4.0));
                        float ditherThreshold = DitherMatrix[ditherIdx];

                        if (fadeFactor < ditherThreshold)
                        {
                            clip(-1.0);
                        }
                    }
                }

                // 4. ☀️ [자연스러운 지형 통합 라이팅 + 실시간 그림자 감쇠 + 뿌리 차폐(AO)]
                float3 normalWS = normalize(input.normalWS);
                if (dot(normalWS, normalWS) < 0.1) normalWS = float3(0.0, 1.0, 0.0);

                // URP 캐스케이드 그림자 좌표 샘플링 (산맥/성벽/나무 그림자 속에서는 풀도 함께 어두워져 발광 현상 제거)
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 lightDir = mainLight.direction;
                if (dot(lightDir, lightDir) < 0.001) lightDir = normalize(half3(0.35, 0.85, 0.35));

                half3 lightCol = mainLight.color;
                if (dot(lightCol, lightCol) < 0.001) lightCol = half3(1.0, 0.96, 0.88);
                half lightAtten = mainLight.distanceAttenuation * mainLight.shadowAttenuation;

                // 완만한 랩(Wrapped) 램버트 수광 + 그림자 감쇠 적용
                float NdotL = saturate(dot(normalWS, lightDir));
                float wrappedDiffuse = saturate(NdotL * 0.55 + 0.45);
                float transmission = saturate(dot(-normalWS, lightDir) * 0.4 + 0.25) * 0.22;
                float directFactor = (wrappedDiffuse * 0.68 + transmission) * lightAtten;

                // 지형 환경광 (SH 라이트 우선, 보조 환경광은 과도한 형광 방지를 위해 톤 다운)
                half3 skyColor = half3(0.36, 0.42, 0.48);
                half3 groundColor = half3(0.22, 0.24, 0.17);
                half3 fallbackAmbient = lerp(groundColor, skyColor, saturate(normalWS.y * 0.5 + 0.5));
                half3 shLight = SampleSH(normalWS);
                half3 envAmbient = (dot(shLight, shLight) > 0.005) ? lerp(fallbackAmbient, shLight, 0.72) : fallbackAmbient;

                // 월드 좌표 기반 미세 군락 색조 변조 (단일 풀색의 단조로움 완화)
                float patchNoise = sin(input.positionWS.x * 0.11 + input.positionWS.z * 0.07) * cos(input.positionWS.z * 0.13 - input.positionWS.x * 0.05);
                half3 naturalTint = lerp(half3(0.88, 0.90, 0.82), half3(1.04, 0.98, 0.88), saturate(patchNoise * 0.5 + 0.5));

                // 지면 밀착 뿌리 AO: 풀 밑동은 어둡고 잎끝으로 갈수록 밝아져 바닥과의 괴리감 제거
                half rootAO = lerp(0.54, 1.0, saturate(input.heightNorm));

                half3 tintedAlbedo = albedo.rgb * naturalTint * rootAO;
                half3 diffuse = tintedAlbedo * lightCol * directFactor;
                half3 ambient = tintedAlbedo * envAmbient * 0.85;

                half3 finalColor = diffuse + ambient;
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, albedo.a);
            }
            ENDHLSL
        }

        // 그림자 투사 패스 (ShadowCaster)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
                float _Cull;
                float _FadeStartDistance;
                float _FadeEndDistance;
                float _WindMultiplier;
                float _WindFrequency;
                float _LeafFlutter;
                half _Smoothness;
                half _Metallic;
                half _TipFadeStart;
                half _TipFadeEnd;
                half _EdgeBleedCrop;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(worldPos, float3(0, 1, 0), _MainLightPosition.xyz));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                // 🌿 상단 칼자국 및 공중 파편 방지 페더링
                half topFade = saturate((_TipFadeEnd - input.uv.y) / max(0.001, _TipFadeEnd - _TipFadeStart));
                topFade = topFade * topFade * (3.0 - 2.0 * topFade);
                half uFade = saturate((input.uv.x - _EdgeBleedCrop) / max(0.001, _EdgeBleedCrop * 2.5)) * saturate(((1.0 - _EdgeBleedCrop) - input.uv.x) / max(0.001, _EdgeBleedCrop * 2.5));
                uFade = uFade * uFade * (3.0 - 2.0 * uFade);
                half vBottomFade = saturate((input.uv.y - 0.004) / 0.012);
                vBottomFade = vBottomFade * vBottomFade * (3.0 - 2.0 * vBottomFade);

                half edgeMask = topFade * uFade * vBottomFade;
                half effectiveCutoff = max(_Cutoff, 0.12);

                clip(col.a * edgeMask - effectiveCutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    // =========================================================================
    // 🌿 [Unity Built-in Render Pipeline 서브셰이더 (빌트인 전용 듀얼 호환)]
    // =========================================================================
    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
        }

        Cull [_Cull]

        Pass
        {
            Name "ForwardBase"
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "Lighting.cginc"

            sampler2D _MainTex;
            sampler2D _BaseMap;
            float4 _MainTex_ST;
            float4 _BaseMap_ST;
            fixed4 _BaseColor;
            fixed _Cutoff;
            float _FadeStartDistance;
            float _FadeEndDistance;
            float _WindMultiplier;
            float _WindFrequency;
            float _LeafFlutter;
            half _TipFadeStart;
            half _TipFadeEnd;
            half _EdgeBleedCrop;

            static const float DitherMatrix[16] =
            {
                1.0 / 17.0,  9.0 / 17.0,  3.0 / 17.0, 11.0 / 17.0,
                13.0 / 17.0,  5.0 / 17.0, 15.0 / 17.0,  7.0 / 17.0,
                4.0 / 17.0, 12.0 / 17.0,  2.0 / 17.0, 10.0 / 17.0,
                16.0 / 17.0,  8.0 / 17.0, 14.0 / 17.0,  6.0 / 17.0
            };

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float distToCam : TEXCOORD3;
                UNITY_FOG_COORDS(4)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float3 wPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float heightNorm = saturate(v.vertex.y / 0.85);

                // 🌿 미풍 바람 애니메이션
                float windTime = _Time.y * _WindFrequency;
                float wave = sin(windTime + wPos.x * 1.5 + wPos.z * 1.2);
                float flutter = sin(windTime * 2.8 + wPos.x * 3.4) * _LeafFlutter;
                float3 windDisp = float3(wave * 0.08 + flutter * 0.03, 0.0, wave * 0.06) * (heightNorm * _WindMultiplier);

                float3 finalWPos = wPos + windDisp;
                o.pos = mul(UNITY_MATRIX_VP, float4(finalWPos, 1.0));
                o.worldPos = finalWPos;

                float3 nWS = UnityObjectToWorldNormal(v.normal);
                if (dot(nWS, nWS) < 0.1) nWS = float3(0.0, 1.0, 0.0);
                o.worldNormal = nWS;

                o.uv = v.uv;
                o.distToCam = distance(finalWPos, _WorldSpaceCameraPos);

                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                // 1. 텍스처 샘플링 (_MainTex 또는 _BaseMap)
                fixed4 albedo = tex2D(_MainTex, i.uv);
                if (albedo.a < 0.001) albedo = tex2D(_BaseMap, i.uv);
                albedo *= _BaseColor;

                // 🌿 상단 칼자국 및 공중 파편 방지 페더링
                half topFade = saturate((_TipFadeEnd - i.uv.y) / max(0.001, _TipFadeEnd - _TipFadeStart));
                topFade = topFade * topFade * (3.0 - 2.0 * topFade);
                half uFade = saturate((i.uv.x - _EdgeBleedCrop) / max(0.001, _EdgeBleedCrop * 2.5)) * saturate(((1.0 - _EdgeBleedCrop) - i.uv.x) / max(0.001, _EdgeBleedCrop * 2.5));
                uFade = uFade * uFade * (3.0 - 2.0 * uFade);
                half vBottomFade = saturate((i.uv.y - 0.004) / 0.012);
                vBottomFade = vBottomFade * vBottomFade * (3.0 - 2.0 * vBottomFade);

                half edgeMask = topFade * uFade * vBottomFade;
                half effectiveCutoff = max(_Cutoff, 0.12);

                // 2. 알파 컷아웃
                clip(albedo.a * edgeMask - effectiveCutoff);

                // 3. 거리 기반 점진적 페이드아웃 (LOD Dither Fade)
                float dist = i.distToCam;
                if (dist > _FadeStartDistance)
                {
                    if (dist >= _FadeEndDistance)
                    {
                        clip(-1.0);
                    }
                    else
                    {
                        float fadeFactor = saturate((_FadeEndDistance - dist) / max(0.001, _FadeEndDistance - _FadeStartDistance));
                        float2 screenPos = i.pos.xy;
                        int ditherIdx = (int(fmod(screenPos.x, 4.0)) << 2) + int(fmod(screenPos.y, 4.0));
                        float ditherThreshold = DitherMatrix[ditherIdx];
                        if (fadeFactor < ditherThreshold) clip(-1.0);
                    }
                }

                // 4. 화사한 삼국지 전장 하프 램버트 라이팅
                float3 normalWS = normalize(i.worldNormal);
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                if (dot(lightDir, lightDir) < 0.001) lightDir = normalize(float3(0.35, 0.85, 0.35));

                fixed3 lightCol = _LightColor0.rgb;
                if (dot(lightCol, lightCol) < 0.001) lightCol = fixed3(1.15, 1.10, 0.98);

                float NdotL = saturate(dot(normalWS, lightDir));
                float halfLambert = NdotL * 0.5 + 0.5;
                float transmission = saturate(dot(-normalWS, lightDir) * 0.5 + 0.5) * 0.4;
                float totalDirect = halfLambert * 0.85 + transmission * 0.35;

                fixed3 skyColor = fixed3(0.55, 0.65, 0.78);
                fixed3 groundColor = fixed3(0.30, 0.42, 0.25);
                fixed3 envAmbient = lerp(groundColor, skyColor, saturate(normalWS.y * 0.5 + 0.5)) * 0.75;
                fixed3 shLight = ShadeSH9(float4(normalWS, 1.0));
                if (dot(shLight, shLight) > 0.01) envAmbient = max(envAmbient, shLight);

                fixed3 diffuse = albedo.rgb * lightCol * totalDirect;
                fixed3 ambient = albedo.rgb * envAmbient;
                fixed3 finalColor = diffuse + ambient;

                UNITY_APPLY_FOG(i.fogCoord, finalColor);
                return fixed4(finalColor, albedo.a);
            }
            ENDCG
        }

        // 빌트인 섀도우 캐스터
        Pass
        {
            Name "Caster"
            Tags { "LightMode" = "ShadowCaster" }

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _BaseMap;
            fixed _Cutoff;
            half _TipFadeStart;
            half _TipFadeEnd;
            half _EdgeBleedCrop;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                fixed4 col = tex2D(_MainTex, i.uv);
                if (col.a < 0.001) col = tex2D(_BaseMap, i.uv);

                half topFade = saturate((_TipFadeEnd - i.uv.y) / max(0.001, _TipFadeEnd - _TipFadeStart));
                topFade = topFade * topFade * (3.0 - 2.0 * topFade);
                half uFade = saturate((i.uv.x - _EdgeBleedCrop) / max(0.001, _EdgeBleedCrop * 2.5)) * saturate(((1.0 - _EdgeBleedCrop) - i.uv.x) / max(0.001, _EdgeBleedCrop * 2.5));
                uFade = uFade * uFade * (3.0 - 2.0 * uFade);
                half vBottomFade = saturate((i.uv.y - 0.004) / 0.012);
                vBottomFade = vBottomFade * vBottomFade * (3.0 - 2.0 * vBottomFade);

                half edgeMask = topFade * uFade * vBottomFade;
                half effectiveCutoff = max(_Cutoff, 0.12);

                clip(col.a * edgeMask - effectiveCutoff);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }

    Fallback "Transparent/Cutout/VertexLit"
    CustomEditor "MiniTotalWar.Editor.TotalWarRadialGrassShaderGUI"
}
