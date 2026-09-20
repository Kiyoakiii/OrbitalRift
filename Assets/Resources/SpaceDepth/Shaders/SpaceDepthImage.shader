Shader "OrbitalRift/SpaceDepthImage"
{
    Properties { _MainTex("Image",2D)="white"{} _Color("Tint",Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;float4 _MainTex_ST,_Color;
            struct a {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            struct v {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            v vert(a i){v o;o.pos=UnityObjectToClipPos(i.vertex);o.uv=TRANSFORM_TEX(i.uv,_MainTex);o.color=i.color*_Color;return o;}
            float4 frag(v i):SV_Target{return tex2D(_MainTex,i.uv)*i.color;}
            ENDCG
        }
    }
}
