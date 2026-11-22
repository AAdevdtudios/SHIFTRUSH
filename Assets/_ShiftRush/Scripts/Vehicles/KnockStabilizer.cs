using UnityEngine;

namespace ShiftRush.Vehicles
{
    /// <summary>
    /// Added to a car hit by a spin (and to a car rolling off a wall). For a moment it keeps the car from rolling over,
    /// and keeps a knocked car sliding sideways for a set distance whatever its tyre grip, so a knock reads the same
    /// on every car. A wall or a power-up finishes the job, the spin sets it up.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class KnockStabilizer : MonoBehaviour
    {
        public float duration = 1.2f;
        public float levelStrength = 30f;
        public float damping = 8f;
        [Tooltip("How fast the knock slide dies away (per second). Slide distance is about knock speed / this.")]
        public float slideDecay = 2.4f;

        Rigidbody body;
        float until;
        Vector3 slide;

        public void Kick() { Kick(Vector3.zero); }

        /// <summary>slide: horizontal knock velocity to hold on to while it decays.</summary>
        public void Kick(Vector3 slideVelocity)
        {
            until = Time.time + duration;
            slide = Vector3.ProjectOnPlane(slideVelocity, Vector3.up);
            enabled = true;
        }

        void Awake() { body = GetComponent<Rigidbody>(); }

        void FixedUpdate()
        {
            if (Time.time > until) { enabled = false; return; }
            float dt = Time.fixedDeltaTime;

            float s = slide.magnitude;
            if (s > 0.3f)
            {
                Vector3 dir = slide / s;
                float along = Vector3.Dot(body.linearVelocity, dir);
                if (along < s) body.linearVelocity += dir * (s - along);
                slide *= Mathf.Exp(-slideDecay * dt);
            }

            float angle; Vector3 axis;
            Quaternion.FromToRotation(transform.up, Vector3.up).ToAngleAxis(out angle, out axis);
            if (angle > 180f) angle -= 360f;
            if (axis.sqrMagnitude < 1e-6f || float.IsInfinity(axis.x)) return;
            Vector3 w = body.angularVelocity;
            Vector3 tilt = w - Vector3.Project(w, Vector3.up);
            body.AddTorque(axis.normalized * (angle * Mathf.Deg2Rad * levelStrength) - tilt * damping, ForceMode.Acceleration);
        }
    }
}
