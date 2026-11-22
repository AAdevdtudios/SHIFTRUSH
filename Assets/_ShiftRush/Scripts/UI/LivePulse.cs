using UnityEngine;
using UnityEngine.UI;

namespace ShiftRush.UI
{
    /// <summary>The white dot in the LIVE tag: breathes like a recording light.</summary>
    public class LivePulse : MonoBehaviour
    {
        [SerializeField] float period = 1.4f;
        Graphic graphic;

        void Awake() { graphic = GetComponent<Graphic>(); }

        void Update()
        {
            if (graphic == null) return;
            float t = Mathf.Repeat(Time.unscaledTime / period, 1f);
            var c = graphic.color;
            c.a = t < 0.6f ? 1f : 0.25f;
            graphic.color = c;
        }
    }
}
