using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ShiftRush.Core;
using ShiftRush.Vehicles;

namespace ShiftRush.CameraFX
{
    /// <summary>
    /// The sense of speed. As the car goes faster the chase camera drops back and low, the field of view widens,
    /// streaks fly past the screen edges and the image picks up a light lens fringe. Hard acceleration and nitro
    /// make the car surge away from the camera before it catches up; nitro shakes the camera (with a jolt as it lights)
    /// and rumbles a controller.
    /// Camera-package agnostic: a bridge (e.g. RCCPSpeedCamera) calls <see cref="Tick"/> every LateUpdate
    /// after the chase camera has positioned itself, and applies <see cref="ExtraFov"/> to its own FOV.
    /// </summary>
    public class SpeedFeel : MonoBehaviour
    {
        [Header("Speed range (km/h)")]
        [Tooltip("Effects start here.")]
        [SerializeField] float startKmh = 70f;
        [Tooltip("Effects are at full strength here.")]
        [SerializeField] float fullKmh = 175f;

        [Header("Camera pull-back (metres)")]
        [Tooltip("How far the camera drops back at full speed.")]
        [SerializeField] float pullBack = 2.4f;
        [Tooltip("Extra pull-back while nitro burns.")]
        [SerializeField] float boostPullBack = 1.6f;
        [Tooltip("Extra pull-back while accelerating hard: the car surges away, then the camera catches up.")]
        [SerializeField] float surgePullBack = 1.2f;
        [Tooltip("How much lower the camera sits at full speed (low reads as faster).")]
        [SerializeField] float drop = 0.35f;
        [Tooltip("How quickly the camera settles into its pulled-back position. Lower = lazier, more dramatic.")]
        [SerializeField] float pullResponse = 2.2f;

        [Header("Field of view (degrees added)")]
        [SerializeField] float extraFov = 14f;
        [SerializeField] float boostFov = 9f;

        [Header("Shake")]
        [SerializeField] float shakeDegrees = 0.35f;
        [SerializeField] float shakeFrequency = 18f;
        [Tooltip("Extra shake (degrees) while nitro burns, on top of the speed shake.")]
        [SerializeField] float boostShakeDegrees = 0.5f;
        [Tooltip("A hard jolt the moment nitro lights (degrees), fading over half a second.")]
        [SerializeField] float boostKickDegrees = 1.6f;
        [Tooltip("Controller rumble while nitro burns (0..1), with a stronger hit as it lights.")]
        [SerializeField] float boostRumble = 0.35f;

        [Header("Speed lines")]
        [SerializeField] Material lineMaterial;
        [SerializeField] float maxLinesPerSecond = 110f;
        [SerializeField] Color lineColor = new Color(1f, 1f, 1f, 0.55f);
        [Tooltip("Lines take this colour while nitro burns.")]
        [SerializeField] Color boostLineColor = new Color(1f, 0.25f, 0.85f, 0.75f);

        [Header("Lens")]
        [SerializeField] float maxChromatic = 0.55f;
        [SerializeField] float maxLensDistortion = -0.22f;
        [SerializeField] float vignetteAtSpeed = 0.32f;

        float intensity, boost, surge, pull, lastSpeed, kick;
        bool wasBoosting, rumbling;
        Camera boundCamera;
        Transform lines;
        ParticleSystem lineSystem;
        Volume volume;
        ChromaticAberration chromatic;
        LensDistortion lens;
        Vignette vignette;

        /// <summary>0..1 speed amount, smoothed.</summary>
        public float Intensity { get { return intensity; } }
        /// <summary>Degrees the camera bridge should add to the chase camera's FOV.</summary>
        public float ExtraFov { get { return (intensity * extraFov + boost * boostFov) * strength * (calm ? 0.5f : 1f); } }

        // Player settings (Settings → Gameplay), read each frame so changes apply mid-race.
        float strength = 1f;
        bool calm, shakeOn = true;

        public void Tick(IVehicle vehicle, bool boosting, Camera cam, bool chaseView, float dt)
        {
            if (cam == null || dt <= 0f) return;
            if (cam != boundCamera) Bind(cam);
            strength = GameSettings.SpeedFxStrength;
            calm = GameSettings.ReducedMotion;
            shakeOn = GameSettings.CameraShake && !calm;

            float speed = vehicle != null ? Mathf.Abs(vehicle.SpeedKmh) : 0f;
            float target = chaseView ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(startKmh, fullKmh, speed)) : 0f;
            intensity = Mathf.MoveTowards(intensity, target, dt * 1.5f);
            boost = Mathf.MoveTowards(boost, boosting && chaseView ? 1f : 0f, dt * (boosting ? 3f : 1.2f));

            // Surge: speed gained per second, beyond gentle acceleration.
            float accel = (speed - lastSpeed) / dt;
            lastSpeed = speed;
            float surgeTarget = chaseView ? Mathf.Clamp01((accel - 15f) / 35f) : 0f;
            surge = Mathf.Lerp(surge, surgeTarget, dt * (surgeTarget > surge ? 4f : 1.2f));

            // Camera: back and low. The actual camera is a child of the chase rig, so offsetting it locally
            // never fights the rig's own smoothing.
            float pullTarget = (intensity * pullBack + boost * boostPullBack + surge * surgePullBack) * strength * (calm ? 0.5f : 1f);
            pull = Mathf.Lerp(pull, pullTarget, dt * pullResponse);
            // Nitro lighting up: a jolt that fades.
            if (boosting && !wasBoosting && chaseView) kick = 1f;
            wasBoosting = boosting;
            kick = Mathf.MoveTowards(kick, 0f, dt * 2f);
            float t = Time.time * shakeFrequency;
            float shake = shakeOn ? ((intensity * 0.6f + boost) * shakeDegrees + boost * boostShakeDegrees + kick * kick * boostKickDegrees) * strength : 0f;
            Rumble(shakeOn ? Mathf.Clamp01(boost * boostRumble + kick * kick * 0.6f) : 0f, shakeOn ? Mathf.Clamp01(intensity * 0.06f + boost * boostRumble * 0.6f) : 0f);
            cam.transform.localPosition = new Vector3(0f, -drop * intensity * strength, -pull);
            cam.transform.localRotation = Quaternion.Euler(
                (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f * shake,
                (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * shake, 0f);

            // Speed lines.
            float lineAmount = calm ? 0f : Mathf.Clamp01(Mathf.InverseLerp(0.25f, 1f, intensity) + boost) * Mathf.Min(strength, 1f);
            var emission = lineSystem.emission;
            emission.rateOverTime = lineAmount * maxLinesPerSecond;
            var main = lineSystem.main;
            main.startColor = Color.Lerp(lineColor, boostLineColor, boost);

            // Lens.
            float lensAmount = calm ? 0f : Mathf.Clamp01(intensity * 0.7f + boost * 0.6f) * strength;
            chromatic.intensity.value = lensAmount * maxChromatic;
            lens.intensity.value = lensAmount * maxLensDistortion;
            vignette.intensity.value = lensAmount * vignetteAtSpeed;
        }

        /// <summary>Controller motors (low = heavy, high = buzz). Only touches the pad while it has something to say.</summary>
        void Rumble(float low, float high)
        {
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (pad == null) return;
            if (Time.timeScale < 0.5f) low = high = 0f; // quiet through the SHIFT slow motion
            bool on = low > 0.01f || high > 0.01f;
            if (!on && !rumbling) return;
            pad.SetMotorSpeeds(low, high);
            rumbling = on;
        }

        /// <summary>Stops the controller rumble (paused, race over, camera gone).</summary>
        public void StopRumble()
        {
            if (!rumbling) return;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (pad != null) pad.SetMotorSpeeds(0f, 0f);
            rumbling = false;
        }

        void OnDisable() { StopRumble(); }

        void Bind(Camera cam)
        {
            boundCamera = cam;
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data != null) data.renderPostProcessing = true;

            if (lines == null) BuildLines();
            lines.SetParent(cam.transform, false);
            if (volume == null) BuildVolume();
        }

        void BuildLines()
        {
            var go = new GameObject("SpeedLines");
            lines = go.transform;
            lineSystem = go.AddComponent<ParticleSystem>();
            lineSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = lineSystem.main;
            main.loop = true;
            main.startLifetime = 0.42f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.08f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 500;
            main.playOnAwake = true;

            var emission = lineSystem.emission;
            emission.rateOverTime = 0f;

            // A ring in front of the camera, hollow in the middle, so streaks frame the car instead of covering it.
            var shape = lineSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 7.5f;
            shape.radiusThickness = 0.45f;
            shape.position = new Vector3(0f, 0.6f, 34f);

            var vel = lineSystem.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            // All three axes must use the same curve mode (two constants), or Unity ignores the velocity.
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.z = new ParticleSystem.MinMaxCurve(-95f, -80f);

            var col = lineSystem.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.075f;
            r.lengthScale = 1f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (lineMaterial != null) r.sharedMaterial = lineMaterial;

            lineSystem.Play();
        }

        void BuildVolume()
        {
            var go = new GameObject("SpeedLens");
            go.transform.SetParent(transform, false);
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 50f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "SpeedLens (runtime)";
            volume.sharedProfile = profile;

            chromatic = profile.Add<ChromaticAberration>(true);
            lens = profile.Add<LensDistortion>(true);
            vignette = profile.Add<Vignette>(true);
            chromatic.intensity.value = 0f;
            lens.intensity.value = 0f;
            vignette.intensity.value = 0f;
            vignette.smoothness.value = 0.45f;
        }
    }
}
