using UnityEngine;

namespace OrbitalRift
{
    /// <summary>Shared geometry for the visible beam and its actual hit test.</summary>
    public static class BossAttackRules
    {
        public static bool IsInsideBeam(Vector2 bossPosition, Vector2 targetPosition, float beamAngleDegrees, int beamCount)
            => IsInsideBeam(bossPosition, targetPosition, beamAngleDegrees, beamCount,
                BossSettings.BeamInnerSafeRadius, BossSettings.BeamLength, BossSettings.BeamHalfWidthDegrees);

        public static bool IsInsideBeam(Vector2 bossPosition, Vector2 targetPosition, float beamAngleDegrees, int beamCount,
            float innerSafeRadius, float length, float halfWidthDegrees)
        {
            var offset = targetPosition - bossPosition;
            var distance = offset.magnitude;
            if (distance < innerSafeRadius || distance > length || distance <= .00001f) return false;
            var direction = offset / distance;
            var minimumDot = Mathf.Cos(halfWidthDegrees * Mathf.Deg2Rad);
            for (var i = 0; i < beamCount; i++)
            {
                var radians = (beamAngleDegrees + i * (360f / beamCount)) * Mathf.Deg2Rad;
                var beamDirection = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                if (Vector2.Dot(direction, beamDirection) >= minimumDot) return true;
            }
            return false;
        }
    }
}
