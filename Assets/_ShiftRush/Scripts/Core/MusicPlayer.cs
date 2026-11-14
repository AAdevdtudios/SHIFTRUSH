using UnityEngine;

namespace ShiftRush.Core
{
    /// <summary>
    /// Plays the background music across every scene (menu and races) from Resources/Audio/Music, following the
    /// player's choice: shuffle all (random order, never the same song twice in a row, cross-faded), one song on
    /// repeat, or off. Routed through the mixer's Music group, so the Music volume slider controls it.
    /// The SHIFT slow motion can bend it down (<see cref="SetSlowMo"/>).
    /// </summary>
    public class MusicPlayer : MonoBehaviour
    {
        const float Fade = 1.6f;

        static MusicPlayer instance;
        readonly AudioSource[] sources = new AudioSource[2];
        int active;
        AudioClip last;
        float slowMo; // 0..1
        bool slowTarget;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject("SR Music");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<MusicPlayer>();
        }

        public static void SetSlowMo(bool on) { if (instance != null) instance.slowTarget = on; }

        void Awake()
        {
            var group = AudioBoot.Group("Music");
            for (int i = 0; i < 2; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.ignoreListenerPause = true;
                s.outputAudioMixerGroup = group;
                s.volume = 0f;
                sources[i] = s;
            }
            MusicLibrary.Changed += Apply;
            Apply();
        }

        void OnDestroy() { MusicLibrary.Changed -= Apply; if (instance == this) instance = null; }

        /// <summary>Choice changed: start what it asks for.</summary>
        void Apply()
        {
            string choice = MusicLibrary.Choice;
            if (choice == MusicLibrary.Off) { CrossTo(null, false); return; }
            var repeat = MusicLibrary.RepeatClip;
            if (repeat != null) { CrossTo(repeat, true); return; }
            CrossTo(NextShuffled(), MusicLibrary.Clips.Length == 1); // a one-song library just loops
        }

        AudioClip NextShuffled()
        {
            var clips = MusicLibrary.Clips;
            if (clips.Length == 0) return null;
            if (clips.Length == 1) return clips[0];
            AudioClip pick;
            do pick = clips[Random.Range(0, clips.Length)]; while (pick == last);
            return pick;
        }

        void CrossTo(AudioClip clip, bool loop)
        {
            var cur = sources[active];
            if (clip != null && cur.clip == clip && cur.isPlaying) { cur.loop = loop; return; }
            active = 1 - active;
            var next = sources[active];
            next.Stop();
            next.clip = clip;
            next.loop = loop;
            next.volume = 0f;
            if (clip != null) { next.Play(); last = clip; }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            var cur = sources[active];
            var other = sources[1 - active];
            cur.volume = Mathf.MoveTowards(cur.volume, cur.clip != null ? 1f : 0f, dt / Fade);
            other.volume = Mathf.MoveTowards(other.volume, 0f, dt / Fade);
            if (other.volume <= 0f && other.isPlaying) other.Stop();

            // Shuffle: start the next song a fade's length before this one ends.
            if (cur.clip != null && !cur.loop && MusicLibrary.Choice == MusicLibrary.Shuffle
                && cur.isPlaying && cur.time >= cur.clip.length - Fade)
                CrossTo(NextShuffled(), false);
            else if (cur.clip != null && !cur.loop && !cur.isPlaying && cur.volume > 0f && MusicLibrary.Choice == MusicLibrary.Shuffle)
                CrossTo(NextShuffled(), false);

            // SHIFT slow motion: the music drops in pitch with time, then springs back.
            slowMo = Mathf.MoveTowards(slowMo, slowTarget ? 1f : 0f, dt * (slowTarget ? 4f : 2.5f));
            float pitch = Mathf.Lerp(1f, 0.72f, slowMo);
            cur.pitch = pitch; other.pitch = pitch;
        }
    }
}
