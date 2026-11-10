using UnityEngine;

namespace ShiftRush.UI
{
    /// <summary>One place for the menu's own sounds (Heat plays its hover/click for its widgets separately).</summary>
    [RequireComponent(typeof(AudioSource))]
    public class MenuSounds : MonoBehaviour
    {
        [Tooltip("Moving between gear-menu items: a mechanical clunk.")]
        [SerializeField] AudioClip shift;
        [SerializeField] AudioClip hover;
        [SerializeField] AudioClip select;
        [SerializeField] AudioClip back;
        [Tooltip("Starting a race.")]
        [SerializeField] AudioClip confirm;
        [SerializeField] AudioClip tick;

        static MenuSounds instance;
        AudioSource source;

        void Awake()
        {
            instance = this;
            source = GetComponent<AudioSource>();
        }

        static void Play(AudioClip clip, float volume = 1f, float pitchJitter = 0f)
        {
            if (instance == null || clip == null) return;
            instance.source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            instance.source.PlayOneShot(clip, volume);
        }

        public static void Shift() { if (instance) Play(instance.shift, 0.9f, 0.04f); }
        public static void Hover() { if (instance) Play(instance.hover, 0.5f, 0.03f); }
        public static void Select() { if (instance) Play(instance.select, 0.9f); }
        public static void Back() { if (instance) Play(instance.back, 0.8f); }
        public static void Confirm() { if (instance) Play(instance.confirm, 1f); }
        public static void Tick() { if (instance) Play(instance.tick, 0.6f, 0.05f); }
    }
}
