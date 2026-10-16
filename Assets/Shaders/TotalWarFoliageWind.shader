Shader "Universal Render Pipeline/TotalWarFoliageWind"
{
    Properties
    {
        [Header(Base Texture)]
        _BaseMap ("Albedo Map", 2D) = "white" {}
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
                float _Cull;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

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

                // 4. 나뭇잎/잔가지 카드 고주파 파르르 떨림 (Leaf Flutter)
                // 잎사귀 메쉬 버텍스별 고유 위상으로 파르르 떠는 토탈워 특유의 생동감 연출
                float leafPhase = windTime * 4.2 + dot(input.positionOS.xyz, float3(2.5, 3.8, 1.7));
                float leafWave = sin(leafPhase) * (0.045 * _LeafFlutter);

                // 5. 오브젝트 로컬 공간 변위 합성
                float3 worldWindDir = float3(normWindDir.x, 0.0, normWindDir.y);
                float3 objWindDir = normalize(TransformWorldToObjectDir(worldWindDir));

                // 줄기 굽힘 변위 (Stem Sway) + 잎사귀 국소 진동 (Leaf Flutter)
                float3 stemDisp = objWindDir * (stemWave * stemBendFactor * _WindMultiplier);
                float3 leafDisp = (input.normalOS * 0.65 + objWindDir * 0.35) * (leafWave * saturate(normY + 0.15) * _WindMultiplier);

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

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

            #if defined(_ALPHATEST_ON)
                clip(albedo.a - _Cutoff);
            #endif

                float3 normalWS = normalize(input.normalWS);
            #if defined(_NORMALMAP)
                half4 nSample = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv);
                half3 normalTS = UnpackNormalScale(nSample, _BumpScale);
                // Two-sided 양면 노멀 보정
                normalWS = normalize(normalWS);
            #endif

                // URP 메인 라이트 (태양광) 조명 연산
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                // 양면 렌더링 나뭇잎 투광(Subsurface Bounce) 근사
                NdotL = max(NdotL, saturate(dot(-normalWS, mainLight.direction) * 0.45));

                half3 diffuse = albedo.rgb * (mainLight.color * (NdotL * mainLight.shadowAttenuation));

                // 앰비언트 환경광 (삼탈워 Trilight 지면 반사광)
                half3 ambient = SampleSH(normalWS) * albedo.rgb;
                half3 finalColor = diffuse + ambient;

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
                float _Cull;
            CBUFFER_END

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

                float leafPhase = windTime * 4.2 + dot(input.positionOS.xyz, float3(2.5, 3.8, 1.7));
                float leafWave = sin(leafPhase) * (0.045 * _LeafFlutter);

                float3 worldWindDir = float3(normWindDir.x, 0.0, normWindDir.y);
                float3 objWindDir = normalize(TransformWorldToObjectDir(worldWindDir));

                float3 stemDisp = objWindDir * (stemWave * stemBendFactor * _WindMultiplier);
                float3 leafDisp = (input.normalOS * 0.65 + objWindDir * 0.35) * (leafWave * saturate(normY + 0.15) * _WindMultiplier);

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
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

            #if defined(_ALPHATEST_ON)
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                clip(alpha - _Cutoff);
            #endif

                return 0;
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}
