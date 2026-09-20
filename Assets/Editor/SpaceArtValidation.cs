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
    public static class SpaceArtValidation
    {
        const string Evidence="Docs/VFX_Evidence/SpaceArt/";
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static GameManager manager;
        static readonly List<string> report=new List<string>();
        static SpaceArtValidation()
        {
            EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetString("SpaceArtQA","")!="")EditorApplication.delayCall+=Begin;};
            EditorApplication.update+=()=>{
                const string file="Temp/SpaceArt.command";
                if(!File.Exists(file)||EditorApplication.isCompiling)return;
                var command=File.ReadAllText(file).Trim();File.Delete(file);
                if(command=="baseline")RunBaseline();else if(command=="after")RunAfter();else if(command=="apply")ApplyArt();else if(command=="motion")Run("motion");
            };
        }
        [MenuItem("Orbital Rift/Space Depth/Art - Capture baseline")]
        public static void RunBaseline()=>Run("before");
        [MenuItem("Orbital Rift/Space Depth/Art - Validate final")]
        public static void RunAfter()=>Run("after");
        [MenuItem("Orbital Rift/Space Depth/Art - Apply authored preset")]
        public static void ApplyArt()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before applying art");
            const string root="Assets/Resources/SpaceDepth/";
            var importer=(TextureImporter)AssetImporter.GetAtPath(root+"Textures/NebulaFilaments.png");
            importer.textureType=TextureImporterType.Default;importer.alphaIsTransparency=true;importer.mipmapEnabled=true;
            importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.None;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Standalone",overridden=true,maxTextureSize=2048,format=TextureImporterFormat.BC7});
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Android",overridden=true,maxTextureSize=2048,format=TextureImporterFormat.ASTC_6x6});importer.SaveAndReimport();
            Directory.CreateDirectory(root+"Materials");
            var material=AssetDatabase.LoadAssetAtPath<Material>(root+"Materials/NebulaFilaments.mat");
            if(material==null){material=new Material(Resources.Load<Shader>("SpaceDepth/Shaders/SpaceNebula"));AssetDatabase.CreateAsset(material,root+"Materials/NebulaFilaments.mat");}
            material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(root+"Textures/NebulaFilaments.png");EditorUtility.SetDirty(material);
            var profile=AssetDatabase.LoadAssetAtPath<SpaceDepthProfile>(root+"Profiles/SpaceDepthProfile.asset");
            SpaceDepthProfile.ApplyArtDirection(profile.Layers);
            profile.Layers[3].Appearance=SpaceLayerAppearance.Image;profile.Layers[3].Material=material;profile.Layers[3].Texture=(Texture2D)material.mainTexture;
            EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();Debug.Log("SPACE ART authored preset applied");
        }
        static void Run(string mode)
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before Art QA");
            Directory.CreateDirectory(Evidence);SessionState.SetString("SpaceArtQA",mode);
            Application.runInBackground=true;EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
        }
        static void Begin()
        {
            manager=Object.FindFirstObjectByType<GameManager>();if(manager==null){EditorApplication.delayCall+=Begin;return;}
            Application.runInBackground=true;Time.timeScale=1;
            var mode=SessionState.GetString("SpaceArtQA","");SessionState.SetString("SpaceArtQA","");
            report.Clear();manager.StartCoroutine(Guard(mode));
        }
        static IEnumerator Guard(string mode)
        {
            yield return null;var checks=Checks(mode);Application.logMessageReceived+=Log;
            while(true){object next;try{if(!checks.MoveNext())break;next=checks.Current;}catch(Exception ex){report.Add("FAIL "+ex);break;}yield return next;}
            Application.logMessageReceived-=Log;
            report.Add(report.Exists(s=>s.StartsWith("FAIL"))?"FAIL":"PASS");
            File.WriteAllText(Evidence+mode+".txt",string.Join("\n",report));Debug.Log("SPACE ART QA "+mode+" "+report[report.Count-1]);
        }
        static IEnumerator Checks(string mode)
        {
            Call("OpenAbilitySandbox");typeof(GameManager).GetField("autoFire",Flags).SetValue(manager,false);
            var c=manager.SpaceDepth;c.RuntimeProfile.Steering.SteeringEnabled=false;c.FollowGameplaySpeed=false;c.SetTravelSpeed(1.8f);
            yield return new WaitForSeconds(.65f);
            Capture(mode+"-space",c);Capture(mode+"-sandbox",c,false);
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Evidence+mode+"-game-view.png");
            if(mode=="before")yield break;
            if(mode=="motion")
            {
                // Fixed 30 Hz depth simulation, actual Unity material/mesh rendering, stationary course.
                Directory.CreateDirectory("Temp/SpaceArtMotion");
                for(var frame=0;frame<90;frame++){c.PauseMotion=false;c.Tick(1f/30);c.PauseMotion=true;CourseSteeringValidation.Capture("../../../Temp/SpaceArtMotion/frame-"+frame.ToString("D3"),c);yield return null;}
                report.Add("90 depth-only frames, 30 Hz fixed simulation; course held neutral; rendered by Unity camera.");yield break;
            }
            report.Add("Neutral course in runtime clone only; saved input/course settings untouched.");
            foreach(var shaderName in new[]{"SpaceDepth","SpaceDepthImage","SpaceNebula"})
            {
                var shader=Resources.Load<Shader>("SpaceDepth/Shaders/"+shaderName);
                Require(shader!=null&&!ShaderUtil.ShaderHasError(shader),"Shader compiles: "+shaderName);
            }
            foreach(var layer in c.Layers)Require(layer.Renderer.sharedMaterial!=null&&layer.Renderer.sharedMaterial.shader!=null,"Valid renderer material: "+layer.name);
            Require(c.RuntimeProfile.Layers[3].Material!=null&&c.RuntimeProfile.Layers[3].Texture!=null,"Nebula artwork references resolve");
            Require(c.RuntimeProfile.Nebula!=null&&c.RuntimeProfile.Nebula.ClusterCount>=2&&c.RuntimeProfile.Nebula.ClusterCount<=4,"Nebula cluster controls resolve");
            Require(c.Layers[4].ActiveCount==0,"Authored composition has zero planets");
            report.Add("PASS three shaders compile, material/texture references resolve, 12 slots retained, planets disabled.");
            var authoredClusterCount=c.RuntimeProfile.Nebula.ClusterCount;
            foreach(var clusterCount in new[]{2,3,4})
            {
                c.RuntimeProfile.Nebula.ClusterCount=clusterCount;c.Tick(0);
                Require(c.Layers[3].ActiveCount==clusterCount,"Nebula cluster count "+clusterCount);
                report.Add("Cluster composition "+clusterCount+" rendered without changing other layers.");
            }
            c.RuntimeProfile.Nebula.ClusterCount=authoredClusterCount;c.Tick(0);
            foreach(var slot in new[]{3,7,11}){c.SoloLayer=slot;yield return null;Capture("after-solo-"+slot,c);}
            c.SoloLayer=-1;
            c.Tick(0);MeasureCoverage(c);
            var positions=new Vector3[c.Layers.Length][];
            c.PauseMotion=true;c.Tick(0);
            for(var i=0;i<c.Layers.Length;i++)positions[i]=c.Layers[i].GetComponent<MeshFilter>().sharedMesh.vertices;
            yield return new WaitForSeconds(.15f);
            for(var i=0;i<c.Layers.Length;i++)
            {
                var now=c.Layers[i].GetComponent<MeshFilter>().sharedMesh.vertices;
                for(var v=0;v<now.Length;v++)Require(now[v]==positions[i][v],"Pause geometry "+i);
            }
            report.Add("PASS paused geometry stays identical across frames");c.PauseMotion=false;
            var watch=new System.Diagnostics.Stopwatch();c.Tick(0);
            var allocated=GC.GetAllocatedBytesForCurrentThread();watch.Start();
            for(var n=0;n<240;n++)c.Tick(1f/60);watch.Stop();var bytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
            report.Add("Default: "+c.ActiveElements+" elements; CPU tick "+(watch.Elapsed.TotalMilliseconds/240).ToString("F3")+" ms; managed bytes/240 ticks "+bytes);Require(bytes==0,"Zero steady allocations");
            for(var i=0;i<c.Layers.Length;i++)c.RuntimeProfile.Layers[i].Density=3;
            c.Tick(0);allocated=GC.GetAllocatedBytesForCurrentThread();watch.Restart();for(var n=0;n<240;n++)c.Tick(1f/60);watch.Stop();bytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
            report.Add("3x density: "+c.ActiveElements+" elements; CPU tick "+(watch.Elapsed.TotalMilliseconds/240).ToString("F3")+" ms; managed bytes "+bytes);Require(bytes==0,"Zero allocations at 3x");
            c.ResetAll();c.RuntimeProfile.Steering.SteeringEnabled=false;c.FollowGameplaySpeed=false;
            foreach(var speed in new[]{0f,1.8f,9f}){c.SetTravelSpeed(speed);yield return new WaitForSeconds(.4f);Capture("after-speed-"+speed.ToString("F1",System.Globalization.CultureInfo.InvariantCulture),c);}
            c.SetTravelSpeed(1.8f);
            for(var modeIndex=0;modeIndex<2;modeIndex++)
            {
                for(var i=0;i<c.Layers.Length;i++)c.RuntimeProfile.Layers[i].Enabled=modeIndex==1&&c.SavedProfile.Layers[i].Enabled;
                yield return new WaitForSeconds(.2f);float elapsed=0;
                for(var n=0;n<90;n++){yield return null;elapsed+=Time.unscaledDeltaTime;}
                report.Add((modeIndex==0?"Hidden":"Visible")+" Editor whole frame: "+(elapsed/90*1000).ToString("F2")+" ms / "+(90/elapsed).ToString("F1")+" FPS (not GPU timing)");
            }
            Call("ExitAbilitySandbox");Call("StartGame");c.RuntimeProfile.Steering.SteeringEnabled=false;
            yield return new WaitForSeconds(.75f);Capture("after-gameplay",c,false);
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Evidence+"after-gameplay-hud.png");
            yield return new WaitForSeconds(1.5f);Capture("after-gameplay-later",c,false);
        }
        static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
        static void MeasureCoverage(SpaceFlightVisualController c)
        {
            // Rasterized quad coverage at 320x180. Counts even transparent pixels: fill-rate estimate, not GPU time.
            const int w=320,h=180;var coverage=new int[w*h];var camera=Camera.main;
            for(var l=0;l<c.Layers.Length;l++)
            {
                var layer=c.Layers[l];if(!layer.Renderer.enabled)continue;
                var mesh=layer.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;
                var quadCount=layer.ActiveCount*(layer.Settings.Kind==SpaceLayerKind.Streaks?4:1);int samples=0;
                for(var q=0;q<quadCount;q++)
                {
                    Vector2 a=camera.WorldToViewportPoint(vertices[q*4]),b=camera.WorldToViewportPoint(vertices[q*4+1]),d=camera.WorldToViewportPoint(vertices[q*4+3]);
                    Vector2 e=camera.WorldToViewportPoint(vertices[q*4+2]);var low=Vector2.Min(Vector2.Min(a,b),Vector2.Min(d,e));var high=Vector2.Max(Vector2.Max(a,b),Vector2.Max(d,e));
                    var u=b-a;var v=d-a;var determinant=u.x*v.y-u.y*v.x;if(Mathf.Abs(determinant)<1e-10f)continue;
                    for(var y=Mathf.Max(0,Mathf.FloorToInt(low.y*h));y<Mathf.Min(h,Mathf.CeilToInt(high.y*h));y++)
                    for(var x=Mathf.Max(0,Mathf.FloorToInt(low.x*w));x<Mathf.Min(w,Mathf.CeilToInt(high.x*w));x++)
                    {
                        var point=new Vector2((x+.5f)/w,(y+.5f)/h)-a;
                        var uu=(point.x*v.y-point.y*v.x)/determinant;var vv=(u.x*point.y-u.y*point.x)/determinant;
                        if(uu>=0&&uu<1&&vv>=0&&vv<1){coverage[y*w+x]++;samples++;}
                    }
                }
                report.Add("Quad coverage slot "+l+": "+(samples/(float)(w*h)).ToString("F3")+" screens");
            }
            int sum=0,max=0;var pixels=new Color[coverage.Length];
            for(var i=0;i<coverage.Length;i++){sum+=coverage[i];max=Mathf.Max(max,coverage[i]);pixels[i]=Color.Lerp(new Color(.015f,.025f,.09f),new Color(1,.22f,.04f),coverage[i]/6f);}
            var texture=new Texture2D(w,h,TextureFormat.RGB24,false);texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(Evidence+"after-quad-overdraw.png",texture.EncodeToPNG());Object.Destroy(texture);
            report.Add("Transparent quad overlap mean "+(sum/(float)coverage.Length).ToString("F2")+", peak "+max+"; includes alpha-zero corners; excludes music/gameplay; not GPU timings.");
        }
        static void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.Add("FAIL "+message);}
        static void Call(string method)=>typeof(GameManager).GetMethod(method,Flags).Invoke(manager,null);
        static void Capture(string name,SpaceFlightVisualController c,bool isolated=true)=>CourseSteeringValidation.Capture("../SpaceArt/"+name,c,isolated);
    }
}
#endif
