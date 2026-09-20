Shader "Orbital Rift/Phoenix Wing Cutout"
{
    Properties
    {
        [PerRendererData] _MainTex ("Wing PNG", 2D) = "white" {}
        [PerRendererData] [HDR] _Color ("Tint", Color) = (1,1,1,1)
        [Toggle] _RemoveCheckerboard ("Remove baked checkerboard", Float) = 1
        _BackgroundTolerance ("Background tolerance", Range(.005,.20)) = .045
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _Color;
            float _RemoveCheckerboard;
            float _BackgroundTolerance;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            float IsFlatCheckerPixel(float4 sample)
            {
                float luminance = dot(sample.rgb, float3(.2126, .7152, .0722));
                float chroma = max(sample.r, max(sample.g, sample.b)) - min(sample.r, min(sample.g, sample.b));
                float neutral = 1 - smoothstep(.012, .085, chroma);
                float white = 1 - smoothstep(_BackgroundTolerance * .45, _BackgroundTolerance, abs(luminance - .992));
                float gray = 1 - smoothstep(_BackgroundTolerance * .45, _BackgroundTolerance, abs(luminance - .776));
                return neutral * max(white, gray);
            }

            float CheckerboardMask(float2 uv)
            {
                // A flat background square is surrounded by the same flat square.
                // The small neighbourhood prevents bright feather highlights from
                // being removed just because they contain nearly-white pixels.
                float2 texelStep = _MainTex_TexelSize.xy * 3.0;
                float centre = IsFlatCheckerPixel(tex2D(_MainTex, uv));
                float left = IsFlatCheckerPixel(tex2D(_MainTex, uv - float2(texelStep.x, 0)));
                float right = IsFlatCheckerPixel(tex2D(_MainTex, uv + float2(texelStep.x, 0)));
                float down = IsFlatCheckerPixel(tex2D(_MainTex, uv - float2(0, texelStep.y)));
                float up = IsFlatCheckerPixel(tex2D(_MainTex, uv + float2(0, texelStep.y)));
                return centre * (left + right + down + up + centre) * .2;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                c.a *= 1 - saturate(CheckerboardMask(i.uv) * _RemoveCheckerboard);
                return c * i.color;
            }
            ENDCG
        }
    }
}
