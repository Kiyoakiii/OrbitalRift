namespace OrbitalRift
{
    public enum OrbitLineType
    {
        Solid,
        Dashed,
        Dotted
    }

    // Главные настройки единственной игровой орбиты. Меняйте значения здесь.
    public static class OrbitSettings
    {
        public static float Radius => OrbitSettingsProfile.Current.Radius;
        public static float Diameter => OrbitSettingsProfile.Current.Diameter;
        public static readonly bool ShowTrajectory = false;
        public static readonly OrbitLineType LineType = OrbitLineType.Dotted;
        public static readonly UnityEngine.Color LineColor = new UnityEngine.Color(.2f, .9f, 1f, .35f);
        public static float LineWidth => OrbitSettingsProfile.Current.LineWidth;
        public static int Segments => OrbitSettingsProfile.Current.Segments;
        public static int DashCount => OrbitSettingsProfile.Current.DashCount;
        public static float DashDuty => OrbitSettingsProfile.Current.DashDuty;
    }
}
