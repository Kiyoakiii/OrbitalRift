Shader "Orbital Rift/Solar Chick Wing"
{
    Properties
    {
        _MainTex ("Original PNG", 2D) = "white" {}
        [HDR] _Tint ("Tint", Color) = (1,1,1,1)
        _Beat ("Animation phase", Float) = 0
        _Fold ("Deformation strength", Range(0,1)) = .65
        [Enum(Bird,0,Flame,1)] _Rig ("Deformation", Float) = 0
        [Toggle] _KeyDarkPlate ("Remove dark blue PNG plate", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination blend", Float) = 10
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
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
            float _Beat,_Fold,_Rig,_KeyDarkPlate;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o;
                // The original art faces right. Only the upper-left wing folds toward
                // its shoulder. The head stays anchored. Requires a subdivided mesh.
                float wing=smoothstep(.38,.78,v.uv.y)*(1-smoothstep(.56,.78,v.uv.x));
                float beat=.5+.5*sin(_Beat);
                if (_Rig < .5)
                {
                    v.vertex.y -= wing*beat*_Fold*.26;
                    v.vertex.x += wing*beat*_Fold*.08;
                }
                else
                {
                    float tip=smoothstep(.05,.9,v.uv.y);
                    v.vertex.x += sin(v.uv.y*6-_Beat)*tip*_Fold*.13;
                    v.vertex.y += cos(v.uv.x*5+_Beat*.7)*tip*_Fold*.06;
                }
                o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float4 c=tex2D(_MainTex,i.uv);
                float lum=dot(c.rgb,float3(.2126,.7152,.0722));
                float key=smoothstep(.06,.18,lum)*saturate((c.r-c.b)*7+.18);
                c.a*=lerp(1,key,_KeyDarkPlate);
                return c*_Tint;
            }
            ENDCG
        }
    }
}
