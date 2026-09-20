Shader "Orbital Rift/Spell Unlit"
{
    Properties
    {
        _MainTex ("Mask", 2D) = "white" {}
        [HDR] _Tint ("Tint / emission", Color) = (1,1,1,1)
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination blend (One additive, OneMinusSrcAlpha alpha)", Float) = 1
        _Flow ("Subtle trail movement", Range(0,1)) = 0
        [Toggle] _TextureColor ("Preserve PNG colors", Float) = 0
        [Toggle] _KeyDarkPlate ("Remove dark blue PNG plate", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha [_DstBlend]
        Cull Off ZWrite Off Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Tint;
            float _Flow;
            float _TextureColor, _KeyDarkPlate;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            v2f vert(appdata v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Tint; return o; }
            float4 frag(v2f i) : SV_Target
            {
                float4 tex=tex2D(_MainTex,i.uv);
                float mask=tex.a;
                float luminance=dot(tex.rgb,float3(.2126,.7152,.0722));
                float key=smoothstep(.06,.18,luminance)*saturate((tex.r-tex.b)*7+.18);
                mask *= lerp(1,key,_KeyDarkPlate);
                mask *= 1 - _Flow * .12 * (1 + sin(i.uv.x * 24 - _Time.y * 5));
                return float4(i.color.rgb * lerp(float3(1,1,1),tex.rgb,_TextureColor), i.color.a * mask);
            }
            ENDCG
        }
    }
}
