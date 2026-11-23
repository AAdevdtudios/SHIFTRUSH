using UnityEngine;

namespace ShiftRush.Vehicles
{
    /// <summary>How a car takes to drifting. Auto: worked out from the drivetrain by the Car Setup wizard.</summary>
    public enum DriftCharacter { Auto, Drifty, Balanced, Grippy }

    /// <summary>
    /// Grip Pro's drift, built to feel like a driver's car, not a scripted spin.
    ///  - Start: steer into a corner above <see cref="minKmh"/> and touch Space (the handbrake). The tail steps out.
    ///  - How far it steps out is up to the player: a light tap gives a small, smooth slide, holding Space keeps
    ///    swinging the tail further (up to <see cref="maxAngle"/>), and costs a little speed while it's held.
    ///  - Like a real handbrake: the slide lasts as long as Space is held. Let go and the rear grips again and the car
    ///    straightens itself (<see cref="recoverRate"/>); steering changes the line but can't keep the slide alive.
    ///    It never flips the other way.
    ///  - The line bends at the rate a car can really corner at that speed (<see cref="latAccel"/>), a little tighter
    ///    than grip driving, so a drift follows the curve ahead instead of turning on the spot.
    ///  - When the car is straight again the drift ends and pays a boost for the tier it charged (three tiers; longer,
    ///    wider and faster charges quicker). A drift that dies (too slow, airborne, hit) pays nothing.
    /// Every car has a <see cref="character"/>: drifty cars step out easily, wide and long and charge fast; grippy cars
    /// need a firmer touch, stay tight and bleed speed, but pay a bigger boost. Weight nudges it: heavy cars swing in
    /// slower and keep their momentum. Works on the Rigidbody, so it carries over to another car controller.
    /// Rivals drive it with <see cref="SetAIDrift"/> (their hold works like the player holding Space).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DefaultExecutionOrder(61)]
    public class DriftMoves : MonoBehaviour
    {
        [Tooltip("Reads the handbrake. Off for rivals, who call SetAIDrift.")]
        public bool playerControlled = true;

        /// <summary>Reads the player's handbrake: allowed (playerControlled) and this is the car the player is driving.</summary>
        public bool IsPlayer { get { return playerControlled && (vehicle == null || vehicle.PlayerDriven); } }

        [Header("Character")]
        [Tooltip("Drifty, Balanced or Grippy. Auto takes what the Car Setup wizard worked out from the drivetrain.")]
        public DriftCharacter character = DriftCharacter.Auto;
        [Tooltip("What Auto means for this car (written by the Car Setup wizard: rear drive drifty, all-wheel balanced, front drive grippy).")]
        public DriftCharacter autoResolvesTo = DriftCharacter.Balanced;
        [Tooltip("On: the numbers below are used as set. Off: they come from the character and the car's weight.")]
        public bool customTuning = false;
        [Tooltip("Adds the drift effects (smoke, sparks, trails, screech) if the car doesn't have them.")]
        public bool addEffects = true;

        [Header("Starting")]
        [Tooltip("Slowest speed a drift can start at.")]
        public float minKmh = 45f;
        [Tooltip("How hard the player must steer for Space to start a drift.")]
        [Range(0f, 1f)] public float steerToStart = 0.3f;
        [Tooltip("Slide angle a quick tap of Space gives, degrees.")]
        public float startAngle = 10f;

        [Header("Angle")]
        [Tooltip("The widest the tail can step out, degrees.")]
        public float maxAngle = 42f;
        [Tooltip("While Space is held the slide keeps opening by this much a second, up to the widest.")]
        public float kickRate = 45f;
        [Tooltip("How quickly the car's actual angle follows (lower = smoother, lazier).")]
        public float angleResponse = 3.5f;
        [Tooltip("After Space is released the slide closes by this many degrees a second (the rear grips again). "
               + "Counter-steering closes it a little faster, steering into the corner a little slower.")]
        public float recoverRate = 90f;
        [Tooltip("Below this angle, with Space up, the drift is over (degrees).")]
        public float endAngle = 4f;
        [Tooltip("How hard the car is turned to hold the slide angle.")]
        public float yawGain = 7f;

        [Header("Line and speed")]
        [Tooltip("How hard the drift can bend the car's line, m/s² (a sports car on grip is about 10). Sets the turn rate for the speed.")]
        public float latAccel = 12f;
        [Tooltip("Speed lost per second while drifting off the throttle, km/h.")]
        public float dragKmh = 8f;
        [Tooltip("Speed lost per second while drifting on the throttle, km/h.")]
        public float dragKmhOnThrottle = 1.5f;
        [Tooltip("Extra speed lost per second while Space is held, km/h (the handbrake costs something).")]
        public float handbrakeDragKmh = 10f;
        [Tooltip("Tyre side grip while drifting, as a share of normal. Lower lets the slide run without the wheels fighting it.")]
        [Range(0.05f, 1f)] public float sideGripWhileDrifting = 0.15f;

        [Header("Charge and boost")]
        [Tooltip("Charge needed for tiers 1, 2 and 3 (about seconds of a good drift).")]
        public float[] tierCharge = { 0.9f, 2f, 3.4f };
        [Tooltip("Boost paid when the drift ends cleanly, km/h, for tiers 1, 2 and 3.")]
        public float[] tierBoostKmh = { 12f, 24f, 38f };
        [Tooltip("Camera jolt when the boost fires, 0..1, at tier 3 (less for lower tiers).")]
        public float boostJolt = 0.7f;
        [Tooltip("How fast this car charges (1 = normal).")]
        public float chargeRate = 1f;
        [Tooltip("Scales the tier boosts (1 = as listed).")]
        public float boostScale = 1f;

        /// <summary>The character in use (Auto resolved).</summary>
        public DriftCharacter Character { get { return character == DriftCharacter.Auto ? (autoResolvesTo == DriftCharacter.Auto ? DriftCharacter.Balanced : autoResolvesTo) : character; } }

        /// <summary>How keen this car is to drift, for rivals choosing whether to drift a corner (drifty 1.3, grippy 0.6).</summary>
        public float DriftEagerness { get { var c = Character; return c == DriftCharacter.Drifty ? 1.3f : c == DriftCharacter.Grippy ? 0.6f : 1f; } }

        /// <summary>A drift began. Direction: -1 left, 1 right.</summary>
        public static event System.Action<DriftMoves> Started;
        /// <summary>The drift reached a new tier (1..3).</summary>
        public static event System.Action<DriftMoves, int> TierUp;
        /// <summary>The drift ended: tier paid (0 = none) and seconds held.</summary>
        public static event System.Action<DriftMoves, int, float> Ended;

        public bool Drifting { get; private set; }
        /// <summary>-1 drifting left, 1 right.</summary>
        public int Direction { get; private set; }
        /// <summary>Current slide angle, degrees.</summary>
        public float SlipAngle { get; private set; }
        /// <summary>0 none, 1..3.</summary>
        public int Tier { get; private set; }
        public float Charge { get; private set; }
        /// <summary>Progress toward the next tier, 0..1 (1 at the top tier).</summary>
        public float NextTier01
        {
            get
            {
                if (Tier >= tierCharge.Length) return 1f;
                float from = Tier == 0 ? 0f : tierCharge[Tier - 1];
                return Mathf.InverseLerp(from, tierCharge[Tier], Charge);
            }
        }
        /// <summary>How hard the drift is right now, 0..1 (for smoke, sound, camera).</summary>
        public float Intensity { get; private set; }
        public string LastResult { get; private set; }

        Rigidbody body;
        IVehicle vehicle;
        IDriftInput driftInput;
        GripMoves moves;
        WheelCollider[] wheels;
        float[] wheelSideStiffness;
        Vector3 lineDir;
        float driftSpeed; // the drift's own speed: the tyres scrubbing sideways would otherwise bleed it away
        float commit;     // the slide angle the player has asked for: opened by Space, closed by the steering
        float startedAt, airborneFor;
        bool aiHold;
        int aiDir;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            vehicle = GetComponent<IVehicle>();
            driftInput = GetComponent<IDriftInput>();
            moves = GetComponent<GripMoves>();
            if (!customTuning) ApplyCharacter();
            wheels = GetComponentsInChildren<WheelCollider>(true);
            wheelSideStiffness = new float[wheels.Length];
            for (int i = 0; i < wheels.Length; i++) wheelSideStiffness[i] = wheels[i].sidewaysFriction.stiffness;
            if (addEffects && GetComponent<VFX.DriftFX>() == null) gameObject.AddComponent<VFX.DriftFX>();
        }

        void OnDisable()
        {
            if (Drifting) Stop(false);
            if (driftInput != null) driftInput.MuteHandbrake = false;
        }

        /// <summary>Sets the drift numbers from the character, then nudges them by weight (1300 kg is the middle).</summary>
        [ContextMenu("Apply Character")]
        public void ApplyCharacter()
        {
            var tuning = DriftTuning.Instance;
            if (tuning != null)
            {
                // The live tuning asset (Resources/SR_DriftTuning): edited in Play, felt on the next drift.
                var p = tuning.For(Character);
                minKmh = p.startKmh; steerToStart = p.steerToStart; startAngle = p.tapAngle; maxAngle = p.maxAngle;
                kickRate = p.openPerSecond; angleResponse = p.swingSpeed;
                recoverRate = p.letGoRecovery;
                latAccel = p.cornerGrip; dragKmh = p.speedLoss; dragKmhOnThrottle = p.speedLossOnThrottle;
                handbrakeDragKmh = p.handbrakeSpeedLoss; sideGripWhileDrifting = p.tyreGripInDrift;
                chargeRate = p.chargeRate; boostScale = p.boostScale;
                Weigh(tuning.weightEffect, tuning.middleWeightKg);
                maxAngle *= partAngleScale; // a big wing keeps the slide narrower
                return;
            }
            switch (Character)
            {
                case DriftCharacter.Drifty:
                    minKmh = 38f; steerToStart = 0.22f; startAngle = 14f; maxAngle = 55f; kickRate = 65f; angleResponse = 4f;
                    recoverRate = 60f; latAccel = 11.5f;
                    dragKmh = 6f; dragKmhOnThrottle = 0.5f; handbrakeDragKmh = 7f; sideGripWhileDrifting = 0.1f; chargeRate = 1.25f; boostScale = 1f;
                    break;
                case DriftCharacter.Grippy:
                    minKmh = 55f; steerToStart = 0.45f; startAngle = 6f; maxAngle = 28f; kickRate = 30f; angleResponse = 3f;
                    recoverRate = 140f; latAccel = 14f;
                    dragKmh = 14f; dragKmhOnThrottle = 4f; handbrakeDragKmh = 14f; sideGripWhileDrifting = 0.25f; chargeRate = 0.8f; boostScale = 1.4f;
                    break;
                default:
                    minKmh = 45f; steerToStart = 0.3f; startAngle = 10f; maxAngle = 42f; kickRate = 45f; angleResponse = 3.5f;
                    recoverRate = 90f; latAccel = 12f;
                    dragKmh = 8f; dragKmhOnThrottle = 1.5f; handbrakeDragKmh = 10f; sideGripWhileDrifting = 0.15f; chargeRate = 1f; boostScale = 1f;
                    break;
            }
            Weigh(1f, 1300f);
            maxAngle *= partAngleScale;
        }

        [Tooltip("Set by fitted parts (spoilers): scales the widest slide. 1 = no change.")]
        public float partAngleScale = 1f;

        /// <summary>Heavy cars swing in slower and keep their momentum, light ones flick in fast.</summary>
        void Weigh(float effect, float middleKg)
        {
            var rb = GetComponent<Rigidbody>();
            float heavy = Mathf.Clamp(rb != null ? rb.mass / Mathf.Max(1f, middleKg) : 1f, 0.7f, 1.8f);
            float k = Mathf.Lerp(1f, Mathf.Sqrt(heavy), effect);
            angleResponse /= k;
            kickRate /= k;
            dragKmh /= k;
            dragKmhOnThrottle /= k;
        }

        /// <summary>For rivals: hold (like the player holding Space) a drift in a direction (-1 left, 1 right), or let go.</summary>
        public void SetAIDrift(bool hold, int dir)
        {
            aiHold = hold;
            if (dir != 0) aiDir = dir > 0 ? 1 : -1;
        }

        float Kmh { get { return Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude * 3.6f; } }

        bool Busy
        {
            get
            {
                if (moves != null && (moves.Leaping || moves.WallRiding)) return true;
                var k = GetComponent<KnockTumble>();
                return k != null && k.Tumbling;
            }
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            bool player = IsPlayer;
            // Live tuning: pick up any change made to the tuning asset (or the character) while the game runs.
            if (!customTuning) ApplyCharacter();
            if (Drifting) SetSideGrip(sideGripWhileDrifting);
            // The player's own steering, not the car controller's: RCC Pro limits steering at speed, which would make a
            // drift impossible to start (and to widen) above about 150 km/h.
            float steer = player && driftInput != null ? driftInput.SteerIntent : (vehicle != null ? vehicle.SteerInput : 0f);
            bool held = player ? (driftInput != null && driftInput.HandbrakeInput > 0.5f) : aiHold;
            float kmh = Kmh;
            bool grounded = vehicle == null || vehicle.AnyWheelGrounded;

            // At drift speed the handbrake belongs to the drift: RCCP never locks the wheels and kills the slide.
            // Slower than that it stays a normal handbrake (handbrake turns, parking).
            if (driftInput != null) driftInput.MuteHandbrake = player && (Drifting || kmh > minKmh * 0.8f);

            if (!Drifting)
            {
                int wantDir = player ? (Mathf.Abs(steer) >= steerToStart ? (steer > 0f ? 1 : -1) : 0) : aiDir;
                if (held && wantDir != 0 && kmh >= minKmh && grounded && !Busy) Begin(wantDir);
                return;
            }

            // ---- ending (broken)
            if (Busy) { Stop(false); LastResult = "Drift broken"; return; }
            airborneFor = grounded ? 0f : airborneFor + dt;
            if (airborneFor > 0.35f) { Stop(false); LastResult = "Drift lost (airborne)"; return; }
            if (driftSpeed * 3.6f < minKmh * 0.7f) { Stop(false); LastResult = "Drift died (too slow)"; return; }

            // ---- how much slide the player is asking for
            float s = Mathf.Clamp(steer * Direction, -1f, 1f);
            // Rivals steer by the direction of travel, so their steer is how much more (or less) the line must bend.
            if (!player) s = Mathf.Clamp(steer * Direction * 1.6f, -1f, 1f);
            bool throttle = vehicle != null && vehicle.PitchInput > 0.3f;
            // Like a real handbrake: while Space is held the rear is loose and the tail keeps swinging out; the moment it's
            // released the rear grips again and the car straightens itself. Steering can't keep the slide alive.
            if (held) commit = Mathf.Min(maxAngle, commit + kickRate * dt);
            else
            {
                float recover = recoverRate * (s < -0.2f ? 1.3f : s > 0.2f ? 0.85f : 1f);
                commit = Mathf.Max(0f, commit - recover * dt);
            }
            float u = (s + 1f) * 0.5f;                                          // 0 counter-steer .. 1 full into the turn
            float target = commit * Mathf.Lerp(0.55f, 1f, u);
            // Swinging out follows the swing speed; coming back after Space is released is quick, so it obeys the button.
            float response = held || target > SlipAngle ? angleResponse : Mathf.Max(angleResponse, 9f);
            SlipAngle = Mathf.Lerp(SlipAngle, target, 1f - Mathf.Exp(-response * dt));

            // ---- clean end: Space up and the car straight again
            if (!held && commit < endAngle && SlipAngle < endAngle + 1f) { Stop(true); return; }

            // ---- the line: bends at the rate the car can really corner at this speed, more steering = tighter
            Vector3 v = body.linearVelocity;
            float actual = Vector3.ProjectOnPlane(v, Vector3.up).magnitude;
            float maxTurn = latAccel / Mathf.Max(8f, driftSpeed) * Mathf.Rad2Deg;   // degrees per second
            float turn = maxTurn * Mathf.Lerp(0.1f, 1f, u) * (held ? 1.15f : 1f) * Direction;
            lineDir = (Quaternion.AngleAxis(turn * dt, Vector3.up) * lineDir).normalized;

            // Only the set drag takes speed off. A real hit (a wall, a car) still slows it.
            driftSpeed = Mathf.Min(driftSpeed, actual + 6f); // a crash costs far more than 6 m/s in one step; tyre scrub never does
            float drag = (throttle ? dragKmhOnThrottle : dragKmh) + (held ? handbrakeDragKmh : 0f);
            driftSpeed = Mathf.Max(0f, driftSpeed - drag / 3.6f * dt);
            body.linearVelocity = lineDir * driftSpeed + Vector3.up * v.y;

            // Nose into the turn, the slide angle off the line.
            Vector3 heading = Quaternion.AngleAxis(Direction * SlipAngle, Vector3.up) * lineDir;
            Vector3 flatFwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            float yawErr = Vector3.SignedAngle(flatFwd, heading, Vector3.up);
            Vector3 w = body.angularVelocity;
            w.y = yawErr * Mathf.Deg2Rad * yawGain + turn * Mathf.Deg2Rad;
            w.x *= 0.85f; w.z *= 0.85f; // no tipping over in the slide
            body.angularVelocity = w;

            // ---- charge: a wider, faster drift charges quicker; a tiny one barely at all
            float angle01 = Mathf.InverseLerp(endAngle, maxAngle, SlipAngle);
            float speed01 = Mathf.InverseLerp(minKmh, 170f, driftSpeed * 3.6f);
            float rate = Mathf.Lerp(0.25f, 1.25f, angle01 * 0.7f + speed01 * 0.3f);
            Charge += rate * chargeRate * dt;
            Intensity = Mathf.Clamp01(0.2f + angle01 * 0.6f + speed01 * 0.2f);
            int tier = 0;
            while (tier < tierCharge.Length && Charge >= tierCharge[tier]) tier++;
            if (tier > Tier)
            {
                Tier = tier;
                if (TierUp != null) TierUp(this, Tier);
            }
        }

        void Begin(int dir)
        {
            Drifting = true;
            Direction = dir;
            lineDir = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).normalized;
            if (lineDir.sqrMagnitude < 0.5f) lineDir = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            SlipAngle = Mathf.Abs(Vector3.SignedAngle(lineDir, Vector3.ProjectOnPlane(transform.forward, Vector3.up), Vector3.up));
            commit = Mathf.Max(startAngle, SlipAngle);
            driftSpeed = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude;
            Charge = 0f; Tier = 0; Intensity = 0.2f;
            startedAt = Time.time; airborneFor = 0f;
            SetSideGrip(sideGripWhileDrifting);
            LastResult = dir < 0 ? "Drift left" : "Drift right";
            if (Started != null) Started(this);
        }

        /// <summary>paid: ended cleanly (boost for the tier reached) or broken (nothing).</summary>
        void Stop(bool paid)
        {
            Drifting = false;
            SetSideGrip(1f);
            int tier = paid ? Tier : 0;
            float held = Time.time - startedAt;
            // The car is near straight by now: it carries on the way its nose points, plus the boost.
            Vector3 v = body.linearVelocity;
            Vector3 flatFwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            float speed = Mathf.Max(driftSpeed, Vector3.ProjectOnPlane(v, Vector3.up).magnitude);
            if (paid)
            {
                float boost = tier > 0 ? tierBoostKmh[Mathf.Min(tier, tierBoostKmh.Length) - 1] * boostScale / 3.6f : 0f;
                body.linearVelocity = flatFwd * (speed + boost) + Vector3.up * v.y;
                Vector3 w = body.angularVelocity; w.y *= 0.3f; body.angularVelocity = w;
                if (tier > 0 && IsPlayer) CameraFX.SpeedFeel.Jolt(boostJolt * tier / 3f);
                LastResult = tier > 0 ? "Drift tier " + tier + " boost +" + (tierBoostKmh[tier - 1] * boostScale).ToString("F0") + " km/h" : "Drift (no charge)";
            }
            Tier = 0; Charge = 0f; Intensity = 0f; SlipAngle = 0f; commit = 0f;
            if (Ended != null) Ended(this, tier, held);
        }

        void SetSideGrip(float share)
        {
            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] == null) continue;
                var f = wheels[i].sidewaysFriction;
                f.stiffness = wheelSideStiffness[i] * share;
                wheels[i].sidewaysFriction = f;
            }
        }
    }
}
