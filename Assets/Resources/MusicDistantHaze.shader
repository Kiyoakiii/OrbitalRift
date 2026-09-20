Shader "OrbitalRift/Music Distant Haze"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                // Only soften the ribbon itself: gameplay behind it stays sharp.
                float across = abs(i.uv.y * 2.0 - 1.0);
                float haze = exp(-across * across * 5.5) * (1.0 - smoothstep(.72, 1.0, across));
                return fixed4(i.color.rgb, i.color.a * haze);
            }
            ENDCG
        }
    }
    Fallback Off
}
