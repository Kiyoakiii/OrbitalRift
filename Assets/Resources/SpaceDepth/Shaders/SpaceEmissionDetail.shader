Shader "OrbitalRift/SpaceEmissionDetail"
{
    Properties
    {
        _MainTex("Main detailed artwork",2D)="black" {}
        _AlternateTex("Alternate cloud artwork",2D)="black" {}
        _UseAlternate("Alternate variants",Float)=0
        _Exposure("Light intensity",Range(0,3))=1
        _Saturation("Saturation",Range(0,2))=1.12
        _NebulaArt("Nebula controls",Float)=0
        _ArtMotion("Travel-driven time",Float)=0
        _Glow("Glow",Range(0,2))=.55
        _Filament("Filament definition",Range(0,1))=.7
        _DarkVoids("Shadow contrast",Range(0,1))=.6
        _Drift("Internal drift",Range(0,1))=.2
        _Dust("Embedded star highlights",Range(0,2))=.6
        _NebulaTint("Cloud tint",Color)=(1,1,1,1)
        _HighlightTint("Highlight tint",Color)=(.75,.9,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend One One
        ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex,_AlternateTex;
            float _UseAlternate,_Exposure,_Saturation,_NebulaArt,_ArtMotion,_Glow,_Filament,_DarkVoids,_Drift,_Dust;
            float4 _NebulaTint,_HighlightTint;
            struct a{float4 vertex:POSITION;float2 uv:TEXCOORD0;float2 seed:TEXCOORD1;float4 color:COLOR;};
            struct v{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float2 seed:TEXCOORD1;float4 color:COLOR;};
            v vert(a i){v o;o.pos=UnityObjectToClipPos(i.vertex);o.uv=i.uv;o.seed=i.seed;o.color=i.color;return o;}
            float4 frag(v i):SV_Target
            {
                float2 uv=i.uv;
                // Displace one sharp sample; never average shifted copies of the detailed source.
                float t=_ArtMotion*.065;
                uv+=float2(sin(uv.y*5+t+i.seed.x),cos(uv.x*4-t*.7+i.seed.x))*.002*_Drift*_NebulaArt;
                float4 art;
                if(_UseAlternate>.5&&i.seed.y>.5)art=tex2D(_AlternateTex,uv);
                else art=tex2D(_MainTex,uv);
                float3 rgb=art.rgb;
                float contrast=lerp(1,1+_DarkVoids*.23,_NebulaArt);
                rgb=pow(max(rgb,0),contrast);
                float lum=dot(rgb,float3(.2126,.7152,.0722));
                rgb=max(0,lerp(lum.xxx,rgb,_Saturation));
                float highlights=smoothstep(.45,1,max(rgb.r,max(rgb.g,rgb.b)));
                float emission=lerp(1,1+_Glow*.35+_Filament*.15+highlights*_Dust*.12,_NebulaArt);
                float3 tint=lerp(float3(1,1,1),lerp(_NebulaTint.rgb,_HighlightTint.rgb,highlights*.16),_NebulaArt);
                float2 edge=smoothstep(0,.045,i.uv)*smoothstep(0,.045,1-i.uv);
                rgb*=tint*i.color.rgb*i.color.a*art.a*edge.x*edge.y*_Exposure*emission;
                // Soft highlight shoulder retains hue and detail instead of clipping channels to white.
                float peak=max(rgb.r,max(rgb.g,rgb.b));
                rgb/=1+max(0,peak-.78)*.8;
                return float4(rgb,0);
            }
            ENDCG
        }
    }
}
