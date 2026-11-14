using UnityEngine;

namespace ShiftRush.VFX
{
    /// <summary>
    /// Every effect the game spawns, in one place: Resources/SR_VFXKit, made by SHIFT RUSH → VFX → Build VFX Kit.
    /// Particle prefabs are generated in the neon style; the full-screen overlays are Hovl Studio prefabs (a paid,
    /// git-ignored pack: without it those slots are empty and the game simply skips them).
    /// </summary>
    [CreateAssetMenu(menuName = "SHIFT RUSH/VFX Kit", fileName = "SR_VFXKit")]
    public class VFXKit : ScriptableObject
    {
        [Header("On the car")]
        public ParticleSystem nitroFlame;
        public Material nitroTrail;
        public ParticleSystem landingPuff;
        public ParticleSystem dirtSpray;

        [Header("SHIFT")]
        public ParticleSystem arrivalBurst;

        [Header("Track air")]
        public ParticleSystem snow;
        public ParticleSystem motes;

        [Header("Full-screen overlays (Hovl Studio)")]
        [Tooltip("While the SHIFT timing bar plays in slow motion.")]
        public GameObject slowMoOverlay;
        [Tooltip("The instant the car warps into a SHIFT world.")]
        public GameObject warpOverlay;
        [Tooltip("While the player's nitro burns.")]
        public GameObject nitroOverlay;
        [Tooltip("Tints for the overlays (the Hovl shader's _Color), in the game's palette. Alpha 0 keeps the pack's own.")]
        public Color slowMoTint = new Color(0.22f, 0.88f, 1f, 1f);
        public Color warpTint = new Color32(255, 64, 217, 255);
        public Color nitroTint = new Color(1f, 0.7f, 0.95f, 1f);

        [Header("Colours")]
        public Color magenta = new Color32(255, 64, 217, 255);
        public Color cyan = new Color32(56, 225, 255, 255);

        static VFXKit instance;
        public static VFXKit Instance { get { if (instance == null) instance = Resources.Load<VFXKit>("SR_VFXKit"); return instance; } }
    }
}
