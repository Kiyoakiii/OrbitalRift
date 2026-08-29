using UnityEngine;

namespace OrbitalRift
{
    public enum CoopTrajectoryShape : byte
    {
        Circle,
        Ellipse,
        FigureEight
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
        public const float EllipseHorizontalScale = 1.12f;
        public const float EllipseVerticalScale = .76f;
        public const float FigureEightHeightScale = .58f;
        public const int LineSegments = 160;
        public const float LineWidth = .026f;

        public static CoopTrajectoryState Evaluate(float elapsedSeconds)
        {
            var time = Mathf.Max(0f, elapsedSeconds);
            var stage = Mathf.FloorToInt(time / StageDuration);
            var local = time - stage * StageDuration;
            var from = (CoopTrajectoryShape)(stage % 3);
            var to = (CoopTrajectoryShape)((stage + 1) % 3);
            if (local < HoldDuration)
                return new CoopTrajectoryState(from, to, 0f, HoldDuration - local);

            var linearBlend = Mathf.InverseLerp(HoldDuration, StageDuration, local);
            var smoothBlend = linearBlend * linearBlend * (3f - 2f * linearBlend);
            return new CoopTrajectoryState(from, to, smoothBlend, 0f);
        }

        public static Vector2 Position(float angleDegrees, float elapsedSeconds)
        {
            var state = Evaluate(elapsedSeconds);
            return Vector2.LerpUnclamped(Position(angleDegrees, state.From), Position(angleDegrees, state.To), state.Blend);
        }

        public static Vector2 Position(float angleDegrees, CoopTrajectoryShape shape)
        {
            var radians = angleDegrees * Mathf.Deg2Rad;
            var radius = OrbitSettings.Radius;
            switch (shape)
            {
                case CoopTrajectoryShape.Ellipse:
                    return new Vector2(
                        Mathf.Cos(radians) * radius * EllipseHorizontalScale,
                        Mathf.Sin(radians) * radius * EllipseVerticalScale);
                case CoopTrajectoryShape.FigureEight:
                    return new Vector2(
                        Mathf.Cos(radians) * radius,
                        Mathf.Sin(radians * 2f) * radius * FigureEightHeightScale);
                default:
                    return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
            }
        }

        public static string Label(CoopTrajectoryShape shape)
        {
            switch (shape)
            {
                case CoopTrajectoryShape.Ellipse: return "ОВАЛ";
                case CoopTrajectoryShape.FigureEight: return "ВОСЬМЕРКА";
                default: return "КРУГ";
            }
        }
    }
}
