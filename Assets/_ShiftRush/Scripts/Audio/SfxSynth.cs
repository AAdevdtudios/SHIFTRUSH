using UnityEngine;

namespace ShiftRush.Audio
{
    /// <summary>
    /// SHIFT//RUSH's built-in sounds, made from code: a small synth (sine, detuned saw, square, noise, a low-pass
    /// filter, bit-crush and soft clip). Everything sits in A (A, C#, E) so the race calls, the SHIFT moments and
    /// the music agree. The house sound: clean bells for things you did right, crunchy bit-crushed grit for
    /// things that went wrong, and tape-style pitch sweeps for the time-bending SHIFT.
    /// </summary>
    public static class SfxSynth
    {
        const int Rate = 44100;
        const float A4 = 440f, Cs5 = 554.37f, E5 = 659.25f, A5 = 880f, Cs6 = 1108.73f, E6 = 1318.51f, A6 = 1760f;

        public static AudioClip Make(Sfx s)
        {
            float[] d;
            switch (s)
            {
                case Sfx.CountTick: d = CountTick(); break;
                case Sfx.CountGo: d = CountGo(); break;
                case Sfx.ShiftOpen: d = ShiftOpen(); break;
                case Sfx.ShiftHit: d = Bell(A5, 0.7f, 1f); break;
                case Sfx.ShiftMiss: d = Glitch(); break;
                case Sfx.ShiftWarp: d = Warp(); break;
                case Sfx.ShiftArrive: d = Arrive(); break;
                case Sfx.NitroGain: d = Arpeggio(new[] { A5, Cs6, E6, A6 }, 0.055f, 0.35f); break;
                case Sfx.RingPass: d = Ping(); break;
                case Sfx.TrapHit: d = Thud(); break;
                case Sfx.Lap: d = Notes(new[] { E5, A5 }, 0.12f, 0.6f); break;
                case Sfx.BestLap: d = Notes(new[] { A5, Cs6, E6, A6 }, 0.08f, 0.8f); break;
                case Sfx.FinalLap: d = FinalLap(); break;
                case Sfx.Finish: d = Chord(new[] { A4, Cs5 * 0.94387f /* C */, E5 }, 1.4f); break;
                case Sfx.FinishPodium: d = Chord(new[] { A4, Cs5, E5, A5 }, 1.8f); break;
                case Sfx.Respawn: d = Respawn(); break;
                case Sfx.Overtake: d = Bell(E6, 0.18f, 0.6f); break;
                default: return null;
            }
            Normalise(d, 0.9f);
            var clip = AudioClip.Create("SFX " + GameSfx.FileName(s), d.Length, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        // ------------------------------------------------------------------ sounds

        // Race countdown: a short, gritty square blip (3, 2, 1 rise in pitch from the caller).
        static float[] CountTick()
        {
            var d = Buf(0.18f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = T(i);
                d[i] = (Square(A5, t) * 0.5f + Sine(A5, t)) * Env(t, 0.003f, 18f);
            }
            Crush(d, 6, 1);
            return d;
        }

        // GO: a bright, wide A major stab with a noise burst at the front.
        static float[] CountGo()
        {
            var d = Buf(0.9f);
            var rng = new System.Random(7);
            foreach (var f in new[] { A5, Cs6, E6, A6 })
                for (int i = 0; i < d.Length; i++) d[i] += Saw(f, T(i), 0.006f) * 0.3f * Env(T(i), 0.004f, 3.5f);
            for (int i = 0; i < d.Length; i++) d[i] += Noise(rng) * 0.12f * Env(T(i), 0.001f, 40f);
            LowPass(d, 5200f);
            return d;
        }

        // Time slows: a tape-stop sweep down, with a breath of falling noise.
        static float[] ShiftOpen()
        {
            var d = Buf(0.65f);
            var rng = new System.Random(3);
            float phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = T(i), k = t / 0.65f;
                float f = Mathf.Lerp(1100f, 160f, 1f - (1f - k) * (1f - k));
                phase += f / Rate;
                float tone = Mathf.Sin(phase * Mathf.PI * 2f) + 0.35f * Mathf.Sin(phase * Mathf.PI * 4.01f);
                d[i] = tone * 0.6f * Env(t, 0.01f, 3f) + Noise(rng) * 0.18f * (1f - k);
            }
            LowPass(d, 3000f);
            return d;
        }

        // Inharmonic bell: the "you nailed it" sound. Struck again a fifth higher for the second hit.
        static float[] Bell(float f, float length, float brightness)
        {
            var d = Buf(length);
            for (int i = 0; i < d.Length; i++)
            {
                float t = T(i);
                d[i] = Sine(f, t) * Env(t, 0.002f, 5f)
                     + Sine(f * 2.76f, t) * 0.4f * brightness * Env(t, 0.002f, 9f)
                     + Sine(f * 5.4f, t) * 0.18f * brightness * Env(t, 0.001f, 16f)
                     + Sine(f * 0.5f, t) * 0.25f * Env(t, 0.005f, 4f);
            }
            return d;
        }

        // A miss: a stuttering burst of crushed static over a low buzz.
        static float[] Glitch()
        {
            var d = Buf(0.3f);
            var rng = new System.Random(11);
            for (int i = 0; i < d.Length; i++)
            {
                float t = T(i);
                bool gate = ((int)(t / 0.045f) % 2) == 0 || t > 0.18f;
                d[i] = (Noise(rng) * 0.7f + Square(110f, t) * 0.35f) * (gate ? 1f : 0.1f) * Env(t, 0.002f, 9f);
            }
            Crush(d, 3, 14);
            return d;
        }

        // Into the warp: everything rises to a peak and cuts off, like a reversed cymbal.
        static float[] Warp()
        {
            const float len = 0.95f;
            var d = Buf(len);
            var rng = new System.Random(5);
            float phase = 0f, lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = T(i), k = t / len;
                float f = Mathf.Lerp(110f, 2400f, k * k * k);
                phase += f / Rate;
                float saw = 2f * (phase - Mathf.Floor(phase + 0.5f));
                float swell = k * k;
                float cut = t > len - 0.03f ? (len - t) / 0.03f : 1f;
                float cutoff = Mathf.Lerp(300f, 9000f, k);
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);
                lp += a * (Noise(rng) - lp);
                d[i] = (Mathf.Sin(phase * Mathf.PI * 2f) * 0.5f + saw * 0.25f + lp * 0.9f) * swell * cut;
            }
            SoftClip(d, 1.6f);
            return d;
        }

        // Back in the race: a deep, dropping thump with a sharp crack on top.
        static float[] Arrive()
        {
            var d = Buf(0.8f);
            var rng = new System.Random(9);
            float phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = T(i);
                float f = 38f + 90f * Mathf.Exp(-t * 9f);
                phase += f / Rate;
                d[i] = Mathf.Sin(phase * Mathf.PI * 2f) * Env(t, 0.002f, 4.5f)
                     + Noise(rng) * 0.6f * Env(t, 0.0005f, 55f);
            }
            SoftClip(d, 2.2f);
            LowPass(d, 6000f);
            return d;
        }

        // Nitro filling: a quick upward arpeggio that rings out.
        static float[] Arpeggio(float[] notes, float step, float tail)
        {
            var d = Buf(step * notes.Length + tail);
            for (int n = 0; n < notes.Length; n++)
            {
                int start = (int)(n * step * Rate);
                for (int i = start; i < d.Length; i++)
                {
                    float t = (i - start) / (float)Rate;
                    d[i] += (Square(notes[n], t) * 0.25f + Sine(notes[n], t)) * 0.4f * Env(t, 0.002f, 7f);
                }
            }
            LowPass(d, 7000f);
            return d;
        }

        // Ring passed: a clean ping (the caller raises the pitch ring by ring).
        static float[] Ping()
        {
            var d = Buf(0.3f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = T(i);
                d[i] = Sine(E6, t) * Env(t, 0.001f, 14f) + Sine(E6 * 2f, t) * 0.3f * Env(t, 0.001f, 24f);
            }
            return d;
        }

        // Trap hit: low, distorted, crushed.
        static float[] Thud()
        {
            var d = Buf(0.35f);
            var rng = new System.Random(13);
            for (int i = 0; i < d.Length; i++)
            {
                float t = T(i);
                d[i] = (Square(70f, t) * 0.8f + Noise(rng) * 0.5f * Env(t, 0.001f, 30f)) * Env(t, 0.002f, 10f);
            }
            SoftClip(d, 3f);
            Crush(d, 4, 4);
            LowPass(d, 2500f);
            return d;
        }

        // Lap chimes: bells in sequence.
        static float[] Notes(float[] notes, float step, float tail)
        {
            var d = Buf(step * notes.Length + tail);
            for (int n = 0; n < notes.Length; n++)
            {
                var b = Bell(notes[n], tail, 0.8f);
                int start = (int)(n * step * Rate);
                for (int i = 0; i < b.Length && start + i < d.Length; i++) d[start + i] += b[i] * 0.7f;
            }
            return d;
        }

        // Final lap: two urgent detuned-saw pulses, then the octave.
        static float[] FinalLap()
        {
            var d = Buf(0.9f);
            float[] at = { 0f, 0.16f, 0.32f };
            float[] f = { A4, A4, A5 };
            for (int n = 0; n < 3; n++)
            {
                int start = (int)(at[n] * Rate);
                float decay = n == 2 ? 3.5f : 14f;
                for (int i = start; i < d.Length; i++)
                {
                    float t = (i - start) / (float)Rate;
                    d[i] += Saw(f[n], t, 0.008f) * 0.5f * Env(t, 0.003f, decay);
                }
            }
            LowPass(d, 4200f);
            return d;
        }

        // Finish: a swelling chord (major for the podium, minor otherwise).
        static float[] Chord(float[] notes, float length)
        {
            var d = Buf(length);
            foreach (var f in notes)
                for (int i = 0; i < d.Length; i++)
                {
                    float t = T(i);
                    float env = Mathf.Min(1f, t / 0.08f) * Mathf.Exp(-t * 1.6f);
                    d[i] += (Saw(f, t, 0.005f) * 0.35f + Sine(f, t) * 0.5f) * env;
                }
            LowPass(d, 3800f);
            return d;
        }

        // Put back on the road: a short reversed whoosh that opens up.
        static float[] Respawn()
        {
            const float len = 0.45f;
            var d = Buf(len);
            var rng = new System.Random(17);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = T(i), k = t / len;
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Lerp(400f, 6000f, k) / Rate);
                lp += a * (Noise(rng) - lp);
                float cut = t > len - 0.025f ? (len - t) / 0.025f : 1f;
                d[i] = (lp + Sine(Mathf.Lerp(300f, 900f, k), t) * 0.3f) * k * k * cut;
            }
            return d;
        }

        // ------------------------------------------------------------------ building blocks

        static float[] Buf(float seconds) { return new float[Mathf.CeilToInt(seconds * Rate)]; }
        static float T(int i) { return i / (float)Rate; }
        static float Sine(float f, float t) { return Mathf.Sin(2f * Mathf.PI * f * t); }
        static float Square(float f, float t) { return Mathf.Repeat(f * t, 1f) < 0.5f ? 1f : -1f; }
        static float Noise(System.Random r) { return (float)(r.NextDouble() * 2.0 - 1.0); }

        // Two slightly detuned saws: thicker than one.
        static float Saw(float f, float t, float detune)
        {
            float a = Mathf.Repeat(f * (1f + detune) * t, 1f), b = Mathf.Repeat(f * (1f - detune) * t, 1f);
            return (a + b) - 1f;
        }

        // Fast attack, exponential decay.
        static float Env(float t, float attack, float decay)
        {
            return t < attack ? t / attack : Mathf.Exp(-(t - attack) * decay);
        }

        static void LowPass(float[] d, float cutoff)
        {
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate), y = 0f;
            for (int i = 0; i < d.Length; i++) { y += a * (d[i] - y); d[i] = y; }
        }

        // Fewer bits and held samples: digital grit.
        static void Crush(float[] d, int bits, int hold)
        {
            float steps = Mathf.Pow(2f, bits - 1);
            float held = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                if (i % Mathf.Max(1, hold) == 0) held = Mathf.Round(d[i] * steps) / steps;
                d[i] = held;
            }
        }

        static void SoftClip(float[] d, float drive)
        {
            float norm = (float)System.Math.Tanh(drive);
            for (int i = 0; i < d.Length; i++) d[i] = (float)System.Math.Tanh(d[i] * drive) / norm;
        }

        static void Normalise(float[] d, float peak)
        {
            float max = 0f;
            for (int i = 0; i < d.Length; i++) max = Mathf.Max(max, Mathf.Abs(d[i]));
            if (max < 1e-5f) return;
            float g = peak / max;
            // 4 ms fade-out so nothing clicks at the end.
            int fade = Mathf.Min(d.Length, (int)(0.004f * Rate));
            for (int i = 0; i < d.Length; i++)
            {
                d[i] *= g;
                int fromEnd = d.Length - 1 - i;
                if (fromEnd < fade) d[i] *= fromEnd / (float)fade;
            }
        }
    }
}
