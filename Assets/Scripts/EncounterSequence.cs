using System;
using UnityEngine;
namespace OrbitalRift
{
    [CreateAssetMenu(menuName="Orbital Rift/Gameplay/Encounter sequence")]
    public sealed class EncounterSequence : ScriptableObject
    {
        public EncounterStep[] Steps=Array.Empty<EncounterStep>();
        public bool Loop=true;
        public EncounterStep Step(int phase) => Steps.Length == 0 ? null : Steps[Loop ? Mathf.Max(0,phase-1)%Steps.Length : Mathf.Clamp(phase-1,0,Steps.Length-1)];
    }
    [Serializable] public sealed class EncounterStep
    {
        public string Name;
        [Tooltip("Назначь босса для одиночной встречи. Пусто = обычные волны.")] public BossDefinition Boss;
        [Min(1)] public int Waves=3, BaseCount=5;
        [Min(0)] public int CountPerPhase=2, CountPerWave=2;
        [Min(0)] public float InitialDelay=.72f;
        [Tooltip("Включено: строки повторяются по порядку. Выключено: случайный выбор с весами.")] public bool OrderedMobs;
        public MobSpawnEntry[] Mobs=Array.Empty<MobSpawnEntry>();
        public MobDefinition Pick(int phase,int ordinal)
        {
            if(Mobs.Length==0)return null;
            if(OrderedMobs) return Mobs[Mathf.Max(0,ordinal)%Mobs.Length].Mob;
            float total=0; foreach(var e in Mobs)if(phase>=e.FromPhase&&(e.ThroughPhase<=0||phase<=e.ThroughPhase)&&e.Mob!=null)total+=Mathf.Max(0,e.Weight);
            float roll=UnityEngine.Random.value*total;
            foreach(var e in Mobs)if(phase>=e.FromPhase&&(e.ThroughPhase<=0||phase<=e.ThroughPhase)&&e.Mob!=null){roll-=Mathf.Max(0,e.Weight);if(roll<0)return e.Mob;}
            return Mobs[0].Mob;
        }
    }
    [Serializable] public sealed class MobSpawnEntry { public MobDefinition Mob; [Min(0)] public float Weight=1; [Min(1)] public int FromPhase=1; [Tooltip("0 = без верхней границы")] public int ThroughPhase; }
}
