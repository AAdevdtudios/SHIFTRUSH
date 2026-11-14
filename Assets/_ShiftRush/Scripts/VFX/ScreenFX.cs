using UnityEngine;

namespace ShiftRush.VFX
{
    /// <summary>
    /// Full-screen overlays pinned in front of whichever camera is drawing the game (the chase camera in a race).
    /// They're kept light and short: each one fades in, sits at a soft strength, and fades fully away (then is
    /// cleared) as soon as its moment ends; the warp overlay is a brief flash. Runs on unscaled time so it keeps
    /// moving during the SHIFT slow motion.
    /// </summary>
    public class ScreenFX : MonoBehaviour
    {
        public enum Overlay { SlowMo, Warp, Nitro }

        // How strong each overlay gets, how fast it fades (per second), and how long a pulse holds.
        static readonly float[] Strength = { 0.55f, 0.75f, 0.35f };
        const float FadeIn = 5f, FadeOut = 3.5f, PulseHold = 0.25f;

        static ScreenFX instance;
        readonly ParticleSystem[] systems = new ParticleSystem[3];
        readonly Material[] mats = new Material[3];
        readonly Color[] tints = new Color[3];
        readonly float[] level = new float[3], target = new float[3], pulseOff = new float[3];
        Camera cam;

        public static ScreenFX Get()
        {
            if (instance != null) return instance;
            var go = new GameObject("Screen FX");
            instance = go.AddComponent<ScreenFX>();
            return instance;
        }

        void OnDestroy() { if (instance == this) instance = null; }

        public static void Set(Overlay o, bool on)
        {
            var kit = VFXKit.Instance;
            if (kit == null) return;
            var fx = Get();
            int i = (int)o;
            if (on)
            {
                var ps = fx.Ensure(o, kit);
                if (ps == null) return;
                if (!ps.isPlaying) ps.Play(true);
                fx.target[i] = Strength[i];
                fx.pulseOff[i] = 0f;
            }
            else fx.target[i] = 0f;
        }

        /// <summary>A brief flash: in, hold a moment, out.</summary>
        public static void Pulse(Overlay o)
        {
            var kit = VFXKit.Instance;
            if (kit == null) return;
            var fx = Get();
            var ps = fx.Ensure(o, kit);
            if (ps == null) return;
            int i = (int)o;
            ps.Clear(true);
            ps.Play(true);
            fx.level[i] = Strength[i];
            fx.target[i] = Strength[i];
            fx.pulseOff[i] = Time.unscaledTime + PulseHold;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                if (ps == null) continue;
                if (pulseOff[i] > 0f && Time.unscaledTime >= pulseOff[i]) { target[i] = 0f; pulseOff[i] = 0f; }
                level[i] = Mathf.MoveTowards(level[i], target[i], dt * (target[i] > level[i] ? FadeIn : FadeOut) * Strength[i]);
                if (mats[i] != null)
                {
                    var c = tints[i];
                    float k = Strength[i] > 0f ? level[i] / Strength[i] : 0f;
                    // Scale colour and alpha together: fades whether the shader adds or blends.
                    mats[i].SetColor("_Color", new Color(c.r * k * Strength[i], c.g * k * Strength[i], c.b * k * Strength[i], c.a * level[i]));
                }
                if (level[i] <= 0f && target[i] <= 0f && (ps.isPlaying || ps.particleCount > 0))
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // fully gone
            }
        }

        ParticleSystem Ensure(Overlay o, VFXKit kit)
        {
            int i = (int)o;
            var current = ActiveCamera();
            if (current != cam)
            {
                // Camera changed (new scene, menu): rebuild the overlays on the new one.
                cam = current;
                for (int k = 0; k < systems.Length; k++)
                {
                    if (systems[k] != null) Destroy(systems[k].gameObject);
                    systems[k] = null; level[k] = 0f; target[k] = 0f; pulseOff[k] = 0f;
                }
            }
            if (systems[i] != null) return systems[i];
            var prefab = o == Overlay.SlowMo ? kit.slowMoOverlay : o == Overlay.Warp ? kit.warpOverlay : kit.nitroOverlay;
            if (prefab == null || cam == null) return null;
            var go = Instantiate(prefab);
            go.name = "Overlay " + o;
            // The overlay's own script reads Camera.main; give it the camera actually drawing.
            foreach (var mb in go.GetComponents<MonoBehaviour>())
            {
                var f = mb.GetType().GetField("sourceCamera");
                if (f != null && f.FieldType == typeof(Camera)) f.SetValue(mb, cam);
                // It places itself this far in front of the camera: keep that past the near clip plane.
                var d = mb.GetType().GetField("fallbackDistance");
                if (d != null && d.FieldType == typeof(float)) d.SetValue(mb, cam.nearClipPlane + 0.03f);
            }
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = Vector3.forward * (cam.nearClipPlane + 0.03f);
            go.transform.localRotation = Quaternion.identity;
            var ps = go.GetComponent<ParticleSystem>();
            if (ps == null) { Destroy(go); return null; }
            var tint = o == Overlay.SlowMo ? kit.slowMoTint : o == Overlay.Warp ? kit.warpTint : kit.nitroTint;
            foreach (var r in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
                if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Color"))
                {
                    var m = new Material(r.sharedMaterial); // own copy: the pack's material stays untouched
                    if (tint.a <= 0f) tint = m.GetColor("_Color");
                    r.sharedMaterial = m;
                    mats[i] = m;
                }
            tints[i] = tint;
            foreach (var p in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = p.main;
                main.useUnscaledTime = true;
                main.playOnAwake = false;
            }
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            level[i] = 0f;
            systems[i] = ps;
            return ps;
        }

        /// <summary>The enabled on-screen camera with the highest depth.</summary>
        public static Camera ActiveCamera()
        {
            Camera best = null;
            foreach (var c in Camera.allCameras)
            {
                if (!c.enabled || c.targetTexture != null || !c.gameObject.activeInHierarchy) continue;
                if (best == null || c.depth > best.depth) best = c;
            }
            return best != null ? best : Camera.main;
        }
    }
}
