using UnityEngine;

namespace OrbitalRift
{
    /// <summary>
    /// Pure, deterministic rules for the paired-lens encounter object. The rules own no
    /// GameObjects and deliberately have no ship API: a lens may only move an eligible
    /// projectile or the relay core.
    /// </summary>
    public readonly struct PairedLensPair
    {
        public readonly Vector2 FirstCenter;
        public readonly Vector2 FirstNormal;
        public readonly Vector2 SecondCenter;
        public readonly Vector2 SecondNormal;
        public readonly float Radius;

        public bool IsValid => Radius > 0f && FirstNormal.sqrMagnitude > .5f && SecondNormal.sqrMagnitude > .5f;

        public PairedLensPair(Vector2 firstCenter, Vector2 firstNormal, Vector2 secondCenter,
            Vector2 secondNormal, float radius)
        {
            FirstCenter = firstCenter;
            FirstNormal = firstNormal.sqrMagnitude < .001f ? Vector2.up : firstNormal.normalized;
            SecondCenter = secondCenter;
            SecondNormal = secondNormal.sqrMagnitude < .001f ? Vector2.down : secondNormal.normalized;
            Radius = Mathf.Max(0f, radius);
        }

        public Vector2 Center(bool first) => first ? FirstCenter : SecondCenter;
        public Vector2 Normal(bool first) => first ? FirstNormal : SecondNormal;
    }

    /// <summary>Transient state belongs to the moved object, not to the lens pair.</summary>
    public struct PairedLensTransitState
    {
        public byte Passes;
        public float Cooldown;
    }

    public readonly struct PairedLensTransitEvent
    {
        public readonly Vector2 EntryPoint;
        public readonly Vector2 ExitPoint;
        public readonly bool EnteredFirst;

        public PairedLensTransitEvent(Vector2 entryPoint, Vector2 exitPoint, bool enteredFirst)
        {
            EntryPoint = entryPoint;
            ExitPoint = exitPoint;
            EnteredFirst = enteredFirst;
        }
    }

    public static class PairedLensRules
    {
        public static float LensRadius => PairedLensRulesProfile.Current.LensRadius;
        public static float PlayerShotRadius => PairedLensRulesProfile.Current.PlayerShotRadius;
        public static float RelayCoreRadius => PairedLensRulesProfile.Current.RelayCoreRadius;
        public static float ExitEpsilon => PairedLensRulesProfile.Current.ExitEpsilon;
        public static float PlayerShotCooldown => PairedLensRulesProfile.Current.PlayerShotCooldown;
        public static float RelayCoreCooldown => PairedLensRulesProfile.Current.RelayCoreCooldown;
        public static int MaxPasses => PairedLensRulesProfile.Current.MaxPasses;

        // The gameplay path must stay at least ship-radius plus half a world unit
        // from the lens centre. The ship itself never interacts with a lens.
        public static float TrajectoryClearance => PairedLensRulesProfile.Current.TrajectoryClearance;
        private const int TrajectorySamples = 96;
        private const int MorphSamples = 36;

        /// <summary>
        /// Builds one static pair from a run seed. A pair is omitted instead of being
        /// regenerated during a fight when the deterministic safety checks cannot find a
        /// legal location.
        /// </summary>
        public static bool TryCreatePair(int runSeed, int nodeIndex, out PairedLensPair pair)
        {
            // The trajectory spends parts of its cycle rotated by 90 degrees, so most
            // visually tempting side placements become unsafe during a later morph. These
            // two pockets are the authored safe pair found by the exhaustive sampler.
            // runSeed/nodeIndex stay in the signature to make future route-specific
            // candidates source-compatible and to keep the encounter construction explicit.
            _ = runSeed;
            _ = nodeIndex;
            var first = new Vector2(-1.80f, -.20f);
            var second = new Vector2(0f, 1.60f);
            var direction = (first - second).normalized;
            var candidate = new PairedLensPair(first, direction, second, -direction, LensRadius);
            if (ValidatePlacement(candidate))
            {
                pair = candidate;
                return true;
            }
            pair = default;
            return false;
        }

        /// <summary>Checks the whole shipped trajectory cycle, including morphed forms.</summary>
        public static bool ValidatePlacement(PairedLensPair pair)
        {
            if (!pair.IsValid) return false;
            if ((pair.FirstCenter - pair.SecondCenter).sqrMagnitude < (pair.Radius * 3.2f) * (pair.Radius * 3.2f))
                return false;
            if (!IsInsideArena(pair.FirstCenter, pair.Radius) || !IsInsideArena(pair.SecondCenter, pair.Radius))
                return false;
            if (!ClearOfThreatSpawn(pair.FirstCenter, pair.Radius) || !ClearOfThreatSpawn(pair.SecondCenter, pair.Radius))
                return false;

            return MinimumTrajectoryDistance(pair) >= TrajectoryClearance;
        }

        /// <summary>Diagnostic value for authored candidate placement and regression tests.</summary>
        public static float MinimumTrajectoryDistance(PairedLensPair pair)
        {
            if (!pair.IsValid) return 0f;
            var minimum = float.MaxValue;
            for (var morph = 0; morph <= MorphSamples; morph++)
            {
                var elapsed = CoopTrajectorySettings.StageDuration * 4f * morph / MorphSamples;
                for (var point = 0; point < TrajectorySamples; point++)
                {
                    var path = CoopTrajectorySettings.Position(point * 360f / TrajectorySamples, elapsed);
                    minimum = Mathf.Min(minimum, Vector2.Distance(pair.FirstCenter, path),
                        Vector2.Distance(pair.SecondCenter, path));
                }
            }
            return minimum;
        }

        public static void Tick(ref PairedLensTransitState state, float deltaTime)
        {
            state.Cooldown = Mathf.Max(0f, state.Cooldown - Mathf.Max(0f, deltaTime));
        }

        /// <summary>
        /// Moves an eligible object to the paired exit. Damage, owner/element and TTL are
        /// intentionally outside this method and are therefore preserved by construction.
        /// </summary>
        public static bool TryTransit(ref Vector2 position, ref Vector2 velocity, Vector2 previousPosition,
            float objectRadius, ref PairedLensTransitState state, PairedLensPair pair, float cooldown,
            out PairedLensTransitEvent transit)
        {
            transit = default;
            if (!pair.IsValid || state.Cooldown > 0f || state.Passes >= MaxPasses) return false;
            objectRadius = Mathf.Max(0f, objectRadius);

            var firstHit = SegmentCircleEntry(previousPosition, position, pair.FirstCenter, pair.Radius + objectRadius,
                out var firstT);
            var secondHit = SegmentCircleEntry(previousPosition, position, pair.SecondCenter, pair.Radius + objectRadius,
                out var secondT);
            if (!firstHit && !secondHit) return false;

            var enteredFirst = firstHit && (!secondHit || firstT <= secondT);
            var exitCenter = pair.Center(!enteredFirst);
            var exitNormal = pair.Normal(!enteredFirst);
            var entryPoint = previousPosition + (position - previousPosition) * (enteredFirst ? firstT : secondT);
            position = exitCenter + exitNormal * (pair.Radius + objectRadius + ExitEpsilon);
            // A paired lens is two mouths of one wormhole.  A shot must leave the
            // destination mouth *outwards*, rather than be rotated back toward the
            // entry mouth.  The old 180-degree rotation made a top-to-bottom shot
            // appear to disappear: it was emitted toward the other lens again.
            // Preserve speed while making the exit direction unambiguous.
            var speed = velocity.magnitude;
            if (speed > .000001f) velocity = exitNormal * speed;
            state.Passes++;
            state.Cooldown = Mathf.Max(0f, cooldown);
            transit = new PairedLensTransitEvent(entryPoint, position, enteredFirst);
            return true;
        }

        private static bool IsInsideArena(Vector2 center, float radius)
        {
            var limit = CoopTrajectorySettings.ArenaRadius * 1.03f - radius;
            return center.sqrMagnitude <= limit * limit;
        }

        private static bool ClearOfThreatSpawn(Vector2 center, float radius)
        {
            return center.magnitude >= CoopTrajectorySettings.ThreatSpawnRadius + radius + .50f;
        }

        private static bool SegmentCircleEntry(Vector2 from, Vector2 to, Vector2 center, float radius, out float entryT)
        {
            entryT = 0f;
            var delta = to - from;
            var a = Vector2.Dot(delta, delta);
            if (a < .000001f) return false;
            var offset = from - center;
            var c = Vector2.Dot(offset, offset) - radius * radius;
            var discriminant = Vector2.Dot(offset, delta);
            var discriminantSquared = discriminant * discriminant - a * c;
            if (discriminantSquared < 0f) return false;
            var root = Mathf.Sqrt(discriminantSquared);
            var first = (-discriminant - root) / a;
            var second = (-discriminant + root) / a;
            if (first >= 0f && first <= 1f) { entryT = first; return true; }
            if (second >= 0f && second <= 1f) { entryT = second; return true; }
            return false;
        }

    }
}
