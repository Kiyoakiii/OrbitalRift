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
    public static class CourseSteeringValidation
    {
        public const string Evidence="Docs/VFX_Evidence/CourseSteering/";
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        private static readonly List<string> report=new List<string>(),errors=new List<string>();
        private static GameManager manager;
        static CourseSteeringValidation(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetInt("CourseQA",0)>0)EditorApplication.delayCall+=Begin;};}
        [MenuItem("Orbital Rift/Space Depth/Phase 2 - Flow checkpoint")]
        public static void RunFlow()=>Run(1);
        [MenuItem("Orbital Rift/Space Depth/Phase 2 - Full validation")]
        public static void RunFull()=>Run(2);
        [MenuItem("Orbital Rift/Space Depth/Validate artwork panel")]
        public static void RunUI()=>Run(3);
        private static void Run(int mode)
        {
            if(EditorApplication.isPlaying){Debug.LogWarning("Stop Play Mode first");return;}
            Directory.CreateDirectory(Evidence);
            EnsureAxes();
            var profile=AssetDatabase.LoadAssetAtPath<SpaceDepthProfile>("Assets/Resources/SpaceDepth/Profiles/SpaceDepthProfile.asset");
            if(profile.Steering==null)profile.Steering=new CourseSteeringSettings();
            foreach(var name in new[]{"SpaceDepth","SpaceDepthImage"})Require(!ShaderUtil.ShaderHasError(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/SpaceDepth/Shaders/"+name+".shader")),"Shader compiles: "+name);
            Application.runInBackground=true;EditorApplication.isPaused=false;
            SessionState.SetInt("CourseQA",mode);EditorApplication.isPlaying=true;
        }
        private static void EnsureAxes()
        {
            var input=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset")[0]);
            var axes=input.FindProperty("m_Axes");
            var names=new[]{"CourseHorizontal","CourseVertical","OrbitKeyboard","OrbitHorizontal"};
            for(var n=0;n<names.Length;n++)
            {
                bool found=false;for(var i=0;i<axes.arraySize;i++)if(axes.GetArrayElementAtIndex(i).FindPropertyRelative("m_Name").stringValue==names[n]){found=true;break;}
                if(found)continue;
                var index=axes.arraySize;axes.InsertArrayElementAtIndex(index);var a=axes.GetArrayElementAtIndex(index);
                foreach(var field in new[]{"descriptiveName","descriptiveNegativeName","negativeButton","positiveButton","altNegativeButton","altPositiveButton"})a.FindPropertyRelative(field).stringValue="";
                a.FindPropertyRelative("m_Name").stringValue=names[n];a.FindPropertyRelative("gravity").floatValue=n==2?3:0;a.FindPropertyRelative("dead").floatValue=n==3?.16f:0;a.FindPropertyRelative("sensitivity").floatValue=1;a.FindPropertyRelative("snap").boolValue=false;a.FindPropertyRelative("invert").boolValue=n==1;a.FindPropertyRelative("type").intValue=n==2?0:2;a.FindPropertyRelative("axis").intValue=n==3?3:n==1?1:0;a.FindPropertyRelative("joyNum").intValue=0;
                if(n==2){a.FindPropertyRelative("negativeButton").stringValue="left";a.FindPropertyRelative("positiveButton").stringValue="right";a.FindPropertyRelative("altNegativeButton").stringValue="a";a.FindPropertyRelative("altPositiveButton").stringValue="d";}
            }
            input.ApplyModifiedProperties();
        }
        private static void Begin()
        {manager=Object.FindFirstObjectByType<GameManager>();if(manager==null){EditorApplication.delayCall+=Begin;return;}Application.runInBackground=true;Time.timeScale=1;var mode=SessionState.GetInt("CourseQA",2);SessionState.SetInt("CourseQA",0);report.Clear();errors.Clear();File.WriteAllText(Evidence+"progress.txt","Begin "+mode);Application.logMessageReceived+=Log;manager.StartCoroutine(Guard(mode));}
        private static IEnumerator Guard(int mode)
        {
            yield return null;var iterator=Checks(mode);
            while(true){object next;try{if(!iterator.MoveNext())break;next=iterator.Current;}catch(Exception ex){errors.Add(ex.ToString());break;}yield return next;}
            Application.logMessageReceived-=Log;report.Add(errors.Count==0?"PASS":"FAIL\n"+string.Join("\n",errors));
            File.WriteAllText(Evidence+(mode==1?"flow":mode==3?"ui":"full")+".txt",string.Join("\n",report));Debug.Log("COURSE QA "+mode+" "+report[report.Count-1]);
        }
        private static IEnumerator Checks(int mode)
        {
            Call("OpenAbilitySandbox");Set("autoFire",false);
            if(mode==3)
            {
                var panel=(SpaceLayerDebugPanel)Get("spaceDepthPanel");panel.Open=true;
                typeof(SpaceLayerDebugPanel).GetField("courseExpanded",Flags).SetValue(panel,false);typeof(SpaceLayerDebugPanel).GetField("artworkExpanded",Flags).SetValue(panel,true);typeof(SpaceLayerDebugPanel).GetField("selected",Flags).SetValue(panel,4);typeof(SpaceLayerDebugPanel).GetField("scroll",Flags).SetValue(panel,new Vector2(0,420));
                yield return new WaitForSeconds(.3f);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Evidence+"sandbox-artwork-panel.png");
                Require(errors.Count==0,"Artwork fields render without errors or missing Editor styles");yield break;
            }
            var c=manager.SpaceDepth;c.FollowGameplaySpeed=false;c.SetTravelSpeed(3.2f);c.RuntimeProfile.Steering.SteeringEnabled=true;
            var course=c.Course;
            for(var i=0;i<c.Layers.Length;i++)c.RuntimeProfile.Layers[i].Enabled=i==2||i==5||i==6||i==8||i==10;
            yield return new WaitForSeconds(.2f);Capture("flow-center",c);
            var speed=c.GetTravelSpeed();var initial=course.CurrentCourseCenter;
            course.Preset(Vector2.right*.8f);
            Require(course.CurrentCourseCenter==initial&&course.TargetCourseCenter.x>initial.x,"Target changes without teleporting Current");
            yield return new WaitForSeconds(.35f);Capture("flow-right-early",c);
            yield return new WaitForSeconds(.65f);Capture("flow-right-middle",c);
            yield return new WaitForSeconds(1f);Capture("flow-right-settled",c);
            Require(c.Layers[10].CurrentEffectiveVanishingPoint.x>c.Layers[2].CurrentEffectiveVanishingPoint.x+1,"Near flow shifts more strongly than Far");
            Require(c.GetTravelSpeed()==speed,"Steering does not alter travel speed");
            var stable=course.TargetCourseCenter;yield return new WaitForSeconds(.2f);Require(course.TargetCourseCenter==stable,"Release holds course by default");
            if(mode==1)yield break;
            var full=FullChecks(c);while(full.MoveNext())yield return full.Current;
        }
        private static IEnumerator FullChecks(SpaceFlightVisualController c)
        {
            MathChecks();
            c.ResetAll();c.FollowGameplaySpeed=false;c.SetTravelSpeed(3.2f);
            var settings=c.RuntimeProfile.Steering;var course=c.Course;
            var player=(Transform)Get("player");
            var directions=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.left,Vector2.down,new Vector2(1,1),new Vector2(-1,1),new Vector2(-1,-1),new Vector2(1,-1)};
            for(var d=0;d<directions.Length;d++)
            {
                course.Preset(directions[d]*.85f);yield return new WaitForSeconds(.9f);
                Require(Mathf.Abs(Vector2.Distance(player.position,manager.ShipOrbitCenter)-OrbitSettings.Radius)<.005f,"Ship orbit radius preserved, preset "+d);
                var vp=c.ViewCamera.WorldToViewportPoint(player.position);Require(vp.x>=0&&vp.x<=1&&vp.y>=0&&vp.y<=1,"Ship inside screen, preset "+d);
                Capture("all-preset-"+d,c);
            }
            Require(Vector2.Dot(c.Layers[3].LargeParallaxOffset,c.CourseOffset)<0,"Large-object parallax opposes course displacement");
            c.SetTravelSpeed(0);var point=c.Layers[10].FirstPosition;
            course.Preset(Vector2.left);yield return new WaitForSeconds(.5f);
            Require(Vector2.Distance(point,c.Layers[10].FirstPosition)<.00001f,"Changing course at zero speed does not teleport particles");
            c.SetTravelSpeed(3.2f);var maxStep=0f;var randomCourse=new System.Random(811);
            for(var i=0;i<180;i++)
            {
                course.Preset(i<60?(i%2==0?Vector2.right:Vector2.left):new Vector2((float)randomCourse.NextDouble()*2-1,(float)randomCourse.NextDouble()*2-1));
                var layer=c.Layers[10];var before=layer.FirstPosition;var recycled=layer.Recycles;c.TickCourse(Vector2.zero,1f/60,true);c.Tick(1f/60);
                var delta=Vector2.Distance(before,layer.FirstPosition);if(recycled==layer.Recycles)maxStep=Mathf.Max(maxStep,delta);
                RequireFinite(layer.FirstPosition);
            }
            Require(maxStep<1,"Rapid reversals remain continuous; max non-recycle step "+maxStep.ToString("F3"));
            foreach(var mode in new[]{CourseAutoTest.Horizontal,CourseAutoTest.Vertical,CourseAutoTest.Circle,CourseAutoTest.FigureEight})
            {course.AutoTest=mode;yield return new WaitForSeconds(1.5f);Capture("auto-"+mode,c);}
            course.AutoTest=CourseAutoTest.Off;
            foreach(var speed in new[]{0f,.08f,9f,30f}){c.SetTravelSpeed(speed);course.Preset(Vector2.right);yield return new WaitForSeconds(.5f);Capture("steering-speed-"+speed.ToString("F2",System.Globalization.CultureInfo.InvariantCulture),c);}
            c.SetTravelSpeed(1.8f);ArtworkChecks(c);Capture("custom-image-layer",c);c.ResetLayer(4);
            settings.SteeringEnabled=false;c.TickCourse(Vector2.one,1);c.Tick(0);
            Require(course.CurrentCourseCenter==course.NeutralCourseCenter&&manager.ShipOrbitCenter==Vector2.zero,"Steering disabled restores neutral ship and VP");
            foreach(var l in c.Layers)Require(l.CurrentEffectiveVanishingPoint==c.NeutralCourseCenter,"Disabled effective VP: "+l.Settings.DisplayName);
            Capture("phase1-disabled",c);settings.SteeringEnabled=true;
            Benchmark(c,1);Benchmark(c,3);c.ResetAll();c.FollowGameplaySpeed=false;
            Call("ExitAbilitySandbox");Call("StartGame");Set("autoFire",false);Set("invincible",100f);
            yield return new WaitForSeconds(.2f);
            Require(!((AbilitySandboxSession)Get("abilitySandbox")).IsOpen&&(bool)Get("playing"),"Real gameplay outside Sandbox");
            course.Preset(new Vector2(1,1));yield return new WaitForSeconds(1);
            Require(Mathf.Abs(Vector2.Distance(player.position,manager.ShipOrbitCenter)-OrbitSettings.Radius)<.005f,"Production orbit follows course without radius change");
            Require(Vector2.Dot(player.up,(manager.ShipOrbitCenter-(Vector2)player.position).normalized)>.999f,"Ship nose points inward at shifted center");
            Set("coreActive",true);Set("coreAngle",(float)Get("playerAngle")+Mathf.PI);typeof(GameManager).GetMethod("UpdateCore",Flags).Invoke(manager,new object[]{0f});
            Require(Mathf.Abs(Vector2.Distance(((Transform)Get("core")).position,manager.ShipOrbitCenter)-OrbitSettings.Radius)<.005f,"Collectible core remains on shifted ship orbit");Set("coreActive",false);
            var source=Get("playerCommandSource");var oldAngle=(float)Get("playerAngle");
            Set("playerCommandSource",new OrbitInput());yield return new WaitForSeconds(.25f);Set("playerCommandSource",source);
            Require(Mathf.Abs(Mathf.DeltaAngle(oldAngle*Mathf.Rad2Deg,(float)Get("playerAngle")*Mathf.Rad2Deg))>1,"Existing orbit command moves ship around displaced center");
            var shots=(List<Projectile>)Get("projectiles");Set("autoFire",true);yield return new WaitForSeconds(.22f);Set("autoFire",false);
            Projectile shot=null;for(var i=shots.Count-1;i>=0;i--)if(shots[i].FromPlayer){shot=shots[i];break;}
            Require(shot!=null&&Vector2.Dot(shot.Velocity.normalized,player.up)>.98f,"Production player shooting follows ship nose");
            var enemyPool=(ObjectPool<Enemy>)Get("enemyPool");var enemies=(List<Enemy>)Get("enemies");var targetEnemy=enemyPool.Get();
            targetEnemy.ResetEnemy(EnemyKind.Scout,0,1,(Sprite)Get("whiteSprite"));targetEnemy.Health=10;targetEnemy.transform.position=player.position+player.up*.6f;enemies.Add(targetEnemy);
            typeof(GameManager).GetMethod("Shoot",Flags).Invoke(manager,new object[]{(Vector2)targetEnemy.transform.position,(Vector2)player.up*10,true,Color.white,DamageElement.Kinetic,1f,false});
            var friendly=shots[shots.Count-1];var enemyHit=(bool)typeof(GameManager).GetMethod("HitEnemies",Flags).Invoke(manager,new object[]{shots.Count-1,friendly,(Vector2)targetEnemy.transform.position-(Vector2)player.up*.1f});
            Require(enemyHit&&targetEnemy.Health<10,"Player projectile hits enemy in world coordinates after steering");
            player.gameObject.SetActive(true);Capture("production-impact",c,false);
            typeof(GameManager).GetMethod("RemoveEnemy",Flags).Invoke(manager,new object[]{enemies.IndexOf(targetEnemy)});
            var hp=(int)Get("shields");Set("invincible",0f);Set("starShields",0);
            var pool=(ObjectPool<Projectile>)Get("projectilePool");var hostile=pool.Get();hostile.ResetProjectile(player.position,Vector2.zero,false,Color.green,DamageElement.Kinetic,1);shots.Add(hostile);
            var hit=(bool)typeof(GameManager).GetMethod("HitPlayer",Flags).Invoke(manager,new object[]{shots.Count-1,hostile});
            Require(hit&&(int)Get("shields")==hp-1,"Hostile projectile collision damages shifted ship");Set("invincible",100f);
            player.gameObject.SetActive(true);Capture("production-course",c,false);
            // A real gameplay pickup becomes an orbiting shield and intercepts a hostile shot.
            var starPool=(ObjectPool<StarParticle>)Get("starPool");var stars=(List<StarParticle>)Get("stars");var shield=starPool.Get();
            shield.IsShield=true;shield.IsPurple=true;shield.ShieldHits=2;shield.ShieldRadius=.5f;shield.ShieldAngle=0;stars.Add(shield);Set("starShields",1);
            typeof(GameManager).GetMethod("UpdateStars",Flags).Invoke(manager,new object[]{0f});
            Require(Mathf.Abs(Vector2.Distance(shield.transform.position,player.position)-.5f)<.005f,"Gameplay shield follows actual displaced ship");
            Set("invincible",0f);var hpBeforeShield=(int)Get("shields");hostile=pool.Get();hostile.ResetProjectile(shield.transform.position,Vector2.zero,false,Color.green,DamageElement.Kinetic,1);shots.Add(hostile);
            hit=(bool)typeof(GameManager).GetMethod("HitPlayer",Flags).Invoke(manager,new object[]{shots.Count-1,hostile});
            Require(hit&&shield.ShieldHits==1&&(int)Get("shields")==hpBeforeShield,"Shifted shield intercepts projectile before hull");
            typeof(GameManager).GetMethod("SetPaused",Flags).Invoke(manager,new object[]{true});var pausedCenter=course.CurrentCourseCenter;var pausedPoint=c.Layers[10].FirstPosition;
            yield return new WaitForSecondsRealtime(.15f);
            Require(pausedCenter==course.CurrentCourseCenter&&pausedPoint==c.Layers[10].FirstPosition,"Gameplay pause freezes course and flow");typeof(GameManager).GetMethod("SetPaused",Flags).Invoke(manager,new object[]{false});
            player.gameObject.SetActive(true);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Evidence+"production-hud.png");
            Call("OpenAbilitySandbox");c.FollowGameplaySpeed=false;course.Preset(Vector2.right*.75f);
            c.RuntimeProfile.Steering.ShowCourseDebug=true;
            var panel=(SpaceLayerDebugPanel)Get("spaceDepthPanel");panel.Open=true;
            yield return new WaitForSeconds(.5f);Capture("course-debug-world",c,false);
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Evidence+"sandbox-course-panel.png");
            typeof(SpaceLayerDebugPanel).GetField("courseExpanded",Flags).SetValue(panel,false);typeof(SpaceLayerDebugPanel).GetField("artworkExpanded",Flags).SetValue(panel,true);typeof(SpaceLayerDebugPanel).GetField("selected",Flags).SetValue(panel,4);typeof(SpaceLayerDebugPanel).GetField("scroll",Flags).SetValue(panel,new Vector2(0,440));
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Evidence+"sandbox-artwork-panel.png");
            panel.Open=false;c.RuntimeProfile.Steering.ShowCourseDebug=false;
            // A deterministic short sequence makes the turn inspectable as motion, without music.
            c.ResetAll();c.FollowGameplaySpeed=false;c.SetTravelSpeed(3.2f);
            for(var i=0;i<c.Layers.Length;i++)c.RuntimeProfile.Layers[i].Enabled=i==2||i==5||i==6||i==8||i==10;
            Directory.CreateDirectory(Evidence+"motion");
            for(var frame=0;frame<64;frame++)
            {if(frame==12)course.Preset(Vector2.right*.9f);if(frame==40)course.Preset(Vector2.left*.9f);Capture("motion/frame-"+frame.ToString("D3"),c);yield return new WaitForSeconds(.0625f);}
            c.ResetAll();c.FollowGameplaySpeed=false;panel.Open=true;
            Require(errors.Count==0,"No runtime or shader errors in full validation");
        }
        private sealed class OrbitInput:IPlayerCommandSource{public PlayerCommandFrame ReadFrame()=>new PlayerCommandFrame(1,false,false);public void Reset(){}}
        private static object Get(string field)=>typeof(GameManager).GetField(field,Flags).GetValue(manager);
        private static void RequireFinite(Vector2 point){if(float.IsNaN(point.x)||float.IsNaN(point.y)||float.IsInfinity(point.x)||float.IsInfinity(point.y))throw new Exception("Non-finite optical flow");}
        private static void MathChecks()
        {
            var go=new GameObject("Course bounds QA");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=5;
            try
            {
                foreach(var aspect in new[]{.5625f,1f,1.777778f,3.555556f})
                {
                    camera.aspect=aspect;camera.orthographicSize=Mathf.Max(5,(OrbitSettings.Radius+.55f)/aspect);
                    var s=new CourseSteeringSettings();var course=new CourseController(camera,Vector2.zero,OrbitSettings.Radius,s);
                    for(var x=-1;x<=1;x++)for(var y=-1;y<=1;y++)
                    {
                        course.Preset(new Vector2(x,y)*20);for(var tick=0;tick<240;tick++)course.Tick(Vector2.zero,1f/60,true);
                        var center=course.WorldCourseCenter;
                        if(Mathf.Abs(center.x)+OrbitSettings.Radius+s.SafeScreenMargin>camera.orthographicSize*aspect+.001f||Mathf.Abs(center.y)+OrbitSettings.Radius+s.SafeScreenMargin>camera.orthographicSize+.001f)throw new Exception("Unsafe orbit bounds at aspect "+aspect);
                    }
                    Require(true,"Whole orbit + margin inside aspect "+aspect.ToString("F3"));
                    course.ResetInstant();course.Tick(Vector2.one*.01f,1);Require(course.TargetCourseCenter==course.NeutralCourseCenter,"Stick deadzone suppresses drift");
                    course.Tick(Vector2.right,1);var target=course.TargetCourseCenter;course.Tick(Vector2.left,1,true);Require(course.TargetCourseCenter==target,"Blocked input does not modify target");
                    s.ReturnToCenter=true;for(var tick=0;tick<300;tick++)course.Tick(Vector2.zero,1f/60);Require(Vector2.Distance(course.CurrentCourseCenter,course.NeutralCourseCenter)<.0001f,"Optional return settles at center");
                }
                var settings=new CourseSteeringSettings();var slow=new CourseController(camera,Vector2.zero,OrbitSettings.Radius,settings);var fast=new CourseController(camera,Vector2.zero,OrbitSettings.Radius,settings);
                for(var i=0;i<30;i++)slow.Tick(Vector2.right,1f/30);for(var i=0;i<120;i++)fast.Tick(Vector2.right,1f/120);
                Require(Vector2.Distance(slow.CurrentCourseCenter,fast.CurrentCourseCenter)<.004f,"30 / 120 FPS course response agrees within .004 viewport units");
            }
            finally{Object.Destroy(go);}
        }
        private static void ArtworkChecks(SpaceFlightVisualController c)
        {
            var s=c.RuntimeProfile.Layers[4];s.Appearance=SpaceLayerAppearance.Image;s.Texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/ship.png");s.Sprite=null;s.Material=null;s.PrimaryColor=Color.white;s.Brightness=1;s.MinAlpha=s.MaxAlpha=1;c.Tick(0);
            var renderer=c.Layers[4].Renderer;Require(s.Texture!=null&&renderer.sharedMaterial.mainTexture==s.Texture,"Texture override reaches real layer renderer");
            var asset=Object.Instantiate(c.RuntimeProfile);var json=EditorJsonUtility.ToJson(asset);var read=ScriptableObject.CreateInstance<SpaceDepthProfile>();EditorJsonUtility.FromJsonOverwrite(json,read);
            Require(read.Layers[4].Texture==s.Texture&&read.Layers[4].Appearance==SpaceLayerAppearance.Image&&read.Steering.MaxCourseOffsetX==asset.Steering.MaxCourseOffsetX,"Profile serialization preserves artwork and steering");Object.Destroy(asset);Object.Destroy(read);
            var binding=new SpaceLayerVisualBinding();var material=new Material(Resources.Load<Shader>("SpaceDepth/Shaders/SpaceDepthImage"));
            material.color=Color.red;var copy=s.Copy();copy.Material=material;binding.Refresh(copy);
            Require(binding.Instance!=material&&material.color==Color.red,"Custom material is cloned without editing the source");
            copy.Sprite=Sprite.Create(s.Texture,new Rect(0,0,s.Texture.width*.5f,s.Texture.height),Vector2.one*.5f);binding.Refresh(copy);
            Require(binding.UvMax.x<=.501f&&Mathf.Abs(binding.Aspect-s.Texture.width*.5f/s.Texture.height)<.001f,"Sprite rect UV and aspect override Texture");
            Object.Destroy(copy.Sprite);binding.Dispose();Object.Destroy(material);
        }
        private static void Benchmark(SpaceFlightVisualController c,float density)
        {
            foreach(var s in c.RuntimeProfile.Layers)s.Density=density;c.Course.AutoTest=CourseAutoTest.FigureEight;
            for(var i=0;i<10;i++){c.TickCourse(Vector2.zero,1f/60);c.Tick(1f/60);}
            var watch=System.Diagnostics.Stopwatch.StartNew();var start=GC.GetAllocatedBytesForCurrentThread();
            for(var i=0;i<240;i++){c.TickCourse(Vector2.zero,1f/60);c.Tick(1f/60);}
            var bytes=GC.GetAllocatedBytesForCurrentThread()-start;watch.Stop();
            report.Add("Density "+density+": "+c.ActiveElements+" elements; course + layer CPU "+(watch.Elapsed.TotalMilliseconds/240).ToString("F3")+" ms; GC bytes / 240 ticks "+bytes);
            Require(bytes==0,"Steady steering tick has zero managed allocations at density "+density);c.Course.AutoTest=CourseAutoTest.Off;
        }
        public static void Capture(string name,SpaceFlightVisualController c,bool onlySpace=true)
        {
            var source=Camera.main;var camera=source;GameObject isolated=null;
            if(onlySpace){isolated=new GameObject("Course QA camera");camera=isolated.AddComponent<Camera>();camera.CopyFrom(source);camera.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;}
            var target=camera.targetTexture;var active=RenderTexture.active;var mask=camera.cullingMask;
            var rt=RenderTexture.GetTemporary(1280,720,24);var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{if(onlySpace){foreach(var l in c.Layers)l.gameObject.layer=30;camera.cullingMask=1<<30;}camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes(Evidence+name+".png",tex.EncodeToPNG());}
            finally{foreach(var l in c.Layers)l.gameObject.layer=0;camera.cullingMask=mask;camera.targetTexture=target;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(tex);if(isolated!=null)Object.Destroy(isolated);}
        }
        private static void Require(bool ok,string value){if(!ok)throw new Exception(value);report.Add("PASS: "+value);}
        private static void Log(string value,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||(type==LogType.Warning&&value.Contains("ObjectField")))errors.Add(value);}
        private static void Call(string name)=>typeof(GameManager).GetMethod(name,Flags).Invoke(manager,null);
        private static void Set(string name,object value)=>typeof(GameManager).GetField(name,Flags).SetValue(manager,value);
    }
}
#endif
