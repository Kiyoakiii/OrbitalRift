using UnityEngine;

namespace OrbitalRift
{
    // Короткие синтезированные эффекты: не требуют сторонних аудиофайлов.
    public static class SoundEffects
    {
        public static AudioClip CreateEnemyHit()
        {
            return Create("Enemy hit", .11f, (t, length) =>
            {
                var envelope = Mathf.Clamp01(1f - t / length);
                var frequency = Mathf.Lerp(650f, 1180f, t / length);
                return Mathf.Sin(t * frequency * Mathf.PI * 2f) * envelope * .28f;
            });
        }

        public static AudioClip CreateEnemyDeath()
        {
            return Create("Enemy destroyed", .34f, (t, length) =>
            {
                var envelope = Mathf.Pow(Mathf.Clamp01(1f - t / length), 1.8f);
                var frequency = Mathf.Lerp(420f, 85f, t / length);
                var tone = Mathf.Sin(t * frequency * Mathf.PI * 2f);
                var noise = Mathf.PerlinNoise(t * 420f, 3.7f) * 2f - 1f;
                return (tone * .65f + noise * .35f) * envelope * .35f;
            });
        }

        private static AudioClip Create(string name, float length, System.Func<float, float, float> sample)
        {
            const int sampleRate = 44100;
            var count = Mathf.CeilToInt(length * sampleRate);
            var samples = new float[count];
            for (var i = 0; i < count; i++) samples[i] = sample(i / (float)sampleRate, length);
            var clip = AudioClip.Create(name, count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
