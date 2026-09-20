using UnityEngine;
using UnityEngine.UI;

namespace OrbitalRift.UI
{
    /// <summary>Vector star chart; no raster dependencies. All positions come from the real route.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CosmosRouteGraphic : MaskableGraphic
    {
        public LivingCosmosRunState Run;
        public int Focus;
        public bool DrawAtmosphere = true;
        private float nextRefresh;
        protected override void OnEnable() { base.OnEnable(); raycastTarget=false; }
        private void Update()
        {
            if(Time.unscaledTime<nextRefresh) return;
            nextRefresh=Time.unscaledTime+.05f;
            SetVerticesDirty();
        }
        private Vector2 Point(int id)
        {
            var p=LivingCosmosRunState.MapPosition(Run.Layout,id);var r=rectTransform.rect;
            return new Vector2(r.xMin+p.x*r.width,r.yMin+p.y*r.height);
        }
        public static Vector2 Curve(Vector2 a,Vector2 b,float t)
        {
            var s=t*t*(3f-2f*t);
            return new Vector2(Mathf.Lerp(a.x,b.x,t),Mathf.Lerp(a.y,b.y,s));
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if(Run?.Layout==null)return;
            var r=rectTransform.rect;
            if(DrawAtmosphere)
            {
                for(var i=0;i<22;i++)
                {
                    var x=i/21f;
                    var center=new Vector2(r.xMin+r.width*(.14f+x*.72f),r.center.y+Mathf.Sin(i*.53f)*r.height*.12f);
                    var tint=Color.Lerp(new Color(.09f,.30f,.56f,.13f),new Color(.47f,.20f,.10f,.11f),x);
                    Disc(vh,center,new Vector2(r.width*.14f,r.height*.28f),tint,true);
                }
                // Distant worlds: a shaded sphere and fine orbital rings, behind the route.
                World(vh,new Vector2(r.xMin+r.width*.31f,r.yMin+r.height*.82f),Mathf.Min(42,r.height*.10f),new Color(.12f,.30f,.40f,.45f));
                World(vh,new Vector2(r.xMin+r.width*.70f,r.yMin+r.height*.18f),Mathf.Min(64,r.height*.10f),new Color(.32f,.18f,.13f,.44f));
            }
            for(var i=0;i<180;i++)
            {
                var x=Mathf.Repeat(i*.6180339f+.13f,1f);var y=Mathf.Repeat(i*i*.0371f+.29f,1f);
                var p=new Vector2(r.xMin+x*r.width,r.yMin+y*r.height);
                Disc(vh,p,Vector2.one*(i%13==0?1.7f:.7f),new Color(.55f,.73f,.92f,i%7==0?.48f:.19f),false,8);
            }
            for(var i=0;i<Run.Layout.Rooms.Count;i++)
            foreach(var next in Run.Layout.Rooms[i].Connections)
            {
                var done=Run.Cleared.Contains(i)&&Run.Visited.Contains(next);
                var available=i==Run.RoomIndex && !Run.IsTerminal;
                var c=done?new Color(.94f,.77f,.43f,.8f):available?new Color(.40f,.78f,.96f,.64f):new Color(.22f,.33f,.43f,.40f);
                var a=Point(i);var b=Point(next);
                for(var s=0;s<40;s++)
                {
                    if(!done && s%5==4)continue;
                    Segment(vh,Curve(a,b,s/40f),Curve(a,b,(s+1)/40f),done?2.1f:1.1f,c);
                }
            }
            for(var i=0;i<Run.Layout.Rooms.Count;i++)
            {
                var p=Point(i);var type=Run.Layout.Rooms[i].Type;
                var active=i==Run.RoomIndex;var available=Run.IsAvailable(i);
                var c=Run.Cleared.Contains(i)?new Color(.96f,.77f,.43f):available||active?new Color(.48f,.84f,1f):new Color(.27f,.39f,.49f);
                if(type==SectorRoomType.Elite && (available||active))c=new Color(1f,.49f,.27f);
                if(i==Focus) Ring(vh,p,31,1.1f,new Color(c.r,c.g,c.b,.65f));
                Disc(vh,p,Vector2.one*38,new Color(c.r,c.g,c.b,.17f),true);
                Disc(vh,p,Vector2.one*15,new Color(.015f,.028f,.043f,1),false);
                Ring(vh,p,17,1.5f,c);
                if(type==SectorRoomType.Shop)
                {
                    Segment(vh,p+Vector2.left*8,p+Vector2.right*8,3,c);
                    Segment(vh,p+Vector2.up*8,p+Vector2.down*8,3,c);
                    Ring(vh,p,23,.8f,new Color(c.r,c.g,c.b,.4f));
                }
                else if(type==SectorRoomType.Boss)
                {
                    for(var k=0;k<3;k++)Segment(vh,p+new Vector2(-8+k*8,-6),p+new Vector2(-8+k*8,8),2,c);
                }
                else Disc(vh,p,Vector2.one*(type==SectorRoomType.Elite?7:4),c,false, type==SectorRoomType.Elite?4:16);
                if(active)
                {
                    var pulse=.65f+Mathf.Sin(Time.unscaledTime*2f)*.15f;
                    Ring(vh,p,25,1.4f,new Color(.87f,.95f,1,pulse));
                }
            }
            var ship=Point(Run.RoomIndex);
            if(Run.Phase==LivingEncounterPhase.Departing && Run.PendingNodeId>=0)
                ship=Curve(ship,Point(Run.PendingNodeId),Mathf.SmoothStep(0,1,Run.Transition));
            ship+=Vector2.up*34;
            Disc(vh,ship,Vector2.one*17,new Color(.5f,.9f,1,.28f),true);
            Triangle(vh,ship+new Vector2(9,0),ship+new Vector2(-6,5),ship+new Vector2(-6,-5),Color.white);
        }
        private static void World(VertexHelper vh,Vector2 p,float radius,Color c)
        {
            Disc(vh,p,Vector2.one*(radius*1.3f),new Color(c.r,c.g,c.b,.07f),true);
            Disc(vh,p,Vector2.one*radius,c,false);
            Disc(vh,p+new Vector2(radius*.25f,-radius*.05f),Vector2.one*(radius*.94f),new Color(.012f,.022f,.035f,.92f),false);
            Ring(vh,p,radius*1.45f,.7f,new Color(c.r,c.g,c.b,.25f));
        }
        private static void Segment(VertexHelper vh,Vector2 a,Vector2 b,float width,Color c)
        {
            var d=b-a;var normal=new Vector2(-d.y,d.x).normalized*width*.5f;
            var i=vh.currentVertCount;vh.AddVert(a-normal,c,Vector2.zero);vh.AddVert(a+normal,c,Vector2.zero);
            vh.AddVert(b+normal,c,Vector2.zero);vh.AddVert(b-normal,c,Vector2.zero);
            vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
        }
        private static void Triangle(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Color tint)
        {var i=vh.currentVertCount;vh.AddVert(a,tint,Vector2.zero);vh.AddVert(b,tint,Vector2.zero);vh.AddVert(c,tint,Vector2.zero);vh.AddTriangle(i,i+1,i+2);}
        private static void Ring(VertexHelper vh,Vector2 p,float radius,float width,Color c)
        {for(var s=0;s<48;s++){var a=s*Mathf.PI*2/48;var b=(s+1)*Mathf.PI*2/48;Segment(vh,p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,p+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,width,c);}}
        private static void Disc(VertexHelper vh,Vector2 p,Vector2 radius,Color c,bool soft,int segments=32)
        {
            var start=vh.currentVertCount;vh.AddVert(p,c,Vector2.zero);var edge=c;if(soft)edge.a=0;
            for(var s=0;s<=segments;s++){var a=s*Mathf.PI*2/segments;vh.AddVert(p+Vector2.Scale(new Vector2(Mathf.Cos(a),Mathf.Sin(a)),radius),edge,Vector2.zero);}
            for(var s=0;s<segments;s++)vh.AddTriangle(start,start+s+1,start+s+2);
        }
    }
}
