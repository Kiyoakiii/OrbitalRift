using UnityEngine;

namespace OrbitalRift
{
    /// <summary>
    /// Purely visual layer for the first classic boss.  It deliberately lives
    /// outside GameManager: pooled enemies can turn into ordinary enemies again
    /// without leaving beams, eye glows, or tentacles behind in the arena.
    /// </summary>
    public sealed class VoidMawBossPresentation : MonoBehaviour
    {
        private const int TentacleCount = 4;
        private const int TentaclePoints = 9;
        private const int MaxBeams = 8;

        private Transform visualRoot;
        private SpriteRenderer eyeGlow;
        private SpriteRenderer eyeCore;
        private LineRenderer[] tentacles;
        private LineRenderer[] beamGlow;
        private LineRenderer[] beamCore;
        private LineRenderer[] rootVines;
        private SpriteRenderer rootSigil;
        private Sprite glowSprite;
        private bool visible;
        private Vector3 spriteBaseScale = Vector3.one;
        private Sprite lastBossSprite;

        public void Configure(Transform arena, Sprite glow)
        {
            glowSprite = glow;
            EnsureVisuals(arena);
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (visualRoot != null) visualRoot.gameObject.SetActive(value);
        }

        public void Render(Enemy boss, float beamAngleDegrees, float telegraph01, bool sweeping,
            float rootAngleDegrees, float rootTelegraph01, bool rootLocked,Vector2 orbitCenter=default)
        {
            if (!visible || boss == null || visualRoot == null) return;

            var healthRatio = boss.MaxHealth <= .001f ? 1f : Mathf.Clamp01(boss.Health / boss.MaxHealth);
            var rage = 1f - healthRatio;
            var theme = boss.Definition != null ? boss.Definition.Theme : new BossEffectStyle();
            var beamAbility = boss.ActiveAbility != null && boss.ActiveAbility.Behaviour == BossAbilityBehaviour.Beam ? boss.ActiveAbility : boss.Definition?.Ability(BossAbilityBehaviour.Beam) ?? BossAssetRegistry.Ability(BossAbilityId.VoidRiftBeam);
            var rootAbility = boss.ActiveAbility != null && boss.ActiveAbility.Behaviour == BossAbilityBehaviour.Roots ? boss.ActiveAbility : boss.Definition?.Ability(BossAbilityBehaviour.Roots) ?? BossAssetRegistry.Ability(BossAbilityId.VoidGravityRoots);
            var beamStyle = beamAbility.Style; var rootStyle = rootAbility.Style;
            var time = Time.time;
            var center = (Vector2)boss.transform.position;
            if (lastBossSprite != boss.Renderer.sprite)
            {
                lastBossSprite = boss.Renderer.sprite;
                spriteBaseScale = boss.Renderer.transform.localScale;
            }
            var pulse = .5f + .5f * Mathf.Sin(time * (3.0f + rage * 3.5f));
            boss.Renderer.transform.localScale = spriteBaseScale * (1f + Mathf.Sin(time * 2.7f) * (.025f + rage * .035f));
            boss.Renderer.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 1.7f) * (2.2f + rage * 2.6f));
            var beamWarmup = Mathf.Clamp01(telegraph01);
            var eyeColor = Color.Lerp(theme.Primary, theme.Secondary, rage);
            if (beamWarmup > 0f || sweeping) eyeColor = Color.Lerp(eyeColor, Color.white, .52f + pulse * .34f);

            eyeGlow.transform.position = center;
            eyeGlow.transform.localScale = Vector3.one * (.78f + pulse * .13f + beamWarmup * .22f);
            eyeGlow.color = new Color(eyeColor.r, eyeColor.g, eyeColor.b, .16f + pulse * .11f + beamWarmup * .22f);
            eyeCore.transform.position = center + new Vector2(Mathf.Sin(time * 1.6f) * .018f, Mathf.Cos(time * 1.35f) * .014f);
            eyeCore.transform.localScale = Vector3.one * (.23f + pulse * .045f + beamWarmup * .055f);
            eyeCore.color = theme.Tint(theme.Highlight, .48f + pulse * .24f);

            // Thin animated wakes sit behind the painted limbs.  The sprite
            // supplies the silhouette; these lines give it a living motion at
            // any scale without deforming the imported artwork.
            for (var tentacle = 0; tentacle < TentacleCount; tentacle++)
            {
                var line = tentacles[tentacle];
                var baseAngle = boss.Angle * Mathf.Rad2Deg + tentacle * 90f + 45f;
                var outward = Direction(baseAngle);
                var normal = new Vector2(-outward.y, outward.x);
                var length = .74f + .12f * Mathf.Sin(time * (1.55f + tentacle * .08f) + tentacle * 2.4f) + rage * .18f;
                for (var p = 0; p < TentaclePoints; p++)
                {
                    var t = p / (TentaclePoints - 1f);
                    var wave = Mathf.Sin(time * (3.1f + rage * 2f) + tentacle * 1.73f + t * 6.0f) * (.035f + rage * .035f) * t;
                    line.SetPosition(p, center + outward * (.18f + length * t) + normal * wave);
                }
                line.startWidth = .045f;
                line.endWidth = .006f;
                theme.ApplyLine(line, .35f);
            }

            var numberOfBeams = beamAbility.Beam.Number(healthRatio);
            var beamVisible = sweeping || beamWarmup > 0.001f;
            var visualLength = beamAbility.Beam.Length * (sweeping ? 1f : Mathf.Lerp(.20f, 1f, beamWarmup));
            var alpha = sweeping ? .60f + pulse * .16f : .13f + beamWarmup * .23f;
            for (var i = 0; i < MaxBeams; i++)
            {
                var active = beamVisible && i < numberOfBeams;
                beamGlow[i].gameObject.SetActive(active);
                beamCore[i].gameObject.SetActive(active);
                if (!active) continue;

                var direction = Direction(beamAngleDegrees + i * (360f / numberOfBeams));
                var start = center + direction * .13f;
                var end = center + direction * visualLength;
                beamGlow[i].SetPosition(0, start);
                beamGlow[i].SetPosition(1, end);
                beamCore[i].SetPosition(0, start);
                beamCore[i].SetPosition(1, end);
                var beamColor = Color.Lerp(beamStyle.Primary, beamStyle.Secondary, rage * .72f);
                beamStyle.ApplyLine(beamGlow[i], alpha * .30f);
                beamCore[i].startColor = beamStyle.Tint(beamStyle.Highlight, alpha);
                beamCore[i].endColor = new Color(beamColor.r, beamColor.g, beamColor.b, alpha * .46f);
                beamGlow[i].startWidth = .17f + pulse * .035f;
                beamGlow[i].endWidth = .065f;
                beamCore[i].startWidth = .031f + pulse * .010f;
                beamCore[i].endWidth = .012f;
            }

            // The gravroot is intentionally a mark on the orbit rather than a
            // hidden collision. The magenta seal grows first, then three living
            // filaments grab the sector for a very short, readable lock.
            var rootVisible = rootLocked || rootTelegraph01 > .001f;
            var target = Direction(rootAngleDegrees) * OrbitSettings.Radius+orbitCenter;
            rootSigil.gameObject.SetActive(rootVisible);
            if (rootVisible)
            {
                var rootPulse = .5f + .5f * Mathf.Sin(time * 12f);
                var rootStrength = rootLocked ? 1f : Mathf.Clamp01(rootTelegraph01);
                rootSigil.transform.position = target;
                rootSigil.transform.localScale = Vector3.one * Mathf.Lerp(.26f, .88f, rootStrength + rootPulse * .08f);
                rootSigil.color = rootStyle.Tint(rootStyle.Primary, .14f + rootStrength * .32f);
            }
            for (var vine = 0; vine < rootVines.Length; vine++)
            {
                var line = rootVines[vine];
                line.gameObject.SetActive(rootVisible);
                if (!rootVisible) continue;
                var rootStrength = rootLocked ? 1f : Mathf.Clamp01(rootTelegraph01);
                var normal = new Vector2(-(target - center).y, (target - center).x).normalized;
                var offset = (vine - 1) * .07f;
                for (var p = 0; p < line.positionCount; p++)
                {
                    var t = p / (line.positionCount - 1f);
                    var bend = Mathf.Sin(t * Mathf.PI) * (.30f + .08f * vine) +
                               Mathf.Sin(time * 7f + vine * 2.3f + t * 9f) * .045f;
                    line.SetPosition(p, Vector2.Lerp(center, target, t) + normal * (bend + offset));
                }
                rootStyle.ApplyLine(line, rootStrength);
                line.startWidth = .026f + rootStrength * .04f;
                line.endWidth = .014f + rootStrength * .025f;
            }
        }

        private void EnsureVisuals(Transform arena)
        {
            if (visualRoot != null) return;
            visualRoot = new GameObject("Void Maw FX").transform;
            visualRoot.SetParent(arena, false);
            eyeGlow = CreateGlow("Void Maw eye bloom", 2);
            eyeCore = CreateGlow("Void Maw eye core", 5);
            tentacles = new LineRenderer[TentacleCount];
            for (var i = 0; i < tentacles.Length; i++) tentacles[i] = CreateLine("Void Maw tentacle wake " + (i + 1), 1, TentaclePoints);
            beamGlow = new LineRenderer[MaxBeams];
            beamCore = new LineRenderer[MaxBeams];
            for (var i = 0; i < MaxBeams; i++)
            {
                beamGlow[i] = CreateLine("Void Maw beam bloom " + (i + 1), 1, 2);
                beamCore[i] = CreateLine("Void Maw beam core " + (i + 1), 4, 2);
                beamGlow[i].numCapVertices = beamCore[i].numCapVertices = 6;
            }
            rootSigil = CreateGlow("Void Maw gravroot seal", 3);
            rootVines = new LineRenderer[3];
            for (var i = 0; i < rootVines.Length; i++)
            {
                rootVines[i] = CreateLine("Void Maw gravroot vine " + (i + 1), 3, 7);
                rootVines[i].numCapVertices = 5;
            }
            SetVisible(false);
        }

        private SpriteRenderer CreateGlow(string name, int sortingOrder)
        {
            var glow = new GameObject(name).AddComponent<SpriteRenderer>();
            glow.transform.SetParent(visualRoot, false);
            glow.sprite = glowSprite;
            glow.sortingOrder = sortingOrder;
            return glow;
        }

        private LineRenderer CreateLine(string name, int sortingOrder, int points)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(visualRoot, false);
            line.useWorldSpace = true;
            line.positionCount = points;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.sortingOrder = sortingOrder;
            line.alignment = LineAlignment.View;
            line.numCornerVertices = 4;
            line.numCapVertices = 4;
            return line;
        }

        private static Vector2 Direction(float angleDegrees)
        {
            var radians = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private void OnDisable()
        {
            if (visualRoot != null) visualRoot.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (visualRoot != null) Destroy(visualRoot.gameObject);
        }
    }
}
