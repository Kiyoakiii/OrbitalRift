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
    public static class SpaceDepthValidation
    {
        private const string Root="Assets/Resources/SpaceDepth/";
        private const string Evidence="Docs/VFX_Evidence/SpaceDepth/";
        private static readonly List<string> report=new List<string>();
        private static readonly List<string> errors=new List<string>();
        private static GameManager manager;
        static SpaceDepthValidation()
        {
            EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetInt("SpaceDepthQA",0)>0)EditorApplication.delayCall+=Begin;};
            EditorApplication.update+=ReadCommand;
        }
        // Local development request file allows repeatable tests in the already running Editor.
        private static void ReadCommand()
        {
            const string file="Temp/SpaceDepth.command";
            if(!File.Exists(file)||EditorApplication.isCompiling)return;
            var command=File.ReadAllText(file).Trim();File.Delete(file);
            if(command=="refresh"){AssetDatabase.Refresh();return;}
            if(command=="stop"){EditorApplication.isPlaying=false;return;}
            if(command=="flow"){CourseSteeringValidation.RunFlow();return;}
            if(command=="course"){CourseSteeringValidation.RunFull();return;}
            if(command=="artui"){CourseSteeringValidation.RunUI();return;}
            if(command=="far")RunFar();else if(command=="mid")RunMid();else if(command=="all")RunAll();
        }
        [MenuItem("Orbital Rift/Space Depth/Validate Far")]
        public static void RunFar()=>Run(1);
        [MenuItem("Orbital Rift/Space Depth/Validate Mid")]
        public static void RunMid()=>Run(2);
        [MenuItem("Orbital Rift/Space Depth/Validate All")]
        public static void RunAll()=>Run(3);
        private static void Run(int stage)
        {
            if(EditorApplication.isPlaying){Debug.LogWarning("Stop Play Mode first");return;}
            Directory.CreateDirectory(Root+"Profiles");Directory.CreateDirectory(Evidence);
            var p=AssetDatabase.LoadAssetAtPath<SpaceDepthProfile>(Root+"Profiles/SpaceDepthProfile.asset");
            if(p==null){p=ScriptableObject.CreateInstance<SpaceDepthProfile>();p.Layers=SpaceDepthProfile.Defaults();AssetDatabase.CreateAsset(p,Root+"Profiles/SpaceDepthProfile.asset");AssetDatabase.SaveAssets();}
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"Shaders/SpaceDepth.shader");
            if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("SpaceDepth shader error");
            SessionState.SetInt("SpaceDepthQA",stage);Application.runInBackground=true;EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
        }
        private static void Begin()
        {
            manager=Object.FindFirstObjectByType<GameManager>();if(manager==null){EditorApplication.delayCall+=Begin;return;}
            Application.runInBackground=true;
            var stage=SessionState.GetInt("SpaceDepthQA",3);SessionState.SetInt("SpaceDepthQA",0);
            report.Clear();errors.Clear();Application.logMessageReceived+=Log;manager.StartCoroutine(Guard(stage));
        }
        private static IEnumerator Guard(int stage)
        {
            yield return null;var checks=Checks(stage);
            while(true)
            {
                object current;try{if(!checks.MoveNext())break;current=checks.Current;}catch(Exception ex){errors.Add(ex.ToString());break;}yield return current;
            }
            Application.logMessageReceived-=Log;report.Add(errors.Count==0?"PASS":"FAIL\n"+string.Join("\n",errors));
            File.WriteAllText(Evidence+"stage-"+stage+".txt",string.Join("\n",report));Debug.Log("SPACE DEPTH QA stage "+stage+": "+report[report.Count-1]);
        }
        private static IEnumerator Checks(int stage)
        {
            typeof(GameManager).GetMethod("OpenAbilitySandbox",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(manager,null);
            var c=manager.SpaceDepth;Require(c!=null,"Existing Sandbox launches with SpaceDepthRoot");
            c.RuntimeProfile.Steering.SteeringEnabled=false;
            c.FollowGameplaySpeed=false;c.SetTravelSpeed(1.8f);
            Require(c.Layers.Length==12,"Twelve settings slots, music rings explicitly external");
            var count=stage==1?5:stage==2?8:12;
            for(var i=0;i<c.Layers.Length;i++)c.RuntimeProfile.Layers[i].Enabled=i<count&&i!=9;
            yield return new WaitForSeconds(.6f);
            Capture("stage-"+stage+"-space",c,true);
            Require(c.ActiveElements>0,"Layer geometry is populated");
            if(stage<3)yield break;
            c.SetTravelSpeed(0);var before=c.Layers[10].MotionDistance;yield return new WaitForSeconds(.2f);Require(c.Layers[10].MotionDistance==before,"Zero speed freezes optical flow");
            c.SetTravelSpeed(1.8f);c.PauseMotion=true;yield return new WaitForSeconds(.2f);Require(c.Layers[10].MotionDistance==before,"Space pause freezes motion independently");c.PauseMotion=false;
            var far=c.Layers[2].MotionDistance;var near=c.Layers[10].MotionDistance;yield return new WaitForSeconds(.3f);
            Require(c.Layers[10].MotionDistance-near>(c.Layers[2].MotionDistance-far)*5,"Near motion is distinctly faster than far motion");
            for(var i=0;i<c.Layers.Length;i++)
            {
                if(i==9)continue;c.SoloLayer=i;yield return null;
                Require(c.Layers[i].ActiveCount>0,"Solo visible: "+i);
                for(var j=0;j<c.Layers.Length;j++)if(j!=i)Require(c.Layers[j].ActiveCount==0,"Solo isolation "+i+" / "+j);
                Capture("solo-"+i.ToString("00"),c,true);
            }
            c.ShowAll(true);c.RuntimeProfile.Layers[2].Density=0;yield return null;Require(c.Layers[2].ActiveCount==0,"Density zero removes layer");c.ResetLayer(2);
            c.RuntimeProfile.Layers[2].SpeedMultiplier=0;before=c.Layers[2].MotionDistance;yield return new WaitForSeconds(.1f);Require(c.Layers[2].MotionDistance==before,"Per-layer speed zero freezes only its motion");c.ResetLayer(2);
            c.RuntimeProfile.Layers[2].Brightness=0;c.Tick(0);
            Require(c.Layers[2].GetComponent<MeshFilter>().sharedMesh.colors[0].r==0,"Brightness updates actual geometry color");c.ResetLayer(2);
            var camera=Camera.main;var cameraPosition=camera.transform.position;c.Tick(0);
            var farPosition=c.Layers[2].GetComponent<MeshFilter>().sharedMesh.vertices[0];
            var nearPosition=c.Layers[10].GetComponent<MeshFilter>().sharedMesh.vertices[0];
            camera.transform.position+=Vector3.right;c.Tick(0);
            var farShift=c.Layers[2].GetComponent<MeshFilter>().sharedMesh.vertices[0].x-farPosition.x-1;
            var nearShift=c.Layers[10].GetComponent<MeshFilter>().sharedMesh.vertices[0].x-nearPosition.x-1;
            Require(Mathf.Abs(nearShift)>Mathf.Abs(farShift)*5,"Camera parallax is stronger for near layers");camera.transform.position=cameraPosition;c.Tick(0);
            c.ShowAll(false);yield return null;Require(c.ActiveElements==0,"Hide all depth layers");c.ResetAll();c.RuntimeProfile.Steering.SteeringEnabled=false;c.FollowGameplaySpeed=false;
            for(var group=0;group<3;group++)
            {
                for(var i=0;i<c.Layers.Length;i++)c.RuntimeProfile.Layers[i].Enabled=i!=9&&(group==0?i<5:group==1?i>=5&&i<8:i>=8);
                yield return new WaitForSeconds(.4f);Capture("group-"+group,c,true);
            }
            c.ResetAll();c.RuntimeProfile.Steering.SteeringEnabled=false;c.FollowGameplaySpeed=false;
            foreach(var speed in new[]{.08f,1.8f,4f,9f}){c.SetTravelSpeed(speed);yield return new WaitForSeconds(.5f);Capture("speed-"+speed.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture),c,true);}
            c.ResetAll();c.RuntimeProfile.Steering.SteeringEnabled=false;c.FollowGameplaySpeed=false;
            Capture("gameplay",c,false);
            var savedSnapshot=Object.Instantiate(c.SavedProfile);
            try
            {
                c.RuntimeProfile.Layers[2].SteeringInfluence=.345f;c.SaveProfile();
                Require(Mathf.Abs(c.SavedProfile.Layers[2].SteeringInfluence-.345f)<.001f,"Save persists steering field");
            }
            finally
            {
                EditorUtility.CopySerialized(savedSnapshot,c.SavedProfile);c.SavedProfile.name="SpaceDepthProfile";
                EditorUtility.SetDirty(c.SavedProfile);AssetDatabase.SaveAssets();Object.Destroy(savedSnapshot);
            }
            c.ResetAll();c.RuntimeProfile.Steering.SteeringEnabled=false;
            Require(c.RuntimeProfile!=c.SavedProfile,"Runtime editing uses separate profile");
            var watch=new System.Diagnostics.Stopwatch();watch.Start();var allocated=GC.GetAllocatedBytesForCurrentThread();
            for(var n=0;n<240;n++)c.Tick(1f/60);var bytes=GC.GetAllocatedBytesForCurrentThread()-allocated;watch.Stop();
            report.Add("Controller CPU average ms: "+(watch.Elapsed.TotalMilliseconds/240).ToString("0.000")+"; managed allocations / 240 ticks: "+bytes);
            Require(bytes==0,"No steady state managed allocations in controller tick");
            for(var i=0;i<c.Layers.Length;i++)c.RuntimeProfile.Layers[i].Density=3;
            c.Tick(0);allocated=GC.GetAllocatedBytesForCurrentThread();watch.Restart();for(var n=0;n<240;n++)c.Tick(1f/60);watch.Stop();bytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
            report.Add("3x density CPU average ms: "+(watch.Elapsed.TotalMilliseconds/240).ToString("0.000")+"; allocations: "+bytes+"; elements: "+c.ActiveElements);
            Require(bytes==0,"No steady allocations at maximum panel density");c.ResetAll();c.RuntimeProfile.Steering.SteeringEnabled=false;
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(GameManager).GetMethod("ExitAbilitySandbox",flags).Invoke(manager,null);
            c.FollowGameplaySpeed=true;
            typeof(GameManager).GetMethod("StartGame",flags).Invoke(manager,null);yield return new WaitForSeconds(.5f);
            var session=(AbilitySandboxSession)typeof(GameManager).GetField("abilitySandbox",flags).GetValue(manager);
            Require(!session.IsOpen&&(bool)typeof(GameManager).GetField("playing",flags).GetValue(manager),"Production gameplay starts outside Sandbox");
            var player=(Transform)typeof(GameManager).GetField("player",flags).GetValue(manager);
            Require(Mathf.Abs(((Vector2)player.position).magnitude-OrbitSettings.Radius)<.01f,"Gameplay preserves player orbit radius and center");
            var stars=(List<StarParticle>)typeof(GameManager).GetField("stars",flags).GetValue(manager);
            foreach(var star in stars)Require(star.IsPurple||star.IsShield,"Legacy stream contains gameplay pickups only");
            Capture("actual-gameplay",c,false);
            yield return new WaitForSeconds(1.5f);Capture("actual-gameplay-later",c,false);
            typeof(GameManager).GetMethod("OpenAbilitySandbox",flags).Invoke(manager,null);
            c.FollowGameplaySpeed=false;
            for(var mode=0;mode<2;mode++)
            {
                c.ShowAll(mode==1);yield return new WaitForSeconds(.2f);
                var elapsed=0f;for(var frame=0;frame<120;frame++){yield return null;elapsed+=Time.unscaledDeltaTime;}
                report.Add((mode==0?"Depth hidden":"Depth visible")+" Editor whole-frame mean: "+(elapsed/120*1000).ToString("0.00")+" ms / "+(120/elapsed).ToString("0.0")+" FPS (VSync / Editor limited)");
            }
            c.ResetAll();c.RuntimeProfile.Steering.SteeringEnabled=false;
            Require(errors.Count==0,"No logged runtime or shader errors");
            var panel=typeof(GameManager).GetField("spaceDepthPanel",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(manager) as SpaceLayerDebugPanel;panel.Open=true;c.FollowGameplaySpeed=false;
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Evidence+"sandbox-panel.png");
        }
        private static void Capture(string name,SpaceFlightVisualController c,bool onlySpace)
        {
            var source=Camera.main;
            var camera=source;
            GameObject isolated=null;
            if(onlySpace){isolated=new GameObject("QA isolated space camera");camera=isolated.AddComponent<Camera>();camera.CopyFrom(source);camera.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;}
            var mask=camera.cullingMask;var target=camera.targetTexture;var active=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(1280,720,24);var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try
            {
                if(onlySpace){foreach(var l in c.Layers)l.gameObject.layer=30;camera.cullingMask=1<<30;}
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes(Evidence+name+".png",tex.EncodeToPNG());
            }
            finally{foreach(var l in c.Layers)l.gameObject.layer=0;camera.cullingMask=mask;camera.targetTexture=target;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(tex);if(isolated!=null)Object.Destroy(isolated);}
        }
        private static void Require(bool ok,string value){if(!ok)throw new Exception(value);report.Add("PASS: "+value);}
        private static void Log(string value,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception)errors.Add(value);}
    }
}
#endif
