// Gambar sprite sebagai siluet satu warna (rgb dari _Color, bentuk dari alpha sprite).
// Dipakai SpriteOutline untuk garis tepi putih. Ada di folder Resources supaya ikut build dan bisa dimuat lewat kode.
Shader "Game/SpriteSolidColor"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float4 color : COLOR;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float4 color : COLOR;
            float2 uv : TEXCOORD0;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            // Flip X / Flip Y SpriteRenderer diterapkan di shader (unity_SpriteProps.xy = 1 atau -1), sama seperti
            // shader sprite bawaan URP. Tanpa ini garis tepi tidak ikut terbalik saat badan memakai sprite 'flipped'.
            float3 positionOS = float3(input.positionOS.xy * unity_SpriteProps.xy, input.positionOS.z);
            output.positionCS = TransformObjectToHClip(positionOS);
            output.uv = input.uv;     // sprite tidak memakai tiling/offset, jadi tanpa _MainTex_ST (agar SRP Batcher 2D tetap aktif)
            output.color = input.color;
            return output;
        }

        half4 Frag(Varyings input) : SV_Target
        {
            half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
            return half4(_Color.rgb, alpha * _Color.a * input.color.a);
        }
        ENDHLSL

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}
