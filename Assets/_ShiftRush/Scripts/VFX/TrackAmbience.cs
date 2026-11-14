using UnityEngine;

namespace ShiftRush.VFX
{
    /// <summary>
    /// The air on a circuit: falling snow (snowy tracks) or drifting pollen motes catching the light, in a box that
    /// follows the camera so it's always around the car. Also sets the colour of dust kicked up on landings.
    /// Put on a track by the track builder; add one by hand to your own tracks.
    /// </summary>
    public class TrackAmbience : MonoBehaviour
    {
        public enum Air { Snow, Motes, None }

        public Air air = Air.Motes;
        [Tooltip("Dust from landings and verges: snow-white on snowy tracks, pale earth elsewhere.")]
        public Color dust = new Color(0.86f, 0.8f, 0.7f, 0.7f);
        [Range(0f, 2f)] public float density = 1f;

        public static Color DustColour { get { return current != null ? current.dust : new Color(0.86f, 0.8f, 0.7f, 0.7f); } }

        static TrackAmbience current;
        ParticleSystem system;
        Camera cam;

        void OnEnable() { current = this; }
        void OnDisable() { if (current == this) current = null; }

        void Start()
        {
            var kit = VFXKit.Instance;
            if (kit == null || air == Air.None) return;
            var prefab = air == Air.Snow ? kit.snow : kit.motes;
            if (prefab == null) return;
            system = Instantiate(prefab, transform);
            var em = system.emission;
            em.rateOverTimeMultiplier *= density;
        }

        void LateUpdate()
        {
            if (system == null) return;
            if (cam == null || !cam.isActiveAndEnabled) cam = ScreenFX.ActiveCamera();
            if (cam == null) return;
            // Sit a little ahead of the camera so the box fills the view.
            system.transform.position = cam.transform.position + cam.transform.forward * 14f;
        }
    }
}
