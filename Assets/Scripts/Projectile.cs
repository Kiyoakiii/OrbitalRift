using UnityEngine;

namespace OrbitalRift
{
    public sealed class Projectile : MonoBehaviour
    {
        public Vector2 Velocity;
        public bool FromPlayer;
        public bool FromRiftEcho;
        public DamageElement Element;
        public float Damage;
        public float Life;
        [System.NonSerialized] public BossShotDefinition Shot;
        public float ShotAge;
        public SpriteRenderer Renderer;
        public bool PreserveSpriteColor;
        public bool VisualOnly;
        public bool SandboxBossEffect;
        public bool FirebirdChickVisual;
        private Vector3 defaultScale;
        private Vector3 firebirdBaseScale = Vector3.one;
        private float firebirdBaseAngle;
        private LineRenderer[] firebirdBranches;
        private LineRenderer[] firebirdRibbons;
        private LineRenderer firebirdHeatRing;
        private LineRenderer[] firebirdSparks;
        private SpriteRenderer[] firebirdGlow;
        private int defaultSortingOrder;
        private static Sprite firebirdGlowSprite;
        public ProjectileVisualStyle VisualStyle;
        [System.NonSerialized] public SpellProjectileVfx SpellVfx;
        private void OnDisable() { if (SpellVfx != null) SpellVfx.End(); }
        private LineRenderer[] spellTrails;
        private Vector3 spellBaseScale = Vector3.one;
        private float spellBaseAngle;

        private void Awake()
        {
            Renderer = GetComponent<SpriteRenderer>();
            defaultScale = transform.localScale;
            defaultSortingOrder = Renderer != null ? Renderer.sortingOrder : 0;
        }
        public void SetVisual(Sprite sprite, bool preserveColor, bool hostile)
        {
            Renderer.sprite = sprite;
            PreserveSpriteColor = preserveColor;
            transform.localScale = hostile ? Vector3.one * .14f : defaultScale;
            SetSpellVisualStyle(ProjectileVisualStyle.Default);
        }

        public void SetSpellVisualStyle(ProjectileVisualStyle style, bool useLegacyPresentation = true)
        {
            VisualStyle = style;
            SetFirebirdChickVisual(style == ProjectileVisualStyle.FirebirdChick && useLegacyPresentation);
            if (!useLegacyPresentation || style == ProjectileVisualStyle.Default || style == ProjectileVisualStyle.FirebirdChick)
            {
                SetSpellTrailsActive(false);
                return;
            }

            spellBaseScale = transform.localScale;
            spellBaseAngle = transform.eulerAngles.z;
            EnsureSpellTrails();
            SetSpellTrailsActive(true);
        }

        public void SetFirebirdChickVisual(bool enabled)
        {
            FirebirdChickVisual = enabled;
            if (!enabled)
            {
                if (firebirdBranches != null)
                {
                    for (var i = 0; i < firebirdBranches.Length; i++)
                        if (firebirdBranches[i] != null) firebirdBranches[i].gameObject.SetActive(false);
                }
                if (firebirdRibbons != null)
                    for (var i = 0; i < firebirdRibbons.Length; i++)
                        if (firebirdRibbons[i] != null) firebirdRibbons[i].gameObject.SetActive(false);
                if (firebirdHeatRing != null) firebirdHeatRing.gameObject.SetActive(false);
                if (firebirdSparks != null)
                    for (var i = 0; i < firebirdSparks.Length; i++)
                        if (firebirdSparks[i] != null) firebirdSparks[i].gameObject.SetActive(false);
                if (firebirdGlow != null)
                    for (var i = 0; i < firebirdGlow.Length; i++)
                        if (firebirdGlow[i] != null) firebirdGlow[i].gameObject.SetActive(false);
                if (Renderer != null) Renderer.sortingOrder = defaultSortingOrder;
                return;
            }
            firebirdBaseScale = transform.localScale;
            firebirdBaseAngle = transform.eulerAngles.z;
            if (Renderer != null) Renderer.sortingOrder = Mathf.Max(defaultSortingOrder, 22);
            EnsureFirebirdPresentation();
            SetFirebirdPresentationActive(true);
        }

        private void EnsureFirebirdPresentation()
        {
            if (firebirdBranches == null)
            {
                firebirdBranches = new LineRenderer[3];
                for (var i = 0; i < firebirdBranches.Length; i++)
                    firebirdBranches[i] = CreateFirebirdLine("Solar chick branch " + (i + 1), 7,
                        i == 0 ? .082f : .062f, 24);
            }
            if (firebirdRibbons == null)
            {
                firebirdRibbons = new LineRenderer[2];
                for (var i = 0; i < firebirdRibbons.Length; i++)
                    firebirdRibbons[i] = CreateFirebirdLine("Solar chick wing ribbon " + (i + 1), 12,
                        i == 0 ? .068f : .054f, 25);
            }
            if (firebirdHeatRing == null)
                firebirdHeatRing = CreateFirebirdLine("Solar chick heat ring", 24, .022f, 23);
            if (firebirdSparks == null)
            {
                firebirdSparks = new LineRenderer[4];
                for (var i = 0; i < firebirdSparks.Length; i++)
                    firebirdSparks[i] = CreateFirebirdLine("Solar chick ember spark " + (i + 1), 2, .032f, 26);
            }
            if (firebirdGlow == null)
            {
                firebirdGlow = new SpriteRenderer[2];
                for (var i = 0; i < firebirdGlow.Length; i++)
                {
                    var glow = new GameObject("Solar chick glow " + (i + 1)).AddComponent<SpriteRenderer>();
                    glow.transform.SetParent(transform, false);
                    glow.sprite = FirebirdGlowSprite();
                    glow.sortingOrder = 18 + i;
                    firebirdGlow[i] = glow;
                }
            }
        }

        private LineRenderer CreateFirebirdLine(string name, int positionCount, float width, int sortingOrder)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.positionCount = positionCount;
            line.useWorldSpace = true;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startWidth = width;
            line.endWidth = 0f;
            line.numCapVertices = 3;
            line.numCornerVertices = 3;
            line.sortingOrder = sortingOrder;
            return line;
        }

        private static Sprite FirebirdGlowSprite()
        {
            if (firebirdGlowSprite != null) return firebirdGlowSprite;
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false)
            {
                name = "Runtime Phoenix glow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[32 * 32];
            for (var y = 0; y < 32; y++)
                for (var x = 0; x < 32; x++)
                {
                    var dx = (x + .5f) / 32f * 2f - 1f;
                    var dy = (y + .5f) / 32f * 2f - 1f;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var alpha = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(distance));
                    pixels[y * 32 + x] = new Color(1f, .32f, .04f, alpha * alpha);
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            firebirdGlowSprite = Sprite.Create(texture, new Rect(0f, 0f, 32f, 32f), new Vector2(.5f, .5f), 32f);
            firebirdGlowSprite.name = "Runtime Phoenix glow sprite";
            return firebirdGlowSprite;
        }

        private void SetFirebirdPresentationActive(bool active)
        {
            if (firebirdBranches != null)
                for (var i = 0; i < firebirdBranches.Length; i++)
                    if (firebirdBranches[i] != null) firebirdBranches[i].gameObject.SetActive(active);
            if (firebirdRibbons != null)
                for (var i = 0; i < firebirdRibbons.Length; i++)
                    if (firebirdRibbons[i] != null) firebirdRibbons[i].gameObject.SetActive(active);
            if (firebirdHeatRing != null) firebirdHeatRing.gameObject.SetActive(active);
            if (firebirdSparks != null)
                for (var i = 0; i < firebirdSparks.Length; i++)
                    if (firebirdSparks[i] != null) firebirdSparks[i].gameObject.SetActive(active);
            if (firebirdGlow != null)
                for (var i = 0; i < firebirdGlow.Length; i++)
                    if (firebirdGlow[i] != null) firebirdGlow[i].gameObject.SetActive(active);
        }

        public void AnimateFirebirdChick(float time)
        {
            if (SpellVfx != null) return;
            if (FirebirdChickVisual)
            {
                var flap = Mathf.Sin(time * 12.5f + transform.position.x * 1.7f) * .095f;
                var pulse = 1f + Mathf.Sin(time * 8.2f) * .045f;
                transform.localScale = firebirdBaseScale * (pulse + flap * .18f);
                transform.rotation = Quaternion.Euler(0f, 0f, firebirdBaseAngle + Mathf.Sin(time * 9.6f) * 7.5f);
                return;
            }
            if (VisualStyle == ProjectileVisualStyle.Default) return;

            var directionAngle = Velocity.sqrMagnitude > .0001f
                ? Mathf.Atan2(Velocity.y, Velocity.x) * Mathf.Rad2Deg : spellBaseAngle;
            if (VisualStyle == ProjectileVisualStyle.HarrierShard)
            {
                var pulse = 1f + Mathf.Sin(time * 12f + transform.position.y) * .09f;
                transform.localScale = spellBaseScale * pulse;
                transform.rotation = Quaternion.Euler(0f, 0f, directionAngle + time * 300f);
            }
            else if (VisualStyle == ProjectileVisualStyle.VoidPulse)
            {
                var pulse = 1f + Mathf.Sin(time * 8.4f + transform.position.x) * .12f;
                transform.localScale = spellBaseScale * pulse;
                transform.rotation = Quaternion.Euler(0f, 0f, directionAngle - time * 150f);
            }
            else if (VisualStyle == ProjectileVisualStyle.SolarLance)
            {
                var pulse = 1f + Mathf.Sin(time * 16f + transform.position.x) * .055f;
                transform.localScale = spellBaseScale * pulse;
                transform.rotation = Quaternion.Euler(0f, 0f, directionAngle);
            }
        }

        // The chick is a tiny bird, so one conventional TrailRenderer reads as a
        // rectangle at phone resolution. Three hand-shaped branches give it a
        // readable magical wake while keeping the projectile hitbox unchanged.
        public void UpdateFirebirdChickTrail(float time)
        {
            if (SpellVfx != null) return;
            if (FirebirdChickVisual)
            {
                UpdateFirebirdChickBranches(time);
                return;
            }
            if (VisualStyle == ProjectileVisualStyle.Default || spellTrails == null || Velocity.sqrMagnitude < .0001f) return;
            var direction = Velocity.normalized;
            var side = new Vector2(-direction.y, direction.x);
            var origin = (Vector2)transform.position - direction * .035f;
            for (var trailIndex = 0; trailIndex < spellTrails.Length; trailIndex++)
            {
                var trail = spellTrails[trailIndex];
                if (trail == null || !trail.gameObject.activeSelf) continue;
                var sign = trailIndex % 2 == 0 ? 1f : -1f;
                var length = VisualStyle == ProjectileVisualStyle.SolarLance ? .72f : VisualStyle == ProjectileVisualStyle.VoidPulse ? .60f : .48f;
                for (var pointIndex = 0; pointIndex < trail.positionCount; pointIndex++)
                {
                    var t = pointIndex / (float)(trail.positionCount - 1);
                    var wave = Mathf.Sin(time * (VisualStyle == ProjectileVisualStyle.VoidPulse ? 12f : 18f) + t * 9f + trailIndex * 2.1f);
                    var spread = VisualStyle == ProjectileVisualStyle.VoidPulse
                        ? sign * Mathf.Sin(t * Mathf.PI * 1.2f) * (.04f + t * .18f) + wave * .025f
                        : sign * t * (.025f + t * .11f) + wave * .012f;
                    trail.SetPosition(pointIndex, origin - direction * (length * t) + side * spread);
                }
                if (VisualStyle == ProjectileVisualStyle.HarrierShard)
                {
                    trail.startColor = new Color(.74f, .96f, 1f, .94f);
                    trail.endColor = new Color(.22f, .46f, 1f, 0f);
                }
                else if (VisualStyle == ProjectileVisualStyle.VoidPulse)
                {
                    trail.startColor = new Color(1f, .46f, 1f, .88f);
                    trail.endColor = new Color(.22f, .04f, .48f, 0f);
                }
                else
                {
                    trail.startColor = new Color(1f, .98f, .62f, .96f);
                    trail.endColor = new Color(1f, .18f, .02f, 0f);
                }
            }
        }

        private void UpdateFirebirdChickBranches(float time)
        {
            if (firebirdBranches == null || Velocity.sqrMagnitude < .0001f) return;
            var direction = Velocity.normalized;
            var side = new Vector2(-direction.y, direction.x);
            var origin = (Vector2)transform.position - direction * .075f;
            for (var branchIndex = 0; branchIndex < firebirdBranches.Length; branchIndex++)
            {
                var line = firebirdBranches[branchIndex];
                if (line == null) continue;
                var branchSign = branchIndex == 0 ? 0f : branchIndex == 1 ? 1f : -1f;
                var length = branchIndex == 0 ? .78f : .64f;
                for (var pointIndex = 0; pointIndex < line.positionCount; pointIndex++)
                {
                    var t = pointIndex / (float)(line.positionCount - 1);
                    var split = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.28f, 1f, t));
                    var breeze = Mathf.Sin(time * 10.5f + t * 8f + branchIndex * 1.9f) * (.014f + t * .026f);
                    var spread = branchSign * split * (.025f + t * .20f);
                    line.SetPosition(pointIndex, origin - direction * (length * t) + side * (spread + breeze));
                }
                line.startColor = new Color(1f, .98f, .70f, .96f);
                line.endColor = new Color(1f, .20f, .025f, 0f);
            }

            var wingFlap = Mathf.Sin(time * 13.5f + transform.position.x * 1.7f);
            if (firebirdRibbons != null)
                for (var wing = 0; wing < firebirdRibbons.Length; wing++)
                {
                    var line = firebirdRibbons[wing];
                    if (line == null) continue;
                    var sign = wing == 0 ? 1f : -1f;
                    for (var point = 0; point < line.positionCount; point++)
                    {
                        var t = point / (float)(line.positionCount - 1);
                        var feather = Mathf.Sin(t * Mathf.PI) * (.045f + .028f * wingFlap);
                        var wingPoint = (Vector2)transform.position
                            + side * sign * (.075f + t * (.31f + .035f * wingFlap))
                            - direction * (.015f + t * (.19f + .025f * Mathf.Sin(time * 8f + wing)));
                        line.SetPosition(point, wingPoint + direction * feather);
                    }
                    line.startColor = new Color(1f, .99f, .72f, .92f);
                    line.endColor = new Color(1f, .20f, .025f, 0f);
                }

            if (firebirdHeatRing != null)
            {
                var ringRadius = .25f + Mathf.Sin(time * 8.4f + transform.position.y) * .025f;
                for (var point = 0; point < firebirdHeatRing.positionCount; point++)
                {
                    var a = point / (float)(firebirdHeatRing.positionCount - 1) * Mathf.PI * 2f + time * 1.8f;
                    var radius = ringRadius * (.86f + Mathf.Sin(point * 2.3f + time * 6f) * .12f);
                    firebirdHeatRing.SetPosition(point, (Vector2)transform.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                }
                firebirdHeatRing.startColor = new Color(1f, .38f, .05f, .36f);
                firebirdHeatRing.endColor = new Color(1f, .12f, .02f, .05f);
            }

            if (firebirdSparks != null)
                for (var spark = 0; spark < firebirdSparks.Length; spark++)
                {
                    var line = firebirdSparks[spark];
                    if (line == null) continue;
                    var t = spark / (float)firebirdSparks.Length;
                    var anchor = (Vector2)transform.position - direction * (.22f + t * .34f)
                        + side * Mathf.Sin(time * 9f + spark * 1.7f) * (.08f + t * .08f);
                    var end = anchor - direction * (.10f + t * .05f)
                        + side * Mathf.Cos(time * 7f + spark) * (.035f + t * .03f);
                    line.SetPosition(0, anchor);
                    line.SetPosition(1, end);
                    line.startColor = new Color(1f, .82f, .22f, .78f - t * .20f);
                    line.endColor = new Color(1f, .18f, .02f, 0f);
                }

            if (firebirdGlow != null)
            {
                var parentScale = Mathf.Max(.001f, transform.lossyScale.x);
                var glowPulse = 1f + Mathf.Sin(time * 8.2f + transform.position.x) * .08f;
                for (var glowIndex = 0; glowIndex < firebirdGlow.Length; glowIndex++)
                {
                    var glow = firebirdGlow[glowIndex];
                    if (glow == null) continue;
                    var worldSize = (glowIndex == 0 ? .62f : .43f) * glowPulse;
                    glow.transform.localPosition = Vector3.zero;
                    glow.transform.localRotation = Quaternion.identity;
                    glow.transform.localScale = Vector3.one * (worldSize / (glow.sprite.bounds.size.x * parentScale));
                    glow.color = new Color(1f, glowIndex == 0 ? .22f : .64f, .04f,
                        (glowIndex == 0 ? .12f : .20f) * (.84f + .16f * glowPulse));
                }
            }
        }

        private void EnsureSpellTrails()
        {
            if (spellTrails != null) return;
            spellTrails = new LineRenderer[3];
            for (var i = 0; i < spellTrails.Length; i++)
            {
                var trail = new GameObject("Spell projectile wake " + (i + 1));
                trail.transform.SetParent(transform, false);
                var line = trail.AddComponent<LineRenderer>();
                line.positionCount = 6;
                line.useWorldSpace = true;
                line.material = new Material(Shader.Find("Sprites/Default"));
                line.startWidth = i == 0 ? .042f : .028f;
                line.endWidth = 0f;
                line.numCapVertices = 3;
                line.numCornerVertices = 3;
                line.sortingOrder = 10;
                spellTrails[i] = line;
            }
        }

        private void SetSpellTrailsActive(bool value)
        {
            if (spellTrails == null) return;
            for (var i = 0; i < spellTrails.Length; i++)
            {
                var trail = spellTrails[i];
                if (trail == null) continue;
                var active = value && (VisualStyle == ProjectileVisualStyle.VoidPulse || i < 2);
                trail.gameObject.SetActive(active);
            }
        }
        public void ResetProjectile(Vector2 position, Vector2 velocity, bool fromPlayer, Color color, DamageElement element, float damage)
        {
            GetComponent<ActorAppearanceView>()?.Configure(null, Renderer);
            Shot = null; ShotAge = 0; Renderer.enabled = true;
            transform.position = position; Velocity = velocity; FromPlayer = fromPlayer; FromRiftEcho = false; Element = element; Damage = Mathf.Max(0f, damage); Life = GameRules.Current.DefaultProjectileLifetime; VisualOnly = false; SandboxBossEffect = false;
            Renderer.color = PreserveSpriteColor ? Color.white : color;
            if (firebirdBranches != null)
                for (var i = 0; i < firebirdBranches.Length; i++)
                    if (firebirdBranches[i] != null)
                        for (var p = 0; p < firebirdBranches[i].positionCount; p++) firebirdBranches[i].SetPosition(p, position);
        }
    }

}
