Shader "CatFight/UIBlur"
{
    // 屏幕快照模糊。配合 BattleIntro 用：把低分辨率快照用双线性放大 + 多点卷积，
    // 得到“由模糊到清晰”的入场过渡。
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BlurSize ("Blur Size", Range(0, 12)) = 4
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float4 color : COLOR;
                float2 uv    : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float _BlurSize;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 9 点高斯近似（分离式两遍会更快，但这里只用于一次性过渡，直接一遍即可）
                float2 texel = _MainTex_TexelSize.xy * _BlurSize;

                fixed4 sum = tex2D(_MainTex, i.uv) * 0.25;

                sum += tex2D(_MainTex, i.uv + float2( texel.x, 0)) * 0.125;
                sum += tex2D(_MainTex, i.uv + float2(-texel.x, 0)) * 0.125;
                sum += tex2D(_MainTex, i.uv + float2(0,  texel.y)) * 0.125;
                sum += tex2D(_MainTex, i.uv + float2(0, -texel.y)) * 0.125;

                sum += tex2D(_MainTex, i.uv + float2( texel.x,  texel.y)) * 0.0625;
                sum += tex2D(_MainTex, i.uv + float2(-texel.x,  texel.y)) * 0.0625;
                sum += tex2D(_MainTex, i.uv + float2( texel.x, -texel.y)) * 0.0625;
                sum += tex2D(_MainTex, i.uv + float2(-texel.x, -texel.y)) * 0.0625;

                return fixed4(sum.rgb, 1.0) * i.color;
            }
        ENDCG
        }
    }
}
