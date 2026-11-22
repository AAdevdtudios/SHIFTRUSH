using ShiftRush.Vehicles;
using UnityEngine;

namespace ShiftRush.Lab
{
    /// <summary>
    /// A test-scene traffic car: laps a closed path at a steady speed in its own lane, so the player has moving
    /// targets to spin into and dodge around. Not a racer: no overtaking, no race state.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class LabCruiser : MonoBehaviour
    {
        public Vector3[] path;
        public float targetKmh = 70f;
        public float lane;

        IVehicle vehicle;
        IDriverInput input;
        int hint;

        void Start()
        {
            vehicle = GetComponent<IVehicle>();
            input = vehicle as IDriverInput;
            var reset = GetComponent<VehicleReset>();
            if (reset != null) reset.listenForInput = false;
        }

        void FixedUpdate()
        {
            if (input == null || path == null || path.Length < 3) return;
            Vector3 p = transform.position;
            int n = path.Length;
            float best = float.MaxValue;
            for (int k = -10; k <= 10; k++)
            {
                int i = ((hint + k) % n + n) % n;
                float d = (path[i] - p).sqrMagnitude;
                if (d < best) { best = d; hint = i; }
            }
            if (best > 40f * 40f) // lost (knocked far away): search the whole loop
                for (int i = 0; i < n; i++) { float d = (path[i] - p).sqrMagnitude; if (d < best) { best = d; hint = i; } }

            float speed = vehicle.Body.linearVelocity.magnitude * 3.6f;
            float look = 10f + speed * 0.15f;
            int j = hint; float run = 0f;
            while (run < look) { int nx = (j + 1) % n; run += Vector3.Distance(path[j], path[nx]); j = nx; }
            Vector3 dir = (path[(j + 1) % n] - path[j]).normalized;
            Vector3 target = path[j] + Vector3.Cross(Vector3.up, dir) * lane;

            Vector3 local = transform.InverseTransformPoint(target);
            float steer = Mathf.Clamp(Mathf.Atan2(local.x, local.z) / (26f * Mathf.Deg2Rad), -1f, 1f);
            float throttle = speed < targetKmh ? 1f : 0f;
            float brake = speed > targetKmh + 12f ? 0.4f : 0f;
            input.SetDriverInput(steer, throttle, brake, 0f, false);
        }
    }
}
