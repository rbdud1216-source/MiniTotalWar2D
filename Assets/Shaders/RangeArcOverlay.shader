Shader "Custom/RangeArcOverlay"
{
    Properties
    {
        _Color ("Main Color", Color) = (0.18, 0.83, 0.45, 0.12)
        _RimColor ("Rim Color", Color) = (0.18, 0.83, 0.45, 0.85)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 2.5
    }

    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent+50" 
            "RenderType"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderPipeline"="UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        ZTest LEqual

        Pass
        {
            Name "RangeArcPass"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _RimColor;
                float _RimPower;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // uv.y: 0.0(최소 사거리 안쪽) -> 1.0(최대 사거리 바깥 테두리)
                // uv.x: 0.0(좌측 한계선) -> 0.5(정면) -> 1.0(우측 한계선)
                float r = input.uv.y;
                float lateral = abs(input.uv.x - 0.5) * 2.0; // 0.0(중앙) -> 1.0(양측 가장자리)

                // 외곽 테두리(Rim) 강조
                float edgeRim = pow(saturate(r), _RimPower);
                float sideRim = pow(saturate(lateral), _RimPower * 1.5);
                float totalRim = saturate(edgeRim + sideRim);

                half4 finalColor = lerp(_Color, _RimColor, totalRim);
                finalColor *= input.color;

                return finalColor;
            }
            ENDHLSL
        }
    }

    // Fallback for Built-in Pipeline
    SubShader
    {
        Tags { "Queue"="Transparent+50" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            float4 _Color;
            float4 _RimColor;
            float _RimPower;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float r = i.uv.y;
                float lateral = abs(i.uv.x - 0.5) * 2.0;
                float edgeRim = pow(saturate(r), _RimPower);
                float sideRim = pow(saturate(lateral), _RimPower * 1.5);
                float totalRim = saturate(edgeRim + sideRim);

                fixed4 finalColor = lerp(_Color, _RimColor, totalRim);
                finalColor *= i.color;
                return finalColor;
            }
            ENDCG
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
