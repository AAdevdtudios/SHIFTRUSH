using UnityEngine;
using UnityEngine.UI;

namespace ShiftRush.UI
{
    /// <summary>A row of slanted segments, like a rev counter, that fill up to a 0–10 value with a quick sweep.</summary>
    public class StatBar : MonoBehaviour
    {
        [SerializeField] Image[] segments = new Image[0];
        [SerializeField] Color empty = new Color(1f, 1f, 1f, 0.12f);

        float shown, target;
        Color fill = Color.white;

        public void SetValue(int value, Color colour)
        {
            target = Mathf.Clamp(value, 0, segments.Length);
            fill = colour;
            shown = 0f; // sweep from empty every time, like a gauge waking up
        }

        void Update()
        {
            shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 24f);
            for (int i = 0; i < segments.Length; i++)
                if (segments[i]) segments[i].color = i < shown ? fill : empty;
        }
    }
}
