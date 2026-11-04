using UnityEngine;

namespace DocksDesSuds.GameModes
{
    /// <summary>
    /// Tiny procedural sound bank: clips are synthesized once at first use, no audio assets needed.
    /// Swap any of these for real AudioClips later if wanted.
    /// </summary>
    public static class ShooterSfx
    {
        const int Rate = 44100;

        static AudioClip _laser, _explosion, _hit, _hurt, _wave, _button, _gameOver;

        public static AudioClip Laser => _laser ??= Make("Laser", 0.14f, (t, d) =>
            Mathf.Sin(2 * Mathf.PI * Mathf.Lerp(1400f, 300f, t / d) * t) * Env(t, d, 0.004f) * 0.5f);

        public static AudioClip Explosion => _explosion ??= Make("Explosion", 0.55f, (t, d) =>
            (Random.value * 2f - 1f) * Mathf.Pow(1f - t / d, 2.5f) * 0.8f
            + Mathf.Sin(2 * Mathf.PI * Mathf.Lerp(120f, 40f, t / d) * t) * (1f - t / d) * 0.5f);

        public static AudioClip Hit => _hit ??= Make("Hit", 0.07f, (t, d) =>
            Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 900f * t)) * Env(t, d, 0.002f) * 0.25f);

        public static AudioClip Hurt => _hurt ??= Make("Hurt", 0.35f, (t, d) =>
            Mathf.Sign(Mathf.Sin(2 * Mathf.PI * Mathf.Lerp(220f, 90f, t / d) * t)) * Env(t, d, 0.005f) * 0.4f);

        public static AudioClip Wave => _wave ??= Make("Wave", 0.5f, (t, d) =>
            Mathf.Sin(2 * Mathf.PI * (t < d * 0.5f ? 660f : 880f) * t) * Env(t, d, 0.01f) * 0.35f);

        public static AudioClip Button => _button ??= Make("Button", 0.08f, (t, d) =>
            Mathf.Sin(2 * Mathf.PI * 1200f * t) * Env(t, d, 0.002f) * 0.4f);

        public static AudioClip GameOver => _gameOver ??= Make("GameOver", 1.2f, (t, d) =>
        {
            float f = t < 0.3f ? 440f : t < 0.6f ? 370f : t < 0.9f ? 311f : 262f;
            return Mathf.Sin(2 * Mathf.PI * f * t) * Env(t, d, 0.01f) * 0.35f;
        });

        public static void Play(AudioClip clip, Vector3 position, float volume = 1f, float pitchJitter = 0f)
        {
            if (clip == null) return;
            var go = new GameObject("SFX " + clip.name);
            go.transform.position = position;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = volume;
            src.spatialBlend = 1f;
            src.minDistance = 1.5f;
            src.maxDistance = 40f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            src.Play();
            Object.Destroy(go, clip.length / Mathf.Max(0.1f, src.pitch) + 0.1f);
        }

        static float Env(float t, float d, float attack)
        {
            float a = Mathf.Clamp01(t / attack);
            float r = 1f - t / d;
            return a * r * r;
        }

        static AudioClip Make(string name, float duration, System.Func<float, float, float> sample)
        {
            int n = Mathf.CeilToInt(duration * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(sample(i / (float)Rate, duration), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
