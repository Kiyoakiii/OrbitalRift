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
        public const float Radius = 3.2f;
        public const float Diameter = Radius * 2f;
        public static readonly OrbitLineType LineType = OrbitLineType.Dotted;
        public static readonly UnityEngine.Color LineColor = new UnityEngine.Color(.2f, .9f, 1f, .35f);
        public const float LineWidth = .015f;
        public const int Segments = 96;
        public const int DashCount = 32;
        public const float DashDuty = .55f;
    }
}
