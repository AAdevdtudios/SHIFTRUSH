using UnityEngine;
using UnityEngine.Rendering;

namespace ShiftRush.VFX
{
    /// <summary>
    /// The menu set's time of day: Night (the wet airstrip under floodlights), Dusk and Day. Each look swaps the sky,
    /// sun, ambient light and fog, dims the set's lamps in daylight and stops the rain. The player picks it in the
    /// garage (L) and it's remembered.
    /// </summary>
    public class ShowroomTime : MonoBehaviour
    {
        [System.Serializable]
        public class Look
        {
            public string name = "NIGHT";
            public Material sky;
            public Color sun = Color.white;
            public float sunIntensity = 0.45f;
            public Vector3 sunAngles = new Vector3(24f, 18f, 0f);
            public Color ambient = new Color(0.07f, 0.08f, 0.12f);
            public Color fog = new Color(0.045f, 0.055f, 0.105f);
            public float fogDensity = 0.014f;
            [Tooltip("Brightness of the set's own lamps (floodlights, bay, beacons).")]
            public float lamps = 1f;
            [Tooltip("Rain, light beams and the bay's glow cards: night and dusk only.")]
            public bool nightPieces = true;
            [Tooltip("Brightens the tarmac: its texture is dark for the wet night look, so daylight needs a lift.")]
            public float groundLift = 1f;
            [Tooltip("Post exposure for this look.")]
            public float exposure = 0.15f;
            [Tooltip("Use the baked night reflections (off = reflect the sky).")]
            public bool bakedReflections = true;
        }

        public Look[] looks = new Look[0];
        public Light sun;
        [Tooltip("Lights the time of day dims (the animated ones are scaled through ShowcaseLights).")]
        public Light[] lamps = new Light[0];
        public GameObject[] nightPieces = new GameObject[0];
        public ShowcaseLights animated;
        public ReflectionProbe probe;
        public Renderer ground;
        public Volume volume;

        const string Key = "GripPro.MenuTime";
        float[] lampBase;
        int current = -1;

        public static ShowroomTime Instance { get; private set; }
        public string Current { get { return current >= 0 && current < looks.Length ? looks[current].name : ""; } }
        public static event System.Action Changed;

        void Awake()
        {
            Instance = this;
            lampBase = new float[lamps.Length];
            for (int i = 0; i < lamps.Length; i++) if (lamps[i] != null) lampBase[i] = lamps[i].intensity;
            Apply(PlayerPrefs.GetInt(Key, 0));
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Next look: Night, Dusk, Day, round again.</summary>
        public void Cycle()
        {
            if (looks.Length == 0) return;
            Apply((current + 1) % looks.Length);
            PlayerPrefs.SetInt(Key, current);
            PlayerPrefs.Save();
        }

        public void Apply(int index)
        {
            if (looks.Length == 0) return;
            current = Mathf.Clamp(index, 0, looks.Length - 1);
            var l = looks[current];
            if (l.sky != null) RenderSettings.skybox = l.sky;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = l.ambient;
            RenderSettings.fogColor = l.fog;
            RenderSettings.fogDensity = l.fogDensity;
            if (sun != null)
            {
                sun.color = l.sun;
                sun.intensity = l.sunIntensity;
                sun.transform.rotation = Quaternion.Euler(l.sunAngles);
            }
            for (int i = 0; i < lamps.Length; i++) if (lamps[i] != null) lamps[i].intensity = lampBase[i] * l.lamps;
            if (animated != null) animated.lightScale = l.lamps;
            foreach (var g in nightPieces) if (g != null) g.SetActive(l.nightPieces);
            if (probe != null) probe.enabled = l.bakedReflections;
            if (ground != null)
            {
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", Color.white * l.groundLift);
                ground.SetPropertyBlock(block);
            }
            if (volume != null && volume.profile != null && volume.profile.TryGet(out UnityEngine.Rendering.Universal.ColorAdjustments ca)) ca.postExposure.value = l.exposure;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            DynamicGI.UpdateEnvironment();
            Changed?.Invoke();
        }
    }
}
