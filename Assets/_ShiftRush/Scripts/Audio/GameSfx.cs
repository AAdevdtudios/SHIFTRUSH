using System.Collections.Generic;
using ShiftRush.Core;
using UnityEngine;

namespace ShiftRush.Audio
{
    /// <summary>The game's one-shot sounds (race calls, SHIFT moments, arena feedback).</summary>
    public enum Sfx
    {
        CountTick, CountGo,
        ShiftOpen, ShiftHit, ShiftMiss, ShiftWarp, ShiftArrive, NitroGain,
        RingPass, TrapHit,
        Lap, BestLap, FinalLap, Finish, FinishPodium,
        Respawn, Overtake,
    }

    /// <summary>
    /// Plays the game's one-shot sounds through the SFX mixer group (so the Car &amp; Effects slider controls them).
    /// Each sound is SHIFT//RUSH's own, synthesised on first use (see <see cref="SfxSynth"/>), all in A so they sit
    /// together and with the music. A file in Resources/Audio/SFX named like the sound (countdown_tick.wav,
    /// shift_hit.ogg, ...) replaces the built-in one, no code needed.
    /// </summary>
    public static class GameSfx
    {
        public const string Folder = "Audio/SFX"; // under Resources

        // Drop-in file names, in Sfx order.
        static readonly string[] Files =
        {
            "countdown_tick", "countdown_go",
            "shift_open", "shift_hit", "shift_miss", "shift_warp", "shift_arrive", "nitro_gain",
            "ring_pass", "trap_hit",
            "lap", "best_lap", "final_lap", "finish", "finish_podium",
            "respawn", "overtake",
        };

        // Mix: how loud each sits next to the engines and music.
        static readonly float[] Volume =
        {
            0.55f, 0.7f,
            0.5f, 0.6f, 0.45f, 0.65f, 0.8f, 0.45f,
            0.45f, 0.42f,
            0.5f, 0.55f, 0.55f, 0.6f, 0.65f,
            0.45f, 0.22f,
        };

        public static string FileName(Sfx s) { return Files[(int)s]; }

        static readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        static AudioSource[] voices;
        static int next;

        public static void Play(Sfx s, float pitch = 1f, float volume = 1f)
        {
            var clip = Clip(s);
            if (clip == null) return;
            var v = Voice();
            if (v == null) return;
            v.pitch = pitch;
            v.volume = Mathf.Clamp01(Volume[(int)s] * volume);
            v.clip = clip;
            v.Play();
        }

        public static AudioClip Clip(Sfx s)
        {
            AudioClip c;
            if (clips.TryGetValue(s, out c) && c != null) return c;
            c = Resources.Load<AudioClip>(Folder + "/" + FileName(s));
            if (c == null) c = SfxSynth.Make(s);
            clips[s] = c;
            return c;
        }

        /// <summary>Builds every sound now, so the first countdown doesn't hitch.</summary>
        public static void Warm()
        {
            foreach (Sfx s in System.Enum.GetValues(typeof(Sfx))) Clip(s);
        }

        static AudioSource Voice()
        {
            if (voices == null || voices[0] == null)
            {
                var go = new GameObject("Game SFX");
                Object.DontDestroyOnLoad(go);
                var group = AudioBoot.Group("SFX");
                voices = new AudioSource[10];
                for (int i = 0; i < voices.Length; i++)
                {
                    var a = go.AddComponent<AudioSource>();
                    a.playOnAwake = false;
                    a.spatialBlend = 0f;
                    a.outputAudioMixerGroup = group;
                    voices[i] = a;
                }
            }
            // Round robin, but don't cut a sound that's still playing if a free voice exists.
            for (int k = 0; k < voices.Length; k++)
            {
                var a = voices[(next + k) % voices.Length];
                if (!a.isPlaying) { next = (next + k + 1) % voices.Length; return a; }
            }
            var v = voices[next];
            next = (next + 1) % voices.Length;
            return v;
        }
    }
}
