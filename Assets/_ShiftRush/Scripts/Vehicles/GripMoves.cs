using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShiftRush.Vehicles
{
    /// <summary>
    /// Grip Pro's driving moves, on top of whichever car controller is installed (works on the Rigidbody only,
    /// so it carries over from RCC Pro to Simcade).
    ///  - Spiral roll: Q / E (gamepad LB / RB). The car pops up and corkscrews one full barrel roll toward that side,
    ///    landing about a lane over with every km/h of forward speed kept. Q + E together: forward spiral roll, the
    ///    same barrel roll straight ahead in its own lane (spins the way of whichever key went down first).
    ///    The landing is played big: suspension slam, camera jolt, shockwave and a boost kick on a clean landing.
    ///  - Wall grip: above a set speed the car sticks to any <see cref="GripSurface"/> it drives onto.
    ///    Rolling sideways into a grip wall rolls onto it. Q or E on a wall: a spiral barrel roll down to the road,
    ///    always spinning toward the open side (the road), whichever key is pressed.
    /// Space stays the drift (handbrake). Rivals call <see cref="TryLeap"/> with <see cref="playerControlled"/> off.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DefaultExecutionOrder(60)]
    public class GripMoves : MonoBehaviour
    {
        [Tooltip("Reads the keyboard and gamepad. Off for rivals, who call TryLeap.")]
        public bool playerControlled = true;

        /// <summary>Reads the player's keys: allowed (playerControlled) and this is the car the player is driving.</summary>
        public bool IsPlayer { get { return playerControlled && (vehicle == null || vehicle.PlayerDriven); } }

        [Header("Leap")]
        [Tooltip("Peak height of the leap, metres (2.5 clears a car roof).")]
        public float leapHeight = 2.5f;
        [Tooltip("Seconds in the air. Short and high reads as a leap, not a float.")]
        public float leapAirTime = 0.65f;
        [Tooltip("How far a side leap lands to the side, metres (about one lane).")]
        public float leapSide = 4.5f;
        public float leapCooldown = 0.8f;
        [Tooltip("Q and E pressed this close together make a front leap.")]
        public float frontWindow = 0.08f;
        [Tooltip("How much steering turns the car in the air, degrees per second.")]
        public float airSteer = 30f;
        [Tooltip("Lean into a side leap, degrees.")]
        public float leanDegrees = 14f;
        [Tooltip("Nose up on take-off, nose down for the landing, degrees.")]
        public float pitchDegrees = 6f;

        [Header("Spiral roll (Q / E)")]
        [Tooltip("Peak height of the spiral roll, metres. Higher than the leap so the whole roll is on show.")]
        public float rollHeight = 3f;
        [Tooltip("Seconds in the air for the spiral roll.")]
        public float rollAirTime = 0.9f;
        [Tooltip("Full barrel rolls per spiral.")]
        public float rollTurns = 1f;
        [Tooltip("Part of the air time the roll takes. The rest is upright, so the car meets the road on its wheels.")]
        [Range(0.5f, 1f)] public float rollFinishAt = 0.82f;
        [Tooltip("How far a Q / E spiral roll carries the car sideways, metres. Wide enough to get fully clear of a big shot.")]
        public float rollSide = 8f;
        [Tooltip("How front-loaded the sideways move is. 0 = even all the way, 1 = most of it in the first moments (a snap out of the way).")]
        [Range(0f, 1f)] public float rollDodgeSnap = 1f;

        [Header("Knock (a spiral roll into a rival)")]
        [Tooltip("Share of the roll that can hit: the low, fast snap at the start. Over the top it passes over cars.")]
        [Range(0.1f, 1f)] public float knockWindow = 0.4f;
        [Tooltip("How far past the body the hit reaches, metres, so it lands just before the panels touch.")]
        public float knockReach = 0.5f;
        [Tooltip("Hitting a car heavier than this share of your weight bounces you off it (a light car into a truck).")]
        [Range(0f, 1f)] public float bounceBelow = 0.6f;
        [Tooltip("Camera jolt on a hit, 0..1.")]
        public float knockJolt = 0.9f;
        [Tooltip("The freeze on contact, real seconds, so the hit lands.")]
        public float hitStopSeconds = 0.06f;

        [Header("Landing")]
        [Tooltip("Downward speed the car slams into its suspension with, m/s.")]
        public float landSlam = 6f;
        [Tooltip("Speed added by a clean landing (upright, all wheels down).")]
        public float landBoostKmh = 10f;
        [Tooltip("Camera jolt on landing, 0..1.")]
        public float landJolt = 0.8f;

        [Header("Wall grip")]
        public float gripMinKmh = 55f;
        [Tooltip("Grip fades out over this many km/h below the minimum, then the car falls off.")]
        public float gripFadeKmh = 15f;
        [Tooltip("How far under the car to look for a grip surface, metres.")]
        public float gripProbe = 2.4f;
        [Tooltip("Extra pull into the surface while gripping, m/s².")]
        public float stickAccel = 4f;
        public float alignStrength = 10f;
        public float alignDamping = 4f;
        [Tooltip("How far a side leap looks for a grip wall to roll onto, metres.")]
        public float latchReach = 7f;
        [Tooltip("Seconds the quarter roll onto a wall takes.")]
        public float latchTime = 0.28f;
        [Tooltip("Leaving a wall: least distance out from the wall the spiral roll lands, metres (further if the ground there isn't flat).")]
        public float wallRollAway = 4f;
        [Tooltip("Leaving a wall: upward pop off the wall, m/s.")]
        public float wallRollPop = 6f;
        [Tooltip("Leaving a wall: gravity during the roll, m/s² (heavier than real so it snaps down).")]
        public float wallRollGravity = 26f;
        [Tooltip("Leaving a wall: shortest time the roll takes, even off a low wall.")]
        public float wallRollMinTime = 0.7f;
        [Tooltip("Leaving a wall: extra full barrel rolls on top of the turn back to upright (about 270°, toward the open side). 1 makes it about 630°.")]
        public float wallRollExtraTurns = 1f;
        [Tooltip("Seconds the roll back onto the wheels takes when the car is too slow to hold a wall.")]
        public float unrollTime = 0.32f;
        [Tooltip("How far the car moves away from the wall while rolling back, metres.")]
        public float unrollAwayMeters = 1.2f;
        [Tooltip("On a wall with no steering, how fast climbing or sinking is evened out (per second).")]
        public float wallHoldLine = 3f;

        /// <summary>-1 left, 1 right, 0 front.</summary>
        public static event System.Action<GripMoves, int> Leapt;
        /// <summary>Landing from a leap: clean (upright, boosted) or not, and seconds in the air.</summary>
        public static event System.Action<GripMoves, bool, float> Landed;

        // ---- state the HUD / debug overlay reads ----
        public bool Leaping { get { return leapT >= 0f || rollT >= 0f || awaitLand; } }
        public bool WallRiding { get; private set; }
        /// <summary>In a spiral roll: Q / E on the road, or rolling off a wall (the wind aura shows only then).</summary>
        public bool Rolling { get { return (leapT >= 0f && barrel) || rollT >= 0f; } }
        public float GripFactor { get; private set; }
        public Vector3 GravityUp { get { return gravityUp; } }
        public float LeapReady01 { get { return lastLeapAt < 0f ? 1f : Mathf.Clamp01((Time.time - lastLeapAt) / leapCooldown); } }
        public string LastMove { get; private set; }
        public int CleanLandings { get; private set; }

        Rigidbody body;
        IVehicle vehicle;
        AirControl air;
        int probeMask;

        // input
        float qAt = -10f, eAt = -10f;
        int queuedLeap = int.MinValue;

        // leap
        float leapT = -1f, lastLeapAt = -10f, leapSpeed, leapYaw, leapVy, leapG, leapLat, leapStartTime;
        int leapDir, forwardSpin = 1;
        bool awaitLand, barrel;
        float airTime;
        HashSet<Rigidbody> rollHits;
        Bounds localBounds;

        // spiral roll off a wall
        float rollT = -1f, rollTime, rollAngle;
        Vector3 rollAxis, rollAway;
        Quaternion rollStartRot, rollEndRot;

        // wall grip
        Vector3 gravityUp = Vector3.up, latchNormal, latchPoint, latchTangent;
        float latchUntil = -1f, detachUntil = -1f;
        float latchT = -1f, latchStartDist, latchRideHeight, latchStartY, latchSpeed;
        Quaternion latchStartRot, latchTargetRot;
        float unrollT = -1f, unrollStartY;
        Vector3 unrollAway;
        Quaternion unrollStartRot, unrollTargetRot;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            vehicle = GetComponent<IVehicle>();
            air = GetComponent<AirControl>();
            localBounds = VehicleBounds.Local(transform);
            probeMask = ~LayerMask.GetMask("RCCP_Vehicle", "RCCP_WheelCollider", "RCCP_DetachablePart", "Ignore Raycast");
        }

        void OnDisable()
        {
            if (body != null) body.useGravity = true;
            if (air != null) air.enabled = true;
        }

        float Kmh { get { return body.linearVelocity.magnitude * 3.6f; } }

        // ------------------------------------------------------------------ input

        void Update()
        {
            if (!IsPlayer) return;
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool q = (kb != null && kb.qKey.wasPressedThisFrame) || (pad != null && pad.leftShoulder.wasPressedThisFrame);
            bool e = (kb != null && kb.eKey.wasPressedThisFrame) || (pad != null && pad.rightShoulder.wasPressedThisFrame);
            float now = Time.time;
            if (q) qAt = now;
            if (e) eAt = now;
            if (!q && !e) return;

            // Both inside the window: forward spiral roll, spinning the way of the key that went down first.
            // A side roll that just started becomes the forward roll and keeps its spin.
            if (Mathf.Abs(qAt - eAt) <= frontWindow)
            {
                if (leapT >= 0f && leapT <= frontWindow + 0.05f && leapDir != 0) { forwardSpin = leapDir; leapDir = 0; leapLat = 0f; LastMove = "Forward spiral roll"; }
                else { forwardSpin = qAt <= eAt ? -1 : 1; TryLeap(0); }
                return;
            }
            TryLeap(q ? -1 : 1);
        }

        /// <summary>-1 left, 1 right, 0 front. On a wall, any leap jumps off it.</summary>
        public bool TryLeap(int dir)
        {
            if (Time.time - lastLeapAt < leapCooldown || Leaping || latchT >= 0f || unrollT >= 0f || Tumbling) return false;
            if (!WallRiding && vehicle != null && !vehicle.AnyWheelGrounded) return false;
            queuedLeap = dir;
            lastLeapAt = Time.time;
            return true;
        }

        // ------------------------------------------------------------------ physics

        /// <summary>Knocked by someone else's roll: the tumble has the car, no moves until it lands.</summary>
        bool Tumbling
        {
            get
            {
                if (tumble == null) tumble = GetComponent<KnockTumble>();
                return tumble != null && tumble.Tumbling;
            }
        }
        KnockTumble tumble;

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (Tumbling) { queuedLeap = int.MinValue; leapT = -1f; rollT = -1f; awaitLand = false; return; }
            WallGrip(dt);

            if (queuedLeap != int.MinValue) { StartLeap(queuedLeap); queuedLeap = int.MinValue; }

            if (latchT >= 0f) { StepLatch(dt); return; }
            if (unrollT >= 0f) { StepUnroll(dt); return; }
            if (rollT >= 0f) { StepWallRoll(dt); return; }
            if (leapT >= 0f) { StepLeap(dt); return; }
            if (awaitLand)
            {
                if (vehicle == null || vehicle.AnyWheelGrounded) Land();
                else if (Time.time - leapStartTime > 4f) awaitLand = false; // fell off something: AirControl has it
                return;
            }

            // On a wall the car holds its height unless the player steers up or down it.
            if (WallRiding && vehicle != null && Mathf.Abs(vehicle.SteerInput) < 0.1f)
            {
                Vector3 v = body.linearVelocity;
                body.linearVelocity = v - Vector3.up * Vector3.Dot(v, Vector3.up) * (1f - Mathf.Exp(-wallHoldLine * dt));
            }
        }

        void StartLeap(int dir)
        {
            if (WallRiding)
            {
                StartWallRoll();
                Leapt?.Invoke(this, dir);
                return;
            }

            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, fwd) * dir;

            // A grip wall on that side: roll onto it instead of leaping.
            RaycastHit hit;
            if (dir != 0 && Kmh >= gripMinKmh
                && Physics.Raycast(body.worldCenterOfMass, side, out hit, latchReach, probeMask, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<GripSurface>() != null
                && Mathf.Abs(Vector3.Dot(hit.normal, Vector3.up)) < 0.5f)
            {
                LastMove = dir < 0 ? "Roll left onto wall" : "Roll right onto wall";
                StartLatch(hit);
                Leapt?.Invoke(this, dir);
                return;
            }

            leapDir = dir;
            barrel = true; // every move on the road is a spiral roll: Q / E to the side, Q + E straight ahead
            float h = barrel ? rollHeight : leapHeight;
            airTime = barrel ? rollAirTime : leapAirTime;
            leapSpeed = Mathf.Max(0f, Vector3.Dot(body.linearVelocity, fwd));
            leapYaw = Quaternion.LookRotation(fwd, Vector3.up).eulerAngles.y;
            // Arc of the given height and air time: start speed up 4h/T, gravity 8h/T² (heavier than real, so it snaps).
            leapVy = 4f * h / airTime;
            leapG = 8f * h / (airTime * airTime);
            leapLat = (barrel ? rollSide : leapSide) / airTime;
            leapT = 0f;
            leapStartTime = Time.time;
            rollHits = new HashSet<Rigidbody> { body };
            LastMove = dir == 0 ? "Forward spiral roll" : dir < 0 ? "Spiral roll left" : "Spiral roll right";
            Leapt?.Invoke(this, dir);
        }

        void StepLeap(float dt)
        {
            leapT += dt;
            float k = Mathf.Clamp01(leapT / airTime);
            body.useGravity = false;
            if (air != null) air.enabled = false;

            float steer = vehicle != null ? vehicle.SteerInput : 0f;
            leapYaw += steer * airSteer * dt;
            Quaternion yaw = Quaternion.Euler(0f, leapYaw, 0f);
            Vector3 fwd = yaw * Vector3.forward, right = yaw * Vector3.right;

            leapVy -= leapG * dt;
            // Sideways: a roll snaps out of the way early (speed falling from 2x to 0, same total distance) so a shot
            // coming at the car misses it, then drifts into its new line for the landing.
            float latShape = barrel ? Mathf.Lerp(1f, 2f * (1f - k), rollDodgeSnap) : 1f;
            body.linearVelocity = fwd * leapSpeed + right * (leapLat * leapDir * latShape) + Vector3.up * leapVy;
            if (barrel && k <= knockWindow) CheckKnock(fwd, right);

            // Style: nose up as it rises, nose down to meet the road, a lean into a side leap.
            float pitch = Mathf.Lerp(-pitchDegrees, pitchDegrees, k);
            if (barrel)
            {
                // One big barrel roll toward the side pressed (positive angle about forward rolls left). Eases in off the
                // road, spins hardest at the top of the arc, and is back on its wheels before touchdown.
                float r = Mathf.Clamp01(k / rollFinishAt);
                float e = r * r * (3f - 2f * r);
                // Q + E (leapDir 0) spins the same way, straight ahead, in the direction of the first key down.
                int spin = leapDir != 0 ? leapDir : forwardSpin;
                body.MoveRotation(Quaternion.AngleAxis(-spin * 360f * rollTurns * e, fwd) * Quaternion.Euler(pitch, leapYaw, 0f));
            }
            else
            {
                float roll = -leapDir * leanDegrees * Mathf.Sin(k * Mathf.PI);
                body.MoveRotation(Quaternion.Euler(pitch, leapYaw, roll));
            }
            body.angularVelocity = Vector3.zero;

            bool falling = leapVy < 0f;
            // Wheel colliders reach down past the body, so only count touchdown late in the arc (lands a full lane over).
            float landFrom = barrel ? Mathf.Max(0.8f, rollFinishAt) : 0.8f;
            if (falling && k > landFrom && vehicle != null && vehicle.AnyWheelGrounded) { Land(); return; }
            if (k >= 1f)
            {
                // Arc done but no road under the wheels yet (leapt off a drop): fall normally, land when it touches.
                leapT = -1f;
                awaitLand = true;
                body.useGravity = true;
                body.MoveRotation(Quaternion.Euler(0f, leapYaw, 0f));
            }
        }

        /// <summary>
        /// The roll's snap hits any car in its way. The roll itself is never stopped. Q / E throw the rival out to that
        /// side, the forward roll throws the car ahead out of the lane. Weight decides how hard: equal cars one roll,
        /// a much heavier roller two, and a light car only shoves a heavy one and bounces back off it.
        /// </summary>
        void CheckKnock(Vector3 fwd, Vector3 right)
        {
            if (rollHits == null) return;
            Vector3 centre = body.worldCenterOfMass;
            Vector3 half = localBounds.extents + new Vector3(knockReach, knockReach, knockReach * 0.6f);
            var hits = Physics.OverlapBox(centre, half, Quaternion.LookRotation(fwd, Vector3.up), ~0, QueryTriggerInteraction.Ignore);
            foreach (var c in hits)
            {
                var rb = c.attachedRigidbody;
                if (rb == null || rollHits.Contains(rb) || rb.GetComponent<IVehicle>() == null) continue;
                rollHits.Add(rb);

                Vector3 toward = Vector3.ProjectOnPlane(rb.worldCenterOfMass - centre, Vector3.up);
                int pushSide = leapDir != 0 ? leapDir : (Mathf.Abs(Vector3.Dot(toward, right)) > 0.2f ? (Vector3.Dot(toward, right) > 0f ? 1 : -1) : forwardSpin);
                Vector3 push = right * pushSide + (leapDir == 0 ? fwd * 0.35f : Vector3.zero);
                float ratio = body.mass / Mathf.Max(1f, rb.mass);
                var set = new HashSet<Rigidbody>(rollHits);
                if (!KnockTumble.Hit(rb, push, ratio, gameObject, 1, set)) continue;
                foreach (var h in set) rollHits.Add(h);

                if (ratio < bounceBelow)
                {
                    // Light into heavy: knocked back out of the way and slowed, the roll carries on.
                    // Side roll: the sideways travel turns back (a third as fast). Forward roll: it loses more speed instead.
                    if (leapDir != 0) leapLat = -Mathf.Abs(leapLat) * 0.35f;
                    leapSpeed *= leapDir != 0 ? 0.85f : 0.7f;
                    LastMove = "Bounced off " + rb.name;
                }
                else LastMove = "Knocked " + rb.name;
                if (IsPlayer)
                {
                    CameraFX.SpeedFeel.Jolt(knockJolt);
                    VFX.HitStop.Do(hitStopSeconds);
                }
            }
        }

        /// <summary>
        /// Leaving a wall: a corkscrew down to the road. The car pops off the wall, rolls the quarter turn back to
        /// upright plus a full extra barrel roll, and lands a lane out from the wall with its speed kept.
        /// </summary>
        void StartWallRoll()
        {
            Vector3 n = gravityUp; // the wall's face
            Vector3 tangent = Vector3.ProjectOnPlane(transform.forward, n);
            Vector3 flat = Vector3.ProjectOnPlane(tangent, Vector3.up);
            if (flat.sqrMagnitude < 0.01f) flat = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            flat.Normalize();
            Vector3 away = Vector3.ProjectOnPlane(n, Vector3.up);
            away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.zero;

            // Where the road is: the first flat ground out from the wall (past the curve at the foot of a pipe).
            Vector3 com = body.worldCenterOfMass;
            float outDist = wallRollAway, groundY = com.y - 10f;
            RaycastHit ground;
            for (float d = wallRollAway; d <= wallRollAway + 12f; d += 1f)
            {
                if (!Physics.Raycast(com + away * d + Vector3.up * 2f, Vector3.down, out ground, 60f, probeMask, QueryTriggerInteraction.Ignore)) continue;
                outDist = d; groundY = ground.point.y;
                // Flat and not part of the wall-ride piece, plus two metres so the wheels clear the pipe's lip.
                if (ground.normal.y > 0.99f && ground.collider.GetComponentInParent<GripSurface>() == null) { outDist = d + 2f; break; }
            }
            float drop = Mathf.Max(0f, com.y - (groundY + 0.6f));

            // Pop up, then fall to the road. Air time comes from the height, never shorter than wallRollMinTime.
            float g = wallRollGravity, v0 = wallRollPop;
            float t = (v0 + Mathf.Sqrt(v0 * v0 + 2f * g * drop)) / g;
            if (t < wallRollMinTime) { t = wallRollMinTime; v0 = (0.5f * g * t * t - drop) / t; }

            // Roll around the direction of travel, spinning toward the open side (the side the car is heading to), so
            // the spin and the motion agree: road on the left rolls left. Ends upright, plus any extra turns.
            float back = Vector3.SignedAngle(transform.up, Vector3.up, flat);
            float openSide = Vector3.Dot(away, Vector3.Cross(Vector3.up, flat)) >= 0f ? 1f : -1f; // 1 right, -1 left
            float spin = -openSide; // a positive angle about forward rolls left
            rollAngle = back;
            if (Mathf.Abs(back) < 1f || Mathf.Sign(back) != spin) rollAngle += spin * 360f;
            rollAngle += spin * 360f * wallRollExtraTurns;
            rollAxis = flat;
            rollStartRot = body.rotation;
            rollEndRot = Quaternion.LookRotation(flat, Vector3.up);
            rollTime = t;
            rollT = 0f;

            leapSpeed = Mathf.Max(0f, Vector3.Dot(body.linearVelocity, flat));
            leapYaw = rollEndRot.eulerAngles.y;
            leapVy = v0;
            leapG = g;
            rollAway = away * (outDist / t);
            leapStartTime = Time.time;
            detachUntil = Time.time + t + 0.3f;
            latchUntil = -1f;
            gravityUp = Vector3.up;
            WallRiding = false;
            LastMove = openSide < 0f ? "Spiral roll off wall, left" : "Spiral roll off wall, right";
        }

        void StepWallRoll(float dt)
        {
            rollT += dt;
            float k = Mathf.Clamp01(rollT / rollTime);
            body.useGravity = false;
            if (air != null) air.enabled = false;

            leapVy -= leapG * dt;
            body.linearVelocity = rollAxis * leapSpeed + rollAway + Vector3.up * leapVy;

            // Fast out of the wall, a beat of hang in the middle, snap upright for the landing.
            float e = k < 0.5f ? 2f * k * k : 1f - Mathf.Pow(-2f * k + 2f, 2f) * 0.5f;
            Quaternion spun = Quaternion.AngleAxis(rollAngle * e, rollAxis) * rollStartRot;
            body.MoveRotation(k < 1f ? spun : rollEndRot);
            body.angularVelocity = Vector3.zero;

            if ((k > 0.85f && vehicle != null && vehicle.AnyWheelGrounded) || k >= 1f)
            {
                rollT = -1f;
                body.MoveRotation(rollEndRot);
                if (vehicle != null && vehicle.AnyWheelGrounded) Land();
                else { awaitLand = true; body.useGravity = true; }
            }
        }

        void Land()
        {
            float airTime = Time.time - leapStartTime;
            leapT = -1f;
            awaitLand = false;
            body.useGravity = true;
            if (air != null) air.enabled = true;

            Vector3 v = body.linearVelocity;
            Vector3 flatFwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            float fwdSpeed = Mathf.Max(leapSpeed, Vector3.Dot(v, flatFwd));
            bool clean = Vector3.Angle(transform.up, Vector3.up) < 15f;
            if (clean) fwdSpeed += landBoostKmh / 3.6f;
            // Speed never drops through a leap. The slam drives the car into its springs for the squash and rebound.
            body.linearVelocity = flatFwd * fwdSpeed - Vector3.up * landSlam;
            body.angularVelocity = Vector3.zero;

            if (clean) CleanLandings++;
            LastMove = clean ? "Clean landing +" + landBoostKmh.ToString("F0") + " km/h" : "Rough landing";
            if (IsPlayer) CameraFX.SpeedFeel.Jolt(landJolt);
            var fx = GetComponent<VFX.CarFX>();
            if (fx != null) fx.Arrive();
            Landed?.Invoke(this, clean, airTime);
        }

        /// <summary>Quarter roll onto a grip wall: keeps the speed along the wall, lifts a little, lands on the wheels.</summary>
        void StartLatch(RaycastHit hit)
        {
            latchNormal = hit.normal;
            latchPoint = hit.point;
            Vector3 com = body.worldCenterOfMass;
            latchStartDist = Vector3.Dot(com - latchPoint, latchNormal);
            RaycastHit ground;
            latchRideHeight = Physics.Raycast(com, Vector3.down, out ground, 3f, probeMask, QueryTriggerInteraction.Ignore) ? ground.distance : 0.6f;
            latchStartY = com.y;
            latchTangent = Vector3.ProjectOnPlane(Vector3.ProjectOnPlane(transform.forward, Vector3.up), latchNormal).normalized;
            latchSpeed = Mathf.Max(0f, Vector3.Dot(body.linearVelocity, latchTangent));
            latchStartRot = body.rotation;
            latchTargetRot = Quaternion.LookRotation(latchTangent, latchNormal);
            latchT = 0f;
        }

        void StepLatch(float dt)
        {
            latchT += dt;
            float k = Mathf.Clamp01(latchT / latchTime);
            float e = k * k * (3f - 2f * k);
            body.MoveRotation(Quaternion.Slerp(latchStartRot, latchTargetRot, e));
            body.angularVelocity = Vector3.zero;

            Vector3 com = body.worldCenterOfMass;
            float dist = Vector3.Dot(com - latchPoint, latchNormal);
            float wantDist = Mathf.Lerp(latchStartDist, latchRideHeight, e);
            float wantY = latchStartY + Mathf.Sin(k * Mathf.PI) * 0.8f;
            body.linearVelocity = latchTangent * latchSpeed
                      + latchNormal * Mathf.Clamp((wantDist - dist) / dt, -30f, 30f)
                      + Vector3.up * Mathf.Clamp((wantY - com.y) / dt, -10f, 10f) * (k < 1f ? 1f : 0f);
            if (k >= 1f)
            {
                latchT = -1f;
                latchUntil = Time.time + 0.4f; // grip holds while the wheels settle onto the wall
                gravityUp = latchNormal;
            }
        }

        /// <summary>Too slow for the wall: a quarter roll back onto the wheels, away from the wall, keeping the speed.</summary>
        void StartUnroll()
        {
            unrollAway = gravityUp.y < 0.9f ? Vector3.ProjectOnPlane(transform.up, Vector3.up).normalized : Vector3.zero;
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.ProjectOnPlane(-transform.up, Vector3.up);
            unrollStartRot = body.rotation;
            unrollTargetRot = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            unrollStartY = body.worldCenterOfMass.y;
            detachUntil = Time.time + unrollTime + 0.3f;
            gravityUp = Vector3.up;
            unrollT = 0f;
        }

        void StepUnroll(float dt)
        {
            unrollT += dt;
            float k = Mathf.Clamp01(unrollT / unrollTime);
            float e = k * k * (3f - 2f * k);
            body.MoveRotation(Quaternion.Slerp(unrollStartRot, unrollTargetRot, e));
            body.angularVelocity = Vector3.zero;
            Vector3 v = body.linearVelocity;
            Vector3 along = Vector3.ProjectOnPlane(Vector3.ProjectOnPlane(v, Vector3.up), unrollAway);
            float wantY = unrollStartY + Mathf.Sin(k * Mathf.PI) * 0.7f;
            float lift = Mathf.Clamp((wantY - body.worldCenterOfMass.y) / dt, -8f, 8f);
            body.linearVelocity = along + unrollAway * (unrollAwayMeters / unrollTime) + Vector3.up * (k < 1f ? lift : 0f);
            if (k >= 1f) unrollT = -1f;
        }

        /// <summary>Rolls this car back upright for a moment. AirControl can't, it waits for every wheel to leave the ground,
        /// and wheels touching the wall still count as grounded.</summary>
        void Settle(float seconds)
        {
            var s = GetComponent<KnockStabilizer>();
            if (s == null) s = gameObject.AddComponent<KnockStabilizer>();
            s.duration = seconds;
            s.Kick();
        }

        void WallGrip(float dt)
        {
            Vector3 targetUp = Vector3.up;
            bool onGrip = false;
            if (Time.time >= detachUntil)
            {
                RaycastHit hit;
                if (latchT >= 0f || Time.time < latchUntil) { targetUp = latchNormal; onGrip = true; }
                else if (Physics.Raycast(body.worldCenterOfMass, -transform.up, out hit, gripProbe, probeMask, QueryTriggerInteraction.Ignore)
                         && hit.collider.GetComponentInParent<GripSurface>() != null)
                {
                    targetUp = hit.normal;
                    onGrip = true;
                    latchUntil = -1f;
                }
            }

            GripFactor = onGrip ? Mathf.InverseLerp(gripMinKmh - gripFadeKmh, gripMinKmh, Kmh) : 0f;
            Vector3 desired = Vector3.Slerp(Vector3.up, targetUp, GripFactor).normalized;
            gravityUp = Vector3.Slerp(gravityUp, desired, 1f - Mathf.Exp(-12f * dt)).normalized;
            float tilt = Vector3.Angle(gravityUp, Vector3.up);
            bool wasRiding = WallRiding;
            WallRiding = GripFactor > 0.01f && tilt > 25f;

            // Too slow to hold the wall: roll back onto the wheels instead of lying on its side.
            if (wasRiding && !WallRiding && latchT < 0f && unrollT < 0f && Time.time >= detachUntil && Vector3.Angle(transform.up, Vector3.up) > 45f)
            {
                StartUnroll();
                LastMove = "Fell off wall";
            }

            if (unrollT >= 0f) { body.useGravity = true; if (air != null) air.enabled = false; return; }

            bool latching = latchT >= 0f;
            if (latching) WallRiding = true;
            bool custom = tilt > 0.5f || latching;
            body.useGravity = !custom;
            if (air != null) air.enabled = !custom;
            if (!custom || latching) return; // the latch roll moves the car itself

            float g = Physics.gravity.magnitude + stickAccel * GripFactor;
            body.AddForce(-gravityUp * g, ForceMode.Acceleration);

            // Turn the car's up toward the surface (roll and pitch only, the player keeps the yaw).
            float angle; Vector3 axis;
            Quaternion.FromToRotation(transform.up, gravityUp).ToAngleAxis(out angle, out axis);
            if (angle > 180f) angle -= 360f;
            if (axis.sqrMagnitude < 1e-6f || float.IsInfinity(axis.x)) return;
            Vector3 w = body.angularVelocity;
            Vector3 tiltSpin = w - Vector3.Project(w, gravityUp);
            body.AddTorque(axis.normalized * (angle * Mathf.Deg2Rad * alignStrength) - tiltSpin * alignDamping, ForceMode.Acceleration);
        }
    }
}
