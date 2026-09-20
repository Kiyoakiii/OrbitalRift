#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace OrbitalRift
{
    [InitializeOnLoad]
    public static class CourseVisualFollowup
    {
        const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
        public const string Root="Docs/VFX_Evidence/CourseFollowup/";
        static CourseVisualFollowup()
        {
            EditorApplication.update+=()=>{const string file="Temp/CourseFollowup.command";if(!File.Exists(file)||EditorApplication.isCompiling)return;var mode=File.ReadAllText(file).Trim();File.Delete(file);if(EditorApplication.isPlaying){Debug.LogWarning("Stop Play Mode before followup");return;}SessionState.SetString("CourseFollowup",mode);EditorApplication.isPaused=false;Application.runInBackground=true;EditorApplication.isPlaying=true;};
            EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetString("CourseFollowup","")!="")EditorApplication.delayCall+=Begin;};
        }
        static readonly List<string> report=new List<string>(),errors=new List<string>();
        static void Begin(){var m=Object.FindFirstObjectByType<GameManager>();if(m==null){EditorApplication.delayCall+=Begin;return;}Application.runInBackground=true;var mode=SessionState.GetString("CourseFollowup","");SessionState.SetString("CourseFollowup","");Directory.CreateDirectory(Root);m.StartCoroutine(Guard(m,mode));}
        static IEnumerator Guard(GameManager m,string mode)
        {
            report.Clear();errors.Clear();Application.logMessageReceived+=Log;
            var iterator=Check(m,mode);
            while(true){object next;try{if(!iterator.MoveNext())break;next=iterator.Current;}catch(Exception ex){errors.Add(ex.ToString());break;}yield return next;}
            Application.logMessageReceived-=Log;File.WriteAllText(Root+mode+".txt",string.Join("\n",report)+"\n"+(errors.Count==0?"PASS":string.Join("\n",errors)+"\nFAIL"));
            Debug.Log("COURSE FOLLOWUP "+mode+": "+(errors.Count==0?"PASS":"FAIL"));
        }
        static void Log(string value,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception)errors.Add(value);}
        static void Require(bool ok,string text){if(!ok)throw new Exception(text);report.Add("PASS: "+text);}
        static object Get(object o,string key)=>o.GetType().GetField(key,F).GetValue(o);
        static IEnumerator Check(GameManager m,string mode)
        {
            yield return null;typeof(GameManager).GetMethod("OpenAbilitySandbox",F).Invoke(m,null);yield return new WaitForSeconds(.5f);
            var c=m.SpaceDepth;var music=(MusicReactiveVisualDirector)Get(m,"musicReactiveVisuals");var distortion=Camera.main.GetComponent<MusicSpaceDistortion>();
            if(mode!="audit"){var iterator=Verify(m,c,music,distortion);while(iterator.MoveNext())yield return iterator.Current;yield break;}
            m.enabled=false;music.enabled=false;
            var log=new List<string>();var renderers=Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);var big=new List<Renderer>();
            foreach(var r in renderers)
            {
                if(!r.enabled||!r.gameObject.activeInHierarchy||r.bounds.size.magnitude<4)continue;
                var sprite=r as SpriteRenderer;var mat=r.sharedMaterial;
                log.Add(r.name+" | bounds="+r.bounds+" | material="+(mat?mat.name:"null")+" | shader="+(mat?mat.shader.name:"null")+" | sprite="+(sprite&&sprite.sprite?AssetDatabase.GetAssetPath(sprite.sprite):"")+" | tint="+(sprite?sprite.color.ToString():""));
                if(sprite)big.Add(r);
            }
            Capture("before-full",c);var musicRoot=(Transform)Get(music,"root");musicRoot.gameObject.SetActive(false);Capture("without-music-geometry",c);musicRoot.gameObject.SetActive(true);
            distortion.enabled=false;Capture("without-post",c);distortion.enabled=true;
            foreach(var r in big)r.enabled=false;Capture("without-large-sprites",c);foreach(var r in big)r.enabled=true;
            File.WriteAllLines(Root+"renderers.txt",log);
            m.enabled=true;music.enabled=true;File.WriteAllText(Root+"status.txt","Audit complete");
        }
        static IEnumerator Verify(GameManager m,SpaceFlightVisualController c,MusicReactiveVisualDirector music,MusicSpaceDistortion distortion)
        {
            var fx=(SandboxLayeredVfx)Get(m,"sandboxLayeredVfx");var plate=(SpriteRenderer)Get(fx,"workshopBackdrop");
            Require(!plate.enabled,"Opaque Workshop plate hidden in normal Sandbox");
            Require(plate.sprite.texture==Texture2D.whiteTexture,"Workshop plate uses solid texture, never the ship sprite");
            var previewOrigin=Vector2.zero;var ship=(Transform)Get(m,"player");
            fx.SetEditorPreview(AbilitySandboxAbilityId.SolarChicks,previewOrigin,Vector2.up,Color.white,(Sprite)Get(m,"shipSprite"),SandboxVfxLayerEditor.AllLayers,1,1,1);
            fx.Tick(0,ship.position,previewOrigin,OrbitSettings.Radius,false,false,false,false,false,false);
            Require(plate.enabled,"Solid Workshop plate still available inside isolated preview");fx.ClearEditorPreview();
            Require(!plate.enabled,"Closing Workshop hides its plate immediately");
            Require(c.SavedProfile.Steering.SteeringEnabled,"Saved steering default remains enabled");
            c.FollowGameplaySpeed=false;c.SetTravelSpeed(1.8f);c.RuntimeProfile.Steering.SteeringEnabled=true;
            c.Course.ResetInstant();yield return new WaitForSeconds(.25f);Capture("after-center",c);
            var root=(Transform)Get(music,"root");var centers=new[]{Vector2.right,Vector2.left,Vector2.up,Vector2.down,new Vector2(1,1),Vector2.zero};
            foreach(var center in centers)
            {
                c.Course.Preset(center);yield return new WaitForSeconds(.8f);
                Require(Vector2.Distance(root.position,c.CurrentVanishingPoint)<.001f,"Whole music geometry follows course "+center);
                Capture("music-"+center.x+"-"+center.y,c);
                var mat=(Material)Get(distortion,"material");var actual=mat.GetVector("_MusicCenter");var expected=distortion.CourseViewportCenter;
                Require(Vector2.Distance(new Vector2(actual.x,actual.y),expected)<.01f,"Post-effect center agrees with music geometry");
            }
            // Refractive impacts and visible rain rings must translate together.
            var origins=(Vector2[])Get(music,"pulseOrigins");foreach(var origin in origins)Require(origin.magnitude<.281f,"Music rain origin remains local to course center");
            var line=((LineRenderer[])Get(music,"rings"))[0];var localPoint=line.GetPosition(0);var oldPoint=line.transform.TransformPoint(localPoint);
            var offset=new Vector2(.3f,.2f);music.SetCourseCenter(c.CurrentVanishingPoint+offset);
            Require(Vector2.Distance((Vector2)line.transform.TransformPoint(localPoint)-(Vector2)oldPoint,offset)<.001f,"Existing music rings translate without shape changes");music.SetCourseCenter(c.CurrentVanishingPoint);
            Capture("music-wave-alignment",c);var impacts=(Vector4[])Get(distortion,"impacts");var impactOffsets=(Vector2[])Get(distortion,"impactCenterOffsets");
            for(var i=0;i<impacts.Length;i++)if(impacts[i].z>=0&&impacts[i].z<1)
            {
                var expected=c.ViewCamera.WorldToViewportPoint(c.CurrentVanishingPoint+impactOffsets[i]);
                Require(Vector2.Distance(new Vector2(impacts[i].x,impacts[i].y),expected)<.01f,"Existing refractive wave follows the same center as visible rings");
            }
            c.SetTravelSpeed(0);var far=c.Layers[2].FirstPosition;var mid=c.Layers[5].FirstPosition;c.Course.Preset(Vector2.left);
            yield return new WaitForSeconds(.3f);Require(far==c.Layers[2].FirstPosition&&mid==c.Layers[5].FirstPosition,"Stationary stars do not teleport when course moves");
            CheckStarFlow();
            c.SetTravelSpeed(1.8f);Directory.CreateDirectory(Root+"stars");Directory.CreateDirectory(Root+"music");
            c.Course.ResetInstant();for(var i=0;i<c.Layers.Length;i++)c.RuntimeProfile.Layers[i].Enabled=i==2||i==5;
            for(var frame=0;frame<64;frame++)
            {if(frame==12)c.Course.Preset(Vector2.right);if(frame==40)c.Course.Preset(Vector2.left);CourseSteeringValidation.Capture("../CourseFollowup/stars/frame-"+frame.ToString("D3"),c);yield return new WaitForSeconds(.0625f);}
            c.ResetAll();c.FollowGameplaySpeed=false;c.Course.ResetInstant();
            for(var frame=0;frame<48;frame++)
            {if(frame==8)c.Course.Preset(Vector2.right);if(frame==30)c.Course.Preset(new Vector2(-1,.5f));Capture("music/frame-"+frame.ToString("D3"),c);yield return new WaitForSeconds(.0625f);}
            c.Course.Center();yield return new WaitForSeconds(.5f);
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Root+"after-game-view.png");
            Require(!plate.enabled,"Workshop plate remains hidden after movement and captures");
            Require(errors.Count==0,"No runtime/shader errors");
        }
        static void CheckStarFlow()
        {
            var obj=new GameObject("Star flow QA");var s=new SpaceLayerSettings{Kind=SpaceLayerKind.Stars,Capacity=1,BaseCount=1,SteeringInfluence=1,SteeringResponseSpeed=100,NoiseAmount=0,SpeedMultiplier=1};
            var layer=obj.AddComponent<SpaceDepthLayer>();layer.Initialize(s,2);
            try
            {
                var items=(Array)Get(layer,"items");var point=items.GetValue(0);var type=point.GetType();
                type.GetField("Position").SetValue(point,new Vector2(1,0));type.GetField("Direction").SetValue(point,Vector2.right);type.GetField("Radius").SetValue(point,1f);type.GetField("Steered").SetValue(point,true);items.SetValue(point,0);
                layer.SetSteering(Vector2.zero,new Vector2(2,0),1,true,1);layer.Tick(.1f,1,Vector2.zero,Vector2.zero,true);
                Require(layer.FirstPosition.x<1&&Mathf.Abs(layer.FirstPosition.y)<.00001f,"Star crossing behind VP changes flow direction without a turning arc");
                type.GetField("Position").SetValue(point,new Vector2(2,0));items.SetValue(point,0);layer.Tick(.1f,1,Vector2.zero,Vector2.zero,true);
                Require(Vector2.Distance(layer.FirstPosition,new Vector2(2,0))<.0001f,"Star at VP has zero expansion velocity, no circling");
            }
            finally{Object.Destroy(obj);}
        }
        public static void Capture(string name,SpaceFlightVisualController c)
        {CourseSteeringValidation.Capture("../CourseFollowup/"+name,c,false);}
    }
}
#endif
