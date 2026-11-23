using UnityEngine;

namespace ShiftRush.Vehicles
{
    /// <summary>
    /// A fitted wing pressing the car into the road: a downward force that grows with the square of speed, so the
    /// tyres bite harder in fast corners and the car settles over crests. Level 1 adds about 15% of the car's weight
    /// at 180 km/h, a big wing (2) about 30%. Added by <see cref="CarLoadout"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PartDownforce : MonoBehaviour
    {
        [Range(0f, 3f)] public float level = 1f;
        [Tooltip("Share of the car's weight added per level at 50 m/s (180 km/h).")]
        public float weightShareAt180 = 0.15f;

        Rigidbody body;

        void Awake() { body = GetComponent<Rigidbody>(); }

        void FixedUpdate()
        {
            if (body == null || level <= 0f) return;
            float v = Vector3.Dot(body.linearVelocity, transform.forward);
            if (v <= 1f) return;
            float k = weightShareAt180 * level * body.mass * Physics.gravity.magnitude / (50f * 50f);
            body.AddForce(-transform.up * Mathf.Min(k * v * v, body.mass * Physics.gravity.magnitude * 0.6f));
        }
    }
}
