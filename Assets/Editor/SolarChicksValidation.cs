#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace OrbitalRift
{
    [InitializeOnLoad]
    public static class SolarChicksValidation
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        private const string Pending="OrbitalRift.SolarChicksQA";
        private static GameManager manager;
        private static readonly List<string> report=new List<string>(), errors=new List<string>();
        private static readonly string Evidence=Path.GetFullPath("Docs/VFX_Evidence/SolarChicks");
        static SolarChicksValidation()
        {
            EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Pending,false))EditorApplication.delayCall+=Begin;};
        }
        [MenuItem("Orbital Rift/Spells/Validate Solar Chicks in Sandbox")]
        public static void Run()
        {
            if(EditorApplication.isPlaying){Debug.LogWarning("Stop Play Mode before Solar Chicks QA.");return;}
            SolarChicksAssets.CreateMissing();ValidateAssets();
            if(!EditorSceneManager.GetActiveScene().path.EndsWith("Boot.unity"))
                EditorSceneManager.OpenScene("Assets/Scenes/Boot.unity");
            SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
        }
        private static void ValidateAssets()
        {
            foreach(var name in new[]{"SolarChick","SolarChickImpact"})
            {
                var root=AssetDatabase.LoadAssetAtPath<GameObject>(SolarChicksAssets.Root+"Prefabs/"+name+".prefab");
                if(root==null)throw new Exception("Missing prefab "+name);
                foreach(var t in root.GetComponentsInChildren<Transform>(true))
                    if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)throw new Exception("Missing script "+t.name);
                foreach(var r in root.GetComponentsInChildren<Renderer>(true))
                    foreach(var mat in r.sharedMaterials)
                        if(mat==null||mat.mainTexture==null||mat.shader==null||ShaderUtil.ShaderHasError(mat.shader))throw new Exception("Invalid material "+r.name);
            }
            var fx=AssetDatabase.LoadAssetAtPath<SpellProjectileVfx>(SolarChicksAssets.Root+"Prefabs/SolarChick.prefab");
            if(fx.Profile==null||fx.ImpactPrefab==null||fx.GetComponent<SolarChickMotion>().Bird==null)throw new Exception("Missing Solar references");
            PlasmaBoltValidation.ValidateAssets();
            Debug.Log("SOLAR ASSETS PASS: own textures/materials/prefabs, shared runtime scripts, valid shaders.");
        }
        private static void Begin()
        {
            manager=Object.FindFirstObjectByType<GameManager>();if(manager==null){EditorApplication.delayCall+=Begin;return;}
            SessionState.SetBool(Pending,false);Directory.CreateDirectory(Evidence);report.Clear();errors.Clear();
            Application.logMessageReceived+=Log;manager.StartCoroutine(Guarded());
        }
        private static IEnumerator Guarded()
        {
            yield return null;yield return null;
            var previous=Get<IPlayerCommandSource>("playerCommandSource");var sequence=Checks();
            while(true)
            {
                object current;
                try{if(!sequence.MoveNext())break;current=sequence.Current;}
                catch(Exception ex){errors.Add(ex.ToString());break;}
                yield return current;
            }
            manager.enabled=true;Set("playerCommandSource",previous);
            Call("OpenAbilitySandbox");Set("autoFire",false);
            Call("TriggerAbilitySandboxSpell",AbilitySandboxAbilityId.SolarChicks);
            Application.logMessageReceived-=Log;
            report.Add(errors.Count==0?"PASS":"FAIL\n"+string.Join("\n",errors));
            File.WriteAllText(Path.Combine(Evidence,"validation.txt"),string.Join("\n",report));
            Debug.Log("SOLAR CHICKS QA "+(errors.Count==0?"PASS":"FAIL")+" "+Evidence);
        }
        private static IEnumerator Checks()
        {
            Call("OpenAbilitySandbox");var input=new TestInput();Set("playerCommandSource",input);Set("autoFire",false);
            var session=Get<AbilitySandboxSession>("abilitySandbox");var pool=Get<SpellVfxPool>("spellVfxPool");
            var shots=Get<List<Projectile>>("projectiles");var hp=Get<int>("shields");var score=Get<int>("score");
            var before=pool.ImpactCount;
            Call("TriggerAbilitySandboxSpell",AbilitySandboxAbilityId.SolarChicks);
            Require(shots.Count(p=>p.VisualStyle==ProjectileVisualStyle.FirebirdChick)==3,"Existing SolarChicks activation launches exactly three birds");
            Require(shots.All(p=>p.SpellVfx!=null&&!p.FirebirdChickVisual),"Shared prefab replaces old procedural bird layers without duplicate renderers");
            var bird=shots[0];var fx=bird.SpellVfx;var motion=fx.GetComponent<SolarChickMotion>();
            yield return new WaitForSeconds(.55f);Capture("01-cast-and-flight");
            Require(fx.MainParticles.particleCount>0&&fx.MicroParticles.particleCount>0,"PNG atlas fragments and micro particles both emit");
            Require(motion.Bird.GetComponent<MeshFilter>().sharedMesh.vertexCount>100,"PNG bird has a subdivided deformation mesh");
            var phase=motion.WingPhase;Capture("02-wing-a",fx.transform.position,1.1f);
            yield return new WaitForSeconds(.11f);Capture("03-wing-b",fx.transform.position,1.1f);
            Require(Mathf.Abs(motion.WingPhase-phase)>.1f,"Bird wing animation progresses independently of projectile velocity");
            for(var wait=0;wait<150&&pool.ImpactCount==before;wait++)
                yield return new WaitForSeconds(.02f);
            Require(pool.ImpactCount>before,"Sandbox chick contact emits a separate warm impact");
            Require(Get<int>("shields")==hp&&Get<int>("score")==score,"Sandbox chick collision leaves player shields and score unchanged");
            input.Direction=1;Call("TriggerAbilitySandboxSpell",AbilitySandboxAbilityId.SolarChicks);
            yield return new WaitForSeconds(.3f);Capture("04-moving-player");input.Direction=0;
            Call("Cleanup");Call("OpenAbilitySandbox");Set("autoFire",false);

            // Both families in the same shared pool must retain their own profile/materials.
            Call("TriggerAbilitySandboxSpell",AbilitySandboxAbilityId.SolarChicks);
            Call("Shoot",new Vector2(0,-3),Vector2.up*5,true,Color.cyan,DamageElement.Kinetic,1f,false);
            Require(shots.Any(p=>p.FromPlayer&&p.SpellVfx!=null&&p.SpellVfx.Profile.name=="PlasmaBolt"),"Plasma Bolt still uses its original profile in mixed casting");
            Require(shots.Any(p=>!p.FromPlayer&&p.SpellVfx!=null&&p.SpellVfx.Profile.name=="SolarChicks"),"Solar Chicks uses its own profile in mixed casting");
            yield return new WaitForSeconds(.2f);Capture("05-mixed-spells");

            // Production hostile hit path, outside Sandbox's no-damage collision loop.
            manager.enabled=false;session.Close();Call("Cleanup");Set("invincible",0f);Set("shields",3);Set("starShields",0);
            var player=Get<Transform>("player");before=pool.ImpactCount;
            Spawn(player.position,Vector2.right*.1f);
            var hostile=shots[shots.Count-1];Call("HitPlayer",shots.Count-1,hostile);
            Require(Get<int>("shields")==2,"Production HitPlayer retains normal damage");
            Require(pool.ImpactCount==before+1,"Production hostile hit creates exactly one Solar impact");
            pool.Tick(.08f);Capture("06-gameplay-impact",player.position,1.4f);
            before=pool.ImpactCount;Spawn(new Vector2(5,5),Vector2.up);shots[shots.Count-1].Life=.001f;
            Call("UpdateProjectiles",.02f);pool.Tick(.02f);
            Require(pool.ImpactCount==before,"Expiry does not create false Solar impacts");Call("Cleanup");
            for(var pass=0;pass<3;pass++)
            {
                Spawn(new Vector2(0,-2),Vector2.up);
                var p=shots[shots.Count-1];var visual=p.SpellVfx;var anim=visual.GetComponent<SolarChickMotion>();
                Require(visual.MainTrail.positionCount<=1&&visual.MainParticles.particleCount==0&&anim.Bird.enabled,"Reused chick starts clean with visible bird #"+pass);
                Call("Cleanup");Require(p.SpellVfx==null&&p.Renderer.enabled&&!anim.Bird.enabled,"Cleanup restores base projectile and hides PNG layers #"+pass);
            }
            Require(pool.ActiveShots==0&&pool.ActiveImpacts==0,"All mixed VFX returned to correct pools");

            manager.enabled=true;Call("OpenAbilitySandbox");Set("autoFire",false);
            session.VfxEditor.Open(session.Catalog,AbilitySandboxAbilityId.SolarChicks);
            yield return new WaitForSeconds(.3f);
            var preview=Object.FindFirstObjectByType<SpellVfxPreview>();
            Require(preview!=null&&preview.SourcePrefab.name=="SolarChick","Existing Layers panel renders the real SolarChick prefab");
            Require(preview.GetComponentsInChildren<Renderer>(true).All(r=>r.gameObject.layer==31),"Workshop prefab remains on the isolated preview layer");
            Capture("07-workshop-prefab");
            preview.Pool.SetLayerMask(0);
            Require(preview.GetComponentsInChildren<Renderer>().All(r=>!r.enabled||r.GetComponent<SpriteRenderer>()!=null),"All eight layer switches can hide the shared preview renderers");
            session.VfxEditor.Close();yield return null;
            Require(preview.Pool.ActiveShots==0&&preview.Pool.ActiveImpacts==0,"Leaving Layers panel clears its preview pool");
            Require(errors.Count==0,"No runtime exceptions during Solar Chicks regression");
        }
        private static void Spawn(Vector2 p,Vector2 velocity)
        {Call("ShootStyledHostile",p,velocity,new Color(1,.5f,.1f),DamageElement.Fire,.62f,Get<Sprite>("firebirdChickProjectileSprite"),false);}
        private static void Capture(string name,Vector3? center=null,float size=0)
        {
            var camera=Camera.main;var oldTarget=camera.targetTexture;var oldPosition=camera.transform.position;var oldSize=camera.orthographicSize;
            var rt=RenderTexture.GetTemporary(1280,720,24,RenderTextureFormat.ARGB32);var oldActive=RenderTexture.active;var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try
            {
                if(center.HasValue){camera.transform.position=new Vector3(center.Value.x,center.Value.y,oldPosition.z);camera.orthographicSize=size;}
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(Evidence,name+".png"),tex.EncodeToPNG());
            }
            finally{camera.targetTexture=oldTarget;camera.transform.position=oldPosition;camera.orthographicSize=oldSize;RenderTexture.active=oldActive;RenderTexture.ReleaseTemporary(rt);Object.Destroy(tex);}
        }
        private static void Require(bool ok,string text){if(!ok)throw new Exception(text);report.Add("PASS: "+text);}
        private static void Log(string text,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(text);}
        private static T Get<T>(string field)=>(T)typeof(GameManager).GetField(field,Flags).GetValue(manager);
        private static void Set(string field,object value)=>typeof(GameManager).GetField(field,Flags).SetValue(manager,value);
        private static object Call(string method,params object[] args)=>typeof(GameManager).GetMethods(Flags).Single(m=>m.Name==method&&m.GetParameters().Length==args.Length).Invoke(manager,args);
        private sealed class TestInput:IPlayerCommandSource{public int Direction;public PlayerCommandFrame ReadFrame()=>new PlayerCommandFrame(Direction,false,false);public void Reset(){Direction=0;}}
    }
}
#endif
