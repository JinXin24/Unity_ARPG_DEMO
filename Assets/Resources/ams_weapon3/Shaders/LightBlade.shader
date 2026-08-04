Shader "Weapon/LightBlade"
{
    Properties
    {
        [HDR] _Color ("光刃颜色", Color) = (0.2, 0.6, 1, 1)
        _MainTex ("光刃贴图", 2D) = "white" {}
        _ScrollSpeed ("UV 滚动速度", Float) = 0.5
        _RimPower  ("边缘光强度", Range(0, 5)) = 2
        _Brightness ("整体亮度", Range(0, 5)) = 1.5
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha One       // Additive
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _ScrollSpeed;
                float _RimPower;
                float _Brightness;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.uv.y += _Time.y * _ScrollSpeed; // UV 滚动
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.viewDirWS = GetWorldSpaceViewDir(posWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // Fresnel 边缘光
                half fresnel = 1.0 - saturate(dot(normalize(IN.normalWS), normalize(IN.viewDirWS)));
                fresnel = pow(fresnel, _RimPower);

                half4 col = tex * _Color * _Brightness;
                col.rgb += fresnel * _Color.rgb * 0.5; // 边缘更亮

                return col;
            }
            ENDHLSL
        }
    }
}
