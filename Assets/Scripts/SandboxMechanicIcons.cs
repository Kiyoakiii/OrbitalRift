using UnityEngine;

namespace OrbitalRift
{
    /// <summary>Dedicated energy glyphs, generated once per catalog entry; no external assets or network needed.</summary>
    public static class SandboxMechanicIcons
    {
        public static Texture2D Create(AbilitySandboxAbilityId id, Color accent)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "Sandbox " + id + " icon", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
            {
                var p = new Vector2((x + .5f) / size * 2f - 1f, (y + .5f) / size * 2f - 1f);
                var d = GlyphDistance(id, p);
                var core = Mathf.Clamp01(1f - d / .032f);
                var rim = Mathf.Exp(-d * 32f) * .8f;
                var glow = Mathf.Exp(-d * 10f) * .38f;
                var plate = Mathf.Clamp01((.94f - p.magnitude) * 6f) * .38f;
                var angle = Mathf.Atan2(p.y, p.x);
                var halo = Mathf.Exp(-Mathf.Abs(p.magnitude - .88f) * 70f) * (.16f + .12f * Mathf.Sin(angle * 7f));
                var color = Color.Lerp(new Color(.17f, .12f, .38f), accent, Mathf.Clamp01(.55f + p.y * .35f));
                color = Color.Lerp(color, new Color(.94f, 1f, 1f), core * .82f);
                color *= Mathf.Clamp01(glow + rim + core + halo + .1f);
                color.a = Mathf.Clamp01(plate + glow + rim + core + halo);
                pixels[y * size + x] = color;
            }
            texture.SetPixels(pixels); texture.Apply(false, true);
            return texture;
        }

        private static float GlyphDistance(AbilitySandboxAbilityId id, Vector2 p)
        {
            var d = 10f;
            switch (id)
            {
                case AbilitySandboxAbilityId.OrbitAnchor:
                    d = Circle(p, new Vector2(0f, .43f), .13f);
                    d = Mathf.Min(d, Segment(p, new Vector2(0f, .29f), new Vector2(0f, -.55f)));
                    d = Mathf.Min(d, Segment(p, new Vector2(-.3f, .07f), new Vector2(.3f, .07f)));
                    d = Mathf.Min(d, Arc(p, new Vector2(0f, -.14f), .41f, Mathf.PI, Mathf.PI));
                    d = Mathf.Min(d, Arrow(p, new Vector2(-.41f, -.14f), Mathf.PI * .5f, .18f));
                    d = Mathf.Min(d, Arrow(p, new Vector2(.41f, -.14f), Mathf.PI * .5f, .18f));
                    break;
                case AbilitySandboxAbilityId.TrajectoryReplay:
                    for (var i = 0; i < 3; i++) d = Mathf.Min(d, Ship(p - new Vector2((i - 1) * .32f, (i - 1) * .23f), .28f));
                    d = Mathf.Min(d, Arc(p, Vector2.zero, .69f, -.4f, 3.2f));
                    break;
                case AbilitySandboxAbilityId.DelayedShot:
                    d = Mathf.Min(Segment(p, new Vector2(-.33f, .53f), new Vector2(.33f, .53f)),
                        Segment(p, new Vector2(-.33f, -.53f), new Vector2(.33f, -.53f)));
                    d = Mathf.Min(d, Segment(p, new Vector2(-.29f, .5f), new Vector2(.29f, -.5f)));
                    d = Mathf.Min(d, Segment(p, new Vector2(.29f, .5f), new Vector2(-.29f, -.5f)));
                    d = Mathf.Min(d, Arc(p, Vector2.zero, .72f, -.6f, 4.8f));
                    d = Mathf.Min(d, Circle(p, new Vector2(0f, -.31f), .04f));
                    break;
                case AbilitySandboxAbilityId.CourseRupture:
                    d = Arc(p, Vector2.zero, .55f, .18f, 2.5f);
                    d = Mathf.Min(d, Arc(p, Vector2.zero, .55f, Mathf.PI + .18f, 2.5f));
                    d = Mathf.Min(d, Arrow(p, new Vector2(Mathf.Cos(2.68f), Mathf.Sin(2.68f)) * .55f, 2.68f + Mathf.PI * .5f, .24f));
                    d = Mathf.Min(d, Arrow(p, new Vector2(Mathf.Cos(2.68f + Mathf.PI), Mathf.Sin(2.68f + Mathf.PI)) * .55f, 2.68f - Mathf.PI * .5f, .24f));
                    d = Mathf.Min(d, Segment(p, new Vector2(-.16f, .13f), new Vector2(.16f, -.13f)));
                    break;
                case AbilitySandboxAbilityId.GravityWave:
                    for (var i = 0; i < 3; i++) d = Mathf.Min(d, Circle(new Vector2(p.x, p.y * 1.32f), Vector2.zero, .2f + i * .25f));
                    d = Mathf.Min(d, Arrow(p, new Vector2(0f, .76f), Mathf.PI * .5f, .18f));
                    break;
                case AbilitySandboxAbilityId.MineRing:
                    d = Circle(p, Vector2.zero, .25f);
                    for (var i = 0; i < 8; i++)
                    {
                        var r = new Vector2(Mathf.Cos(i * Mathf.PI * .25f), Mathf.Sin(i * Mathf.PI * .25f));
                        d = Mathf.Min(d, Segment(p, r * .2f, r * .40f));
                        d = Mathf.Min(d, Diamond(p - r * .65f, .08f));
                    }
                    break;
                case AbilitySandboxAbilityId.Polarity:
                    d = Circle(p, new Vector2(-.34f, .22f), .25f);
                    d = Mathf.Min(d, Circle(p, new Vector2(.34f, -.22f), .25f));
                    d = Mathf.Min(d, Segment(p, new Vector2(-.47f, .22f), new Vector2(-.21f, .22f)));
                    d = Mathf.Min(d, Segment(p, new Vector2(-.34f, .09f), new Vector2(-.34f, .35f)));
                    d = Mathf.Min(d, Segment(p, new Vector2(.21f, -.22f), new Vector2(.47f, -.22f)));
                    d = Mathf.Min(d, Arc(p, Vector2.zero, .72f, .1f, 2.6f));
                    d = Mathf.Min(d, Arc(p, Vector2.zero, .72f, 3.24f, 2.6f));
                    break;
                case AbilitySandboxAbilityId.GhostTrail:
                    for (var i = 0; i < 20; i++)
                    {
                        var a = i / 20f; var b = (i + 1) / 20f;
                        d = Mathf.Min(d, Segment(p, new Vector2(Mathf.Sin(a * 6f) * .3f, -.68f + a * .98f),
                            new Vector2(Mathf.Sin(b * 6f) * .3f, -.68f + b * .98f)));
                    }
                    d = Mathf.Min(d, Ship(p - new Vector2(0f, .37f), .32f));
                    d = Mathf.Min(d, Diamond(p - new Vector2(-.35f, -.25f), .10f));
                    break;
                case AbilitySandboxAbilityId.Gigantism:
                    d = Ship(p, .46f);
                    for (var i = 0; i < 4; i++)
                    {
                        var a = Mathf.PI * .25f + i * Mathf.PI * .5f;
                        var point = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * .78f;
                        d = Mathf.Min(d, Segment(p, point * .63f, point));
                        d = Mathf.Min(d, Arrow(p, point, a, .19f));
                    }
                    break;
            }
            return d;
        }

        private static float Ship(Vector2 p, float r)
        {
            var a = new Vector2(0f, r); var b = new Vector2(-r * .75f, -r * .7f);
            var c = new Vector2(0f, -r * .3f); var d = new Vector2(r * .75f, -r * .7f);
            return Mathf.Min(Mathf.Min(Segment(p, a, b), Segment(p, b, c)), Mathf.Min(Segment(p, c, d), Segment(p, d, a)));
        }
        private static float Diamond(Vector2 p, float r) { return Mathf.Abs(Mathf.Abs(p.x) + Mathf.Abs(p.y) - r) * .7071f; }
        private static float Circle(Vector2 p, Vector2 center, float r) { return Mathf.Abs((p - center).magnitude - r); }
        private static float Arc(Vector2 p, Vector2 center, float r, float angle, float span)
        {
            var d = p - center; var a = Mathf.Repeat(Mathf.Atan2(d.y, d.x) - angle, Mathf.PI * 2f);
            if (a <= span) return Mathf.Abs(d.magnitude - r);
            return Mathf.Min(Vector2.Distance(d, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r),
                Vector2.Distance(d, new Vector2(Mathf.Cos(angle + span), Mathf.Sin(angle + span)) * r));
        }
        private static float Arrow(Vector2 p, Vector2 point, float angle, float size)
        {
            var f = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)); var side = new Vector2(-f.y, f.x);
            return Mathf.Min(Segment(p, point, point - f * size + side * size * .65f), Segment(p, point, point - f * size - side * size * .65f));
        }
        private static float Segment(Vector2 p, Vector2 a, Vector2 b)
        { var d = b - a; return (p - a - d * Mathf.Clamp01(Vector2.Dot(p - a, d) / Mathf.Max(.00001f, d.sqrMagnitude))).magnitude; }
    }
}
