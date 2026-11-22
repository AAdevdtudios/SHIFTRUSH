using UnityEngine;

namespace ShiftRush.VFX
{
    /// <summary>
    /// A split-second freeze on a big hit, so it lands. Time drops to a crawl for a few real hundredths of a second,
    /// then goes back to whatever it was (the SHIFT slow motion included). Skipped with Reduced Motion on.
    /// </summary>
    public class HitStop : MonoBehaviour
    {
        static HitStop runner;
        float until = -1f, before = 1f;

        public static void Do(float seconds, float scale = 0.05f)
        {
            if (seconds <= 0f || Core.GameSettings.ReducedMotion || Time.timeScale <= 0f) return;
            if (runner == null)
            {
                var go = new GameObject("HitStop");
                DontDestroyOnLoad(go);
                runner = go.AddComponent<HitStop>();
            }
            runner.Begin(seconds, scale);
        }

        void Begin(float seconds, float scale)
        {
            if (until < 0f) before = Time.timeScale;
            until = Time.unscaledTime + seconds;
            Time.timeScale = Mathf.Min(before, scale);
        }

        void Update()
        {
            if (until < 0f || Time.unscaledTime < until) return;
            until = -1f;
            Time.timeScale = before;
        }
    }
}
