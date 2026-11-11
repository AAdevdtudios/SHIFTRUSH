using Michsky.UI.Heat;
using UnityEngine;

namespace ShiftRush.UI
{
    /// <summary>
    /// Fades Heat UI's hover sounds when they repeat (scrolling down Settings, sweeping across rows), then fades
    /// them back in after a short pause. Heat plays every hover through UIManagerAudio's AudioSource and fires the
    /// widget's onHover right after, so this listens to those events and sets that source's volume.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class HeatHoverFade : MonoBehaviour
    {
        [SerializeField] RepeatFade fade = new RepeatFade();
        [Tooltip("How quickly the volume rises back (per second).")]
        [SerializeField] float riseSpeed = 1.5f;

        AudioSource source;
        float volume = 1f;

        void Awake() { source = GetComponent<AudioSource>(); }

        void Start()
        {
            foreach (var e in FindObjectsByType<SettingsElement>(FindObjectsInactive.Include, FindObjectsSortMode.None)) e.onHover.AddListener(OnHover);
            foreach (var b in FindObjectsByType<ButtonManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)) b.onHover.AddListener(OnHover);
            foreach (var p in FindObjectsByType<PanelButton>(FindObjectsInactive.Include, FindObjectsSortMode.None)) p.onHover.AddListener(OnHover);
        }

        void OnHover()
        {
            // The hover sound has just started on this source: turn it down straight away if it's a quick repeat.
            volume = fade.Hit();
            source.volume = volume;
        }

        void Update()
        {
            float target = fade.Level;
            if (target > volume) volume = Mathf.MoveTowards(volume, target, Time.unscaledDeltaTime * riseSpeed);
            source.volume = volume;
        }
    }
}
