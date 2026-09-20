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
    public static class BossAssetRuntimeValidation
    {
        const string Evidence = "Docs/VFX_Evidence/BossAssets/";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly List<string> report = new List<string>();
        static GameManager manager;
        static BossAssetRuntimeValidation()
        {
            EditorApplication.update += () =>
            {
                const string command = "Temp/BossAssetQA.command";
                if (!File.Exists(command) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                File.Delete(command); Run();
            };
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("BossAssetRuntimeQA", false)) EditorApplication.delayCall += Begin;
            };
        }
        [MenuItem("Orbital Rift/Bosses/Validate attacks in Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before Boss Asset QA");
            Directory.CreateDirectory(Evidence);
            SessionState.SetBool("BossAssetRuntimeQA", true); EditorApplication.isPaused = false; EditorApplication.isPlaying = true;
        }
        static void Begin()
        {
            manager = Object.FindFirstObjectByType<GameManager>();
            if (manager == null) { EditorApplication.delayCall += Begin; return; }
            SessionState.SetBool("BossAssetRuntimeQA", false); Application.runInBackground = true;
            manager.StartCoroutine(Guard());
        }
        static IEnumerator Guard()
        {
            report.Clear(); Application.logMessageReceived += Log;
            var checks = Checks();
            while (true)
            {
                object next;
                try { if (!checks.MoveNext()) break; next = checks.Current; }
                catch (Exception ex) { report.Add("FAIL " + ex); break; }
                yield return next;
            }
            (checks as IDisposable)?.Dispose();
            if (manager != null) { manager.enabled = true; Call("StartGame"); }
            Application.logMessageReceived -= Log;
            report.Add(report.Exists(s => s.StartsWith("FAIL")) ? "FAIL" : "PASS");
            File.WriteAllText(Evidence + "runtime.txt", DateTime.Now.ToString("O") + "\n" + string.Join("\n", report));
            Debug.Log("BOSS ASSET RUNTIME QA " + report[report.Count - 1]);
        }
        static IEnumerator Checks()
        {
            var errors = new List<string>(); BossAssetValidator.Validate(errors);
            Require(errors.Count == 0, string.Join("\n", errors));
            report.Add("All boss/shot/ability links, prefabs, scripts and materials resolve.");
            Call("StartGame"); manager.enabled = false;
            Set("invincible", 10000f); Set("autoFire", false);
            var enemies = Get<List<Enemy>>("enemies");
            var shots = Get<List<Projectile>>("projectiles");
            var pool = Get<SpellVfxPool>("spellVfxPool");
            var defs = new[] { BossAssetRegistry.Get(BossArchetype.AstralFirebird), BossAssetRegistry.Get(BossArchetype.VoidMaw), BossAssetRegistry.Get(BossArchetype.UmbralHarrier) };
            for (var bossIndex = 0; bossIndex < defs.Length; bossIndex++)
            {
                Call("StartGame"); Set("phase", (bossIndex + 1) * 3); Set("warpTimer", 0f); Set("spawnsLeft", 0); Set("invincible", 10000f);
                Call("SpawnBoss"); var boss = enemies[0]; var definition = defs[bossIndex];
                Require(boss.Definition == definition && boss.MaxHealth == definition.MaxHp, "Boss prefab and HP use the definition");
                foreach (var ability in definition.Abilities)
                {
                    if (ability.SandboxOnly) continue;
                    ClearShots(shots, pool); Call("RemoveHarrierClones");
                    boss.Health = boss.MaxHealth = definition.MaxHp;
                    if (ability.Behaviour == BossAbilityBehaviour.RebirthEgg)
                    {
                        Call("BeginFirebirdEgg", boss);
                        Require(boss.Health == ability.Egg.Health && boss.BossStateTimer == ability.Duration, "Egg uses configured HP and duration");
                    }
                    else Call("BeginBossAbility", boss, ability, 1f, false);
                    var frames = Mathf.CeilToInt((ability.CastDelay + .48f) * 30);
                    for (var frame = 0; frame < frames; frame++)
                    {
                        Call("UpdateEnemies", 1f / 30); Call("UpdateProjectiles", 1f / 30); pool.Tick(1f / 30);
                        manager.SpaceDepth.Tick(1f / 30);
                        if (frame % 6 == 0) yield return null;
                    }
                    if (ability.Shot != null || ability.SecondaryAbility != null)
                        Require(shots.Count > 0 && shots.TrueForAll(s => s.Shot != null && s.SpellVfx != null), "Configured attacks use shot assets and pooled VFX: " + ability.name);
                    if (ability.Behaviour == BossAbilityBehaviour.Summon)
                        Require(enemies.FindAll(e => e.Kind == EnemyKind.ShadeClone).Count == ability.Summon.Count, "Summon count comes from ability asset");
                    Capture(ability.name);
                    report.Add(definition.BossId + "/" + ability.name + ": state=" + boss.BossState + ", shots=" + shots.Count);
                    if (ability.Behaviour == BossAbilityBehaviour.RebirthEgg)
                    {
                        Call("UpdateBoss", boss, ability.Duration);
                        Require(boss.Health == ability.Egg.ReviveHealth && boss.BossState == BossAiState.Orbit, "Phoenix revives with configured HP");
                        report.Add("Phoenix egg -> rebirth verified.");
                    }
                }
                foreach (var shot in definition.Shots)
                {
                    ClearShots(shots, pool);
                    var p = (Projectile)Call("SpawnConfiguredShot", shot, Vector2.zero, Vector2.up);
                    Require(p != null && p.Shot == shot && p.Damage == shot.Damage && Mathf.Approximately(p.Velocity.magnitude, shot.FlightSpeed(Get<int>("phase"))) && p.Life == shot.Lifetime,
                        "Shot gameplay values propagate: " + shot.name);
                    Require(p.SpellVfx != null && p.SpellVfx.Profile == shot.Vfx && p.SpellVfx.ImpactPrefab == shot.ImpactPrefab, "Shot -> prefab -> VFX -> impact links are used");
                }
            }
            ClearShots(shots, pool); Call("RemoveHarrierClones");
            // Mutate temporary asset copies only: prove Inspector fields affect runtime.
            var custom = Object.Instantiate(defs[0].Shots[0]);
            var profile = Object.Instantiate(custom.Vfx);
            var attack = Object.Instantiate(defs[0].InitialAbility);
            try
            {
                custom.Damage = 3; custom.Speed = 8; custom.ScaleSpeedWithPhase = false; custom.Lifetime = .6f;
                custom.Homing = true; custom.HomingDegreesPerSecond = 180; custom.ProjectileCount = 5;
                custom.FireInterval = .2f; custom.PlayerHitRadius = .12f; custom.ShieldCanBlock = false; custom.Vfx = profile;
                var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.red, 0), new GradientColorKey(Color.blue, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
                profile.TrailColor = gradient; profile.TrailLifetime = .42f;
                var p = (Projectile)Call("SpawnConfiguredShot", custom, Vector2.zero, Vector2.right);
                Require(p.Damage == 3 && p.Life == .6f && p.Velocity.magnitude == 8 && p.SpellVfx.MainTrail.time == .42f, "Edited shot damage/speed/lifetime/trail reach the pooled projectile");
                Require(p.SpellVfx.MainTrail.colorGradient.Evaluate(.5f) == gradient.Evaluate(.5f), "Editable trail gradient reaches TrailRenderer");
                Get<Transform>("player").position = new Vector2(0, 2);
                Call("TickConfiguredShot", p, .1f); Require(p.Velocity.y > 1, "Edited homing changes heading");
                Call("UpdateProjectiles", .7f); Require(!p.gameObject.activeSelf, "Edited lifetime actually releases the projectile");
                Set("invincible", 0f); Set("shields", 9); Call("ClearStarShields");
                p = (Projectile)Call("SpawnConfiguredShot", custom, new Vector2(-.2f, 2), Vector2.right);
                p.transform.position = new Vector2(.2f, 2);
                var hit = (bool)Call("HitPlayer", shots.IndexOf(p), p, new Vector2(-.2f, 2));
                Require(hit && Get<int>("shields") == 6, "Swept collision applies configured 3 damage");
                Set("invincible", 10000f); ClearShots(shots, pool);
                attack.Shot = custom; attack.FirstShotDelay = 0; attack.CastDelay = 0; attack.Duration = 3;
                var boss = enemies.Find(e => e.Kind == EnemyKind.Boss);
                Call("BeginBossAbility", boss, attack, 1f, false); Call("UpdateBoss", boss, .01f);
                Require(shots.Count == 5 && Mathf.Approximately(boss.FireTimer, .2f), "Edited shot count and fire interval affect the real boss attack");
                report.Add("Temporary asset edit proof: 3 damage, 8 units/s, 0.6s lifetime, homing, 5 projectiles, 0.2s interval, custom gradient, 0.42s trail: PASS.");
                ClearShots(shots, pool);
            }
            finally { Object.Destroy(custom); Object.Destroy(profile); Object.Destroy(attack); }
            Call("OpenAbilitySandbox");
            Call("TriggerAbilitySandboxSpell", AbilitySandboxAbilityId.SolarChicks);
            Require(shots.Count == defs[0].Ability(BossAbilityId.FirebirdSolarChicks).Shot.ProjectileCount && shots.TrueForAll(s => s.Shot != null && s.SpellVfx != null), "Sandbox Solar Chicks uses configured shots");
            for (var frame = 0; frame < 12; frame++) { Call("UpdateAbilitySandboxProjectiles", 1f / 30); pool.Tick(1f / 30); yield return null; }
            Capture("Sandbox-SolarChicks");
            report.Add("Sandbox Solar Chicks asset references and flight: PASS.");
            Call("ExitAbilitySandbox");
            // User workflow: duplicate data, assign images/frames/materials, reference from sequence.
            Call("StartGame");
            foreach(var mob in GameRules.Current.Mobs)
            {
                var e=(Enemy)Call("SpawnConfiguredMob",mob,false);
                Require(e.Mob==mob && e.Health==mob.Health,"Mob health and definition "+mob.name);
                var position=e.transform.position;Call("MoveConfiguredMob",e,0,.2f);
                Require(e.transform.position!=position,"Mob movement "+mob.name);
                var shot=(Projectile)Call("SpawnConfiguredShot",mob.Shot,Vector2.zero,Vector2.up);
                Require(shot.Damage==mob.Shot.Damage,"Mob shot damage "+mob.name);
                Call("TickMobVisualsAndSpells",e,.2f,true);
            }
            report.Add("Four mob assets: Classic movement, HP, configured shots and Defense tick PASS.");
            // Exercise the actual Defense spawning and movement loop with the saved player identity.
            Call("BeginDefenseMode");
            Require(Get<bool>("defensePlaying"),"Defense launches");
            Call("BeginDefenseWave");Call("SpawnDefenseEnemy");Set("defenseIntermissionTimer",0f);
            var defenseEnemies=Get<List<Enemy>>("enemies");var defender=defenseEnemies[0];var beforeDefense=defender.transform.position;
            Call("UpdateDefenseMode",.1f);
            Require(defender.Mob!=null&&defender.transform.position!=beforeDefense,"Defense uses mob definition and approach movement");
            Capture("Defense-assets");Call("ExitDefenseMode");
            report.Add("Actual Defense mode launch, wave spawn and movement: PASS.");
            var customBoss=Object.Instantiate(defs[0]);
            var customSpell=Object.Instantiate(defs[0].InitialAbility);
            var savedBoss=GameRules.Current.Classic.Steps[0].Boss;
            try
            {
                customBoss.BossId="qa-new-boss";customBoss.DisplayName="QA NEW BOSS";customBoss.UseLegacyPresentation=false;
                customBoss.InitialAbility=customSpell;customBoss.Abilities=new[]{customSpell};
                customBoss.Appearance.Enabled=true;customBoss.Appearance.Frames=new[]{defs[0].Sprite,defs[1].Sprite};customBoss.Appearance.FramesPerSecond=2;customBoss.Appearance.Size=1.3f;
                customBoss.Appearance.AuraSprite=defs[0].Sprite;customBoss.Appearance.AuraColor=new Color(.2f,.8f,1,.2f);
                GameRules.Current.Classic.Steps[0].Boss=customBoss;
                Call("StartGame");Call("SpawnBoss");
                var e=Get<List<Enemy>>("enemies")[0];
                Require(e.Definition==customBoss,"Sequence spawns arbitrary BossDefinition without enum registration");
                var image=e.transform.Find("Configured image").GetComponent<SpriteRenderer>();
                e.AppearanceView.Tick(0);Require(image.sprite==defs[0].Sprite,"Animation frame 0");
                e.AppearanceView.Tick(.6f);Require(image.sprite==defs[1].Sprite,"Animation frame 1");
                Require(!e.Renderer.enabled&&image.enabled,"Custom image replaces body");
                Capture("Custom-boss-animation");
                report.Add("New boss via sequence + independent copied spell + PNG frames + aura: PASS.");
            }
            finally { GameRules.Current.Classic.Steps[0].Boss=savedBoss;Object.Destroy(customBoss);Object.Destroy(customSpell); }

        }
        static void ClearShots(List<Projectile> shots, SpellVfxPool pool) { while (shots.Count > 0) Call("RemoveProjectile", shots.Count - 1); pool.Clear(); }
        static void Capture(string name) => typeof(SpaceReferenceArtValidation).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { "../BossAssets/" + name, manager.SpaceDepth, 1280, 720, false });
        static object Call(string method, params object[] args) => typeof(GameManager).GetMethod(method, Private).Invoke(manager, args);
        static T Get<T>(string field) => (T)typeof(GameManager).GetField(field, Private).GetValue(manager);
        static void Set(string field, object value) => typeof(GameManager).GetField(field, Private).SetValue(manager, value);
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        static void Log(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.Add("FAIL " + message); }
    }
}
#endif
