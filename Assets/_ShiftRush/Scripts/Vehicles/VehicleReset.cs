using UnityEngine;
using UnityEngine.InputSystem;

namespace ShiftRush.Vehicles
{
    /// <summary>
    /// Puts the car back on the road. Press R, or it happens on its own when the car falls off the world or
    /// stays upside down. Until race checkpoints exist (Milestone 4) the respawn point is the last place the
    /// car was driving normally on all four wheels; the race manager can override it with <see cref="SetRespawnPoint"/>.
    /// </summary>
    public class VehicleReset : MonoBehaviour
    {
        [SerializeField] float killHeight = -15f;
        [SerializeField] float flippedSecondsBeforeReset = 2.5f;
        [SerializeField] float safePointInterval = 0.75f;
        [Tooltip("Seconds the player can't drive after a reset, so resetting isn't a free shortcut.")]
        [SerializeField] float controlLockSeconds = 0.6f;
        [SerializeField] float respawnLift = 0.8f;

        IVehicle vehicle;
        Vector3 safePos;
        Quaternion safeRot;
        bool hasOverride;
        float safeTimer, flippedTimer, unlockAt = -1f;

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
            if (kb != null && kb.rKey.wasPressedThisFrame) { ResetNow(); return; }

            if (transform.position.y < killHeight) { ResetNow(); return; }

            bool upsideDown = Vector3.Dot(transform.up, Vector3.up) < 0.2f;
            flippedTimer = upsideDown && vehicle.Body.linearVelocity.magnitude < 3f ? flippedTimer + Time.deltaTime : 0f;
            if (flippedTimer > flippedSecondsBeforeReset) { ResetNow(); return; }

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

        public void ResetNow()
        {
            flippedTimer = 0f;
            vehicle.Teleport(safePos + Vector3.up * respawnLift, safeRot, 0f);
            vehicle.SetControllable(false);
            unlockAt = Time.time + controlLockSeconds;
        }

        static Quaternion FlatRotation(Vector3 forward)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }
    }
}
