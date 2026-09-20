Shader "OrbitalRift/SpaceDepth"
{
    Properties { _Kind("Shape",Float)=0 _Softness("Particle softness",Range(0,1))=.6 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Kind,_Softness;
            struct a { float4 vertex:POSITION;float2 uv:TEXCOORD0;float2 seed:TEXCOORD1;float4 color:COLOR; };
            struct v {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float seed:TEXCOORD1;float4 color:COLOR;};
            v vert(a i){v o;o.pos=UnityObjectToClipPos(i.vertex);o.uv=i.uv;o.seed=i.seed.x;o.color=i.color;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
            float fbm(float2 p){return noise(p)*.55+noise(p*2.03)*.28+noise(p*4.1)*.12+noise(p*8.2)*.05;}
            float4 frag(v i):SV_Target
            {
                float2 p=(i.uv-.5)*2;float r=length(p);float4 c=i.color;
                if(_Kind<.5){c.a*=pow(saturate(1-r),1.6);c.rgb*=1.15;}
                else if(_Kind<1.5){c.a*=pow(saturate(1-abs(p.x)),1.5)*pow(saturate(1-abs(p.y)),.6);}
                else if(_Kind<2.5){float n=fbm(p*3+i.seed);c.a*=smoothstep(.28,.72,n)*pow(saturate(1-r),1.4);c.rgb*=.6+n;}
                else if(_Kind<3.5)
                {
                    float rocky=step(3.5,_Kind);float edge=1-rocky*(.09+.09*sin(atan2(p.y,p.x)*7+i.seed)+.04*sin(atan2(p.y,p.x)*13));
                    float alpha=1-smoothstep(edge-.018,edge,r);float z=sqrt(saturate(1-dot(p,p)));
                    float light=saturate(dot(float3(p,z),normalize(float3(-.7,.55,.6))));
                    float n=fbm(p*(rocky>0?10:4)+i.seed);
                    c.rgb*= (.08+light*.92)*(.5+n*.9);
                    c.rgb+= (1-rocky)*float3(.08,.18,.35)*pow(saturate(1-z),4)*light;
                    c.a*=alpha;
                }
                else if(_Kind<4.5)
                {
                    // Independent low-frequency facets and high-frequency erosion break the round pebble outline.
                    float a=atan2(p.y,p.x);
                    float edge=.78+.09*sin(a*3+i.seed)+.075*cos(a*5-i.seed*2)+.045*sin(a*9+i.seed);
                    edge*=.91/max(.8,cos((frac(a*1.1140846+i.seed*.05)-.5)*.897598));
                    float rr=r/max(.5,edge);
                    float grain=noise(p*19+i.seed);
                    float facets=noise(p*4.3+i.seed);
                    float z=sqrt(saturate(1-rr*rr));
                    float3 normal=normalize(float3(p*.85+float2(facets-.5,grain*.13),z*.8));
                    float light=saturate(dot(normal,normalize(float3(-.65,.65,.8))));
                    float crags=.57+facets*.6+grain*.16;
                    c.rgb*=crags*(.18+light*1.2);
                    c.rgb+=float3(.12,.2,.34)*pow(saturate(1-z),3)*light;
                    c.a*=1-smoothstep(edge-.025,edge,r);
                }
                else if(_Kind<5.5)
                {
                    float angle=atan2(p.y,p.x);float arms=pow(.5+.5*sin(angle*2-r*13+i.seed),3);
                    float halo=exp(-r*5);float n=fbm(p*6+i.seed);
                    c.a*=saturate(halo+arms*n*.7)*smoothstep(1,.6,r);c.rgb=lerp(c.rgb,float3(.9,.83,.75),exp(-r*10));
                }
                else if(_Kind<6.5){float n=fbm(p*3+i.seed);c.rgb*=.3+n*.8;c.a*=smoothstep(1,.7,r);}
                else
                {
                    // Analytic defocus on the particle quad only: no scene texture, blur pass or extra camera.
                    float soft=exp(-dot(p,p)*lerp(16,4,_Softness))*smoothstep(1,.68,r);
                    float core=pow(saturate(1-r),7);
                    c.a*=lerp(core,soft,_Softness);c.rgb*=1.1;
                }
                return c;
            }
            ENDCG
        }
    }
}
