// Dimmer UI dengan lubang lingkaran atau kapsul (pill) yang dihitung per pixel, jadi pinggirannya halus (anti-aliased).
// Posisi dan ukuran lubang dikirim oleh script SpotlightDimmer dari RectTransform hole di scene.
Shader "UI/SpotlightDimmer"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Feather ("Feather (soft edge, canvas units)", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            #define MAX_HOLES 8

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _Feather;

            // xy = pusat lubang, z = radius sudut, w = setengah panjang bagian lurus (kapsul).
            // w >= 0: memanjang horizontal, w < 0: memanjang vertikal, w = 0: lingkaran biasa.
            float4 _Holes[MAX_HOLES];
            float _HoleCount;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                float2 pos = IN.worldPosition.xy;
                // Lebar 1 pixel layar dalam satuan canvas, untuk anti-aliasing.
                float aa = max(fwidth(pos.x), fwidth(pos.y)) * 0.75;

                float visible = 1.0;
                for (int i = 0; i < MAX_HOLES; i++)
                {
                    if (i < (int)_HoleCount)
                    {
                        float2 d = pos - _Holes[i].xy;
                        float seg = _Holes[i].w;
                        d = seg >= 0.0
                            ? float2(max(abs(d.x) - seg, 0.0), d.y)
                            : float2(d.x, max(abs(d.y) + seg, 0.0));
                        float dist = length(d);
                        float radius = _Holes[i].z;
                        visible *= smoothstep(radius - aa - _Feather, radius + aa, dist);
                    }
                }
                color.a *= visible;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                return color;
            }
        ENDCG
        }
    }
}
