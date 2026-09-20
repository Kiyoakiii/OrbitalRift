using System;
using System.IO;
using System.Reflection;
using OrbitalRift;
using OrbitalRift.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class LivingCosmosRegression
{
    static int checks;
    static int engineErrors;
    static void OnLog(string message,string stack,LogType type) { if(type==LogType.Error || type==LogType.Exception) engineErrors++; }
    public static void Run()
    {
        try
        {
            Application.logMessageReceived += OnLog;
            Directory.CreateDirectory("Results");
            CheckState();
            TempoRulesRegression.Run(Require);
            PairedLensRulesRegression.Run(Require);
            CheckGraphics();
            CheckSelector();
            CheckTempoChoice();
            CheckMap();
            Require(engineErrors==0,"Unity logged runtime/render errors: "+engineErrors);
            File.WriteAllText("Results/report.txt", "PASS: " + checks + " assertions. Actual production state, assets, shader and Canvas selector. Not a full-game or device test.");
            Debug.Log("PASS Living Cosmos: " + checks + " assertions");
            EditorApplication.Exit(0);
        }
        catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Require(bool value, string message)
    { checks++; if (!value) throw new Exception(message); }

    static void CheckState()
    {
        var outcome=new OutcomeUnderTest();
        outcome.RecordCoopOutcome(null,true);outcome.RecordCoopOutcome(null,true);
        Require(outcome.mmr==1337 && outcome.bestScore==999 && outcome.coopResultMmrDelta==0,"Unranked result preserves local rating and best");
        outcome.RecordCoopOutcome(null,false);
        Require(outcome.mmr==1337 && outcome.coopResultScore==0,"Unranked defeat never writes profile");
        for(var seed=0;seed<1000;seed++)
        {
            var route=LivingCosmosRunState.CreatePreviewRoute(seed);
            Require(LivingCosmosRunState.ValidateRoute(route), "Directed route validation");
            Require(route.Signature()==LivingCosmosRunState.CreatePreviewRoute(seed).Signature(), "Route reproducibility");
        }
        var run=new LivingCosmosRunState(); run.EnterRoom(1);
        run.Tick(1f,10,5,2f,false,false,false);
        Require(!run.CanDealDamage, "Arrival blocks damage");
        run.Tick(.1f,10,5,0f,false,false,false);
        Require(run.CanDealDamage,"Combat entry");
        run.Tick(1f,10,5,0,false,false,false);
        run.Tick(0f,10,5,0,false,false,false);
        Require(run.CombatSeconds==1f,"Pause freezes clock");
        run.Tick(.1f,0,5,0,false,false,false);
        Require(run.ClearSequence==1 && !run.CanDealDamage,"Single clear event");
        for(var i=0;i<100;i++) run.Tick(.001f,0,5,0,false,false,false);
        Require(run.ClearSequence==1,"Duplicate clear not counted");
        run.Tick(2f,0,5,0,false,false,false);
        Require(run.Phase==LivingEncounterPhase.RouteChoice,"Clear opens real route choice");
        Require(!run.TryChoose(7) && run.TryChoose(3) && !run.TryChoose(3),"Only linked destination, one commit");
        run.Tick(.3f,0,5,0,false,false,false);
        Require(run.Jump>.5f && !run.CanDealDamage,"Visible jump without damage");
        Require(run.Tick(2f,0,5,0,false,false,false),"Departure commits advance");
        Require(!run.Tick(.01f,0,5,3f,false,false,false),"Advance event not repeated");
        run.EnterRoom(3); run.Tick(.1f,0,5,3f,true,false,false);
        Require(run.Phase==LivingEncounterPhase.Docking,"Docking phase");
        run.Tick(.1f,0,5,0,true,true,false);
        Require(run.Phase==LivingEncounterPhase.Shop,"Shop phase");
        run.Tick(10f,0,5,0,true,true,false);
        Require(run.Phase==LivingEncounterPhase.Shop && run.CombatSeconds==0,"Shop never auto-skips");
        run.Tick(.1f,0,5,0,false,false,false);
        Require(run.Phase==LivingEncounterPhase.Clear,"Shop chosen advances");
        run.EnterRoom(5); run.Tick(.1f,10,5,0,false,false,true);
        run.Tick(.1f,0,0,0,false,false,true);
        Require(run.Phase==LivingEncounterPhase.Failed,"Failure wins same-tick death");
        run.Tick(10f,0,5,0,false,false,true);
        Require(run.Phase==LivingEncounterPhase.Failed,"Terminal state is sticky");
        run.EnterRoom(5); run.Tick(.1f,10,5,0,false,false,true);
        run.Tick(.1f,0,5,0,false,false,true); run.Tick(2f,0,5,0,false,false,true);
        Require(run.Phase==LivingEncounterPhase.Completed,"Final room completes");
        Require(LivingCosmosRunState.RegionForRoom(3)==0 && LivingCosmosRunState.RegionForRoom(4)==1,"Region boundary");
        foreach(var first in new[]{1,2}) foreach(var second in new[]{4,5})
        {
            var routeRun=new LivingCosmosRunState();routeRun.Initialize(17);
            var sequence=new[]{first,3,second,6,7};
            foreach(var next in sequence)
            {
                var type=routeRun.Layout.Rooms[routeRun.RoomIndex].Type;
                if(type==SectorRoomType.Shop)
                {
                    routeRun.Tick(.1f,0,5,0,true,false,false);routeRun.Tick(.1f,0,5,0,true,true,false);
                    var wanted=routeRun.RoomIndex==3?(first==2?2:1):(second==5?2:1);
                    Require(routeRun.ShopChoicesRemaining==wanted,"Risk really changes shop reward");
                    for(var pick=0;pick<wanted;pick++)Require(routeRun.ConsumeShopChoice(),"Valid shop choice");
                    Require(!routeRun.ConsumeShopChoice(),"No unlimited extra modules");
                    routeRun.Tick(.1f,0,5,0,false,false,false);
                }
                else
                {routeRun.Tick(.1f,10,5,0,false,false,false);routeRun.Tick(.1f,0,5,0,false,false,false);}
                routeRun.Tick(2f,0,5,0,false,false,false);
                Require(routeRun.TryChoose(next),"Chosen path follows links");
                Require(routeRun.Tick(2f,0,5,0,false,false,false),"Departure completes");
                routeRun.EnterRoom(routeRun.PendingNodeId);
            }
            Require(routeRun.Visited.Count==6 && !routeRun.Visited.Contains(first==1?2:1),"Unchosen branch not visited");
            routeRun.Tick(.1f,10,5,0,false,false,true);routeRun.Tick(.1f,0,5,0,false,false,true);routeRun.Tick(2f,0,5,0,false,false,true);
            Require(routeRun.IsTerminal && routeRun.ClearedCount==6,"All four routes end at boss without branch farming");
        }
    }

    static void CheckGraphics()
    {
        var go=new GameObject("QA Camera"); var camera=go.AddComponent<Camera>();
        camera.orthographic=true; camera.orthographicSize=6;
        var effect=go.AddComponent<MusicSpaceDistortion>(); effect.Initialize(camera);
        var render=typeof(MusicSpaceDistortion).GetMethod("OnRenderImage",BindingFlags.Instance|BindingFlags.NonPublic);
        Require(Resources.Load<SpaceVisualProfile>("SpaceBlueFrontier")!=null,"Blue asset deserializes");
        Require(Resources.Load<SpaceVisualProfile>("SpaceAmberFront")!=null,"Amber asset deserializes");
        foreach(var size in new[]{new Vector2Int(720,1280),new Vector2Int(1280,720)})
        foreach(var music in new[]{false,true})
        for(var frame=0;frame<3;frame++)
        {
            var t=frame*.5f;
            effect.SetRegion(true,0,1,t); effect.SetTravel(9f,frame==1?1:0,true,4.25f);
            effect.SetFrame(9f,.3f,.3f,.2f,.1f,.1f,.5f,1f,Color.cyan,Color.blue,Color.white);
            effect.SetActive(music);
            var source=RenderTexture.GetTemporary(size.x,size.y,0,RenderTextureFormat.ARGBFloat);
            var target=RenderTexture.GetTemporary(size.x,size.y,0,RenderTextureFormat.ARGBFloat);
            Graphics.Blit(Texture2D.blackTexture,source);
            render.Invoke(effect,new object[]{source,target});
            Save(target,"region-"+size.x+"-"+frame+"-music-"+music+".png");
            RenderTexture.active=null; RenderTexture.ReleaseTemporary(source); RenderTexture.ReleaseTemporary(target);
        }
        effect.SetRegion(false,0,0,0);
        Require(!(bool)typeof(MusicSpaceDistortion).GetField("regionActive",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(effect),"Legacy region reset");
        UnityEngine.Object.DestroyImmediate(go);
    }

    static void CheckSelector()
    {
        var go=new GameObject("SelectorCamera");var camera=go.AddComponent<Camera>();
        camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;
        var root=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1080,1920);scaler.matchWidthOrHeight=.5f;
        var panel=new GameObject("Selector",typeof(RectTransform));var rect=panel.GetComponent<RectTransform>();rect.SetParent(root.transform,false);
        rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        var selector=panel.AddComponent<ExpeditionModeChoiceView>();selector.Show();
        foreach(var size in new[]{new Vector2Int(720,1280),new Vector2Int(1280,720)})
        {
            var target=RenderTexture.GetTemporary(size.x,size.y,24);camera.targetTexture=target;
            Canvas.ForceUpdateCanvases();
            foreach(var text in root.GetComponentsInChildren<TMP_Text>()) { text.ForceMeshUpdate(); Require(!text.isTextOverflowing,"Text overflow: "+text.text); }
            camera.Render();Save(target,"selector-"+size.x+".png");
            camera.targetTexture=null;RenderTexture.active=null;RenderTexture.ReleaseTemporary(target);
        }
        var selected=0; selector.Selected+=experimental=>{Require(experimental,"Experimental option selected"); selected++;};
        selector.EnsureBuilt();selector.EnsureBuilt();
        panel.transform.Find("Blocker/Living Cosmos").GetComponent<Button>().onClick.Invoke();
        Require(selected==1 && !selector.IsOpen,"Selector no duplicate listeners");
        UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(go);
    }

    static void Save(RenderTexture target,string name)
    {
        RenderTexture.active=target;
        var image=new Texture2D(target.width,target.height,TextureFormat.RGBAFloat,false);
        image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
        var pixels=image.GetPixels();var total=0f;
        foreach(var p in pixels)
        {
            if(float.IsNaN(p.r)||float.IsInfinity(p.r)||float.IsNaN(p.g)||float.IsInfinity(p.g)||float.IsNaN(p.b)||float.IsInfinity(p.b))
                throw new Exception("Non-finite GPU output");
            total+=p.r+p.g+p.b;
        }
        Require(total>1,"Black frame");
        var png=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
        for(var i=0;i<pixels.Length;i++)pixels[i].a=1;
        png.SetPixels(pixels);png.Apply();File.WriteAllBytes("Results/"+name,png.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(png);RenderTexture.active=null;
    }

    static void CheckTempoChoice()
    {
        TempoRewardState state=null;
        for(var seed=0;seed<1000 && state==null;seed++)
        {
            var candidate=new TempoRewardState("ui",seed);candidate.BeginEncounter(1,SectorRoomType.Combat,30000);
            candidate.FinishEncounter(1,true,true);if(candidate.Pending!=null)state=candidate;
        }
        Require(state!=null,"Reward choice fixture");
        var cameraGo=new GameObject("Reward camera");var camera=cameraGo.AddComponent<Camera>();camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);
        var root=new GameObject("Reward Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,1920);scaler.matchWidthOrHeight=.5f;
        var panel=new GameObject("Reward",typeof(RectTransform));var rect=panel.GetComponent<RectTransform>();rect.SetParent(root.transform,false);rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        var view=panel.AddComponent<TempoRewardChoiceView>();view.Apply(state);Canvas.ForceUpdateCanvases();
        foreach(var text in root.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();Require(!text.isTextOverflowing,"Reward text overflow: "+text.text);}
        foreach(var size in new[]{new Vector2Int(720,1280),new Vector2Int(1280,720)})
        {
            var target=RenderTexture.GetTemporary(size.x,size.y,24);camera.targetTexture=target;Canvas.ForceUpdateCanvases();
            foreach(var text in root.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();Require(!text.isTextOverflowing,"Reward text overflow: "+text.text);}
            camera.Render();Save(target,"tempo-choice-"+size.x+".png");camera.targetTexture=null;RenderTexture.ReleaseTemporary(target);
        }
        var choice=TempoModule.None;var replacement=TempoModule.None;
        view.ChoiceRequested+=(module,replace)=>{choice=module;replacement=replace;};
        panel.transform.Find("Blocker/First module").GetComponent<Button>().onClick.Invoke();
        Require(choice==state.Pending.First && replacement==TempoModule.None,"Reward card emits command, does not mutate state");
        var slots=(System.Collections.Generic.List<TempoModuleSlot>)typeof(TempoRewardState).GetField("modules",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(state);
        foreach(TempoModule module in new[]{TempoModule.RapidFire,TempoModule.FastPlasma,TempoModule.ReserveCapacitor})
            if(module!=state.Pending.First) slots.Add(CreateTempoSlot(module,1));
        choice=TempoModule.None;replacement=TempoModule.None;view.Apply(state);panel.transform.Find("Blocker/First module").GetComponent<Button>().onClick.Invoke();
        Require(panel.transform.Find("Blocker/Replace first").gameObject.activeSelf,"Full slots reveal explicit replacement choices");
        panel.transform.Find("Blocker/Replace first").GetComponent<Button>().onClick.Invoke();
        Require(choice==state.Pending.First && replacement!=TempoModule.None,"Replacement confirms selected reward module");
        view.Apply(null);Require(!view.IsOpen,"Reward modal hides when no pending state");
        UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(cameraGo);
    }

    static TempoModuleSlot CreateTempoSlot(TempoModule module,int fights)
    {
        var constructor=typeof(TempoModuleSlot).GetConstructor(BindingFlags.NonPublic|BindingFlags.Instance,
            null,new[]{typeof(TempoModule),typeof(int)},null);
        return (TempoModuleSlot)constructor.Invoke(new object[]{module,fights});
    }

    static void CheckMap()
    {
        var camGo=new GameObject("Map camera");var camera=camGo.AddComponent<Camera>();
        camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;
        var root=new GameObject("Map Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,1920);scaler.matchWidthOrHeight=.5f;
        var panel=new GameObject("Map",typeof(RectTransform));var rect=panel.GetComponent<RectTransform>();rect.SetParent(root.transform,false);
        rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        var view=panel.AddComponent<CosmosRouteMapView>();
        var run=new LivingCosmosRunState();run.Initialize(17);run.Tick(.1f,0,5,0,false,false,false);run.Tick(2f,0,5,0,false,false,false);
        foreach(var size in new[]{new Vector2Int(720,1280),new Vector2Int(1280,720),new Vector2Int(1600,1200)})
        {
            var target=RenderTexture.GetTemporary(size.x,size.y,24);camera.targetTexture=target;
            Canvas.ForceUpdateCanvases();view.Apply(run,false);Canvas.ForceUpdateCanvases();
            foreach(var text in root.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();Require(!text.isTextOverflowing,"Map text overflow: "+text.text);}
            camera.Render();Save(target,"map-"+size.x+".png");camera.targetTexture=null;RenderTexture.ReleaseTemporary(target);
        }
        var choices=0;view.DestinationRequested+=id=>{Require(run.TryChoose(id),"Map click sends a valid route command");choices++;};
        panel.transform.Find("Set course").GetComponent<Button>().onClick.Invoke();
        Require(choices==1 && run.PendingNodeId==1,"Map is interactive, not a picture");
        view.Apply(run,true);Require(!panel.transform.Find("Set course").GetComponent<Button>().interactable,"Paused map cannot recommit route");
        UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(camGo);
    }
}
