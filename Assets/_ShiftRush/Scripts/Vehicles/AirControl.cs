using UnityEngine;

namespace ShiftRush.Vehicles
{
    /// <summary>
    /// Limited mid-air control. While no wheel touches the ground the car is steadily turned back to level
    /// (so jumps land on four wheels, even off a badly taken ramp), and steering gently yaws it.
    /// Throttle deliberately does not pitch the car: players keep the accelerator held through jumps.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class AirControl : MonoBehaviour
    {
        [SerializeField] float yawStrength = 2.0f;
        [Tooltip("How hard the car is turned back to level in the air.")]
        [SerializeField] float levelStrength = 6f;
        [Tooltip("Damps pitch/roll spin in the air so levelling doesn't wobble.")]
        [SerializeField] float tiltDamping = 3f;
        [Tooltip("Ignore tiny hops; air control starts after this long airborne.")]
        [SerializeField] float airborneDelay = 0.15f;
        [Tooltip("Log each jump's air time to the console (handy for tuning SHIFT ramps).")]
        [SerializeField] bool logJumps = true;

        IVehicle vehicle;
        Rigidbody body;
        float airTime;

        public bool IsAirborne { get { return airTime > airborneDelay; } }
        public float AirTime { get { return airTime; } }

        void Awake()
        {
            vehicle = GetComponent<IVehicle>();
            body = GetComponent<Rigidbody>();
        }

        void FixedUpdate()
        {
            if (vehicle == null) return;

            if (vehicle.AnyWheelGrounded)
            {
                if (logJumps && IsAirborne)
                    Debug.Log("[SHIFT//RUSH] Jump: " + airTime.ToString("F2") + "s airborne, landed upright=" + (Vector3.Dot(transform.up, Vector3.up) > 0.8f));
                airTime = 0f;
                return;
            }
            airTime += Time.fixedDeltaTime;
            if (!IsAirborne) return;

            // Rotation that would bring the car's up back to world up (works even fully upside down).
            float angle; Vector3 axis;
            Quaternion.FromToRotation(transform.up, Vector3.up).ToAngleAxis(out angle, out axis);
            if (angle > 180f) angle -= 360f;
            Vector3 level = axis.sqrMagnitude > 0.0001f ? axis.normalized * (angle * Mathf.Deg2Rad * levelStrength) : Vector3.zero;

            // Damp spin except around the world vertical (that is the player's yaw).
            Vector3 w = body.angularVelocity;
            Vector3 tiltSpin = w - Vector3.Project(w, Vector3.up);

            Vector3 torque = level - tiltSpin * tiltDamping + Vector3.up * vehicle.SteerInput * yawStrength;
            body.AddTorque(torque, ForceMode.Acceleration);
        }
    }
}
