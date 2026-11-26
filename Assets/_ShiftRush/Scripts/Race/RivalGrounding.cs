using ShiftRush.Vehicles;
using UnityEngine;

namespace ShiftRush.Race
{
    /// <summary>
    /// Keeps a rival on its wheels. Always on:
    /// - leans it back level when it tips past a small angle, and damps any roll or pitch wobble,
    /// - presses it into the road harder the faster it goes, and pulls it down sooner when it's in the air,
    /// - if it still ends up on its side or roof, it rolls itself back onto its wheels in under half a second,
    ///   in place, keeping some of its speed, instead of sitting there until the reset.
    /// Stands aside while the car is doing a move (leap, roll, wall ride) or being thrown by a knock.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DefaultExecutionOrder(70)]
    public class RivalGrounding : MonoBehaviour
    {
        [Tooltip("Degrees of tilt allowed before it's pushed back level.")]
        public float freeTilt = 18f;
        public float levelStrength = 14f;
        public float tiltDamping = 5f;
        [Tooltip("Extra downforce per (m/s)², as acceleration, while the wheels are down.")]
        public float downforce = 0.006f;
        public float maxDownforce = 9f;
        [Tooltip("Extra gravity while all wheels are off the ground.")]
        public float airGravity = 9f;
        [Tooltip("Seconds on its side or roof before it rolls back up.")]
        public float rightAfter = 0.35f;
        public float rightTime = 0.45f;
        [Tooltip("Share of horizontal speed kept through the roll back up.")]
        public float keepSpeed = 0.5f;

        Rigidbody body;
        IVehicle vehicle;
        GripMoves moves;
        KnockTumble tumble;
        float tippedFor, rightT = -1f, airFor;
        Quaternion rightFrom, rightTo;

        /// <summary>Times rivals rolled themselves back up, for tuning (RaceProbe).</summary>
        public static int SelfRights;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            vehicle = GetComponent<IVehicle>();
            moves = GetComponent<GripMoves>();
        }

        void FixedUpdate()
        {
            if (body == null || body.isKinematic) return;
            float dt = Time.fixedDeltaTime;

            if (rightT >= 0f) { Righting(dt); return; }

            if (tumble == null) tumble = GetComponent<KnockTumble>();
            bool busy = (tumble != null && tumble.Tumbling) || (moves != null && (moves.Leaping || moves.WallRiding));
            if (busy) { tippedFor = 0f; airFor = 0f; return; }

            float up = Vector3.Dot(transform.up, Vector3.up);
            bool grounded = vehicle != null && vehicle.AnyWheelGrounded;
            Vector3 v = body.linearVelocity;
            float speed = new Vector3(v.x, 0f, v.z).magnitude;

            // On its side or roof: roll back up.
            tippedFor = up < 0.35f ? tippedFor + dt : 0f;
            if (tippedFor > rightAfter) { BeginRighting(); return; }

            // Lean back level past the free angle, and damp the wobble.
            Quaternion.FromToRotation(transform.up, Vector3.up).ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (axis.sqrMagnitude > 1e-6f && !float.IsInfinity(axis.x))
            {
                Vector3 w = body.angularVelocity;
                Vector3 tilt = w - Vector3.Project(w, Vector3.up);
                float over = Mathf.Max(0f, Mathf.Abs(angle) - freeTilt) * Mathf.Sign(angle);
                body.AddTorque(axis.normalized * (over * Mathf.Deg2Rad * levelStrength) - tilt * tiltDamping * (grounded ? 0.5f : 1f), ForceMode.Acceleration);
            }

            if (grounded)
            {
                airFor = 0f;
                body.AddForce(-transform.up * Mathf.Min(maxDownforce, downforce * speed * speed), ForceMode.Acceleration);
            }
            else
            {
                // A short hop is fine; a longer flight is pulled back down so it lands before it can turn over.
                airFor += dt;
                if (airFor > 0.15f && v.y > -25f) body.AddForce(Vector3.down * airGravity, ForceMode.Acceleration);
            }
        }

        void BeginRighting()
        {
            tippedFor = 0f;
            rightT = 0f;
            rightFrom = body.rotation;
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.05f) fwd = Vector3.ProjectOnPlane(transform.up, Vector3.up); // nose straight up or down
            if (fwd.sqrMagnitude < 0.05f) fwd = Vector3.forward;
            rightTo = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            Vector3 v = body.linearVelocity;
            body.linearVelocity = new Vector3(v.x * keepSpeed, Mathf.Max(v.y, 0f), v.z * keepSpeed);
            body.angularVelocity = Vector3.zero;
            body.position += Vector3.up * 0.7f; // room to turn over without catching the road
            SelfRights++;
        }

        void Righting(float dt)
        {
            rightT += dt / rightTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(rightT));
            body.MoveRotation(Quaternion.Slerp(rightFrom, rightTo, k));
            body.angularVelocity = Vector3.zero;
            Vector3 v = body.linearVelocity;
            if (v.y < -2f) body.linearVelocity = new Vector3(v.x, -2f, v.z); // settle, don't drop
            if (rightT >= 1f) rightT = -1f;
        }
    }
}
