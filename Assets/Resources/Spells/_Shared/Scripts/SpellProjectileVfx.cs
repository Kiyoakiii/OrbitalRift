using UnityEngine;

namespace OrbitalRift
{
    // Presentation only. The existing Projectile/GameManager own travel and damage.
    public sealed class SpellProjectileVfx : MonoBehaviour
    {
        public SpellVfxProfile Profile;
        public SpellImpactVfx ImpactPrefab;
        public MeshRenderer Core, Glow;
        public TrailRenderer MainTrail, RibbonA, RibbonB;
        public ParticleSystem MainParticles, MicroParticles;
        private MaterialPropertyBlock block;
        private Projectile owner;
        private float age, remaining;
        private bool ending;
        private SpellVfxMotion motion;
        public Projectile Owner => owner;

        public void Begin(Projectile projectile)
        {
            owner = projectile; ending = false; age = 0;
            owner.SpellVfx = this;
            owner.Renderer.enabled = owner.Shot != null && owner.Shot.Sprite != null && !owner.Shot.Appearance.Enabled;
            transform.SetPositionAndRotation(owner.transform.position, Quaternion.identity);
            FaceVelocity();
            Core.enabled = Glow.enabled = true;
            if(owner.Shot != null && owner.Shot.Appearance.Enabled) Core.enabled=false;
            ConfigureTrail(MainTrail, Profile.TrailWidth, Profile.TrailColor);
            ConfigureTrail(RibbonA, .024f, Profile.UseLifetimeGradients ? Profile.RibbonOverTrail : RibbonGradient(Profile.RibbonColor));
            ConfigureTrail(RibbonB, .018f, Profile.UseLifetimeGradients ? Profile.RibbonOverTrail : RibbonGradient(Profile.ParticleColor));
            ConfigureParticles(MainParticles, 1, Profile.ParticleCount);
            ConfigureParticles(MicroParticles, .40f, Profile.MicroCount);
            RenderBody();
            motion = GetComponent<SpellVfxMotion>();
            if (motion != null) { if(owner.Shot != null && owner.Shot.Appearance.Enabled) { motion.End(); motion=null; } else motion.Begin(Profile, (projectile.GetInstanceID() & 1023) * .071f); }
            SetLayerMask(255);
        }

        public void End()
        {
            if (ending) return;
            ending = true;
            if (motion != null) motion.End();
            if (owner != null) { owner.SpellVfx = null; owner.Renderer.enabled = true; }
            owner = null;
            Core.enabled = Glow.enabled = false;
            MainTrail.emitting = RibbonA.emitting = RibbonB.emitting = false;
            MainParticles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            MicroParticles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            remaining = Mathf.Max(Profile.TrailLifetime, Profile.ParticleLifetime) + .08f;
        }

        public bool Tick(float dt)
        {
            if (!ending && (owner == null || !owner.gameObject.activeInHierarchy)) End();
            if (ending) remaining -= dt;
            else
            {
                age += dt;
                transform.position = owner.transform.position;
                FaceVelocity(); RenderBody();
                if (motion != null) motion.Tick(age, dt);
            }
            // Manual simulation follows the game's pause state, not wall-clock time.
            if (dt > 0)
            {
                MainParticles.Simulate(dt, false, false, false);
                MicroParticles.Simulate(dt, false, false, false);
            }
            return ending && remaining <= 0;
        }

        private void FaceVelocity()
        {
            if (owner.Velocity.sqrMagnitude > .00001f)
                transform.up = owner.Velocity.normalized;
        }

        private void RenderBody()
        {
            var p = Profile;
            var spawn = Mathf.Lerp(.6f, 1, Mathf.Clamp01(age / .055f));
            var pulse = 1 + Mathf.Sin(age * p.PulseSpeed * Mathf.PI * 2) * p.PulseAmount;
            Core.transform.localScale = new Vector3(p.CoreSize.x, p.CoreSize.y, 1) * (spawn * pulse);
            Glow.transform.localScale = new Vector3(p.GlowSize.x, p.GlowSize.y, 1) * (spawn * (2 - pulse));
            var life = owner != null && owner.Shot != null ? owner.Shot.Lifetime : 4.25f;
            var t = Mathf.Clamp01(age / Mathf.Max(.01f, life));
            var core = p.UseLifetimeGradients ? p.CoreColor * p.CoreOverLife.Evaluate(t) : p.CoreColor;
            var glow = p.UseLifetimeGradients ? p.GlowColor * p.GlowOverLife.Evaluate(t) : p.GlowColor;
            if (p.UseLifetimeGradients) { core.a *= p.AlphaOverLife.Evaluate(t); glow.a *= p.GlowFade.Evaluate(t); }
            Tint(Core, core, p.CoreBrightness);
            Tint(Glow, glow, p.GlowBrightness);
            var phase = age * p.SecondaryMotionSpeed * Mathf.PI * 2;
            RibbonA.transform.localPosition = new Vector3(Mathf.Sin(phase) * p.SecondaryMotionRadius, -.08f + Mathf.Cos(phase) * .035f, 0);
            RibbonB.transform.localPosition = new Vector3(Mathf.Sin(-phase * .79f + 2.1f) * p.SecondaryMotionRadius, -.14f, 0);
        }

        private void Tint(Renderer renderer, Color color, float brightness)
        {
            if (block == null) block = new MaterialPropertyBlock();
            block.SetColor("_Tint", new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a));
            renderer.SetPropertyBlock(block);
        }

        private void ConfigureTrail(TrailRenderer trail, float width, Gradient gradient)
        {
            trail.Clear(); trail.time = Profile.TrailLifetime;
            trail.widthMultiplier = width; trail.widthCurve = Profile.TrailShape;
            trail.colorGradient = gradient; trail.emitting = true;
            Tint(trail, Color.white, Profile.TrailBrightness);
        }

        private static Gradient RibbonGradient(Color color)
        {
            var result = new Gradient();
            result.SetKeys(new[] { new GradientColorKey(color, 0), new GradientColorKey(color, 1) },
                new[] { new GradientAlphaKey(color.a, 0), new GradientAlphaKey(0, 1) });
            return result;
        }

        private void ConfigureParticles(ParticleSystem ps, float size, float count)
        {
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startSize = new ParticleSystem.MinMaxCurve(Profile.ParticleSize * size * .55f, Profile.ParticleSize * size * 1.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(Profile.ParticleSpeed * .2f, Profile.ParticleSpeed);
            main.startLifetime = new ParticleSystem.MinMaxCurve(Profile.ParticleLifetime * .45f, Profile.ParticleLifetime);
            main.startColor = new ParticleSystem.MinMaxGradient(Profile.ParticleColor * new Color(.6f, .6f, .8f, .6f), Profile.ParticleColor);
            var emission = ps.emission; emission.rateOverTime = count;
            var noise = ps.noise; noise.strength = Profile.NoiseStrength;
            if (Profile.UseLifetimeGradients)
            {
                var color = ps.colorOverLifetime; color.enabled = true; color.color = Profile.ParticleOverLife;
                var scale = ps.sizeOverLifetime; scale.enabled = true; scale.size = new ParticleSystem.MinMaxCurve(1, Profile.ParticleSizeOverLife);
            }
            ps.Simulate(0, false, true, false);
        }

        private void OnDisable()
        {
            if (motion != null) motion.End();
            if (owner != null) { owner.SpellVfx = null; owner.Renderer.enabled = true; owner = null; }
            MainTrail.Clear(); RibbonA.Clear(); RibbonB.Clear();
            MainParticles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            MicroParticles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void SetLayerMask(int mask)
        {
            Core.enabled = !ending && (mask & 1) != 0 && !(owner != null && owner.Shot != null && (owner.Shot.Appearance.Enabled || owner.Shot.Sprite != null));
            Glow.enabled = !ending && (mask & 32) != 0;
            MainTrail.enabled = (mask & 4) != 0;
            RibbonA.enabled = RibbonB.enabled = (mask & 2) != 0;
            MainParticles.GetComponent<ParticleSystemRenderer>().enabled = (mask & 8) != 0;
            MicroParticles.GetComponent<ParticleSystemRenderer>().enabled = (mask & 8) != 0;
            if (motion != null) motion.SetLayerMask(mask);
        }
    }
}
