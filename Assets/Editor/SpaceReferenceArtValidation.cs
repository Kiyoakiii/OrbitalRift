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
    public static class SpaceReferenceArtValidation
    {
        const string Evidence="Docs/VFX_Evidence/SpaceReference/";
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static GameManager manager;
        static readonly List<string> report=new List<string>();
        static SpaceReferenceArtValidation()
        {
            EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetString("SpaceReferenceQA","")!="")EditorApplication.delayCall+=Begin;};
            EditorApplication.update+=()=>{
                const string file="Temp/SpaceReference.command";
                if(!File.Exists(file)||EditorApplication.isCompiling)return;
                var command=File.ReadAllText(file).Trim();File.Delete(file);
                if(command=="apply")Apply();else Run(command);
            };
        }
        static void Run(string mode)
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before reference QA");
            Directory.CreateDirectory(Evidence);SessionState.SetString("SpaceReferenceQA",mode);
            Application.runInBackground=true;EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
        }
        static void Begin()
        {
            manager=Object.FindFirstObjectByType<GameManager>();if(manager==null){EditorApplication.delayCall+=Begin;return;}
            Application.runInBackground=true;Time.timeScale=1;
            var mode=SessionState.GetString("SpaceReferenceQA","");SessionState.SetString("SpaceReferenceQA","");
            report.Clear();manager.StartCoroutine(Guard(mode));
        }
        static IEnumerator Guard(string mode)
        {
            yield return null;var checks=Checks(mode);Application.logMessageReceived+=Log;
            while(true){object next;try{if(!checks.MoveNext())break;next=checks.Current;}catch(Exception ex){report.Add("FAIL "+ex);break;}yield return next;}
            Application.logMessageReceived-=Log;report.Add(report.Exists(s=>s.StartsWith("FAIL"))?"FAIL":"PASS");
            File.WriteAllText(Evidence+mode+".txt",string.Join("\n",report));Debug.Log("SPACE REFERENCE QA "+mode+" "+report[report.Count-1]);
        }
        static IEnumerator Checks(string mode)
        {
            Call("OpenAbilitySandbox");typeof(GameManager).GetField("autoFire",Flags).SetValue(manager,false);
            var c=manager.SpaceDepth;c.RuntimeProfile.Steering.SteeringEnabled=false;c.FollowGameplaySpeed=false;c.SetTravelSpeed(1.8f);
            yield return new WaitForSeconds(.4f);
            report.Add("Camera half-height "+c.ViewCamera.orthographicSize+", aspect "+c.ViewCamera.aspect);
            Capture(mode+"-space",c,1920,1080,true);
            Capture(mode+"-sandbox",c,1920,1080,false);
            if(mode=="before")yield break;
            if(mode=="motion")
            {
                Directory.CreateDirectory("Temp/SpaceReferenceMotion");
                for(var f=0;f<90;f++){c.PauseMotion=false;c.Tick(1f/30);c.PauseMotion=true;Capture("../../../Temp/SpaceReferenceMotion/frame-"+f.ToString("D3"),c,1280,720,true);yield return null;}
                yield break;
            }
            foreach(var slot in new[]{1,3}){c.SoloLayer=slot;c.Tick(0);Capture(mode+"-solo-"+slot,c,1920,1080,true);}
            c.SoloLayer=-1;c.Tick(0);
            var camera=c.ViewCamera;var aspect=camera.aspect;var ortho=camera.orthographicSize;
            try{camera.aspect=9f/16;camera.orthographicSize=Mathf.Max(ortho,3.75f/camera.aspect*GameplayCameraZoomSettings.Value);c.Tick(0);Capture(mode+"-portrait",c,1080,1920,true);}
            finally{camera.aspect=aspect;camera.orthographicSize=ortho;c.Tick(0);}
            foreach(var l in c.Layers)Require(l.Renderer.sharedMaterial!=null&&l.Renderer.sharedMaterial.shader!=null,"Layer material resolves");
            Require(c.Layers[4].ActiveCount==0,"No planets");
            var density=c.RuntimeProfile.Layers[1].Density;c.RuntimeProfile.Layers[1].Density=3;c.Tick(0);
            Require(c.Layers[1].ActiveCount==2,"Galaxy density cannot stack duplicate images at the same anchor");
            c.RuntimeProfile.Layers[1].Density=density;c.Tick(0);
            foreach(var slot in new[]{1,3})
            {
                var mat=c.Layers[slot].Renderer.sharedMaterial;Require(!ShaderUtil.ShaderHasError(mat.shader),"Detailed emission shader compiles");
                var t=(Texture2D)mat.mainTexture;
                report.Add("Slot "+slot+" runtime texture: "+t.width+" x "+t.height+", format "+t.format+", mip limit "+t.activeMipmapLimit);
                Require(t.width>=1200&&t.activeMipmapLimit==0,"Artwork retains full source resolution");
            }
            var renders=c.GetComponentsInChildren<Renderer>();var active=0;foreach(var r in renders)if(r.enabled)active++;
            report.Add("Active renderers including nebula children: "+active+"; elements: "+c.ActiveElements);
            c.PauseMotion=true;Capture("pause-a",c,960,540,true);yield return new WaitForSeconds(.15f);Capture("pause-b",c,960,540,true);c.PauseMotion=false;
            var watch=new System.Diagnostics.Stopwatch();c.Tick(0);var bytes=GC.GetAllocatedBytesForCurrentThread();watch.Start();
            for(var i=0;i<240;i++)c.Tick(1f/60);watch.Stop();bytes=GC.GetAllocatedBytesForCurrentThread()-bytes;
            report.Add("CPU tick: "+(watch.Elapsed.TotalMilliseconds/240).ToString("F3")+" ms; GC bytes/240 ticks: "+bytes);Require(bytes==0,"No steady allocations");
            float elapsed=0;for(var i=0;i<120;i++){yield return null;elapsed+=Time.unscaledDeltaTime;}
            report.Add("Editor whole frame: "+(elapsed/120*1000).ToString("F2")+" ms / "+(120/elapsed).ToString("F1")+" FPS; not device GPU timing.");
            Call("ExitAbilitySandbox");Call("StartGame");c.RuntimeProfile.Steering.SteeringEnabled=false;
            yield return new WaitForSeconds(.7f);Capture(mode+"-gameplay",c,1920,1080,false);
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Evidence+mode+"-gameplay-hud.png");
        }
        [MenuItem("Orbital Rift/Space Depth/Reference - Apply vivid artwork")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before applying reference art");
            const string root="Assets/Resources/SpaceDepth/";
            foreach(var name in new[]{"NebulaAzureRidge","NebulaCrimsonVeil","GalaxySpiralDust"})
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(root+"Textures/"+name+".png");
                importer.textureType=TextureImporterType.Default;importer.alphaSource=TextureImporterAlphaSource.None;
                importer.mipmapEnabled=true;importer.ignoreMipmapLimit=true;importer.wrapMode=TextureWrapMode.Clamp;
                importer.filterMode=FilterMode.Trilinear;importer.maxTextureSize=4096;importer.npotScale=TextureImporterNPOTScale.None;
                importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Standalone",overridden=true,maxTextureSize=4096,format=TextureImporterFormat.RGBA32});
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Android",overridden=true,maxTextureSize=4096,format=TextureImporterFormat.ASTC_4x4});
                importer.SaveAndReimport();
            }
            var nebula=MakeMaterial(root,"NebulaReference", "NebulaAzureRidge");
            nebula.SetTexture("_AlternateTex",Resources.Load<Texture2D>("SpaceDepth/Textures/NebulaCrimsonVeil"));
            nebula.SetFloat("_UseAlternate",1);nebula.SetFloat("_NebulaArt",1);nebula.SetFloat("_Exposure",1.05f);
            var galaxy=MakeMaterial(root,"GalaxyReference","GalaxySpiralDust");galaxy.SetFloat("_Exposure",1.15f);galaxy.SetFloat("_UseAlternate",0);galaxy.SetFloat("_NebulaArt",0);
            var p=AssetDatabase.LoadAssetAtPath<SpaceDepthProfile>(root+"Profiles/SpaceDepthProfile.asset");
            var n=p.Layers[3];n.Appearance=SpaceLayerAppearance.Image;n.Material=nebula;n.Texture=(Texture2D)nebula.mainTexture;n.Sprite=null;
            n.Enabled=true;n.FrameComposition=true;n.PreserveImageAspect=true;n.Density=1;n.Capacity=4;n.BaseCount=3;
            n.MinScale=6.7f;n.MaxScale=7.2f;n.MinAlpha=.92f;n.MaxAlpha=1;n.Brightness=1;n.PrimaryColor=Color.white;n.SecondaryColor=Color.white;n.ColorVariation=0;
            n.RadialMotion=false;n.SpeedMultiplier=.025f;n.RotationSpeed=.12f;n.NoiseAmount=.025f;
            p.Nebula=p.Nebula??new SpaceNebulaSettings();p.Nebula.ClusterCount=3;p.Nebula.ClusterSize=7.2f;p.Nebula.Brightness=1;p.Nebula.Opacity=1;
            p.Nebula.Glow=.55f;p.Nebula.Filament=.7f;p.Nebula.DarkVoids=.6f;p.Nebula.Drift=.2f;p.Nebula.Dust=.6f;p.Nebula.Tint=Color.white;p.Nebula.HighlightTint=new Color(.85f,.92f,1);
            var g=p.Layers[1];g.Appearance=SpaceLayerAppearance.Image;g.Material=galaxy;g.Texture=(Texture2D)galaxy.mainTexture;g.Sprite=null;
            g.Enabled=true;g.FrameComposition=true;g.PreserveImageAspect=true;g.Density=1;g.BaseCount=2;g.Capacity=Mathf.Max(g.Capacity,2);
            g.MinScale=g.MaxScale=2.9f;g.MinAlpha=.92f;g.MaxAlpha=1;g.Brightness=1;g.PrimaryColor=Color.white;g.SecondaryColor=Color.white;g.ColorVariation=0;
            g.RadialMotion=false;g.SpeedMultiplier=.01f;g.RotationSpeed=.06f;g.NoiseAmount=.015f;
            p.Layers[4].Enabled=false;
            EditorUtility.SetDirty(p);EditorUtility.SetDirty(nebula);EditorUtility.SetDirty(galaxy);AssetDatabase.SaveAssets();Debug.Log("SPACE REFERENCE artwork applied");
        }
        static Material MakeMaterial(string root,string name,string texture)
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(root+"Materials/"+name+".mat");
            if(material==null){material=new Material(Resources.Load<Shader>("SpaceDepth/Shaders/SpaceEmissionDetail"));AssetDatabase.CreateAsset(material,root+"Materials/"+name+".mat");}
            material.mainTexture=Resources.Load<Texture2D>("SpaceDepth/Textures/"+texture);return material;
        }
        static void Capture(string name,SpaceFlightVisualController c,int width,int height,bool isolated)
        {
            var source=c.ViewCamera;var camera=source;GameObject temporary=null;
            var framingAspect=source.aspect;
            if(isolated){source.aspect=width/(float)height;c.Tick(0);}
            var renderers=c.GetComponentsInChildren<Renderer>();var layers=new int[renderers.Length];
            if(isolated){temporary=new GameObject("Reference art QA camera");camera=temporary.AddComponent<Camera>();camera.CopyFrom(source);camera.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=1<<30;}
            var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;var oldAspect=camera.aspect;
            var rt=RenderTexture.GetTemporary(width,height,24);var tex=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                for(var i=0;i<renderers.Length;i++){layers[i]=renderers[i].gameObject.layer;if(isolated)renderers[i].gameObject.layer=30;}
                camera.aspect=width/(float)height;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();File.WriteAllBytes(Evidence+name+".png",tex.EncodeToPNG());
            }
            finally
            {
                for(var i=0;i<renderers.Length;i++)renderers[i].gameObject.layer=layers[i];
                camera.targetTexture=oldTarget;camera.aspect=oldAspect;RenderTexture.active=oldActive;
                RenderTexture.ReleaseTemporary(rt);Object.Destroy(tex);if(temporary!=null)Object.Destroy(temporary);
                if(isolated){source.aspect=framingAspect;c.Tick(0);}
            }
        }
        static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
        static void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.Add("FAIL "+message);}
        static void Call(string method)=>typeof(GameManager).GetMethod(method,Flags).Invoke(manager,null);
    }
}
#endif
