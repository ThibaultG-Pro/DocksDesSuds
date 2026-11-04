using System;

namespace DocksDesSuds.GameModes
{
    public enum MusicTrack { None, Action, Upbeat }

    /// <summary>
    /// Pure-C# synthesis of seamless music / ambience loops (no Unity API: safe to run on a worker thread).
    /// Every note is written with wrap-around indexing so the loop point is inaudible.
    /// </summary>
    public static class ProceduralMusic
    {
        public const int SampleRate = 22050;

        // ---------------------------------------------------------------- tracks

        /// <summary>Driving minor-key loop for Shot Fight (A minor: Am F C G), 128 bpm, 8 bars.</summary>
        public static float[] Action() => Song(
            bpm: 128,
            chords: new[] { new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 48, 52, 55 }, new[] { 55, 59, 62 } },
            arpPattern: new[] { 0, 1, 2, 1, 0, 2, 1, 2, 0, 1, 2, 1, 2, 1, 0, 1 },
            arpOctave: 12, hatsEvery16th: false, bassOffbeat: true, arpBright: 0.35, seed: 7);

        /// <summary>Bright major-key loop for Reflex Rush (C major: C G Am F), 140 bpm, 8 bars.</summary>
        public static float[] Upbeat() => Song(
            bpm: 140,
            chords: new[] { new[] { 60, 64, 67 }, new[] { 55, 59, 62 }, new[] { 57, 60, 64 }, new[] { 53, 57, 60 } },
            arpPattern: new[] { 0, 2, 1, 2, 0, 2, 1, 2, 0, 2, 1, 2, 0, 1, 2, 1 },
            arpOctave: 12, hatsEvery16th: true, bassOffbeat: false, arpBright: 0.2, seed: 11);

        /// <summary>Quiet hangar room tone: brown noise + faint mains hum + slow air movement. 12 s loop.</summary>
        public static float[] Ambience()
        {
            int n = SampleRate * 12;
            var buf = new float[n];
            var rng = new Random(3);
            double brown = 0, lp = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / SampleRate;
                brown = (brown + (rng.NextDouble() * 2 - 1) * 0.02) * 0.995;
                lp += (brown - lp) * 0.05;
                // slow swell, 3 full cycles per loop -> seamless
                double swell = 0.75 + 0.25 * Math.Sin(2 * Math.PI * t * 3 / 12.0);
                double hum = 0.012 * Math.Sin(2 * Math.PI * 50 * t) + 0.006 * Math.Sin(2 * Math.PI * 100 * t);
                buf[i] = (float)(lp * 2.2 * swell + hum);
            }
            buf = CrossfadeLoop(buf, SampleRate * 2);
            Normalize(buf, 0.6f);
            return buf;
        }

        // ---------------------------------------------------------------- song builder

        static float[] Song(double bpm, int[][] chords, int[] arpPattern, int arpOctave, bool hatsEvery16th, bool bassOffbeat, double arpBright, int seed)
        {
            const int bars = 8;
            double step = 60.0 / bpm / 4.0; // 16th note
            int stepSamples = (int)Math.Round(step * SampleRate);
            int n = stepSamples * 16 * bars;
            var buf = new float[n];
            var rng = new Random(seed);

            for (int bar = 0; bar < bars; bar++)
            {
                int[] chord = chords[bar / 2 % chords.Length];
                for (int s = 0; s < 16; s++)
                {
                    int start = (bar * 16 + s) * stepSamples;

                    // Drums
                    if (s % 4 == 0) Kick(buf, start);
                    if (s == 4 || s == 12) Snare(buf, start, rng);
                    if (hatsEvery16th || s % 2 == 1) Hat(buf, start, rng, s % 2 == 1 ? 0.12 : 0.06);

                    // Bass: 8ths (optionally only off-beats for a pumping feel)
                    if (s % 2 == 0 && (!bassOffbeat || s % 4 == 2 || s == 0))
                        Tone(buf, start, Midi(chord[0] - 24), step * 1.8, 0.32, Wave.Bass);

                    // Arpeggio
                    int note = chord[arpPattern[s] % chord.Length] + arpOctave + (s >= 12 && bar % 2 == 1 ? 12 : 0);
                    Tone(buf, start, Midi(note), step * 0.9, 0.11, arpBright > 0.3 ? Wave.Pulse : Wave.Soft);
                }

                // Pad: whole chord for the full bar, quiet
                int barStart = bar * 16 * stepSamples;
                foreach (int c in chord) Tone(buf, barStart, Midi(c), step * 16, 0.035, Wave.Pad);
            }

            for (int i = 0; i < n; i++) buf[i] = (float)Math.Tanh(buf[i] * 1.3);
            Normalize(buf, 0.85f);
            return buf;
        }

        enum Wave { Bass, Pulse, Soft, Pad }

        static double Midi(int note) => 440.0 * Math.Pow(2, (note - 69) / 12.0);

        static void Tone(float[] buf, int start, double freq, double dur, double amp, Wave wave)
        {
            int len = (int)(dur * SampleRate);
            double attack = wave == Wave.Pad ? 0.3 : 0.004;
            for (int i = 0; i < len; i++)
            {
                double t = (double)i / SampleRate;
                double env = Math.Min(1, t / attack) * Math.Pow(1 - (double)i / len, wave == Wave.Pad ? 0.5 : 2);
                double ph = 2 * Math.PI * freq * t;
                double v = wave switch
                {
                    Wave.Bass => Math.Sin(ph) + 0.35 * Math.Sin(2 * ph) + 0.15 * Math.Sin(3 * ph),
                    Wave.Pulse => Math.Sin(ph) + 0.33 * Math.Sin(3 * ph) + 0.2 * Math.Sin(5 * ph),
                    Wave.Soft => Math.Sin(ph) + 0.15 * Math.Sin(2 * ph),
                    _ => Math.Sin(ph) + 0.5 * Math.Sin(ph * 1.005) + 0.3 * Math.Sin(2 * ph * 0.998),
                };
                Add(buf, start + i, v * env * amp);
            }
        }

        static void Kick(float[] buf, int start)
        {
            int len = (int)(0.28 * SampleRate);
            double phase = 0;
            for (int i = 0; i < len; i++)
            {
                double t = (double)i / SampleRate;
                double f = 45 + 90 * Math.Exp(-t * 30);
                phase += 2 * Math.PI * f / SampleRate;
                Add(buf, start + i, Math.Sin(phase) * Math.Exp(-t * 11) * 0.55);
            }
        }

        static void Snare(float[] buf, int start, Random rng)
        {
            int len = (int)(0.18 * SampleRate);
            for (int i = 0; i < len; i++)
            {
                double t = (double)i / SampleRate;
                double v = (rng.NextDouble() * 2 - 1) * Math.Exp(-t * 22) * 0.28 + Math.Sin(2 * Math.PI * 185 * t) * Math.Exp(-t * 30) * 0.15;
                Add(buf, start + i, v);
            }
        }

        static void Hat(float[] buf, int start, Random rng, double amp)
        {
            int len = (int)(0.04 * SampleRate);
            double prev = 0;
            for (int i = 0; i < len; i++)
            {
                double t = (double)i / SampleRate;
                double noise = rng.NextDouble() * 2 - 1;
                double hp = noise - prev; // crude high-pass
                prev = noise;
                Add(buf, start + i, hp * Math.Exp(-t * 90) * amp);
            }
        }

        static void Add(float[] buf, int index, double v) => buf[index % buf.Length] += (float)v;

        static float[] CrossfadeLoop(float[] buf, int fade)
        {
            // Blend the tail into the head, then drop the tail: the end flows into the start seamlessly.
            int n = buf.Length - fade;
            var outBuf = new float[n];
            Array.Copy(buf, outBuf, n);
            for (int i = 0; i < fade; i++)
            {
                double k = (double)i / fade;
                outBuf[i] = (float)(buf[i] * k + buf[n + i] * (1 - k));
            }
            return outBuf;
        }

        static void Normalize(float[] buf, float peak)
        {
            float max = 0.0001f;
            foreach (float v in buf) max = Math.Max(max, Math.Abs(v));
            float k = peak / max;
            for (int i = 0; i < buf.Length; i++) buf[i] *= k;
        }
    }
}
