using UnityEngine;

namespace OrbitalRift
{
    public enum CoopTrajectoryShape : byte
    {
        Circle,
        Ellipse,
        FigureEight,
        Square
    }

    public readonly struct CoopTrajectoryState
    {
        public readonly CoopTrajectoryShape From;
        public readonly CoopTrajectoryShape To;
        public readonly float Blend;
        public readonly float SecondsUntilTransition;

        public bool IsTransitioning => Blend > .001f;

        public CoopTrajectoryState(CoopTrajectoryShape from, CoopTrajectoryShape to, float blend, float secondsUntilTransition)
        {
            From = from;
            To = to;
            Blend = Mathf.Clamp01(blend);
            SecondsUntilTransition = Mathf.Max(0f, secondsUntilTransition);
        }
    }

    /// <summary>
    /// Deterministic co-op path morph. The host owns trajectory time; clients predict it
    /// between snapshots and correct smoothly, so reconnecting cannot select another form.
    /// </summary>
    public static class CoopTrajectorySettings
    {
        public const float HoldDuration = 12f;
        public const float TransitionDuration = 5f;
        public const float StageDuration = HoldDuration + TransitionDuration;
        // Co-op owns a larger arena than the solo orbit. The camera follows the
        // current shape extent, so it remains fully visible on narrow phones.
        public const float ArenaRadius = 4.25f;
        public const float EllipseHorizontalScale = 1.12f;
        public const float EllipseVerticalScale = .76f;
        public const float FigureEightHeightScale = .58f;
        public const float InitialElapsedSeconds = StageDuration * 2f;
        public const float ThreatSpawnRadius = .55f;
        public const float ThreatOrbitRadius = ArenaRadius * .80f;
        public const float CameraMargin = .55f;
        public const float MaxHorizontalExtent = ArenaRadius * 1.414214f;
        public const float MaxVerticalExtent = ArenaRadius * 1.414214f;
        public const int LineSegments = 160;
        public const float LineWidth = .026f;
        private const int FramingSamples = 64;

        public static CoopTrajectoryState Evaluate(float elapsedSeconds)
        {
            var time = Mathf.Max(0f, elapsedSeconds);
            var stage = Mathf.FloorToInt(time / StageDuration);
            var local = time - stage * StageDuration;
            var from = (CoopTrajectoryShape)(stage % 4);
            var to = (CoopTrajectoryShape)((stage + 1) % 4);
            if (local < HoldDuration)
                return new CoopTrajectoryState(from, to, 0f, HoldDuration - local);

            var linearBlend = Mathf.InverseLerp(HoldDuration, StageDuration, local);
            var smoothBlend = linearBlend * linearBlend * (3f - 2f * linearBlend);
            return new CoopTrajectoryState(from, to, smoothBlend, 0f);
        }

        public static Vector2 Position(float angleDegrees, float elapsedSeconds)
        {
            var state = Evaluate(elapsedSeconds);
            var position = Vector2.LerpUnclamped(Position(angleDegrees, state.From), Position(angleDegrees, state.To), state.Blend);
            return Rotate(position, TrajectoryRotationDegrees(elapsedSeconds));
        }

        public static Vector2 FramingExtents(float elapsedSeconds)
        {
            var maximum = Vector2.zero;
            for (var i = 0; i < FramingSamples; i++)
            {
                var position = Position(i * 360f / FramingSamples, elapsedSeconds);
                maximum.x = Mathf.Max(maximum.x, Mathf.Abs(position.x));
                maximum.y = Mathf.Max(maximum.y, Mathf.Abs(position.y));
            }
            return maximum;
        }

        public static Vector2 Position(float angleDegrees, CoopTrajectoryShape shape)
        {
            var radians = angleDegrees * Mathf.Deg2Rad;
            var radius = ArenaRadius;
            Vector2 position;
            switch (shape)
            {
                case CoopTrajectoryShape.Ellipse:
                    position = new Vector2(
                        Mathf.Cos(radians) * radius * EllipseHorizontalScale,
                        Mathf.Sin(radians) * radius * EllipseVerticalScale);
                    break;
                case CoopTrajectoryShape.FigureEight:
                    position = new Vector2(
                        Mathf.Cos(radians) * radius,
                        Mathf.Sin(radians * 2f) * radius * FigureEightHeightScale);
                    break;
                case CoopTrajectoryShape.Square:
                    // Superellipse (n=4): visibly square, but with genuinely
                    // rounded corners so a path morph never forms sharp hooks.
                    var cosineSquare = Mathf.Cos(radians);
                    var sineSquare = Mathf.Sin(radians);
                    position = new Vector2(
                        Mathf.Sign(cosineSquare) * Mathf.Sqrt(Mathf.Abs(cosineSquare)),
                        Mathf.Sign(sineSquare) * Mathf.Sqrt(Mathf.Abs(sineSquare))) * radius;
                    break;
                default:
                    return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
            }
            return position;
        }

        public static float HorizontalExtent(CoopTrajectoryShape shape)
        {
            switch (shape)
            {
                case CoopTrajectoryShape.Ellipse: return ArenaRadius * EllipseHorizontalScale;
                case CoopTrajectoryShape.Square: return ArenaRadius * Mathf.Sqrt(2f);
                default: return ArenaRadius;
            }
        }

        public static float VerticalExtent(CoopTrajectoryShape shape)
        {
            switch (shape)
            {
                case CoopTrajectoryShape.Ellipse: return ArenaRadius * EllipseVerticalScale;
                case CoopTrajectoryShape.FigureEight: return ArenaRadius * FigureEightHeightScale;
                case CoopTrajectoryShape.Square: return ArenaRadius * Mathf.Sqrt(2f);
                default: return ArenaRadius;
            }
        }

        public static string Label(CoopTrajectoryShape shape)
        {
            switch (shape)
            {
                case CoopTrajectoryShape.Ellipse: return "ОВАЛ";
                case CoopTrajectoryShape.FigureEight: return "ВОСЬМЕРКА";
                case CoopTrajectoryShape.Square: return "КВАДРАТ";
                default: return "КРУГ";
            }
        }

        public static float InitialElapsedForRun(int runSeed)
        {
            var positiveSeed = runSeed == int.MinValue ? 0 : Mathf.Abs(runSeed);
            var stage = positiveSeed % 4;
            var entry = (positiveSeed / 7 % 4) * (HoldDuration * .20f);
            return stage * StageDuration + entry;
        }

        public static float InitialAngleOffsetForRun(int runSeed)
        {
            var positiveSeed = runSeed == int.MinValue ? 0 : Mathf.Abs(runSeed);
            return (positiveSeed / 11 % 4) * 90f;
        }

        private static float TrajectoryRotationDegrees(float elapsedSeconds)
        {
            var time = Mathf.Max(0f, elapsedSeconds);
            var stage = Mathf.FloorToInt(time / StageDuration);
            var local = time - stage * StageDuration;
            var stageInCycle = stage % 4;
            var baseRotation = Mathf.FloorToInt(stage / 4f) * 90f;
            if (stageInCycle < 1) return baseRotation;
            if (stageInCycle > 1 || local < HoldDuration) return stageInCycle > 1 ? baseRotation + 90f : baseRotation;

            // Rotate the complete interpolated curve as one rigid shape while
            // the oval becomes a figure-eight. This preserves every segment's
            // neighbours and cannot create a hook or a broken polyline.
            var linear = Mathf.InverseLerp(HoldDuration, StageDuration, local);
            var smooth = linear * linear * (3f - 2f * linear);
            return baseRotation + smooth * 90f;
        }

        private static Vector2 Rotate(Vector2 value, float degrees)
        {
            if (Mathf.Abs(degrees) < .001f) return value;
            var radians = degrees * Mathf.Deg2Rad;
            var cosine = Mathf.Cos(radians);
            var sine = Mathf.Sin(radians);
            return new Vector2(value.x * cosine - value.y * sine, value.x * sine + value.y * cosine);
        }

    }
}
