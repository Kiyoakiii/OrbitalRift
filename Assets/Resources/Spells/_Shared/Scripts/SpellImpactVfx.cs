using UnityEngine;

namespace OrbitalRift
{
    public sealed class SpellImpactVfx : MonoBehaviour
    {
        public MeshRenderer Flash, Ring, Mist, Aftermath;
        public ParticleSystem Sparks;
        private SpellVfxProfile profile;
        private MaterialPropertyBlock block;
        private float age;

        public void Begin(Vector3 position, SpellVfxProfile settings)
        {
            profile = settings; age = 0; transform.position = position;
            transform.rotation = Quaternion.identity;
            Sparks.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = Sparks.main;
            main.startColor = new ParticleSystem.MinMaxGradient(profile.ImpactColor, profile.CoreColor);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f * profile.ImpactSize, 3.3f * profile.ImpactSize);
            main.startLifetime = new ParticleSystem.MinMaxCurve(profile.ImpactDuration * .18f, profile.ImpactDuration * .5f);
            main.startSize = new ParticleSystem.MinMaxCurve(.018f * profile.ImpactSize, .065f * profile.ImpactSize);
            if (profile.UseLifetimeGradients)
            {
                var colors = Sparks.colorOverLifetime; colors.enabled = true; colors.color = profile.ImpactOverLife;
                var sizes = Sparks.sizeOverLifetime; sizes.enabled = true; sizes.size = new ParticleSystem.MinMaxCurve(1, profile.ParticleSizeOverLife);
            }
            Sparks.Emit(profile.ImpactParticleCount);
            SetLayerMask(255);
            Render();
        }

        public bool Tick(float dt)
        {
            age += dt; Render();
            if (dt > 0) Sparks.Simulate(dt, false, false, false);
            return age >= profile.ImpactDuration;
        }

        private void Render()
        {
            var t = age / Mathf.Max(.01f, profile.ImpactDuration);
            Layer(Flash, profile.CoreColor, .46f * (1 + t * 3), Mathf.Pow(Mathf.Clamp01(1 - t / .09f), 2), 1.6f);
            Layer(Ring, profile.ImpactColor, Mathf.Lerp(.15f, 1.3f, Mathf.Clamp01(t / .35f)), Mathf.Pow(Mathf.Clamp01(1 - t / .35f), .7f), .9f);
            Layer(Mist, profile.RibbonColor, .7f + t * .7f, Mathf.Sin(Mathf.Clamp01((t - .06f) / .86f) * Mathf.PI) * .13f, .6f);
            Layer(Aftermath, profile.ImpactColor, .33f + t * .35f, Mathf.Sin(Mathf.Clamp01((t - .28f) / .72f) * Mathf.PI) * .19f, .7f);
            Mist.transform.localRotation = Quaternion.Euler(0, 0, t * 35);
        }

        private void Layer(MeshRenderer r, Color color, float scale, float alpha, float brightness)
        {
            if (profile.UseLifetimeGradients)
            {
                var t = Mathf.Clamp01(age / Mathf.Max(.01f, profile.ImpactDuration));
                color *= profile.ImpactOverLife.Evaluate(t);
                alpha *= color.a * profile.ImpactFade.Evaluate(t);
            }
            r.transform.localScale = Vector3.one * (scale * profile.ImpactSize);
            if (block == null) block = new MaterialPropertyBlock();
            block.SetColor("_Tint", new Color(color.r * brightness * profile.ImpactBrightness,
                color.g * brightness * profile.ImpactBrightness, color.b * brightness * profile.ImpactBrightness, alpha));
            r.SetPropertyBlock(block);
        }
        private void OnDisable() { if (Sparks != null) Sparks.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear); }
        public void SetLayerMask(int mask)
        {
            Flash.enabled = Ring.enabled = Sparks.GetComponent<ParticleSystemRenderer>().enabled = (mask & 64) != 0;
            Mist.enabled = Aftermath.enabled = (mask & 128) != 0;
        }
    }
}
