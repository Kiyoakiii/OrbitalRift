#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OrbitalRift
{
    [InitializeOnLoad]
    public static class PlasmaBoltValidation
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string Pending = "OrbitalRift.PlasmaQA";
        private static GameManager manager;
        private static readonly List<string> report = new List<string>();
        private static readonly List<string> errors = new List<string>();
        private static string evidence;
        static PlasmaBoltValidation()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
                    EditorApplication.delayCall += Begin;
            };
        }

        [MenuItem("Orbital Rift/Spells/Validate Plasma Bolt in Sandbox")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play Mode before starting Plasma QA."); return; }
            PlasmaBoltAssets.CreateMissing();
            ValidateAssets();
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        public static void ValidateAssets()
        {
            var shot = AssetDatabase.LoadAssetAtPath<GameObject>(PlasmaBoltAssets.Spell + "Prefabs/PlasmaBolt.prefab");
            var impact = AssetDatabase.LoadAssetAtPath<GameObject>(PlasmaBoltAssets.Spell + "Prefabs/PlasmaBoltImpact.prefab");
            if (shot == null || impact == null) throw new Exception("Missing prefabs");
            foreach (var go in new[] { shot, impact })
            {
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0) throw new Exception("Missing script: " + t.name);
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                        if (m == null || m.shader == null || ShaderUtil.ShaderHasError(m.shader) || m.mainTexture == null)
                            throw new Exception("Invalid material: " + r.name);
            }
            var fx = shot.GetComponent<SpellProjectileVfx>();
            if (fx.Profile == null || fx.ImpactPrefab == null || fx.Core == null || fx.Glow == null || fx.MainTrail == null || fx.RibbonA == null || fx.RibbonB == null || fx.MainParticles == null || fx.MicroParticles == null)
                throw new Exception("Missing projectile layer reference");
            var hit = impact.GetComponent<SpellImpactVfx>();
            if (hit.Flash == null || hit.Ring == null || hit.Mist == null || hit.Aftermath == null || hit.Sparks == null)
                throw new Exception("Missing impact layer reference");
            Debug.Log("PLASMA: prefab references, scripts, textures and shaders PASS");
        }

        private static void Begin()
        {
            manager = Object.FindFirstObjectByType<GameManager>();
            if (manager == null) { EditorApplication.delayCall += Begin; return; }
            SessionState.SetBool(Pending, false);
            evidence = Path.GetFullPath("Docs/VFX_Evidence/PlasmaBolt"); Directory.CreateDirectory(evidence);
            report.Clear(); errors.Clear(); Application.logMessageReceived += Log;
            manager.StartCoroutine(Guarded());
        }

        private static IEnumerator Guarded()
        {
            yield return null; yield return null;
            var previousInput = Get<IPlayerCommandSource>("playerCommandSource");
            var test = Checks();
            while (true)
            {
                object current;
                try { if (!test.MoveNext()) break; current = test.Current; }
                catch (Exception ex) { errors.Add(ex.ToString()); break; }
                yield return current;
            }
            Set("playerCommandSource", previousInput); manager.enabled = true;
            Call("OpenAbilitySandbox");
            Set("autoFire", true);
            var session = Get<AbilitySandboxSession>("abilitySandbox");
            typeof(AbilitySandboxSession).GetField("loadoutOpen", Flags).SetValue(session, false);
            Application.logMessageReceived -= Log;
            report.Add(errors.Count == 0 ? "PASS" : "FAIL\n" + string.Join("\n", errors));
            File.WriteAllText(Path.Combine(evidence, "validation.txt"), string.Join("\n",report));
            Debug.Log("PLASMA QA " + (errors.Count == 0 ? "PASS" : "FAIL") + " " + evidence);
        }

        private static IEnumerator Checks()
        {
            Call("OpenAbilitySandbox");
            var input = new TestInput(); Set("playerCommandSource", input);
            var session = Get<AbilitySandboxSession>("abilitySandbox");
            typeof(AbilitySandboxSession).GetField("loadoutOpen", Flags).SetValue(session, false);
            var pool = Get<SpellVfxPool>("spellVfxPool");
            Require(pool != null, "Existing ObjectPool backs spell visuals");
            var dummy = Get<Enemy>("sandboxBoss"); var hp = dummy.Health; var score = Get<int>("score");
            Set("autoFire", true);
            yield return new WaitForSeconds(.21f);
            var live = Get<List<Projectile>>("projectiles").Find(p => p.SpellVfx != null);
            Require(live != null && live.SpellVfx.MainParticles.particleCount > 0 && live.SpellVfx.MicroParticles.particleCount > 0,
                "Both world-space particle scales actually emit in flight");
            Capture("01-stationary");
            yield return new WaitForSeconds(1.2f);
            Require(pool.ImpactCount > 0, "Sandbox shots hit dummy and emit separate impacts");
            Require(dummy.Health == hp && Get<int>("score") == score, "Sandbox leaves dummy health and score unchanged");
            input.Direction = 1;
            yield return new WaitForSeconds(.8f);
            Capture("02-moving-ship");
            var camera = Camera.main; var original = camera.transform.position;
            for (var frame=0;frame<12;frame++)
            {
                camera.transform.position = original + new Vector3(frame*.025f,frame*.015f,0);
                yield return new WaitForEndOfFrame();
            }
            Capture("03-camera-offset"); camera.transform.position = original;
            input.Direction = 0; Set("autoFire", false);
            yield return new WaitForSeconds(1.1f);
            var shots = Get<List<Projectile>>("projectiles");
            var impactCount = pool.ImpactCount;
            for (var i=0;i<7;i++)
            {
                var angle = i * Mathf.PI * 2 / 7;
                var origin = (Vector2)dummy.transform.position + new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*2.5f;
                Shoot(origin, ((Vector2)dummy.transform.position-origin).normalized*5);
            }
            yield return new WaitForSeconds(.18f); Capture("04-seven-projectiles");
            yield return new WaitForSeconds(.28f); Capture("05-overlapping-impacts");
            Require(pool.ImpactCount >= impactCount + 7, "Seven concurrent shots each produce one impact");
            // Shots fired while moving can miss the dummy and live for the full three seconds.
            for (var wait=0;wait<250&&(pool.ActiveImpacts>0||pool.ActiveShots>0);wait++)
                yield return new WaitForSeconds(.02f);
            Require(pool.ActiveImpacts == 0 && pool.ActiveShots == 0, "Trails and aftermath drain back into pools");

            // Exercise the production collision/damage method, outside Sandbox's update path.
            manager.enabled = false; session.Close();
            dummy.Health = 100; var before = pool.ImpactCount;
            Shoot((Vector2)dummy.transform.position + Vector2.down*2, Vector2.up*10);
            for (var i=0;i<12;i++) { Call("UpdateProjectiles", .025f); pool.Tick(.025f); }
            Require(dummy.Health < 100, "Production UpdateProjectiles/HitEnemies still applies weapon damage");
            Require(pool.ImpactCount == before+1, "Production hit creates exactly one Plasma impact");
            Require(Vector2.Distance(SpellVfxPool.ContactPoint(new Vector2(-5,0), new Vector2(5,0),Vector2.zero,1), Vector2.left)<.0001f,
                "Fast swept hits place impact on target surface, not beyond the target");
            Capture("06-gameplay-impact");

            before = pool.ImpactCount;
            Shoot(new Vector2(5,5), Vector2.right);
            var expiring = shots[shots.Count-1]; expiring.Life = .001f;
            Call("UpdateProjectiles", .02f); pool.Tick(.02f);
            Require(pool.ImpactCount == before, "Lifetime expiry does not masquerade as collision");
            Call("Cleanup");
            Require(pool.ActiveShots == 0 && pool.ActiveImpacts == 0, "Cleanup returns all VFX and restores projectile sprites");
            for (var i=0;i<3;i++)
            {
                Call("OpenAbilitySandbox"); Set("autoFire",false);
                Shoot(new Vector2(0,-3), Vector2.up*5);
                var p = shots[shots.Count-1]; var fx=p.SpellVfx;
                Require(fx!=null && fx.MainTrail.positionCount<=1 && fx.MainParticles.particleCount==0, "Pool reuse starts without old trails or particles #"+i);
                Call("Cleanup");
                Require(p.SpellVfx==null && p.Renderer.enabled, "Pool reuse restores fallback sprite #"+i);
            }
            Require(errors.Count==0, "No runtime exceptions or shader errors during checks");
        }

        private static void Shoot(Vector2 origin, Vector2 velocity)
        { Call("Shoot", origin,velocity,true,Color.cyan,DamageElement.Kinetic,1f,false); }
        private static void Capture(string name)
        {
            var camera = Camera.main; var previous=camera.targetTexture;
            var rt = RenderTexture.GetTemporary(1280,720,24,RenderTextureFormat.ARGB32);
            var active=RenderTexture.active;
            var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try { camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt; tex.ReadPixels(new Rect(0,0,1280,720),0,0); tex.Apply(); File.WriteAllBytes(Path.Combine(evidence,name+".png"),tex.EncodeToPNG()); }
            finally { camera.targetTexture=previous; RenderTexture.active=active; RenderTexture.ReleaseTemporary(rt); Object.Destroy(tex); }
        }
        private static void Log(string message,string trace,LogType type)
        { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert) errors.Add(message); }
        private static void Require(bool condition,string message)
        { if(!condition) throw new Exception(message); report.Add("PASS: "+message); }
        private static T Get<T>(string field) => (T)typeof(GameManager).GetField(field,Flags).GetValue(manager);
        private static void Set(string field,object value) => typeof(GameManager).GetField(field,Flags).SetValue(manager,value);
        private static void Call(string method,params object[] args) => typeof(GameManager).GetMethod(method,Flags).Invoke(manager,args);
        private sealed class TestInput : IPlayerCommandSource
        {
            public int Direction;
            public PlayerCommandFrame ReadFrame() => new PlayerCommandFrame(Direction,false,false);
            public void Reset() { Direction=0; }
        }
    }
}
#endif
