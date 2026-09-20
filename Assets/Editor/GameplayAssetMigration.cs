#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace OrbitalRift
{
    [InitializeOnLoad]
    public static class GameplayAssetMigration
    {
        static GameplayAssetMigration(){EditorApplication.update+=()=>{const string path="Temp/GameplayAssets.command";if(!File.Exists(path)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;File.Delete(path);try{Create();File.WriteAllText("Temp/GameplayAssets.result.txt","PASS "+DateTime.Now);}catch(Exception e){File.WriteAllText("Temp/GameplayAssets.result.txt",e.ToString());Debug.LogException(e);}};}
        static T Asset<T>(string path) where T:ScriptableObject
        {var a=AssetDatabase.LoadAssetAtPath<T>(path);if(a!=null)return a;Directory.CreateDirectory(Path.GetDirectoryName(path));a=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(a,path);return a;}
        [MenuItem("Orbital Rift/Gameplay/Create missing configuration assets")]
        public static void Create()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first");
            Directory.CreateDirectory("Assets/Resources/Gameplay");Directory.CreateDirectory("Assets/Resources/Mobs");AssetDatabase.Refresh();
            Asset<BalanceSettingsProfile>("Assets/Resources/Gameplay/BalanceSettings.asset");
            Asset<OrbitSettingsProfile>("Assets/Resources/Gameplay/OrbitSettings.asset");
            Asset<OrbitalAbilitySettingsProfile>("Assets/Resources/Gameplay/OrbitalAbilitySettings.asset");
            Asset<BonusSettingsProfile>("Assets/Resources/Gameplay/BonusSettings.asset");
            Asset<StarStreamSettingsProfile>("Assets/Resources/Gameplay/StarStreamSettings.asset");
            Asset<GameAudioSettingsProfile>("Assets/Resources/Gameplay/GameAudioSettings.asset");
            Asset<MmrSettingsProfile>("Assets/Resources/Gameplay/MmrSettings.asset");
            Directory.CreateDirectory("Assets/Resources/Gameplay/Ships");AssetDatabase.Refresh();
            for(int i=0;i<4;i++) { var type=(ShipArchetype)i;var path="Assets/Resources/Gameplay/Ships/"+type+".asset";if(AssetDatabase.LoadAssetAtPath<ShipDefinition>(path)!=null)continue;var original=ShipLoadoutSettings.Get(type);var title=ShipLoadoutSettings.Title(type);var ship=Asset<ShipDefinition>(path);ship.Archetype=type;ship.DisplayName=title;ship.Element=original.Element;ship.FireIntervalMultiplier=original.FireIntervalMultiplier;ship.ProjectileSpeedMultiplier=original.ProjectileSpeedMultiplier;ship.DamageMultiplier=original.DamageMultiplier;ship.ProjectileColor=original.ProjectileColor;EditorUtility.SetDirty(ship);}
            Asset<CoopTrajectorySettingsProfile>("Assets/Resources/Gameplay/CoopTrajectorySettings.asset");
Asset<PairedLensRulesProfile>("Assets/Resources/Gameplay/PairedLensRules.asset");
Asset<TempoRewardRulesProfile>("Assets/Resources/Gameplay/TempoRewardRules.asset");
Asset<GameplayCameraZoomSettingsProfile>("Assets/Resources/Gameplay/GameplayCameraZoomSettings.asset");
            var rules=Asset<GameRules>("Assets/Resources/Gameplay/GameRules.asset");
            if(rules.Mobs.Length==0)
            {
                rules.Mobs=new MobDefinition[4];
                var kinds=new[]{EnemyKind.Scout,EnemyKind.Spiral,EnemyKind.Diver,EnemyKind.Turret};
                for(int i=0;i<4;i++)
                {
                    var m=Asset<MobDefinition>("Assets/Resources/Mobs/"+kinds[i]+".asset");rules.Mobs[i]=m;
                    m.MobId=kinds[i].ToString();m.DisplayName=m.MobId;m.Movement=kinds[i];m.Health=i==3?5:i==2?2:1;m.Points=new[]{100,175,250,350}[i];m.Size=i==3?.32f:.38f;
                    m.Tint=i==0?new Color(1,.55f,.12f):i==1?new Color(1,.16f,.45f):i==2?new Color(.95f,.25f,.8f):new Color(1,.8f,.18f);
                    if(i<2){var path="Assets/Resources/"+(i==0?"enemy_orange":"enemy_pink_can")+".png";var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer!=null){importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.SaveAndReimport();m.Sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);m.Tint=Color.white;}}
                    m.Radius=new[]{3.1f,2.35f,2.4f,2.7f}[i];m.AngularSpeed=new[]{.7f,1.4f,0,.55f}[i];m.RadialAmplitude=i==1?.75f:i==2?1.5f:0;m.RadialFrequency=i==2?2.6f:2.2f;
                    m.DefenseSpeed=i==2?.70f:i==3?.60f:.52f;m.DefenseWobble=i==1?.62f:i==2?.28f:0;m.DefenseWobbleFrequency=i==2?4.5f:3.2f;m.DefenseAngularSpeed=i==1?1.7f:.75f;
                    var shot=Asset<BossShotDefinition>("Assets/Resources/Mobs/"+kinds[i]+"Shot.asset");shot.ShotId=m.MobId+"Shot";shot.DisplayName=m.DisplayName+" выстрел";shot.Damage=1;shot.Element=DamageElement.Poison;shot.Speed=1.55f;shot.ScaleSpeedWithPhase=true;shot.Lifetime=3;shot.FireInterval=2.05f;shot.ProjectileCount=i==3?3:1;shot.SpreadDegrees=18;shot.Scale=.1f;m.Shot=shot;
                    EditorUtility.SetDirty(shot);EditorUtility.SetDirty(m);
                }
            }
            if(rules.Classic==null)
            {
                rules.Classic=Asset<EncounterSequence>("Assets/Resources/Gameplay/ClassicSequence.asset");rules.Classic.Steps=new EncounterStep[9];
                var bossNames=new[]{"Phoenix","VoidMaw","UmbralHarrier"};
                for(int i=0;i<9;i++){var s=new EncounterStep{Name="Фаза "+(i+1),Mobs=Entries(rules.Mobs,false)};if(i%3==2){var n=bossNames[i/3];s.Boss=AssetDatabase.LoadAssetAtPath<BossDefinition>("Assets/Resources/Bosses/"+n+"/Boss/"+n+"Boss.asset");s.Name=s.Boss.DisplayName;s.Waves=1;}rules.Classic.Steps[i]=s;}
                EditorUtility.SetDirty(rules.Classic);
            }
            if(rules.Defense==null){rules.Defense=Asset<EncounterSequence>("Assets/Resources/Gameplay/DefenseSequence.asset");rules.Defense.Steps=new[]{new EncounterStep{Name="Оборона — повтор с ростом количества",Waves=1,BaseCount=6,CountPerPhase=2,CountPerWave=0,InitialDelay=.18f,Mobs=Entries(rules.Mobs,true)}};EditorUtility.SetDirty(rules.Defense);}
            foreach(var pair in new[]{rules.Classic,rules.Defense})if(pair!=null)foreach(var step in pair.Steps)if(step.Mobs.Length==4&&Mathf.Approximately(step.Mobs[2].Weight,.23f)&&Mathf.Approximately(step.Mobs[3].Weight,.14f)){step.Mobs=Entries(rules.Mobs,pair==rules.Defense);EditorUtility.SetDirty(pair);}
            EditorUtility.SetDirty(rules);AssetDatabase.SaveAssets();
        }
        static MobSpawnEntry[] Entries(MobDefinition[] mobs,bool defense)
        {
            var entries=new System.Collections.Generic.List<MobSpawnEntry>();
            for(int tier=0;tier<3;tier++)
            {
                int from=tier==0?1:tier==1?3:defense?6:5;int through=tier==0?2:tier==1?(defense?5:4):0;
                float turret=tier==2?.14f:0;float diver=tier>=1?.37f:0;
                float[] weights=defense?new[]{(1-turret)*(1-diver)*.48f,(1-turret)*(1-diver)*.52f,(1-turret)*diver,turret}:tier==0?new[]{.55f,.45f,0,0}:tier==1?new[]{.55f,.08f,.37f,0}:new[]{.55f,.08f,.23f,.14f};
                for(int i=0;i<4;i++)if(weights[i]>0)entries.Add(new MobSpawnEntry{Mob=mobs[i],Weight=weights[i],FromPhase=from,ThroughPhase=through});
            }
            return entries.ToArray();
        }
    }
}
#endif
