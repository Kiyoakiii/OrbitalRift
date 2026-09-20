#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace OrbitalRift {
public static class CreateConfiguredEnemy {
[MenuItem("Assets/Create/Orbital Rift/Ready-to-edit boss")]
public static void CreateBoss(){
 const string root="Assets/Resources/Bosses/Custom";Directory.CreateDirectory(root);AssetDatabase.Refresh();
 var source=BossAssetRegistry.Get(BossArchetype.AstralFirebird);
 if(source==null)throw new InvalidOperationException("Create original boss assets first");
 var boss=UnityEngine.Object.Instantiate(source);boss.BossId=Guid.NewGuid().ToString("N");boss.DisplayName="НОВЫЙ БОСС";boss.UseLegacyPresentation=false;boss.Presentation=null;
 var path=AssetDatabase.GenerateUniqueAssetPath(root+"/NewBoss.asset");var folder=path.Substring(0,path.Length-6);Directory.CreateDirectory(folder);AssetDatabase.Refresh();
 var shot=UnityEngine.Object.Instantiate(source.InitialAbility.Shot);shot.ShotId=Guid.NewGuid().ToString("N");shot.DisplayName="Новый выстрел";AssetDatabase.CreateAsset(shot,folder+"/Shot.asset");
 var spell=UnityEngine.Object.Instantiate(source.InitialAbility);spell.DisplayName="Новая атака";spell.Shot=shot;spell.ShowInGuide=true;AssetDatabase.CreateAsset(spell,folder+"/Spell.asset");
 boss.InitialAbility=spell;boss.Shots=new[]{shot};boss.Abilities=new[]{spell};boss.Phases=new[]{new BossPhaseDefinition{Name="Основная",MaxHealthFraction=1,Attacks=new[]{new BossAttackEntry{Ability=spell,Chance=1}}}};
 boss.Appearance.Enabled=true;boss.Appearance.Sprite=boss.Sprite;boss.Appearance.Size=boss.WorldSize;
 AssetDatabase.CreateAsset(boss,path);AssetDatabase.SaveAssets();Selection.activeObject=boss;EditorGUIUtility.PingObject(boss);
}
}}
#endif
