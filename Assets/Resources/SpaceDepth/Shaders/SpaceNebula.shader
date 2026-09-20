Shader "OrbitalRift/SpaceNebula"
{
    Properties
    {
        _MainTex("Nebula filaments (RGBA)",2D)="black" {}
        _Color("Tint",Color)=(1,1,1,1)
        _ArtMotion("Travel driven animation",Float)=0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Color;
            float _ArtMotion;
            struct a{float4 vertex:POSITION;float2 uv:TEXCOORD0;float2 seed:TEXCOORD1;float4 color:COLOR;};
            struct v{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float seed:TEXCOORD1;float4 color:COLOR;};
            v vert(a i){v o;o.pos=UnityObjectToClipPos(i.vertex);o.uv=i.uv;o.seed=i.seed.x;o.color=i.color*_Color;return o;}
            float4 frag(v i):SV_Target
            {
                // Two very small counter-moving samples add internal drift without a full-screen noise pass.
                float t=_ArtMotion*.12;
                float2 flow=float2(sin(t+i.seed),cos(t*.71+i.seed*1.7))*.012;
                float2 uv=(i.uv-.5)*.96+.5;
                float4 cloud=tex2D(_MainTex,uv+flow);
                float4 veil=tex2D(_MainTex,uv-flow*.65);
                cloud=lerp(cloud,veil,.28);
                float2 edge=smoothstep(0,.13,i.uv)*smoothstep(0,.13,1-i.uv);
                cloud.rgb*=i.color.rgb*1.2;
                cloud.a*=i.color.a*edge.x*edge.y;
                return cloud;
            }
            ENDCG
        }
    }
}
