using ShiftRush.Vehicles;
using UnityEngine;

namespace ShiftRush.Shift
{
    /// <summary>A Trap Run obstacle: slides side to side, spins, or pops up and down. Reports hits to its arena.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class TrapMover : MonoBehaviour
    {
        public enum Motion { Slide, Spin, Pop }

        public Motion motion;
        [Tooltip("Slide: metres either side. Pop: metres it sinks.")]
        public float amplitude = 6f;
        [Tooltip("Cycles per second (Spin: turns per second).")]
        public float speed = 0.5f;
        [Range(0f, 1f)] public float phase;

        Rigidbody body;
        Vector3 basePos;
        Quaternion baseRot;
        ShiftArena arena;
        float t0;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            arena = GetComponentInParent<ShiftArena>();
        }

        void OnEnable()
        {
            basePos = transform.position;
            baseRot = transform.rotation;
            t0 = Time.time;
        }

        void FixedUpdate()
        {
            float t = (Time.time - t0) * speed + phase;
            switch (motion)
            {
                case Motion.Slide:
                    body.MovePosition(basePos + baseRot * Vector3.right * Mathf.Sin(t * Mathf.PI * 2f) * amplitude);
                    break;
                case Motion.Spin:
                    body.MoveRotation(baseRot * Quaternion.Euler(0f, t * 360f, 0f));
                    break;
                case Motion.Pop:
                    float up = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Sin(t * Mathf.PI * 2f) * 2f + 0.5f));
                    body.MovePosition(basePos - Vector3.up * amplitude * (1f - up));
                    break;
            }
        }

        void OnCollisionEnter(Collision c)
        {
            if (arena != null && c.rigidbody != null && c.rigidbody.GetComponent<IVehicle>() != null) arena.TrapHit();
        }
    }
}
