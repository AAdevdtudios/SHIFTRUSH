using UnityEngine;

namespace ShiftRush.UI
{
    /// <summary>
    /// Keeps repeated menu sounds from nagging. Each sound that follows another quickly plays a little quieter;
    /// when the player pauses, the level fades back to full. Used for scrolling through Settings rows and lists.
    /// </summary>
    [System.Serializable]
    public class RepeatFade
    {
        [Tooltip("How much each quick repeat turns the sound down.")]
        public float perRepeat = 0.6f;
        [Tooltip("How fast it recovers once the player stops (repeats per second forgotten).")]
        public float recovery = 2.5f;
        [Tooltip("Never quieter than this.")]
        [Range(0f, 1f)] public float floor = 0.2f;

        float pressure;
        float lastTime = -10f;

        /// <summary>Call when a repeatable sound plays; returns the volume multiplier for it.</summary>
        public float Hit()
        {
            Decay();
            float volume = Level;
            pressure += 1f;
            return volume;
        }

        /// <summary>Current multiplier without registering a new sound.</summary>
        public float Level { get { Decay(); return Mathf.Max(floor, 1f / (1f + pressure * perRepeat)); } }

        void Decay()
        {
            float now = Time.unscaledTime;
            pressure = Mathf.Max(0f, pressure - (now - lastTime) * recovery);
            lastTime = now;
        }
    }
}
