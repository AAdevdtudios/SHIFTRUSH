using UnityEngine;
using UnityEngine.InputSystem;

namespace ShiftRush.Vehicles
{
    /// <summary>
    /// Puts the car back on the road. Press R, or it happens on its own when the car falls off the world, stays
    /// upside down, is hung up on a rail, or runs off the circuit for a couple of seconds. On a circuit the car is put back on the race
    /// line where it left it, facing the right way; in free roam it goes back to the last place it was driving
    /// normally on all four wheels.
    /// </summary>
    public class VehicleReset : MonoBehaviour
    {
        [SerializeField] float killHeight = -15f;
        [SerializeField] float flippedSecondsBeforeReset = 2.5f;
        [SerializeField] float safePointInterval = 0.75f;
        [Tooltip("Seconds the player can't drive after a reset, so resetting isn't a free shortcut.")]
        [SerializeField] float controlLockSeconds = 0.6f;
        [SerializeField] float respawnLift = 0.8f;
        [Tooltip("Seconds off the circuit (past the verge, not on a shortcut) before the car is put back.")]
        [SerializeField] float offTrackSeconds = 2f;
        [Tooltip("Metres past the edge of the drivable surface that count as off the circuit.")]
        [SerializeField] float offTrackMargin = 6f;

        /// <summary>False on rivals: the R key is the player's.</summary>
        [HideInInspector] public bool listenForInput = true;

        IVehicle vehicle;
        Vector3 safePos;
        Quaternion safeRot;
        bool hasOverride;
        float safeTimer, flippedTimer, offTimer, beachedTimer, unlockAt = -1f;
        int lineHint = -1;

        void Awake()
        {
            vehicle = GetComponent<IVehicle>();
            safePos = transform.position;
            safeRot = FlatRotation(transform.forward);
        }

        public void SetRespawnPoint(Vector3 position, Quaternion rotation)
        {
            safePos = position; safeRot = rotation; hasOverride = true;
        }

        void Update()
        {
            if (vehicle == null) return;

            if (unlockAt > 0f && Time.time >= unlockAt) { vehicle.SetControllable(true); unlockAt = -1f; }

            var kb = Keyboard.current;
            if (listenForInput && kb != null && kb.rKey.wasPressedThisFrame) { ResetNow(); return; }

            if (transform.position.y < killHeight) { ResetNow(); return; }

            bool upsideDown = Vector3.Dot(transform.up, Vector3.up) < 0.2f;
            flippedTimer = upsideDown && vehicle.Body.linearVelocity.magnitude < 3f ? flippedTimer + Time.deltaTime : 0f;
            if (flippedTimer > flippedSecondsBeforeReset) { ResetNow(); return; }

            // Beached: hung up on a rail or rock, wheels not all down, barely moving.
            bool beached = !vehicle.AllWheelsGrounded && Mathf.Abs(vehicle.SpeedKmh) < 8f && Vector3.Dot(transform.up, Vector3.up) < 0.94f;
            beachedTimer = beached ? beachedTimer + Time.deltaTime : 0f;
            if (beachedTimer > 3f) { ResetNow(); return; }

            // Off the circuit: past the verge and not on a shortcut. Ignored far from the line (SHIFT worlds).
            var line = Race.RaceLine.Current;
            if (line != null && line.points != null && line.points.Length > 1)
            {
                lineHint = line.Nearest(transform.position, lineHint);
                float lat = Mathf.Abs(line.Lateral(transform.position, lineHint));
                float away = (line.points[lineHint] - transform.position).magnitude;
                bool off = away < 150f && lat > line.halfWidth + offTrackMargin && !Race.ShortcutPath.OnAny(transform.position, 3f);
                offTimer = off ? offTimer + Time.deltaTime : 0f;
                if (offTimer > offTrackSeconds) { ResetNow(); return; }
            }

            if (!hasOverride)
            {
                safeTimer += Time.deltaTime;
                if (safeTimer >= safePointInterval && vehicle.AllWheelsGrounded && !upsideDown && Mathf.Abs(vehicle.SpeedKmh) > 5f)
                {
                    safeTimer = 0f;
                    safePos = transform.position;
                    safeRot = FlatRotation(transform.forward);
                }
            }
        }

        /// <summary>Any car put back on the track (R, flipped, off-track or stuck).</summary>
        public static event System.Action<VehicleReset> Respawned;

        public void ResetNow()
        {
            flippedTimer = 0f; offTimer = 0f; beachedTimer = 0f;
            Vector3 pos; Quaternion rot;
            if (!OnRaceLine(out pos, out rot)) { pos = safePos; rot = safeRot; }
            vehicle.Teleport(pos + Vector3.up * respawnLift, rot, 0f);
            vehicle.SetControllable(false);
            unlockAt = Time.time + controlLockSeconds;
            Respawned?.Invoke(this);
        }

        /// <summary>The race line where the car is (centre of the road, race direction), nudged forward if
        /// another car is sitting there. False in free roam or when far from the circuit.</summary>
        bool OnRaceLine(out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero; rot = Quaternion.identity;
            var line = Race.RaceLine.Current;
            if (line == null || line.points == null || line.points.Length < 2) return false;
            int i = line.Nearest(transform.position, lineHint);
            if ((line.points[i] - transform.position).magnitude > 400f) return false;
            float d = line.distances[i];
            for (int tries = 0; tries < 6; tries++, d += 7f)
            {
                int k = line.IndexAt(d);
                pos = line.PointAt(d);
                rot = Quaternion.LookRotation(line.Direction(k), Vector3.up);
                float best = float.MinValue;
                foreach (var hit in Physics.RaycastAll(pos + Vector3.up * 2.5f, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore))
                    if (hit.rigidbody == null && hit.point.y > best) best = hit.point.y;
                if (best > float.MinValue) pos.y = best;
                bool blocked = false;
                foreach (var c in Physics.OverlapSphere(pos + Vector3.up, 3.5f, ~0, QueryTriggerInteraction.Ignore))
                    if (c.attachedRigidbody != null && c.attachedRigidbody != vehicle.Body) { blocked = true; break; }
                if (!blocked) break;
            }
            lineHint = line.IndexAt(d);
            return true;
        }

        static Quaternion FlatRotation(Vector3 forward)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }
    }
}
