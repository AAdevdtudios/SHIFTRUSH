using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ShiftRush.Core;
using ShiftRush.Vehicles;

namespace ShiftRush.CameraFX
{
    /// <summary>
    /// The sense of speed. As the car goes faster the chase camera drops back and low, the field of view widens,
    /// streaks fly past the screen edges, the lens bows the world outward left and right (leaning into the steering, so
    /// a turn warps the world towards it), the image fringes, and motion blur streaks the scenery while the car stays
    /// sharp (per-object blur). Hard acceleration and nitro
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

        [Header("Drift")]
        [Tooltip("At full drift the camera turns this many degrees back toward the direction of travel, so the car's angle shows.")]
        [SerializeField] float driftSwingDegrees = 16f;
        [Tooltip("And slides out to the side of the slide this far, metres.")]
        [SerializeField] float driftSwingMetres = 1.3f;
        [Tooltip("And pulls back this much further, metres.")]
        [SerializeField] float driftPullBack = 0.7f;
        [Tooltip("Controller rumble at full drift (0..1).")]
        [SerializeField] float driftRumble = 0.3f;

        [Header("Speed lines")]
        [SerializeField] Material lineMaterial;
        [SerializeField] float maxLinesPerSecond = 110f;
        [SerializeField] Color lineColor = new Color(1f, 1f, 1f, 0.55f);
        [Tooltip("Lines take this colour while nitro burns.")]
        [SerializeField] Color boostLineColor = new Color(1f, 0.25f, 0.85f, 0.75f);

        [Header("Lens")]
        [SerializeField] float maxChromatic = 0.55f;
        [Tooltip("Barrel bend at full speed with nitro (positive bows the world outward).")]
        [SerializeField] float maxLensDistortion = 0.42f;
        [Tooltip("How much of the bend is sideways (x) against up and down (y): mostly sideways reads as speed.")]
        [SerializeField] Vector2 lensAxes = new Vector2(1f, 0.35f);
        [Tooltip("How far the bend's centre leans into the steering (share of the screen).")]
        [SerializeField] float lensSteerLean = 0.09f;
        [SerializeField] float vignetteAtSpeed = 0.32f;

        [Header("Motion blur")]
        [Tooltip("Blur at full speed; nitro adds the boost amount.")]
        [SerializeField] float blurAtSpeed = 0.35f;
        [SerializeField] float blurBoost = 0.25f;

        float intensity, boost, surge, pull, lastSpeed, kick, swing, swingSide;
        DriftMoves drift;
        IVehicle driftOf;
        static float pendingJolt;

        /// <summary>A one-off camera jolt and rumble, 0..1 (a big landing, a hit). Uses the nitro-light kick.</summary>
        public static void Jolt(float amount) { pendingJolt = Mathf.Max(pendingJolt, Mathf.Clamp01(amount)); }
        bool wasBoosting, rumbling;
        Camera boundCamera;
        Transform lines;
        ParticleSystem lineSystem;
        Volume volume;
        ChromaticAberration chromatic;
        LensDistortion lens;
        Vignette vignette;
        MotionBlur blur;
        float lean;

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
            // Drift: the camera swings back toward the line of travel and out to the side, so the slide angle reads.
            if (vehicle != driftOf) { driftOf = vehicle; drift = vehicle != null ? vehicle.Transform.GetComponent<DriftMoves>() : null; }
            float swingTarget = drift != null && drift.Drifting && chaseView ? drift.Intensity * Mathf.InverseLerp(0f, drift.maxAngle, drift.SlipAngle) : 0f;
            if (drift != null && drift.Drifting) swingSide = drift.Direction;
            swing = Mathf.Lerp(swing, swingTarget, dt * (swingTarget > swing ? 3f : 2f));
            float swingK = swing * strength * (calm ? 0.4f : 1f);

            float pullTarget = (intensity * pullBack + boost * boostPullBack + surge * surgePullBack + swing * driftPullBack) * strength * (calm ? 0.5f : 1f);
            pull = Mathf.Lerp(pull, pullTarget, dt * pullResponse);
            // Nitro lighting up: a jolt that fades.
            if (boosting && !wasBoosting && chaseView) kick = 1f;
            wasBoosting = boosting;
            if (pendingJolt > 0f && chaseView) { kick = Mathf.Max(kick, pendingJolt); }
            pendingJolt = 0f;
            kick = Mathf.MoveTowards(kick, 0f, dt * 2f);
            float t = Time.time * shakeFrequency;
            float shake = shakeOn ? ((intensity * 0.6f + boost) * shakeDegrees + boost * boostShakeDegrees + kick * kick * boostKickDegrees) * strength : 0f;
            float driftShake = drift != null && drift.Drifting ? drift.Intensity * driftRumble : 0f;
            Rumble(shakeOn ? Mathf.Clamp01(boost * boostRumble + kick * kick * 0.6f + driftShake * 0.7f) : 0f, shakeOn ? Mathf.Clamp01(intensity * 0.06f + boost * boostRumble * 0.6f + driftShake) : 0f);
            cam.transform.localPosition = new Vector3(-swingSide * driftSwingMetres * swingK, -drop * intensity * strength, -pull);
            cam.transform.localRotation = Quaternion.Euler(
                (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f * shake,
                (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * shake - swingSide * driftSwingDegrees * swingK, 0f);

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
            lens.xMultiplier.value = lensAxes.x; lens.yMultiplier.value = lensAxes.y;
            lens.scale.value = 1f + lensAmount * maxLensDistortion * 0.25f; // crop in a touch so the bowed edges stay filled
            // The bend's centre leans into the turn: steer left and the world warps towards the left.
            float steer = vehicle != null && chaseView ? vehicle.SteerInput : 0f;
            lean = Mathf.Lerp(lean, -steer * lensSteerLean * lensAmount, dt * 3f);
            lens.center.value = new Vector2(0.5f + lean, 0.5f);
            vignette.intensity.value = lensAmount * vignetteAtSpeed;
            // Motion blur: the scenery streaks, the car itself (moving with the camera) stays sharp.
            float blurAmount = calm ? 0f : Mathf.Clamp01(Mathf.InverseLerp(0.35f, 1f, intensity) * blurAtSpeed + boost * blurBoost) * Mathf.Min(strength, 1.2f);
            if (blur != null) { blur.active = blurAmount > 0.01f; blur.intensity.value = blurAmount; }
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
            blur = profile.Add<MotionBlur>(true);
            blur.mode.value = MotionBlurMode.CameraAndObjects;
            blur.quality.value = MotionBlurQuality.Medium;
            blur.clamp.value = 0.06f;
            blur.intensity.value = 0f;
            blur.active = false;
            chromatic.intensity.value = 0f;
            lens.intensity.value = 0f;
            vignette.intensity.value = 0f;
            vignette.smoothness.value = 0.45f;
        }
    }
}
