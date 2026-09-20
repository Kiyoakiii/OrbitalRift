using System;
using System.Collections.Generic;

namespace OrbitalRift
{
    public enum TempoModule { None, RapidFire, FastPlasma, ReserveCapacitor }

    /// <summary>M3 rules only. Activation in gameplay requires the M4 checkpoint/UI adapter.</summary>
    public static class TempoRewardRules
    {
        public const int LootVersion = 1;
        public static int ModuleDuration => TempoRewardRulesProfile.Current.ModuleDuration;
        public static int MaximumModules => TempoRewardRulesProfile.Current.MaximumModules;
        public static float FireRateMultiplier => TempoRewardRulesProfile.Current.FireRateMultiplier;
        public static float ProjectileSpeedMultiplier => TempoRewardRulesProfile.Current.ProjectileSpeedMultiplier;
        public static float MinimumFireInterval => TempoRewardRulesProfile.Current.MinimumFireInterval;

        // Starting references from the design slice. They are deliberately kept in one place
        // so playtest measurements can replace them without changing reward state or saves.
        public static int ReferenceMilliseconds(SectorRoomType type)
        {
            switch (type)
            {
                case SectorRoomType.Elite: return 45000;
                case SectorRoomType.Boss: return 75000;
                default: return 30000;
            }
        }

        public static bool Eligible(SectorRoomType type) => type == SectorRoomType.Combat ||
            type == SectorRoomType.Elite || type == SectorRoomType.Boss;
        public static bool Valid(TempoModule module) => module >= TempoModule.RapidFire && module <= TempoModule.ReserveCapacitor;

        // Integer basis points, rounded to nearest with midpoint upwards. No float/FPS/RNG input.
        public static int ChanceBasisPoints(int combatMs, int referenceMs, int misses)
        {
            if (combatMs < 0 || referenceMs <= 0 || misses < 0) throw new ArgumentOutOfRangeException();
            if (misses >= 2) return 10000;
            long denominator = 45L * referenceMs;
            long numerator = Math.Min(denominator, Math.Max(0L, 100L * (referenceMs - (long)combatMs)));
            int qualityBonus = (int)((4000L * numerator + denominator / 2) / denominator);
            return Math.Min(7500, 1500 + qualityBonus + misses * 1000);
        }

        // Versioned, stable 32-bit hash. Do not replace with GetHashCode or UnityEngine.Random.
        public static uint Draw(int seed, int node, int ordinal, uint stream)
        {
            unchecked
            {
                uint h = (uint)seed ^ 0x9E3779B9u;
                h = Mix(h ^ (uint)node); h = Mix(h ^ (uint)ordinal);
                return Mix(h ^ (uint)LootVersion ^ stream);
            }
        }
        private static uint Mix(uint x)
        {
            unchecked { x ^= x >> 16; x *= 0x7FEB352Du; x ^= x >> 15; x *= 0x846CA68Bu; return x ^ (x >> 16); }
        }

        public static string Name(TempoModule module)
        {
            switch (module) {
                case TempoModule.RapidFire: return "ФОРСАЖ ЗАТВОРА";
                case TempoModule.FastPlasma: return "ДАЛЬНОБОЙНАЯ ПЛАЗМА";
                case TempoModule.ReserveCapacitor: return "РЕЗЕРВНЫЙ КОНДЕНСАТОР";
                default: return string.Empty;
            }
        }
    }

    public sealed class TempoRewardOutcome
    {
        public string RewardId { get; }
        public int NodeId { get; }
        public int CombatMilliseconds { get; }
        public int ReferenceMilliseconds { get; }
        public int ChanceBasisPoints { get; }
        public int RollBasisPoints { get; }
        public bool Granted => RollBasisPoints < ChanceBasisPoints;
        public TempoModule First { get; }
        public TempoModule Second { get; }
        internal TempoRewardOutcome(string id, int node, int time, int reference, int chance, uint draw, uint options)
        {
            RewardId = id; NodeId = node; CombatMilliseconds = time; ReferenceMilliseconds = reference;
            ChanceBasisPoints = chance; RollBasisPoints = (int)(draw % 10000);
            if (!Granted) return;
            int first = (int)(options % 3);
            First = (TempoModule)(1 + first);
            Second = (TempoModule)(1 + (first + 1 + (options / 3 % 2)) % 3);
        }
        internal TempoRewardOutcome(TempoOutcomeCheckpoint value)
        {
            RewardId=value.rewardId; NodeId=value.nodeId; CombatMilliseconds=value.combatMilliseconds;
            ReferenceMilliseconds=value.referenceMilliseconds; ChanceBasisPoints=value.chanceBasisPoints;
            RollBasisPoints=value.rollBasisPoints; First=(TempoModule)value.first; Second=(TempoModule)value.second;
        }
        internal TempoOutcomeCheckpoint ToCheckpoint() => new TempoOutcomeCheckpoint {
            rewardId=RewardId, nodeId=NodeId, combatMilliseconds=CombatMilliseconds,
            referenceMilliseconds=ReferenceMilliseconds, chanceBasisPoints=ChanceBasisPoints,
            rollBasisPoints=RollBasisPoints, first=(int)First, second=(int)Second
        };
    }

    public struct TempoModuleSlot
    {
        public TempoModule Module { get; }
        public int RemainingCombats { get; }
        internal TempoModuleSlot(TempoModule module, int remaining) { Module = module; RemainingCombats = remaining; }
    }

    /// <summary>One instance per run. Commands are idempotent by node/reward ID, not only last event.</summary>
    public sealed class TempoRewardState
    {
        private readonly string runId;
        private readonly int seed;
        private readonly Dictionary<int, TempoRewardOutcome> resolved = new Dictionary<int, TempoRewardOutcome>();
        private readonly HashSet<int> finished = new HashSet<int>();
        private readonly HashSet<string> chosen = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<TempoModuleSlot> modules = new List<TempoModuleSlot>(2);
        private int activeNode = -1, referenceMs, ordinal;
        public int CombatMilliseconds { get; private set; }
        public int ConsecutiveMisses { get; private set; }
        public bool EncounterActive => activeNode >= 0;
        public bool Failed { get; private set; }
        public bool ReserveChargeAvailable { get; private set; }
        public TempoRewardOutcome Pending { get; private set; }
        public int ModuleCount => modules.Count;
        public TempoModuleSlot ModuleAt(int index) => modules[index];

        public TempoRewardState(string runId, int seed)
        {
            if (string.IsNullOrWhiteSpace(runId)) throw new ArgumentException("Run ID required", nameof(runId));
            this.runId = runId; this.seed = seed;
        }
        public bool Has(TempoModule module)
        {
            for (int i = 0; i < modules.Count; i++) if (modules[i].Module == module) return true;
            return false;
        }

        // referenceMilliseconds must come from a calibrated encounter profile, never current buffs.
        public bool BeginEncounter(int node, SectorRoomType type, int referenceMilliseconds)
        {
            if (node < 0 || referenceMilliseconds <= 0 || !TempoRewardRules.Eligible(type) ||
                Failed || EncounterActive || Pending != null || finished.Contains(node)) return false;
            activeNode = node; referenceMs = referenceMilliseconds; CombatMilliseconds = 0;
            ReserveChargeAvailable = Has(TempoModule.ReserveCapacitor);
            return true;
        }
        // Call with authoritative elapsed milliseconds. Paused/forced no-target intervals do not count.
        // Ordinary misses, armour mechanics, and voluntary waiting MUST keep targetAvailable true.
        public void AdvanceCombat(int elapsedMilliseconds, bool paused, bool targetAvailable)
        {
            if (elapsedMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMilliseconds));
            if (!EncounterActive || paused || !targetAvailable) return;
            CombatMilliseconds = (int)Math.Min(int.MaxValue, (long)CombatMilliseconds + elapsedMilliseconds);
        }

        public TempoRewardOutcome FinishEncounter(int node, bool enemyDefeated, bool hullAlive)
        {
            if (resolved.TryGetValue(node, out var previous)) return previous;
            if (node != activeNode || !EncounterActive || finished.Contains(node)) return null;
            // Terminal failure wins over simultaneous enemy death; no roll and no pity increment.
            if (!hullAlive) { Failed = true; finished.Add(node); activeNode = -1; ReserveChargeAvailable = false; return null; }
            if (!enemyDefeated) return null;
            finished.Add(node); activeNode = -1; ReserveChargeAvailable = false;
            for (int i = modules.Count - 1; i >= 0; i--)
            {
                var slot = modules[i];
                if (slot.RemainingCombats <= 1) modules.RemoveAt(i);
                else modules[i] = new TempoModuleSlot(slot.Module, slot.RemainingCombats - 1);
            }
            ordinal++;
            int chance = TempoRewardRules.ChanceBasisPoints(CombatMilliseconds, referenceMs, ConsecutiveMisses);
            var result = new TempoRewardOutcome(runId + ":" + node + ":" + ordinal + ":" + TempoRewardRules.LootVersion,
                node, CombatMilliseconds, referenceMs, chance, TempoRewardRules.Draw(seed, node, ordinal, 0),
                TempoRewardRules.Draw(seed, node, ordinal, 0xB5297A4Du));
            resolved.Add(node, result);
            ConsecutiveMisses = result.Granted ? 0 : ConsecutiveMisses + 1;
            if (result.Granted) Pending = result;
            return result;
        }

        // UI must explicitly name the replaced module when both slots are full.
        public bool Choose(string rewardId, TempoModule module, TempoModule replace = TempoModule.None)
        {
            if (Failed || EncounterActive || Pending == null || Pending.RewardId != rewardId || chosen.Contains(rewardId) ||
                (module != Pending.First && module != Pending.Second)) return false;
            int index = modules.FindIndex(slot => slot.Module == module);
            if (index < 0 && modules.Count == TempoRewardRules.MaximumModules)
            {
                index = modules.FindIndex(slot => slot.Module == replace);
                if (index < 0) return false;
            }
            var updated = new TempoModuleSlot(module, TempoRewardRules.ModuleDuration);
            if (index >= 0) modules[index] = updated; else modules.Add(updated);
            chosen.Add(rewardId); Pending = null;
            return true;
        }
        // Ordinary star shields are consumed by the existing damage pipeline first.
        public bool TryBlockDamage(int ordinaryShieldsRemaining)
        {
            if (!EncounterActive || ordinaryShieldsRemaining != 0 || !ReserveChargeAvailable) return false;
            ReserveChargeAvailable = false; return true;
        }

        public TempoRewardCheckpoint CreateCheckpoint()
        {
            var checkpoint = new TempoRewardCheckpoint {
                runId=runId, seed=seed, activeNode=activeNode, referenceMilliseconds=referenceMs,
                ordinal=ordinal, combatMilliseconds=CombatMilliseconds, consecutiveMisses=ConsecutiveMisses,
                failed=Failed, reserveChargeAvailable=ReserveChargeAvailable,
                pendingRewardId=Pending == null ? string.Empty : Pending.RewardId
            };
            foreach (var node in finished) checkpoint.finishedNodes.Add(node);
            checkpoint.finishedNodes.Sort();
            foreach (var entry in resolved) checkpoint.resolved.Add(entry.Value.ToCheckpoint());
            checkpoint.resolved.Sort((a,b) => a.nodeId.CompareTo(b.nodeId));
            foreach (var reward in chosen) checkpoint.chosenRewardIds.Add(reward);
            checkpoint.chosenRewardIds.Sort(StringComparer.Ordinal);
            for (var i=0;i<modules.Count;i++) checkpoint.modules.Add(new TempoModuleCheckpoint {
                module=(int)modules[i].Module, remainingCombats=modules[i].RemainingCombats
            });
            return checkpoint;
        }

        public static TempoRewardState Restore(TempoRewardCheckpoint checkpoint)
        {
            if (checkpoint == null || checkpoint.version != TempoRewardCheckpoint.CurrentVersion ||
                string.IsNullOrWhiteSpace(checkpoint.runId) || checkpoint.ordinal < 0 ||
                checkpoint.combatMilliseconds < 0 || checkpoint.consecutiveMisses < 0 || checkpoint.modules.Count > TempoRewardRules.MaximumModules)
                throw new ArgumentException("Invalid tempo checkpoint", nameof(checkpoint));
            var state = new TempoRewardState(checkpoint.runId, checkpoint.seed) {
                activeNode=checkpoint.activeNode, referenceMs=checkpoint.referenceMilliseconds, ordinal=checkpoint.ordinal,
                CombatMilliseconds=checkpoint.combatMilliseconds, ConsecutiveMisses=checkpoint.consecutiveMisses,
                Failed=checkpoint.failed, ReserveChargeAvailable=checkpoint.reserveChargeAvailable
            };
            for (var i=0;i<checkpoint.finishedNodes.Count;i++)
                if (checkpoint.finishedNodes[i] < 0 || !state.finished.Add(checkpoint.finishedNodes[i])) throw new ArgumentException("Invalid finished node");
            for (var i=0;i<checkpoint.resolved.Count;i++)
            {
                var value=checkpoint.resolved[i];
                bool granted=value != null && value.rollBasisPoints < value.chanceBasisPoints;
                bool choicesValid=value != null && (granted
                    ? TempoRewardRules.Valid((TempoModule)value.first) && TempoRewardRules.Valid((TempoModule)value.second) && value.first != value.second
                    : value.first == (int)TempoModule.None && value.second == (int)TempoModule.None);
                if (value == null || string.IsNullOrWhiteSpace(value.rewardId) || value.nodeId < 0 ||
                    value.chanceBasisPoints < 0 || value.chanceBasisPoints > 10000 || value.rollBasisPoints < 0 || value.rollBasisPoints > 9999 ||
                    !choicesValid || !state.finished.Contains(value.nodeId)) throw new ArgumentException("Invalid outcome");
                if (state.resolved.ContainsKey(value.nodeId)) throw new ArgumentException("Duplicate outcome node");
                state.resolved.Add(value.nodeId,new TempoRewardOutcome(value));
            }
            for (var i=0;i<checkpoint.chosenRewardIds.Count;i++)
                if (string.IsNullOrWhiteSpace(checkpoint.chosenRewardIds[i]) || !state.chosen.Add(checkpoint.chosenRewardIds[i])) throw new ArgumentException("Invalid chosen reward");
            for (var i=0;i<checkpoint.modules.Count;i++)
            {
                var module=checkpoint.modules[i];
                if (module == null || !TempoRewardRules.Valid((TempoModule)module.module) || module.remainingCombats < 1 ||
                    module.remainingCombats > TempoRewardRules.ModuleDuration || state.Has((TempoModule)module.module)) throw new ArgumentException("Invalid module slot");
                state.modules.Add(new TempoModuleSlot((TempoModule)module.module,module.remainingCombats));
            }
            if (!string.IsNullOrEmpty(checkpoint.pendingRewardId))
            {
                foreach (var entry in state.resolved)
                    if (entry.Value.RewardId == checkpoint.pendingRewardId) { state.Pending=entry.Value; break; }
                if (state.Pending == null || state.chosen.Contains(checkpoint.pendingRewardId) || state.EncounterActive || state.Failed)
                    throw new ArgumentException("Invalid pending reward");
            }
            if (state.EncounterActive && (state.referenceMs <= 0 || state.finished.Contains(state.activeNode)))
                throw new ArgumentException("Invalid active encounter");
            if (state.Failed && (state.EncounterActive || state.Pending != null)) throw new ArgumentException("Invalid terminal state");
            return state;
        }
    }

    [Serializable]
    public sealed class TempoRewardCheckpoint
    {
        public const int CurrentVersion = 1;
        public int version=CurrentVersion, seed, activeNode=-1, referenceMilliseconds, ordinal, combatMilliseconds, consecutiveMisses;
        public string runId, pendingRewardId;
        public bool failed, reserveChargeAvailable;
        public List<int> finishedNodes=new List<int>();
        public List<TempoOutcomeCheckpoint> resolved=new List<TempoOutcomeCheckpoint>();
        public List<string> chosenRewardIds=new List<string>();
        public List<TempoModuleCheckpoint> modules=new List<TempoModuleCheckpoint>();
    }
    [Serializable] public sealed class TempoOutcomeCheckpoint
    {
        public string rewardId; public int nodeId, combatMilliseconds, referenceMilliseconds, chanceBasisPoints, rollBasisPoints, first, second;
    }
    [Serializable] public sealed class TempoModuleCheckpoint { public int module, remainingCombats; }

    public readonly struct TempoWeaponStats
    {
        public readonly float FireInterval, ProjectileSpeed;
        public TempoWeaponStats(float interval, float speed) { FireInterval = interval; ProjectileSpeed = speed; }
    }

    public static class WeaponStatsResolver
    {
        // Inputs already include loadout/shop. Always resolve from those bases, never a prior result.
        // Deliberately opt-in: legacy ranked balance is not clamped or changed by this class.
        public static TempoWeaponStats ResolveTempo(float baseInterval, float baseSpeed, TempoRewardState tempo)
        {
            if (baseInterval <= 0 || baseSpeed <= 0 || float.IsNaN(baseInterval) || float.IsNaN(baseSpeed) ||
                float.IsInfinity(baseInterval) || float.IsInfinity(baseSpeed)) throw new ArgumentOutOfRangeException();
            bool active = tempo != null && tempo.EncounterActive;
            return new TempoWeaponStats(Math.Max(TempoRewardRules.MinimumFireInterval,
                baseInterval / (active && tempo.Has(TempoModule.RapidFire) ? TempoRewardRules.FireRateMultiplier : 1f)),
                baseSpeed * (active && tempo.Has(TempoModule.FastPlasma) ? TempoRewardRules.ProjectileSpeedMultiplier : 1f));
        }
    }
}
