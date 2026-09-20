using UnityEngine;

namespace OrbitalRift
{
    // One reusable quad buffer per layer. No per-star objects, Update, Instantiate or Destroy.
    public sealed class SpaceDepthLayer : MonoBehaviour
    {
        public SpaceLayerSettings Settings { get; private set; }
        public int ActiveCount { get; private set; }
        public float MotionDistance { get; private set; }
        public MeshRenderer Renderer { get; private set; }
        private struct Item { public float Angle, Radius, Seed, Spin; public Vector2 Position, Direction; public bool Steered; }
        public Vector2 TargetEffectiveVanishingPoint { get; private set; }
        public Vector2 CurrentEffectiveVanishingPoint { get; private set; }
        public Vector2 LargeParallaxOffset { get; private set; }
        public Vector2 FirstPosition => items[0].Position;
        public int Recycles { get; private set; }
        private bool steeringActive, effectiveInitialized;
        private SpaceLayerVisualBinding visual;
        private Vector2[] history;
        private int segments;
        private Item[] items;
        private Vector3[] vertices;
        private Color[] colors;
        private Vector2[] uv, seeds;
        private int[] indices;
        private Mesh mesh;
        private System.Random random;
        private int lastCount=-1;
        private SpaceNebulaSettings nebulaSettings;
        private SpaceNebulaClusterVisual nebulaDetails;
        private Camera artworkCamera;
        public void Initialize(SpaceLayerSettings settings,int index)
        {
            Settings=settings;random=new System.Random(7103+index*397);
            artworkCamera=Camera.main;
            var capacity=Mathf.Clamp(settings.Capacity,1,2048);
            segments=settings.Kind==SpaceLayerKind.Streaks?4:1;
            items=new Item[capacity]; vertices=new Vector3[capacity*4*segments];colors=new Color[capacity*4*segments];
            uv=new Vector2[vertices.Length];seeds=new Vector2[vertices.Length];indices=new int[capacity*6*segments];history=new Vector2[capacity*5];
            for(var i=0;i<capacity;i++)
            {
                items[i]=new Item{Angle=Next()*Mathf.PI*2,Radius=Mathf.Lerp(.8f,settings.DespawnRadius,Mathf.Sqrt(Next())),Seed=Next(),Spin=Next()*360};
                if(settings.Kind==SpaceLayerKind.Planet){items[i].Angle=2.65f+i*2.4f;items[i].Radius=5.7f+i*3;}
                if(settings.Kind==SpaceLayerKind.Galaxy){items[i].Angle=.5f+i*2.7f;items[i].Radius=5.6f;}
                if(settings.Kind==SpaceLayerKind.Nebula){items[i].Angle=.38f+i*2.35f;items[i].Radius=7.2f;items[i].Spin=-22+i*137;}
                if(settings.FrameComposition&&settings.Kind==SpaceLayerKind.Nebula)items[i].Spin=i==0?108:i==1?62:i==2?18:172;
                if(settings.FrameComposition&&settings.Kind==SpaceLayerKind.Galaxy)items[i].Spin=i==0?-18:27;
                if(settings.Kind==SpaceLayerKind.Asteroids&&settings.DepthVariety&&i%12==0){items[i].Radius=5.8f;items[i].Angle=.18f+(i/12)*3.15f;}
                for(var part=0;part<segments;part++)
                {
                    var v=(i*segments+part)*4;
                    for(var j=0;j<4;j++)seeds[v+j]=new Vector2(items[i].Seed*31,i%2);
                    var t=(i*segments+part)*6;indices[t]=v;indices[t+1]=v+1;indices[t+2]=v+2;indices[t+3]=v;indices[t+4]=v+2;indices[t+5]=v+3;
                }
            }
            mesh=new Mesh{name=name+" recycled quads"};mesh.MarkDynamic();
            mesh.vertices=vertices;mesh.uv=uv;mesh.uv2=seeds;
            gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            Renderer=gameObject.AddComponent<MeshRenderer>();visual=new SpaceLayerVisualBinding();UpdateAppearance();
            Renderer.sortingOrder=-95+index*5;
            Renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;Renderer.receiveShadows=false;
            if(settings.Kind==SpaceLayerKind.Nebula)
            {
                var details=new GameObject("Nebula cluster details");details.transform.SetParent(transform,false);
                nebulaDetails=details.AddComponent<SpaceNebulaClusterVisual>();
                nebulaDetails.Initialize(this,nebulaSettings);
            }
        }
        private float Next()=>(float)random.NextDouble();
        public void SetSettings(SpaceLayerSettings value){Settings=value;}
        public void SetNebulaSettings(SpaceNebulaSettings value){nebulaSettings=value;nebulaDetails?.SetSettings(value);}
        public Vector2 GetItemPosition(int index)=>items[index].Position;
        public float GetItemSpin(int index)=>items[index].Spin;
        public Vector2 GetRenderPosition(int index,Vector2 center,Vector2 cameraOffset)
        {
            var p=items[index];var s=Settings;
            var position=p.Position+cameraOffset*(1-s.Depth*s.ParallaxMultiplier);
            if(!s.RadialMotion)position+=LargeParallaxOffset;
            position+=new Vector2(Mathf.Sin(MotionDistance*.4f+p.Seed*50),Mathf.Cos(MotionDistance*.3f+p.Seed*36))*s.NoiseAmount*s.Depth;
            return position;
        }
        public void ReloadArtwork(){visual.Invalidate();UpdateAppearance();}
        public void SetSteering(Vector2 neutral,Vector2 course,float strength,bool enabled,float dt)
        {
            if(!effectiveInitialized){CurrentEffectiveVanishingPoint=neutral;effectiveInitialized=true;}
            steeringActive=enabled;
            TargetEffectiveVanishingPoint=neutral+(course-neutral)*Settings.SteeringInfluence*strength;
            if(!enabled)CurrentEffectiveVanishingPoint=TargetEffectiveVanishingPoint=neutral;
            else CurrentEffectiveVanishingPoint=Vector2.Lerp(CurrentEffectiveVanishingPoint,TargetEffectiveVanishingPoint,1-Mathf.Exp(-Mathf.Max(0,Settings.SteeringResponseSpeed)*dt));
            LargeParallaxOffset=-(CurrentEffectiveVanishingPoint-neutral)*Settings.ParallaxMultiplier;
        }
        private void UpdateAppearance()
        {
            if(!visual.Refresh(Settings))return;
            Renderer.sharedMaterial=visual.Instance;
            for(var i=0;i<items.Length;i++)for(var part=0;part<segments;part++)
            {
                var v=(i*segments+part)*4;var min=visual.IsImage?visual.UvMin:Vector2.zero;var max=visual.IsImage?visual.UvMax:Vector2.one;
                var y0=Mathf.Lerp(min.y,max.y,part/(float)segments);var y1=Mathf.Lerp(min.y,max.y,(part+1)/(float)segments);
                uv[v]=new Vector2(min.x,y0);uv[v+1]=new Vector2(max.x,y0);uv[v+2]=new Vector2(max.x,y1);uv[v+3]=new Vector2(min.x,y1);
            }
            mesh.uv=uv;
        }
        public void Tick(float dt,float speed,Vector2 center,Vector2 cameraOffset,bool visible)
        {
            var s=Settings;
            UpdateAppearance();
            ActiveCount=visible&&s.Enabled&&s.Kind!=SpaceLayerKind.ExternalMusicRings?Mathf.Clamp(Mathf.RoundToInt(s.BaseCount*s.Density),0,items.Length):0;
            // Large transparent cards have a deliberate fill-rate budget, even at 3x density.
            if(s.Kind==SpaceLayerKind.Nebula)
            {
                var clusterCount=nebulaSettings==null?s.BaseCount:Mathf.RoundToInt(nebulaSettings.ClusterCount*s.Density);
                ActiveCount=visible&&s.Enabled?Mathf.Clamp(clusterCount,0,Mathf.Min(items.Length,4)):0;
            }
            if(s.Kind==SpaceLayerKind.Planet)ActiveCount=Mathf.Min(ActiveCount,1);
            if(s.Kind==SpaceLayerKind.Galaxy&&s.FrameComposition)ActiveCount=Mathf.Min(ActiveCount,2);
            Renderer.enabled=ActiveCount>0;if(ActiveCount==0){nebulaDetails?.Tick(dt,speed,center,cameraOffset,false);return;}
            float layerMovement=dt*speed*Mathf.Max(0,s.SpeedMultiplier);MotionDistance+=layerMovement;
            visual.UpdateArt(MotionDistance,s.Softness);
            if(s.Kind==SpaceLayerKind.Nebula)visual.UpdateNebula(nebulaSettings);
            var framed=s.FrameComposition&&artworkCamera!=null&&!s.RadialMotion&&(s.Kind==SpaceLayerKind.Nebula||s.Kind==SpaceLayerKind.Galaxy);
            var halfHeight=artworkCamera!=null?artworkCamera.orthographicSize:3.7f;
            var halfWidth=halfHeight*(artworkCamera!=null?artworkCamera.aspect:1.7778f);
            var frameScale=framed?Mathf.Min(halfWidth,halfHeight)/3.7f:1;
            for(var i=0;i<ActiveCount;i++)
            {
                var p=items[i];
                // Stable classes in one mesh: rare near silhouettes, medium rocks, distant fragments.
                var rock=s.Kind==SpaceLayerKind.Asteroids&&s.DepthVariety;
                var dust=s.Kind==SpaceLayerKind.Dust&&s.DepthVariety;
                var near=rock?i%12==0:dust&&i%11==0;
                var medium=rock?i%3==1:dust&&i%3==1;
                var movement=layerMovement*(rock?(near?.9f:medium?.6f:.28f):dust?(near?1.2f:medium?1:.7f):1);
                var oldPosition=center+new Vector2(Mathf.Cos(p.Angle),Mathf.Sin(p.Angle))*p.Radius;
                var enterSteering=steeringActive&&s.RadialMotion&&(p.Steered||(CurrentEffectiveVanishingPoint-center).sqrMagnitude>.00000001f);
                var justEntered=enterSteering&&!p.Steered;
                if(justEntered){p.Position=oldPosition;p.Direction=new Vector2(Mathf.Cos(p.Angle),Mathf.Sin(p.Angle));p.Steered=true;}
                if(!steeringActive)p.Steered=false;
                bool recycled=false;
                var limit=Mathf.Max(s.SpawnRadius+.1f,s.DespawnRadius);
                if(s.RadialMotion)
                {
                    p.Radius+=movement*(.35f+p.Radius*.35f);
                    if(p.Radius>limit){p.Radius=s.SpawnRadius+Mathf.Repeat(p.Radius-limit,limit-s.SpawnRadius);p.Angle=Next()*Mathf.PI*2;recycled=true;Recycles++;}
                }
                else p.Angle+=movement*.035f;
                p.Angle+=movement*s.TangentialMotion;
                p.Spin+=movement*s.RotationSpeed*(rock?Mathf.Lerp(-1.3f,1.3f,p.Seed):1);
                var direction=new Vector2(Mathf.Cos(p.Angle),Mathf.Sin(p.Angle));
                if(p.Steered)
                {
                    if(recycled){p.Direction=direction;p.Position=CurrentEffectiveVanishingPoint+direction*p.Radius;}
                    else if(s.Kind==SpaceLayerKind.Stars)
                    {
                        // Projected stars expand from the vanishing point. Steering changes
                        // their velocity field, not a heading with a turning circle.
                        var delta=p.Position-CurrentEffectiveVanishingPoint;
                        var flow=delta*(movement*.35f*(1+1/Mathf.Max(.35f,p.Radius)));
                        flow+=new Vector2(-delta.y,delta.x)*(movement*s.TangentialMotion);
                        p.Position+=flow;
                        if(flow.sqrMagnitude>.0000000001f)p.Direction=flow.normalized;
                    }
                    else
                    {
                        var desired=(p.Position-CurrentEffectiveVanishingPoint).normalized;
                        if(desired.sqrMagnitude<.001f)desired=p.Direction;
                        var angleNow=Mathf.Atan2(p.Direction.y,p.Direction.x)*Mathf.Rad2Deg;
                        var angleTarget=Mathf.Atan2(desired.y,desired.x)*Mathf.Rad2Deg;
                        var angleNew=Mathf.MoveTowardsAngle(angleNow,angleTarget,dt*(20+Mathf.Max(0,s.SteeringResponseSpeed)*12))*Mathf.Deg2Rad+movement*s.TangentialMotion;
                        p.Direction=new Vector2(Mathf.Cos(angleNew),Mathf.Sin(angleNew));
                        p.Position+=p.Direction*movement*(.35f+p.Radius*.35f);
                    }
                    direction=p.Direction;
                }
                else p.Position=center+direction*p.Radius;
                if(framed)
                {
                    Vector2 anchor;
                    if(s.Kind==SpaceLayerKind.Nebula)anchor=i%4==0?new Vector2(-1.07f,-.08f):i%4==1?new Vector2(1.04f,-.06f):i%4==2?new Vector2(.07f,-1.04f):new Vector2(.12f,1.05f);
                    else anchor=i%2==0?new Vector2(-.62f,.52f):new Vector2(.64f,.40f);
                    p.Position=center+new Vector2(anchor.x*halfWidth,anchor.y*halfHeight);
                    p.Position+=new Vector2(Mathf.Sin(MotionDistance*.025f+p.Seed*8),Mathf.Cos(MotionDistance*.02f+p.Seed*9))*.06f*frameScale;
                }
                items[i]=p;
                // Far layers follow the camera; near layers lag, producing stronger screen parallax.
                var position=p.Position+cameraOffset*(1-s.Depth*s.ParallaxMultiplier);
                if(!s.RadialMotion)position+=LargeParallaxOffset;
                position+=new Vector2(Mathf.Sin(MotionDistance*.4f+p.Seed*50),Mathf.Cos(MotionDistance*.3f+p.Seed*36))*s.NoiseAmount*s.Depth;
                if(s.Kind==SpaceLayerKind.DeepSpace)position=center+cameraOffset*(1-s.Depth*s.ParallaxMultiplier)+LargeParallaxOffset;
                var radial=Mathf.InverseLerp(s.SpawnRadius,limit,p.Radius);
                var fade=s.RadialMotion?Mathf.SmoothStep(0,1,radial*8)*Mathf.SmoothStep(0,1,(1-radial)*7):1;
                var scale=Mathf.Lerp(s.MinScale,Mathf.Max(s.MinScale,s.MaxScale),p.Seed);
                if(s.RadialMotion)scale*=Mathf.Lerp(.55f,1.7f,radial*s.Depth);
                if(rock)scale*=near?2.7f:medium?1:.3f;
                if(dust)scale*=near?14:medium?3.3f:1;
                var size=new Vector2(scale,scale);
                if(s.Kind==SpaceLayerKind.Nebula)size*=s.FrameComposition?(i%4<2?1:.8f):(i%3==0?1.15f:i%3==1?.85f:.7f);
                size*=framed&&s.Kind==SpaceLayerKind.Galaxy?halfWidth/6.5f:frameScale;
                if(framed&&s.Kind==SpaceLayerKind.Galaxy)size*=i%2==0?1:.6f;
                if(s.Kind==SpaceLayerKind.Nebula&&nebulaSettings!=null)size*=nebulaSettings.ClusterSize/7.2f;
                if(rock)size.x*=Mathf.Lerp(.64f,1.25f,Mathf.Repeat(p.Seed*7,1));
                var angle=p.Spin*Mathf.Deg2Rad;
                if(visual.IsImage&&s.PreserveImageAspect)size.x*=visual.Aspect;
                if(s.Kind==SpaceLayerKind.Streaks){size.y*=Mathf.Lerp(1,s.Stretch,Mathf.Clamp01(radial*Mathf.Min(speed,5)*.7f));angle=Mathf.Atan2(direction.y,direction.x)-Mathf.PI*.5f;}
                if(s.Kind==SpaceLayerKind.Galaxy&&!visual.IsImage)size.y*=.55f;
                var x=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*size.x*.5f;
                var y=new Vector2(-Mathf.Sin(angle),Mathf.Cos(angle))*size.y*.5f;
                var tint=Color.Lerp(s.PrimaryColor,s.SecondaryColor,Mathf.Repeat(p.Seed*3.17f,1)*s.ColorVariation);tint.r*=s.Brightness;tint.g*=s.Brightness;tint.b*=s.Brightness;
                tint.a=Mathf.Lerp(s.MinAlpha,s.MaxAlpha,p.Seed)*fade;
                if(s.Kind==SpaceLayerKind.Nebula&&nebulaSettings!=null)
                {
                    tint.r*=nebulaSettings.Brightness;tint.g*=nebulaSettings.Brightness;tint.b*=nebulaSettings.Brightness;tint.a*=nebulaSettings.Opacity;
                }
                if(rock||dust)
                {
                    if(near)tint.a*=Mathf.SmoothStep(0,1,(Vector2.Distance(position,center)-s.CenterClearance)/2.2f);
                    if(rock){var light=near?1.12f:medium?.85f:.5f;tint.r*=light;tint.g*=light;tint.b*=light;}
                    if(dust)tint.a*=near?.24f:medium?.6f:1;
                }
                var h=i*5;
                if(segments>1)
                {
                    if(!p.Steered||justEntered||recycled)for(var k=0;k<5;k++)history[h+k]=position+y-direction*(size.y*k/4);
                    else
                    {
                        history[h]=position+y;
                        var response=1-Mathf.Exp(-dt*Mathf.Max(1,speed*s.SpeedMultiplier*(.35f+p.Radius*.35f)/Mathf.Max(.005f,size.y/4)));
                        for(var k=1;k<5;k++)history[h+k]=Vector2.Lerp(history[h+k],history[h+k-1]-direction*(size.y/4),response);
                    }
                }
                for(var part=0;part<segments;part++)
                {
                    var v=(i*segments+part)*4;
                    var bottom=segments==1?position-y:history[h+4-part];var top=segments==1?position+y:history[h+3-part];
                    var width=x;
                    if(p.Steered&&segments>1){var tangent=(top-bottom).normalized;width=new Vector2(tangent.y,-tangent.x)*size.x*.5f;}
                    vertices[v]=bottom-width;vertices[v+1]=bottom+width;vertices[v+2]=top+width;vertices[v+3]=top-width;
                    for(var j=0;j<4;j++)colors[v+j]=tint;
                }
            }
            mesh.vertices=vertices;mesh.colors=colors;
            if(lastCount!=ActiveCount){mesh.SetIndices(indices,0,ActiveCount*6*segments,MeshTopology.Triangles,0,false);lastCount=ActiveCount;}
            mesh.bounds=new Bounds(center,new Vector3(150,150,2));
            nebulaDetails?.Tick(dt,speed,center,cameraOffset,visible&&s.Enabled&&!visual.UsesDetailedArtwork);
        }
        private void OnDestroy(){if(mesh!=null)Destroy(mesh);visual?.Dispose();}
    }
}
