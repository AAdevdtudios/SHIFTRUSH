using UnityEngine;

namespace ShiftRush.Vehicles
{
    /// <summary>
    /// The three drift characters in one place, for tuning by feel. Edit it while the game runs: every car reads it
    /// every physics step, so a change shows up on the next drift, and because it's an asset the change is kept when
    /// Play stops. Lives at Resources/SR_DriftTuning. SHIFT RUSH → Cars → Drift Tuning selects it.
    /// A car with Custom Tuning ticked on its DriftMoves ignores this and uses its own numbers.
    /// </summary>
    [CreateAssetMenu(menuName = "SHIFT RUSH/Drift Tuning", fileName = "SR_DriftTuning")]
    public class DriftTuning : ScriptableObject
    {
        [System.Serializable]
        public class Profile
        {
            [Header("Getting into a drift")]
            [Tooltip("Slowest speed Space starts a drift at, km/h.")]
            public float startKmh = 45f;
            [Tooltip("How hard you must be steering for Space to start a drift (0 = any steer, 1 = full lock).")]
            [Range(0f, 1f)] public float steerToStart = 0.3f;
            [Tooltip("Slide angle a quick tap of Space gives, degrees. Small = a gentle step out.")]
            public float tapAngle = 10f;

            [Header("How far the tail swings out")]
            [Tooltip("The widest the tail can swing out, degrees. 25 = tidy, 40 = proper drift, 55+ = showboating.")]
            public float maxAngle = 42f;
            [Tooltip("While Space is held, the slide opens by this many degrees a second. Low = you must hold Space longer for a big angle.")]
            public float openPerSecond = 45f;
            [Tooltip("How quickly the car's body follows the angle you asked for. Low = lazy, floaty swing. High = snappy.")]
            public float swingSpeed = 3.5f;

            [Header("Letting go")]
            [Tooltip("After Space is released the rear grips again and the slide closes by this many degrees a second, "
                   + "like letting off a real handbrake. High = snaps straight, low = a longer tail-out.")]
            public float letGoRecovery = 90f;

            [Header("Line and speed")]
            [Tooltip("How hard the drift can bend the car's line, m/s². Higher = tighter corners while drifting. A sports car on grip is about 10.")]
            public float cornerGrip = 12f;
            [Tooltip("Speed lost per second while drifting off the throttle, km/h.")]
            public float speedLoss = 8f;
            [Tooltip("Speed lost per second while drifting on the throttle, km/h.")]
            public float speedLossOnThrottle = 1.5f;
            [Tooltip("Extra speed lost per second while Space is held, km/h (the handbrake costs something).")]
            public float handbrakeSpeedLoss = 10f;
            [Tooltip("Tyre side grip while drifting, as a share of normal (0.05..1). Low = the tyres don't fight the slide.")]
            [Range(0.05f, 1f)] public float tyreGripInDrift = 0.15f;

            [Header("Reward")]
            [Tooltip("How fast the drift charges its tiers (1 = normal).")]
            public float chargeRate = 1f;
            [Tooltip("Scales the tier boosts, 12 / 24 / 38 km/h (1 = as listed).")]
            public float boostScale = 1f;
        }

        [Tooltip("Rear-drive style: steps out easily, swings wide, keeps its speed.")]
        public Profile drifty = new Profile
        {
            // Tuned by feel in MovesLab, 2026-11-22.
            startKmh = 45f, steerToStart = 0.30f, tapAngle = 10f, maxAngle = 53f, openPerSecond = 44f, swingSpeed = 3.5f,
            letGoRecovery = 137f, cornerGrip = 12f,
            speedLoss = 8f, speedLossOnThrottle = 1.5f, handbrakeSpeedLoss = 10f, tyreGripInDrift = 0.65f, chargeRate = 1f, boostScale = 1f,
        };
        [Tooltip("All-round: in between.")]
        public Profile balanced = new Profile
        {
            // Between the tuned drifty and grippy.
            startKmh = 50f, steerToStart = 0.38f, tapAngle = 8f, maxAngle = 40f, openPerSecond = 37f, swingSpeed = 3.25f,
            letGoRecovery = 138f, cornerGrip = 13f,
            speedLoss = 11f, speedLossOnThrottle = 2.75f, handbrakeSpeedLoss = 12f, tyreGripInDrift = 0.45f, chargeRate = 0.9f, boostScale = 1.2f,
        };
        [Tooltip("Front-drive or race-car style: needs a firm touch, stays tight, bleeds speed, bigger boost.")]
        public Profile grippy = new Profile
        {
            startKmh = 55f, steerToStart = 0.45f, tapAngle = 6f, maxAngle = 28f, openPerSecond = 30f, swingSpeed = 3f,
            letGoRecovery = 140f, cornerGrip = 14f,
            speedLoss = 14f, speedLossOnThrottle = 4f, handbrakeSpeedLoss = 14f, tyreGripInDrift = 0.25f, chargeRate = 0.8f, boostScale = 1.4f,
        };

        [Header("Weight")]
        [Tooltip("How much a car's weight changes its drift (0 = not at all, 1 = full). Heavy cars swing slower and keep momentum.")]
        [Range(0f, 1f)] public float weightEffect = 1f;
        [Tooltip("The weight counted as middle, kg.")]
        public float middleWeightKg = 1300f;

        public Profile For(DriftCharacter c)
        {
            return c == DriftCharacter.Drifty ? drifty : c == DriftCharacter.Grippy ? grippy : balanced;
        }

        static DriftTuning instance;
        public static DriftTuning Instance { get { if (instance == null) instance = Resources.Load<DriftTuning>("SR_DriftTuning"); return instance; } }
    }
}
