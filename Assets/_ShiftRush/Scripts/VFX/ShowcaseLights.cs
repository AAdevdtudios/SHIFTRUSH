using UnityEngine;

namespace ShiftRush.VFX
{
    /// <summary>
    /// Keeps the main menu's night airstrip alive: aircraft strobes and beacon, police lights flashing far off,
    /// turning props and floodlights that breathe a little. Built by SHIFT RUSH → Build Menu Showcase.
    /// Runs on unscaled time so pausing never freezes the backdrop.
    /// </summary>
    public class ShowcaseLights : MonoBehaviour
    {
        [System.Serializable]
        public class Blinker
        {
            public Renderer bulb;
            public Light light;
            [ColorUsage(false, true)] public Color color = Color.white;
            [Tooltip("Seconds per cycle.")] public float period = 1.2f;
            [Tooltip("Share of the cycle the light is on.")] [Range(0.02f, 1f)] public float duty = 0.08f;
            [Tooltip("Where in the cycle it starts, 0..1.")] public float phase;
            [Tooltip("Pulse smoothly instead of a hard flash.")] public bool smooth;
            [HideInInspector] public float lightMax;
        }

        public Blinker[] blinkers;
        [Tooltip("Spun around their local Z axis (engine props).")]
        public Transform[] props;
        public float propSpeed = 900f;
        [Tooltip("Helicopter rotors, spun around their local Y axis, alternate ones the other way (tandem rotors).")]
        public Transform[] rotors;
        [Tooltip("Idle rotor speed, degrees a second: slow enough to read the blades.")]
        public float rotorSpeed = 140f;
        [Tooltip("Lights that flicker very slightly, like big lamps on a generator.")]
        public Light[] breathers;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseId = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock block;
        float[] breatherBase;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            if (blinkers != null) foreach (var b in blinkers) if (b.light != null) b.lightMax = b.light.intensity;
            if (breathers != null)
            {
                breatherBase = new float[breathers.Length];
                for (int i = 0; i < breathers.Length; i++) if (breathers[i] != null) breatherBase[i] = breathers[i].intensity;
            }
        }

        void Update()
        {
            float t = Time.unscaledTime;
            if (blinkers != null)
                foreach (var b in blinkers)
                {
                    float c = Mathf.Repeat(t / Mathf.Max(0.05f, b.period) + b.phase, 1f);
                    float on = b.smooth ? Mathf.Pow(Mathf.Max(0f, Mathf.Sin(c * Mathf.PI * 2f)), 3f) : (c < b.duty ? 1f : 0f);
                    if (b.bulb != null)
                    {
                        b.bulb.GetPropertyBlock(block);
                        block.SetColor(EmissionId, b.color * on);
                        block.SetColor(BaseId, Color.Lerp(new Color(0.06f, 0.06f, 0.08f), b.color, on)); // unlit HDR bulbs
                        b.bulb.SetPropertyBlock(block);
                    }
                    if (b.light != null) b.light.intensity = b.lightMax * on;
                }

            if (props != null)
                foreach (var p in props) if (p != null) p.Rotate(0f, 0f, propSpeed * Time.unscaledDeltaTime, Space.Self);

            if (rotors != null)
                for (int i = 0; i < rotors.Length; i++)
                    if (rotors[i] != null) rotors[i].Rotate(0f, (i % 2 == 0 ? 1f : -1f) * rotorSpeed * Time.unscaledDeltaTime, 0f, Space.Self);

            if (breathers != null)
                for (int i = 0; i < breathers.Length; i++)
                    if (breathers[i] != null)
                        breathers[i].intensity = breatherBase[i] * (0.94f + 0.06f * Mathf.PerlinNoise(t * 3.1f, i * 7.3f));
        }
    }
}
