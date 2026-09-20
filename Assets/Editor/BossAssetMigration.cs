#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OrbitalRift
{
    // One-time migration. Existing completed boss assets are never overwritten.
    [InitializeOnLoad]
    public static class BossAssetMigration
    {
        const string Root = "Assets/Resources/Bosses/";
        static string folder;
        static BossDefinition current;
        static BossAssetMigration()
        {
            EditorApplication.update += () =>
            {
                const string request = "Temp/BossAssets.command";
                if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                var command = File.ReadAllText(request).Trim(); File.Delete(request);
                try
                {
                    if (command == "migrate") CreateAssets();
                    if (command == "finalize") FinalizeLinks();
                    if (command == "last-build")
                    {
                        var report = UnityEditor.Build.Reporting.BuildReport.GetLatestReport();
                        var lines = new System.Collections.Generic.List<string>();
                        if (report != null) foreach (var step in report.steps) foreach (var msg in step.messages)
                            if (msg.type == LogType.Error || msg.type == LogType.Exception) lines.Add(step.name + ": " + msg.content);
                        Directory.CreateDirectory("Docs/VFX_Evidence/GameplayRestoration");
                        File.WriteAllLines("Docs/VFX_Evidence/GameplayRestoration/build-errors.txt", lines);
                    }
                }
                catch (Exception ex) { File.WriteAllText("Temp/BossAssets.error.txt", ex.ToString()); Debug.LogException(ex); }
            };
        }
        [MenuItem("Orbital Rift/Bosses/Create missing boss assets")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before migration.");
            BuildPhoenix(); BuildVoid(); BuildHarrier();
            AssetDatabase.SaveAssets(); BossAssetRegistry.Reset();
            File.WriteAllText("Temp/BossAssets.migrated.txt", DateTime.Now.ToString("O"));
            Debug.Log("BOSS ASSETS migration complete: Phoenix, Void Maw, Umbral Harrier.");
        }
        static bool StartBoss(string name, BossArchetype type, string title, string sprite, float hp, Color primary, Color secondary)
        {
            folder = Root + name + "/";
            foreach (var child in new[] { "Boss", "Shots", "Abilities", "VFX", "Prefabs" }) Directory.CreateDirectory(folder + child);
            AssetDatabase.Refresh();
            current = AssetDatabase.LoadAssetAtPath<BossDefinition>(folder + "Boss/" + name + "Boss.asset");
            if (current != null && current.Phases.Length > 0) return false;
            if (current == null) current = Asset<BossDefinition>("Boss/" + name + "Boss");
            current.BossId = name; current.Archetype = type; current.DisplayName = title; current.MaxHp = hp;
            current.Sprite = Sprite(sprite); current.Prefab = ActorPrefab<Enemy>(name + "Boss", current.Sprite);
            current.Theme = Style(primary, secondary);
            current.Presentation = type == BossArchetype.AstralFirebird
                ? AssetDatabase.LoadAssetAtPath<BossVfxProfile>("Assets/Resources/BossAbilities/Profiles/AstralFirebird.asset")
                : Asset<BossVfxProfile>("VFX/" + name + "Presentation");
            return true;
        }
        static void BuildPhoenix()
        {
            if (!StartBoss("Phoenix", BossArchetype.AstralFirebird, "ЖАР-ПТИЦА\nПЕПЕЛЬНАЯ КОРОНА", "boss_astral_firebird", 36, new Color(1,.18f,.04f), new Color(1,.84f,.16f))) return;
            current.FireResistance = .64f; current.ColdResistance = 1f; current.PoisonResistance = 1.28f;
            var main = Shot("Phoenix_Ember", "Одиночный уголь", 1, 0, .88f, 1.18f, .16f, DamageElement.Fire, new Color(1,.72f,.16f));
            var chicks = Shot("Phoenix_Chick", "Солнечный птенец", 3, 19, .58f, 1.06f, .62f, DamageElement.Fire, new Color(1,.44f,.07f), true);
            var ring = Shot("Phoenix_DiveRing", "Кольцо нырка", 5, 0, .74f, .9f, .16f, DamageElement.Fire, new Color(1,.23f,.04f));
            ring.Pattern = BossShotPattern.Radial; ring.VisualStyle = ProjectileVisualStyle.SolarLance;
            var orbit = Ability("Phoenix_MainShot", BossAbilityId.FirebirdMainShot, "ПРИЦЕЛЬНЫЙ УГОЛЬ", BossAiState.Orbit, BossAbilityBehaviour.Projectiles, 2.75f, .24f, main);
            orbit.ShowInGuide = false; orbit.Movement = Movement(2.45f,1.8f,1.08f,.72f,.22f,1.9f);
            var young = Ability("Phoenix_Chicks", BossAbilityId.FirebirdSolarChicks, "СОЛНЕЧНЫЕ ПТЕНЦЫ", BossAiState.Barrage, BossAbilityBehaviour.Projectiles, 3.15f, .18f, chicks, "firebird_solar_chicks");
            young.Description = "Веер анимированных птенцов. Shot задаёт число, урон и полёт; VFX — крылья, хвост и угасание.";
            young.Movement = Movement(2.72f,1.5f,1.05f,0,0,0,true);
            young.Vfx = chicks.Vfx;
            var egg = Ability("Phoenix_AshenEgg", BossAbilityId.FirebirdAshenEgg, "ПЕПЕЛЬНОЕ ЯЙЦО", BossAiState.Egg, BossAbilityBehaviour.RebirthEgg, 5, 99, null, "firebird_ashen_egg");
            egg.Description = "Одно перерождение: после обнуления HP начинается окно уничтожения яйца. По истечении Duration птица восстанавливает Revive Health.";
            egg.Movement = Movement(.8f,1.8f,.26f,0,0,0,true);
            egg.Style.Primary = new Color(1,.64f,.12f,.98f); egg.Style.Highlight = new Color(1,.98f,.62f);
            var dive = Ability("Phoenix_Dive", BossAbilityId.FirebirdPhoenixDive, "КОМЕТНЫЙ НЫРОК", BossAiState.Charge, BossAbilityBehaviour.Dash, 2.45f, .12f, ring, "firebird_phoenix_dive");
            dive.Cooldown = 2.45f; dive.MaxHealthFraction = .58f; dive.Movement = Movement(1.18f,1.35f,1.45f); dive.Movement.MoveTowardsRadius = true;
            current.InitialAbility = orbit; current.Shots = new[] { main, chicks, ring }; current.Abilities = new[] { young, egg, dive, orbit };
            current.Phases = new[] { Phase("Раненая птица",.58f, Entry(dive,.46f),Entry(young,.58f),Entry(orbit)), Phase("Полное здоровье",1,Entry(young,.58f),Entry(orbit)) };
            SaveBoss();
        }
        static void BuildHarrier()
        {
            if (!StartBoss("UmbralHarrier", BossArchetype.UmbralHarrier, "ТЕНЕВОЙ ОХОТНИК\nКОПИИ РАЗЛОМА", "boss_umbral_harrier",225,new Color(.55f,.18f,1),new Color(.68f,.38f,1))) return;
            current.FireResistance = 1.22f; current.ColdResistance = .70f; current.PoisonResistance = 1f;
            var main = Shot("Harrier_MainShard", "Прицельный осколок",1,0,.82f,1.16f,.15f,DamageElement.Cold,new Color(.74f,.42f,1));
            var fan = Shot("Harrier_ColdFan", "Холодный веер",2,12,.95f,1.18f,.135f,DamageElement.Cold,new Color(.62f,.23f,1));
            var dash = Shot("Harrier_DashRing", "Осколки рывка",3,0,1.15f,.96f,.115f,DamageElement.Cold,new Color(.76f,.35f,1)); dash.Pattern = BossShotPattern.Radial;
            var cloneShot = Shot("Harrier_CloneShard", "Выстрел двойника",1,0,1.15f,1.02f,.105f,DamageElement.Cold,new Color(.58f,.22f,1));
            foreach (var shot in new[] { main,fan,dash,cloneShot }) shot.VisualStyle = ProjectileVisualStyle.HarrierShard;
            var hunt = Ability("Harrier_Hunt",BossAbilityId.HarrierMainShot,"ПРИЦЕЛЬНЫЙ ОСКОЛОК",BossAiState.Orbit,BossAbilityBehaviour.Projectiles,2.4f,.24f,main);
            hunt.ShowInGuide = false; hunt.Movement = Movement(2.45f,2.1f,1.35f,-.92f,.17f,2.1f);
            var cold = Ability("Harrier_ColdFan",BossAbilityId.HarrierColdFan,"ХОЛОДНЫЙ ВЕЕР",BossAiState.Barrage,BossAbilityBehaviour.Projectiles,3.8f,.2f,fan,"harrier_cold_fan");
            cold.Movement = Movement(2.64f,2,1.7f,0,0,0,true);
            var copies = Ability("Harrier_RiftCopies",BossAbilityId.HarrierRiftCopies,"КОПИИ РАЗЛОМА",BossAiState.Barrage,BossAbilityBehaviour.Summon,3.8f,.2f,null,"harrier_rift_copies");
            copies.Cooldown = 4.2f; copies.SecondaryAbility = cold; copies.Movement = cold.Movement;
            copies.Summon.Prefab = ActorPrefab<Enemy>("HarrierClone",current.Sprite); copies.Summon.Sprite = current.Sprite; copies.Summon.Shot = cloneShot;
            copies.Description = "Призывает уничтожаемых двойников. Параллельная атака задаётся Secondary Ability → Холодный веер.";
            var jump = Ability("Harrier_PhaseDash",BossAbilityId.HarrierPhaseDash,"ФАЗОВЫЙ РЫВОК",BossAiState.Dash,BossAbilityBehaviour.Dash,2.1f,.10f,dash,"harrier_phase_dash");
            jump.Cooldown = 3; jump.Movement = Movement(1.08f,2.15f,2.72f); jump.Movement.MoveTowardsRadius = true;
            current.InitialAbility = hunt; current.Shots = new[] { main,fan,dash,cloneShot }; current.Abilities = new[] { copies,jump,cold,hunt };
            current.Phases = new[] { Phase("Ярость",.5f,Entry(copies,.58f),Entry(jump,1,2.6f/2.1f)),Phase("Охота",1,Entry(copies,.58f),Entry(jump)) };
            SaveBoss();
        }
        static void BuildVoid()
        {
            if (!StartBoss("VoidMaw",BossArchetype.VoidMaw,"ПАСТЬ БЕЗДНЫ\nПЕРВЫЙ РАЗЛОМ","boss_void_maw",180,new Color(.16f,.86f,1),new Color(.96f,.22f,1))) return;
            current.FireResistance=.65f; current.ColdResistance=1.35f; current.PoisonResistance=.8f;
            var aim=Shot("Void_AimedBurst","Прицельная пара",2,17,.95f,1.05f,.18f,DamageElement.Cold,new Color(.25f,.9f,1));
            var barrage=Shot("Void_Barrage","Залп бездны",3,15,.55f,1.05f,.18f,DamageElement.Poison,new Color(.68f,.34f,1));
            var radial=Shot("Void_ChargeRing","Кольцо сближения",6,0,.72f,1.05f*.86f,.18f,DamageElement.Fire,new Color(.68f,.34f,1)); radial.Pattern=BossShotPattern.Radial;
            foreach(var shot in new[]{aim,barrage,radial})shot.VisualStyle=ProjectileVisualStyle.VoidPulse;
            var orbit=Ability("Void_AimedBurst",BossAbilityId.VoidMainShot,"ПРИЦЕЛЬНЫЙ ЗАЛП",BossAiState.Orbit,BossAbilityBehaviour.Projectiles,3.1f,.28f,aim);
            orbit.ShowInGuide=false; orbit.Movement=Movement(2.45f,1.5f,.85f,.82f,.2f,1.6f);
            var rain=Ability("Void_Barrage",BossAbilityId.VoidBarrage,"ЗАЛП БЕЗДНЫ",BossAiState.Barrage,BossAbilityBehaviour.Projectiles,2.8f,.12f,barrage,"void_barrage");
            rain.Movement=Movement(2.75f,1.8f,1.55f,0,0,0,true);
            var charge=Ability("Void_Charge",BossAbilityId.VoidCharge,"СБЛИЖЕНИЕ БЕЗДНЫ",BossAiState.Charge,BossAbilityBehaviour.Dash,2.15f,.15f,radial);
            charge.MaxHealthFraction=.48f; charge.Movement=Movement(1.12f,1.45f,1.8f); charge.Movement.MoveTowardsRadius=true;
            var beam=Ability("Void_RiftBeam",BossAbilityId.VoidRiftBeam,"ЛУЧ РАЗЛОМА",BossAiState.BeamTelegraph,BossAbilityBehaviour.Beam,4.35f,0,null,"void_rift_beam");
            beam.CastDelay=1.05f; beam.Cooldown=4.35f; beam.UseCastMovement=true;
            beam.Movement=Movement(2.38f,1.65f,.20f,0,0,0,true); beam.CastMovement=Movement(2.28f,2,.24f,0,0,0,true);
            var roots=Ability("Void_GravityRoots",BossAbilityId.VoidGravityRoots,"ГРАВИКОРНИ",BossAiState.RootTelegraph,BossAbilityBehaviour.Roots,1,99,null,"void_gravity_roots");
            roots.CastDelay=1.05f; roots.Cooldown=3.8f; roots.Roots.HalfWidthDegrees=OrbitalAbilitySettings.RootCatchHalfWidthDegrees;
            roots.Roots.LockDuration=OrbitalAbilitySettings.RootHoldDuration; roots.Style=Style(new Color(1,.28f,.76f),new Color(.68f,.18f,.88f));
            roots.UseCastMovement=true; roots.CastMovement=Movement(2.10f,1.8f,.18f,0,0,0,true); roots.Movement=Movement(2.18f,2.2f,.1f,0,0,0,true);
            current.InitialAbility=orbit; current.Shots=new[]{aim,barrage,radial}; current.Abilities=new[]{beam,roots,rain,charge,orbit};
            current.Phases=new[]{Phase("Открытая бездна",.48f,Entry(roots,.29f),Entry(beam,.47f),Entry(charge,.52f),Entry(rain,.55f),Entry(orbit)),Phase("Разлом",.5f,Entry(roots,.29f),Entry(beam,.47f),Entry(rain,.55f),Entry(orbit)),Phase("Наблюдение",1,Entry(roots,.18f),Entry(beam,.3f),Entry(rain,.55f),Entry(orbit))};
            SaveBoss();
        }
        static BossShotDefinition Shot(string id,string title,int count,float spread,float interval,float multiplier,float scale,DamageElement element,Color color,bool chick=false)
        {
            var shot=Asset<BossShotDefinition>("Shots/"+id); shot.ShotId=id; shot.DisplayName=title;
            shot.ProjectileCount=count; shot.SpreadDegrees=spread; shot.FireInterval=interval; shot.Speed=BalanceSettings.EnemyProjectileSpeed(1)*multiplier;
            shot.Scale=scale; shot.Element=element; shot.Prefab=ActorPrefab<Projectile>(id,null);
            var basePrefab=Resources.Load<SpellProjectileVfx>(chick?"Spells/SolarChicks/Prefabs/SolarChick":"Spells/PlasmaBolt/Prefabs/PlasmaBolt");
            var profile=Object.Instantiate(basePrefab.Profile); profile.name=id+"VFX";
            if(!chick)
            {
                profile.CoreColor=Color.Lerp(color,Color.white,.55f); profile.GlowColor=new Color(color.r,color.g,color.b,.26f);
                profile.RibbonColor=color; profile.ParticleColor=color; profile.ImpactColor=color;
                profile.CoreSize=new Vector2(scale,scale*1.7f); profile.GlowSize=profile.CoreSize*2.1f;
                profile.TrailWidth=scale*.45f; profile.TrailLifetime=.16f; profile.ParticleCount=10; profile.MicroCount=5;
                profile.TrailColor=Gradient(Color.Lerp(color,Color.white,.65f),color,new Color(color.r*.25f,color.g*.2f,color.b*.35f),.85f);
            }
            profile.UseLifetimeGradients=true;
            profile.CoreOverLife=Gradient(Color.white,Color.white,Color.white,1,false);
            profile.GlowOverLife=Gradient(Color.white,Color.white,Color.white,1,false);
            profile.ParticleOverLife=Gradient(Color.white,Color.Lerp(color,Color.white,.65f),color,.85f);
            profile.ImpactOverLife=Gradient(Color.white,Color.Lerp(color,Color.white,.8f),Color.white,1,false);
            profile.RibbonOverTrail=profile.TrailColor;
            profile.AlphaOverLife=new AnimationCurve(new Keyframe(0,1),new Keyframe(.84f,1),new Keyframe(1,0));
            profile.GlowFade=new AnimationCurve(new Keyframe(0,1),new Keyframe(.75f,1),new Keyframe(1,0));
            profile.ImpactFade=AnimationCurve.Linear(0,1,1,1);
            if(chick)
            {
                var motion=basePrefab.GetComponent<SolarChickMotion>(); profile.UseBirdMotionSettings=true;
                profile.BirdSize=motion.BirdSize; profile.WingBeatSpeed=motion.WingBeatSpeed; profile.WingFold=motion.WingFold;
                profile.BodyBob=motion.BodyBob; profile.BankAngle=motion.BankAngle; profile.FlameSize=motion.FlameSize;
                profile.FlameSway=motion.FlameSway; profile.FlameBrightness=motion.FlameBrightness;
                shot.VisualStyle=ProjectileVisualStyle.FirebirdChick;
            }
            var path=folder+"VFX/"+id+"VFX.asset";
            if(AssetDatabase.LoadAssetAtPath<SpellVfxProfile>(path)==null)AssetDatabase.CreateAsset(profile,path);
            else{Object.DestroyImmediate(profile);profile=AssetDatabase.LoadAssetAtPath<SpellVfxProfile>(path);}
            shot.Vfx=profile;
            // A separate prefab keeps asset -> prefab -> visual easy to follow.
            var go=(GameObject)PrefabUtility.InstantiatePrefab(basePrefab.gameObject); go.name=id+"Visual";
            var fx=go.GetComponent<SpellProjectileVfx>(); fx.Profile=profile;
            var impact=Object.Instantiate(basePrefab.ImpactPrefab.gameObject); impact.name=id+"Impact";
            shot.ImpactPrefab=PrefabUtility.SaveAsPrefabAsset(impact,folder+"Prefabs/"+id+"Impact.prefab").GetComponent<SpellImpactVfx>();
            Object.DestroyImmediate(impact); fx.ImpactPrefab=shot.ImpactPrefab;
            shot.VfxPrefab=PrefabUtility.SaveAsPrefabAsset(go,folder+"Prefabs/"+id+"Visual.prefab").GetComponent<SpellProjectileVfx>();
            Object.DestroyImmediate(go);
            return shot;
        }
        static BossAbilityDefinition Ability(string name,BossAbilityId id,string title,BossAiState state,BossAbilityBehaviour behaviour,float duration,float firstShot,BossShotDefinition shot,string icon=null)
        {
            var ability=Asset<BossAbilityDefinition>("Abilities/"+name); ability.AbilityId=id; ability.DisplayName=title; ability.State=state; ability.Behaviour=behaviour;
            ability.Duration=duration; ability.FirstShotDelay=firstShot; ability.Shot=shot; ability.Shake=0;
            ability.Icon=icon!=null?Sprite("BossAbilities/"+icon):current.Sprite;
            ability.Style=Style(current.Theme.Primary,current.Theme.Secondary);
            if(shot!=null)ability.Vfx=shot.Vfx;
            else
            {
                var template=Resources.Load<SpellVfxProfile>("Spells/PlasmaBolt/Profiles/PlasmaBolt");
                var p=Object.Instantiate(template); p.name=name+"VFX"; p.CoreColor=Color.Lerp(current.Theme.Primary,Color.white,.7f);
                p.ImpactColor=current.Theme.Primary; p.RibbonColor=current.Theme.Secondary;
                p.UseLifetimeGradients=true; p.ImpactOverLife=Gradient(Color.white,Color.white,Color.white,1,false);
                p.ParticleOverLife=Gradient(Color.white,current.Theme.Primary,current.Theme.Secondary,.9f);
                p.ImpactFade=AnimationCurve.Linear(0,1,1,0); p.ImpactDuration=.5f;
                AssetDatabase.CreateAsset(p,folder+"VFX/"+name+"VFX.asset"); ability.Vfx=p;
            }
            var baseImpact=Resources.Load<SpellImpactVfx>("Spells/PlasmaBolt/Prefabs/PlasmaBoltImpact");
            ability.AftereffectPrefab=baseImpact;
            if(behaviour==BossAbilityBehaviour.Beam||behaviour==BossAbilityBehaviour.Roots||behaviour==BossAbilityBehaviour.Dash||behaviour==BossAbilityBehaviour.RebirthEgg)ability.CastPrefab=baseImpact;
            return ability;
        }
        static T Asset<T>(string path) where T:ScriptableObject
        { var existing=AssetDatabase.LoadAssetAtPath<T>(folder+path+".asset"); if(existing!=null)return existing; var asset=ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset,folder+path+".asset");return asset; }
        static T ActorPrefab<T>(string name,Sprite sprite) where T:Component
        {
            var path=folder+"Prefabs/"+name+".prefab"; var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null)return existing.GetComponent<T>();
            var go=new GameObject(name); var renderer=go.AddComponent<SpriteRenderer>(); renderer.sprite=sprite;renderer.sortingOrder=10;
            go.AddComponent<T>();var prefab=PrefabUtility.SaveAsPrefabAsset(go,path);Object.DestroyImmediate(go);return prefab.GetComponent<T>();
        }
        static Sprite Sprite(string resource)
        {
            var path="Assets/Resources/"+resource+".png";
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null)return null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path)) if (asset is Sprite existing) return existing;
            // The project used Multiple with an empty slice list and created its
            // whole-sheet sprites at runtime. Give that same rectangle a stable asset ID.
            importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=1024; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        static BossMovementSettings Movement(float radius,float response,float angular,float offset=0,float sway=0,float frequency=0,bool spin=false)
            =>new BossMovementSettings{Radius=radius,RadiusSpeed=response,AngularSpeed=angular,PlayerAngleOffset=offset,SwayAmplitude=sway,SwayFrequency=frequency,AngularMotion=spin?BossAngularMotion.Spin:BossAngularMotion.TrackPlayer};
        static BossAttackEntry Entry(BossAbilityDefinition ability,float chance=1,float duration=1)=>new BossAttackEntry{Ability=ability,Chance=chance,DurationMultiplier=duration};
        static BossPhaseDefinition Phase(string name,float hp,params BossAttackEntry[] entries)=>new BossPhaseDefinition{Name=name,MaxHealthFraction=hp,Attacks=entries};
        static BossEffectStyle Style(Color primary,Color secondary)=>new BossEffectStyle{Primary=primary,Secondary=secondary,Highlight=Color.Lerp(primary,Color.white,.8f),RibbonGradient=Gradient(primary,secondary,secondary,.8f),ParticleGradient=Gradient(Color.white,primary,secondary,.9f)};
        static Gradient Gradient(Color first,Color middle,Color last,float alpha,bool fade=true)
        {var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(first,0),new GradientColorKey(middle,.4f),new GradientColorKey(last,1)},new[]{new GradientAlphaKey(alpha,0),new GradientAlphaKey(alpha*.7f,.4f),new GradientAlphaKey(fade?0:alpha,1)});return g;}
        static void SaveBoss()
        {
            EditorUtility.SetDirty(current);
            foreach(var shot in current.Shots)EditorUtility.SetDirty(shot);
            foreach(var ability in current.Abilities)EditorUtility.SetDirty(ability);
            AssetDatabase.SaveAssets();
        }
        static void FinalizeLinks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before finalizing links");
            AddSandboxAssets();
            foreach (var boss in Resources.LoadAll<BossDefinition>("Bosses"))
            {
                var resource = boss.Archetype == BossArchetype.AstralFirebird ? "boss_astral_firebird" : boss.Archetype == BossArchetype.VoidMaw ? "boss_void_maw" : "boss_umbral_harrier";
                if (boss.Sprite == null) boss.Sprite = Sprite(resource);
                if (boss.Prefab != null)
                {
                    boss.Prefab.GetComponent<SpriteRenderer>().sprite = boss.Sprite;
                    PrefabUtility.SavePrefabAsset(boss.Prefab.gameObject);
                }
                var profiles = new System.Collections.Generic.List<SpellVfxProfile>();
                foreach (var shot in boss.Shots) if (shot != null && shot.Vfx != null && !profiles.Contains(shot.Vfx)) profiles.Add(shot.Vfx);
                foreach (var ability in boss.Abilities)
                {
                    if (ability == null) continue;
                    if (ability.Icon == null)
                    {
                        var icons = new[] { "firebird_solar_chicks", "firebird_ashen_egg", "firebird_phoenix_dive", "harrier_rift_copies", "harrier_phase_dash", "harrier_cold_fan", "void_rift_beam", "void_gravity_roots", "void_barrage" };
                        ability.Icon = (int)ability.AbilityId < icons.Length ? Sprite("BossAbilities/" + icons[(int)ability.AbilityId]) : boss.Sprite;
                    }
                    if (ability.Behaviour == BossAbilityBehaviour.Summon && ability.Summon.Sprite == null)
                    {
                        ability.Summon.Sprite = boss.Sprite;
                        ability.Summon.Prefab.GetComponent<SpriteRenderer>().sprite = boss.Sprite;
                        PrefabUtility.SavePrefabAsset(ability.Summon.Prefab.gameObject);
                    }
                    if (ability.Vfx != null && !profiles.Contains(ability.Vfx)) profiles.Add(ability.Vfx);
                    if (string.IsNullOrWhiteSpace(ability.Description)) ability.Description = ability.Shot != null
                        ? "Параметры залпа, урона и полёта — в Shot. Цвет, хвост, частицы и угасание — в Shot → Vfx. Длительность и движение атаки — здесь."
                        : "Тайминг и специальные параметры способности — здесь. Внешний вид подготовки и остаточного эффекта — в Vfx; цвет линий — в Style.";
                    EditorUtility.SetDirty(ability);
                }
                boss.VfxProfiles = profiles.ToArray();
                if (boss.Archetype != BossArchetype.AstralFirebird) boss.Presentation = null;
                EditorUtility.SetDirty(boss);
            }
            AssetDatabase.SaveAssets();
            foreach (var name in new[] { "VoidMaw", "UmbralHarrier" }) AssetDatabase.DeleteAsset(Root + name + "/VFX/" + name + "Presentation.asset");
            BossAssetRegistry.Reset(); File.WriteAllText("Temp/BossAssets.finalized.txt", DateTime.Now.ToString("O"));
        }
        static void AddSandboxAssets()
        {
            foreach (var name in new[] { "Phoenix", "VoidMaw" })
            {
                folder = Root + name + "/"; current = AssetDatabase.LoadAssetAtPath<BossDefinition>(folder + "Boss/" + name + "Boss.asset");
                var abilities = new System.Collections.Generic.List<BossAbilityDefinition>(current.Abilities);
                if (name == "VoidMaw" && current.Ability(BossAbilityId.VoidBlackHole) == null)
                {
                    var well = Ability("Void_BlackHole", BossAbilityId.VoidBlackHole, "ЧЁРНАЯ ДЫРА", BossAiState.Orbit, BossAbilityBehaviour.SandboxGravityWell, 5.5f, 0, null, "void_rift_beam");
                    well.SandboxOnly = true; well.ShowInGuide = false; well.Style = Style(new Color(.10f,.72f,1),new Color(.74f,.16f,1));
                    well.Description = "Существующий эффект Sandbox: притягивает корабль, снаряды, двойников и частицы. В боевых фазах не используется.";
                    abilities.Add(well);
                }
                if (name == "Phoenix")
                    foreach (var id in new[] { BossAbilityId.FirebirdSolarPlume, BossAbilityId.FirebirdEmberCore })
                    {
                        if (current.Ability(id) != null) continue;
                        var plume = id == BossAbilityId.FirebirdSolarPlume;
                        var aura = Ability(plume ? "Phoenix_SolarPlume" : "Phoenix_EmberCore", id, plume ? "СОЛНЕЧНОЕ ОПЕРЕНИЕ" : "УГОЛЬНОЕ ЯДРО", BossAiState.Orbit, BossAbilityBehaviour.SandboxAura, 2, 0, null, plume ? "firebird_solar_chicks" : "firebird_phoenix_dive");
                        aura.SandboxOnly = true; aura.ShowInGuide = false;
                        aura.Vfx.GlowSize = Vector2.one * (plume ? .76f : .44f);
                        aura.Vfx.GlowColor = plume ? new Color(1,.38f,.08f,.16f) : new Color(1,.18f,.04f,.19f);
                        aura.Vfx.CoreColor = new Color(1,.75f,.22f,.84f); aura.Vfx.CoreSize = Vector2.one * .18f;
                        aura.Vfx.PulseSpeed = plume ? 4 : 8; aura.Vfx.PulseAmount = plume ? .05f : .04f;
                        aura.Style.AlphaOverLife = AnimationCurve.Linear(0,1,1,1);
                        aura.Description = "Пассивная визуальная настройка манекена Sandbox. Размер, пульсация и цвета — в Vfx; градиент/затухание — в Style.";
                        EditorUtility.SetDirty(aura.Vfx); abilities.Add(aura);
                    }
                current.Abilities = abilities.ToArray(); SaveBoss();
            }
        }
    }
}
#endif
