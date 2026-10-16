Shader "Skybox/TotalWarPanoramicWithSun"
{
    Properties
    {
        [Header(Skybox Texture)]
        _MainTex ("Panoramic Sky Texture (3K Total War)", 2D) = "white" {}
        _Exposure ("Sky Exposure", Range(0.1, 3.0)) = 1.0
        _Tint ("Sky Tint", Color) = (1, 1, 1, 1)
        _SkyRotation ("Sky Rotation Yaw", Range(0.0, 360.0)) = 0.0

        [Header(Sky Dome Horizon and Fog)]
        _HorizonOffset ("Horizon Height Offset", Range(-0.3, 0.3)) = 0.0
        _HorizonFogIntensity ("Horizon Fog Intensity", Range(0.0, 1.0)) = 0.65
        _HorizonFogHeight ("Horizon Fog Spread Height", Range(0.05, 0.5)) = 0.22
        _HorizonFogColor ("Horizon Fog Color", Color) = (0.72, 0.75, 0.78, 1)
        _GroundColor ("Ground Fill Color", Color) = (0.42, 0.38, 0.30, 1)

        [Header(Total War Sun Disk)]
        [Toggle] _EnableSun ("Enable Sun Disk", Float) = 1.0
        _SunDirection ("Sun Direction (Normalized)", Vector) = (0.766, 0.643, 0.0, 0)
        [HDR] _SunColor ("Sun Disk Color (HDR)", Color) = (2.2, 2.0, 1.7, 1)
        _SunSize ("Sun Disk Angular Radius", Range(0.005, 0.08)) = 0.016
        _SunEdgeSoftness ("Sun Edge Softness", Range(0.001, 0.02)) = 0.004

        [Header(Sun Atmospheric Corona)]
        [HDR] _SunGlowColor ("Sun Corona Glow Color (HDR)", Color) = (1.0, 0.82, 0.55, 1)
        _SunGlowExponent ("Sun Glow Falloff Exponent", Range(1.0, 24.0)) = 6.0
        _SunGlowIntensity ("Sun Glow Intensity", Range(0.0, 1.5)) = 0.50
        _SunCoronaRadiusMultiplier ("Sun Corona Radius Multiplier", Range(1.5, 6.0)) = 2.8
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 rayDir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half _Exposure;
            half4 _Tint;
            float _SkyRotation;

            float _HorizonOffset;
            float _HorizonFogIntensity;
            float _HorizonFogHeight;
            half4 _HorizonFogColor;
            half4 _GroundColor;

            float _EnableSun;
            float4 _SunDirection;
            half4 _SunColor;
            float _SunSize;
            float _SunEdgeSoftness;

            half4 _SunGlowColor;
            float _SunGlowExponent;
            float _SunGlowIntensity;
            float _SunCoronaRadiusMultiplier;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.rayDir = v.vertex.xyz;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.rayDir);

                // 1. 방위각 U 연산: 360도 1바퀴 완벽 래핑 (절단선 방지, 0~1)
                float radYaw = _SkyRotation * (3.1415926535 / 180.0);
                float sinY = sin(radYaw);
                float cosY = cos(radYaw);
                // Y축 회전 적용
                float3 rotDir = float3(
                    dir.x * cosY - dir.z * sinY,
                    dir.y,
                    dir.x * sinY + dir.z * cosY
                );

                // [-pi, pi] -> [0, 1] 완전한 360도 파노라마
                float u = frac(atan2(rotDir.z, rotDir.x) / (2.0 * 3.1415926535) + 0.5);

                // 2. 삼탈워 스카이돔 수직 V 연산 (천정: V=0.0, 지평선: V=1.0)
                float elevation = asin(clamp(rotDir.y + _HorizonOffset, -1.0, 1.0));
                float normElevation = clamp(elevation / (0.5 * 3.1415926535), 0.0, 1.0);
                float v = 1.0 - normElevation;

                float2 uv = float2(u, v);

                // 3. 360도 래핑 Seam line 편미분 튐 완벽 제거 (tex2Dgrad)
                float2 dx = ddx(uv);
                float2 dy = ddy(uv);
                if (abs(dx.x) > 0.5) dx.x = 0.0;
                if (abs(dy.x) > 0.5) dy.x = 0.0;

                half4 skyCol = tex2Dgrad(_MainTex, uv, dx, dy) * _Tint * _Exposure;

                // 4. 삼탈워 공식 지평선 대기 안개 (Atmospheric Horizon Fog)
                // 지평선(elevation=0) 부근을 부드러운 안개빛으로 덮어 지형과 자연스럽게 블렌딩
                float fogDist = abs(elevation) / max(0.01, _HorizonFogHeight);
                float horizonFogFactor = exp(-fogDist * fogDist * 2.0) * _HorizonFogIntensity;
                skyCol.rgb = lerp(skyCol.rgb, _HorizonFogColor.rgb * _Exposure, horizonFogFactor);

                // 5. 지평선 아래 영역 (지면 색상)
                if (rotDir.y + _HorizonOffset < 0.0)
                {
                    float groundFactor = saturate(-(rotDir.y + _HorizonOffset) * 5.0);
                    skyCol.rgb = lerp(skyCol.rgb, _GroundColor.rgb * _Exposure, groundFactor);
                }

                // 6. 삼국지 토탈워 공식 태양 광구 & 국소 코로나 (Sun Disk & Corona Glow)
                if (_EnableSun > 0.5)
                {
                    float3 sunDir = normalize(_SunDirection.xyz);
                    float cosTheta = dot(dir, sunDir);

                    // A. 태양 본체 광구 디스크 (Sun Disk)
                    float cosSunRadius = cos(_SunSize);
                    float diskAlpha = smoothstep(cosSunRadius - _SunEdgeSoftness, cosSunRadius, cosTheta);
                    half3 sunDisk = _SunColor.rgb * diskAlpha;

                    // B. 태양 대기 코로나 후광 (태양 중심 국소 영역 제한)
                    float maxCoronaAngle = clamp(_SunSize * _SunCoronaRadiusMultiplier, 0.04, 0.35);
                    float cosCorona = cos(maxCoronaAngle);
                    float glowFactor = saturate((cosTheta - cosCorona) / max(0.0001, 1.0 - cosCorona));
                    glowFactor = pow(glowFactor, max(1.0, _SunGlowExponent));
                    half3 coronaGlow = _SunGlowColor.rgb * (glowFactor * _SunGlowIntensity);

                    // C. 가산 합성
                    skyCol.rgb += coronaGlow + sunDisk;
                }

                return half4(skyCol.rgb, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
