using UnityEngine;

namespace OrbitalRift
{
    /// <summary>
    /// Living fire around the painted bird: it supplies readability and motion
    /// without deforming the authored sprite. The egg has a separate, quiet
    /// silhouette so its five-second break window is unmistakable.
    /// </summary>
    public sealed class FirebirdBossPresentation : MonoBehaviour
    {
        private const int FeatherCount = 9;
        private Transform visualRoot;
        private SpriteRenderer[] feathers;
        private SpriteRenderer aura;
        private SpriteRenderer eggShell;
        private SpriteRenderer eggFlash;
        private SpriteRenderer leftWingSprite;
        private SpriteRenderer rightWingSprite;
        private LineRenderer eggHalo;
        private LineRenderer[] eggCracks;
        private LineRenderer[] diveRings;
        private ParticleSystem ambientParticles;
        private Sprite glowSprite;
        private BossVfxProfile vfxProfile;
        private BossEffectStyle effectStyle = new BossEffectStyle();
        private BossEffectStyle bodyStyle = new BossEffectStyle();
        private Material wingMaterial;
        private Material particleMaterial;
        private bool visible;
        private float diveBurstTimer;
        private float eggFlashTimer;
        private Vector3 spriteBaseScale = Vector3.one;
        private Sprite lastBossSprite;

        public void Configure(Transform arena, Sprite glow, BossVfxProfile profile = null)
        {
            glowSprite = glow;
            vfxProfile = profile != null ? profile : Resources.Load<BossVfxProfile>("BossAbilities/Profiles/AstralFirebird");
            EnsureVisuals(arena);
            ConfigureAmbientParticles();
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (!value)
            {
                diveBurstTimer = 0f;
                eggFlashTimer = 0f;
            }
            if (visualRoot != null) visualRoot.gameObject.SetActive(value);
            if (ambientParticles != null)
            {
                if (value) ambientParticles.Play(true);
                else ambientParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        public void TriggerEggBreakFlash()
        {
            eggFlashTimer = .78f;
        }

        public void TriggerPhoenixDiveBurst()
        {
            diveBurstTimer = .72f;
        }

        public void Render(Enemy boss, bool egg)
        {
            if (!visible || boss == null || visualRoot == null) return;
            var time = Time.time;
            var center = (Vector2)boss.transform.position;
            if (ambientParticles != null) ambientParticles.transform.position = center;
            if (lastBossSprite != boss.Renderer.sprite)
            {
                lastBossSprite = boss.Renderer.sprite;
                spriteBaseScale = boss.Renderer.transform.localScale;
            }
            var healthRatio = boss.MaxHealth <= .001f ? 1f : Mathf.Clamp01(boss.Health / boss.MaxHealth);
            var flame = .5f + .5f * Mathf.Sin(time * 4.2f);
            bodyStyle = boss.Definition != null ? boss.Definition.Theme : bodyStyle;
            effectStyle = boss.ActiveAbility != null ? boss.ActiveAbility.Style : bodyStyle;
            var fire = Color.Lerp(bodyStyle.Primary, bodyStyle.Secondary, flame * .5f);

            // The painted creature breathes and banks slightly; all motion is
            // local to the sprite so its gameplay hitbox remains deterministic.
            var pulse = 1f + Mathf.Sin(time * (egg ? 5.2f : 3.3f)) * (egg ? .028f : .045f);
            boss.Renderer.transform.localScale = spriteBaseScale * pulse;
            boss.Renderer.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * (egg ? 2.1f : 2.8f)) * (egg ? 2.5f : 5.5f));
            boss.Renderer.sortingOrder = 8;

            aura.transform.position = center;
            aura.transform.localScale = Vector3.one * (egg ? 1.42f + flame * .17f : 1.66f + flame * .23f);
            aura.color = new Color(fire.r, fire.g, fire.b, egg ? .28f + flame * .18f : .12f + flame * .14f);
            eggShell.gameObject.SetActive(egg);
            eggShell.transform.position = center;
            eggShell.transform.localScale = new Vector3(.68f, .92f, 1f) * (1f + flame * .07f);
            eggShell.color = Color.Lerp(effectStyle.Primary, effectStyle.Highlight, flame);

            for (var i = 0; i < feathers.Length; i++)
            {
                var feather = feathers[i];
                var angle = time * (egg ? -78f : 116f) + i * (360f / feathers.Length);
                var direction = Direction(angle);
                var distance = egg ? .64f + Mathf.Sin(time * 5f + i) * .045f : .78f + Mathf.Sin(time * 3.4f + i * 1.7f) * .16f;
                feather.transform.position = center + direction * distance;
                var size = egg ? .085f : .055f + .04f * Mathf.PingPong(time * 2.8f + i * .36f, 1f);
                feather.transform.localScale = Vector3.one * size;
                feather.color = new Color(fire.r, fire.g, fire.b, egg ? .44f : .18f + (1f - healthRatio) * .18f);
            }

            RenderPaintedWings(center, egg, time, boss.Renderer.transform.eulerAngles.z);
            RenderEggDetails(center, fire, egg, flame, time);
            RenderDiveRings(center, fire, egg, time);
            if (eggFlash != null)
            {
                eggFlash.gameObject.SetActive(eggFlashTimer > 0f);
                if (eggFlashTimer > 0f)
                {
                    eggFlashTimer = Mathf.Max(0f, eggFlashTimer - Time.deltaTime);
                    var progress = 1f - eggFlashTimer / .78f;
                    var flashAlpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / .18f)) * (1f - Mathf.Clamp01((progress - .18f) / .82f));
                    eggFlash.transform.position = center;
                    eggFlash.transform.localScale = Vector3.one * (1.4f + progress * 2.7f);
                    eggFlash.color = effectStyle.Tint(effectStyle.Highlight, flashAlpha * .78f);
                }
            }
        }

        private void EnsureVisuals(Transform arena)
        {
            if (visualRoot != null) return;
            visualRoot = new GameObject("Astral firebird FX").transform;
            visualRoot.SetParent(arena, false);
            aura = CreateGlow("Firebird solar plume", 1);
            eggShell = CreateGlow("Firebird last egg", 6);
            eggFlash = CreateGlow("Firebird white orange flash", 20);
            eggFlash.gameObject.SetActive(false);
            feathers = new SpriteRenderer[FeatherCount];
            for (var i = 0; i < feathers.Length; i++) feathers[i] = CreateGlow("Firebird feather " + (i + 1), 4);
            var wingShader = Shader.Find("Orbital Rift/Phoenix Wing Cutout");
            if (wingShader != null)
            {
                wingMaterial = new Material(wingShader)
                {
                    name = "Phoenix wing checkerboard cutout",
                    hideFlags = HideFlags.HideAndDontSave
                };
                wingMaterial.SetFloat("_RemoveCheckerboard", 1f);
            }
            var wingSheet = Resources.Load<Texture2D>("BossAbilities/firebird_boss_wings");
            if (wingSheet != null)
            {
                leftWingSprite = CreateWingRenderer("Firebird painted left wing", CreateWingSprite(wingSheet, false), 7);
                rightWingSprite = CreateWingRenderer("Firebird painted right wing", CreateWingSprite(wingSheet, true), 7);
            }
            ambientParticles = CreateAmbientParticles();
            eggHalo = CreateFlameLine("Firebird egg halo", 9, .035f);
            eggCracks = new LineRenderer[4];
            for (var i = 0; i < eggCracks.Length; i++) eggCracks[i] = CreateFlameLine("Firebird egg crack " + (i + 1), 14, .032f);
            diveRings = new LineRenderer[2];
            for (var i = 0; i < diveRings.Length; i++) diveRings[i] = CreateFlameLine("Firebird dive ring " + (i + 1), 15, .065f - i * .018f);
            SetVisible(false);
        }

        private LineRenderer CreateFlameLine(string name, int order, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(visualRoot, false);
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 7;
            line.useWorldSpace = true;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startWidth = width;
            line.endWidth = width * .18f;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.sortingOrder = order;
            line.gameObject.SetActive(false);
            return line;
        }

        // These are separate feather layers cut from the firebird art, not line
        // effects. The body stays collision-stable while the wings have a gentle
        // asymmetric flap, so the creature reads as alive at small phone scale.
        private void RenderPaintedWings(Vector2 center, bool egg, float time, float bodyRotation)
        {
            if (leftWingSprite != null) leftWingSprite.gameObject.SetActive(!egg);
            if (rightWingSprite != null) rightWingSprite.gameObject.SetActive(!egg);
            if (egg || leftWingSprite == null || rightWingSprite == null) return;

            var wingScale = vfxProfile != null ? vfxProfile.WingScale : .52f;
            var flapAngle = vfxProfile != null ? vfxProfile.WingFlapAngle : 10f;
            var secondaryFlapAngle = vfxProfile != null ? vfxProfile.WingSecondaryFlapAngle : 3.5f;
            var flap = Mathf.Sin(time * 5.3f) * flapAngle + Mathf.Sin(time * 10.6f) * secondaryFlapAngle;
            var leftScale = wingScale + Mathf.Sin(time * 5.3f) * .028f;
            var rightScale = wingScale - Mathf.Sin(time * 5.3f) * .028f;
            leftWingSprite.transform.position = center + Rotate(new Vector2(-.04f, .035f), bodyRotation);
            rightWingSprite.transform.position = center + Rotate(new Vector2(.04f, .035f), bodyRotation);
            leftWingSprite.transform.rotation = Quaternion.Euler(0f, 0f, bodyRotation + flap);
            rightWingSprite.transform.rotation = Quaternion.Euler(0f, 0f, bodyRotation - flap);
            leftWingSprite.transform.localScale = Vector3.one * leftScale;
            rightWingSprite.transform.localScale = Vector3.one * rightScale;
            var wingColor = vfxProfile != null ? vfxProfile.WingColor : new Color(1f, .92f, .76f, .94f);
            leftWingSprite.color = wingColor;
            rightWingSprite.color = wingColor;
        }

        private SpriteRenderer CreateWingRenderer(string name, Sprite sprite, int sortingOrder)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(visualRoot, false);
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            renderer.sharedMaterial = wingMaterial;
            return renderer;
        }

        private ParticleSystem CreateAmbientParticles()
        {
            var go = new GameObject("Firebird ambient embers");
            go.transform.SetParent(visualRoot, false);
            go.transform.localPosition = Vector3.zero;
            var system = go.AddComponent<ParticleSystem>();
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 12;
            particleMaterial = new Material(Shader.Find("Sprites/Default"))
            {
                name = "Firebird ambient particle material",
                hideFlags = HideFlags.HideAndDontSave
            };
            if (glowSprite != null) particleMaterial.mainTexture = glowSprite.texture;
            renderer.sharedMaterial = particleMaterial;
            return system;
        }

        private void ConfigureAmbientParticles()
        {
            if (ambientParticles == null) return;
            var settings = vfxProfile;
            if (settings == null) return;

            var main = ambientParticles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = Mathf.Max(1, settings.ParticleMaxCount);
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                Mathf.Min(settings.ParticleLifetimeMin, settings.ParticleLifetimeMax),
                Mathf.Max(settings.ParticleLifetimeMin, settings.ParticleLifetimeMax));
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                Mathf.Min(settings.ParticleSpeedMin, settings.ParticleSpeedMax),
                Mathf.Max(settings.ParticleSpeedMin, settings.ParticleSpeedMax));
            main.startSize = new ParticleSystem.MinMaxCurve(
                Mathf.Min(settings.ParticleSizeMin, settings.ParticleSizeMax),
                Mathf.Max(settings.ParticleSizeMin, settings.ParticleSizeMax));
            main.startColor = new ParticleSystem.MinMaxGradient(settings.ParticleColorMin, settings.ParticleColorMax);
            if (settings.UseParticleGradient)
            {
                var gradient = ambientParticles.colorOverLifetime; gradient.enabled = true; gradient.color = settings.ParticleOverLifetime;
                var sizes = ambientParticles.sizeOverLifetime; sizes.enabled = true; sizes.size = new ParticleSystem.MinMaxCurve(1, settings.ParticleSizeOverLifetime);
            }
            main.startRotation3D = false;
            main.startRotation = new ParticleSystem.MinMaxCurve(
                settings.ParticleRotationMinMax.x, settings.ParticleRotationMinMax.y);
            main.gravityModifier = settings.ParticleGravity;

            var emission = ambientParticles.emission;
            emission.enabled = settings.ParticleEmissionRate > 0f;
            emission.rateOverTime = Mathf.Max(0f, settings.ParticleEmissionRate);

            var shape = ambientParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(0f, settings.ParticleEmissionRadius);
            shape.arc = Mathf.Clamp(settings.ParticleEmissionArc, 0f, 360f);

            var noise = ambientParticles.noise;
            noise.enabled = settings.ParticleNoiseEnabled;
            noise.strength = Mathf.Max(0f, settings.ParticleNoiseStrength);
            noise.frequency = Mathf.Max(.01f, settings.ParticleNoiseFrequency);
            noise.scrollSpeed = Mathf.Max(0f, settings.ParticleNoiseScrollSpeed);
            noise.damping = true;

            var velocity = ambientParticles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.radial = new ParticleSystem.MinMaxCurve(.035f, .11f);

            var rotation = ambientParticles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(
                settings.ParticleAngularVelocityMinMax.x, settings.ParticleAngularVelocityMinMax.y);

            var size = ambientParticles.sizeOverLifetime;
            size.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, .15f),
                new Keyframe(.16f, 1f),
                new Keyframe(.72f, .82f),
                new Keyframe(1f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, settings.UseParticleGradient ? settings.ParticleSizeOverLifetime : sizeCurve);

            ambientParticles.Clear(true);
            if (visible) ambientParticles.Play(true);
        }

        private static Sprite CreateWingSprite(Texture2D sheet, bool rightWing)
        {
            var width = sheet.width / 2;
            var rect = new Rect(rightWing ? width : 0, 0, width, sheet.height);
            // The roots lie on the inside edges of the two halves. Keeping the
            // pivots there gives a real shoulder-like flap rather than scaling a
            // decorative overlay from its centre.
            var pivot = rightWing ? new Vector2(.055f, .45f) : new Vector2(.945f, .45f);
            return Sprite.Create(sheet, rect, pivot, 512f);
        }

        private void RenderEggDetails(Vector2 center, Color fire, bool egg, float flame, float time)
        {
            if (eggHalo != null) eggHalo.gameObject.SetActive(egg);
            if (eggCracks == null) return;
            for (var i = 0; i < eggCracks.Length; i++)
                eggCracks[i].gameObject.SetActive(egg);
            if (!egg) return;

            effectStyle.ApplyLine(eggHalo);
            for (var i = 0; i < eggHalo.positionCount; i++)
            {
                var angle = i / (float)(eggHalo.positionCount - 1) * Mathf.PI * 2f;
                var radius = new Vector2(.48f + flame * .07f, .72f + flame * .08f);
                eggHalo.SetPosition(i, center + new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y));
            }

            var pulse = 1f + Mathf.Sin(time * 6.2f) * .025f;
            for (var i = 0; i < eggCracks.Length; i++)
            {
                var angle = (-1.15f + i * .82f) + Mathf.Sin(time * 1.7f + i) * .06f;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var side = new Vector2(-dir.y, dir.x);
                var start = center + dir * (.18f * pulse);
                var middle = center + dir * (.43f * pulse) + side * ((i % 2 == 0 ? 1f : -1f) * .08f);
                var end = center + dir * (.70f * pulse) + side * ((i % 2 == 0 ? -1f : 1f) * .13f);
                effectStyle.ApplyLine(eggCracks[i]);
                eggCracks[i].SetPosition(0, start);
                eggCracks[i].SetPosition(1, middle);
                eggCracks[i].SetPosition(2, end);
                for (var p = 3; p < eggCracks[i].positionCount; p++) eggCracks[i].SetPosition(p, end);
            }
        }

        private void RenderDiveRings(Vector2 center, Color fire, bool egg, float time)
        {
            if (diveRings == null) return;
            var active = !egg && diveBurstTimer > 0f;
            if (diveBurstTimer > 0f) diveBurstTimer = Mathf.Max(0f, diveBurstTimer - Time.deltaTime);
            for (var i = 0; i < diveRings.Length; i++)
            {
                var ring = diveRings[i];
                ring.gameObject.SetActive(active);
                if (!active) continue;
                var progress = 1f - Mathf.Clamp01(diveBurstTimer / .72f);
                var radius = .34f + progress * (1.34f + i * .22f);
                var gapCenter = time * (i == 0 ? 2.8f : -2.2f) + i * 2.1f;
                var alpha = Mathf.Clamp01(1f - progress * .9f);
                effectStyle.ApplyLine(ring, alpha * effectStyle.AlphaOverLife.Evaluate(1-alpha));
                for (var p = 0; p < ring.positionCount; p++)
                {
                    var t = p / (float)(ring.positionCount - 1);
                    var angle = gapCenter + t * Mathf.PI * 2.0f;
                    var wobble = Mathf.Sin(t * Mathf.PI * 6f + time * 9f) * .035f;
                    ring.SetPosition(p, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius + wobble));
                }
            }
        }

        private static Vector2 Rotate(Vector2 value, float degrees)
        {
            var r = degrees * Mathf.Deg2Rad;
            var c = Mathf.Cos(r);
            var s = Mathf.Sin(r);
            return new Vector2(value.x * c - value.y * s, value.x * s + value.y * c);
        }

        private SpriteRenderer CreateGlow(string name, int order)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(visualRoot, false);
            renderer.sprite = glowSprite;
            renderer.sortingOrder = order;
            return renderer;
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
            if (wingMaterial != null) Destroy(wingMaterial);
            if (particleMaterial != null) Destroy(particleMaterial);
            if (visualRoot != null) Destroy(visualRoot.gameObject);
        }
    }
}
