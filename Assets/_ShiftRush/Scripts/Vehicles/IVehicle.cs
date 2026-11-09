using UnityEngine;

namespace ShiftRush.Vehicles
{
    /// <summary>
    /// What SHIFT//RUSH game code needs from a car. Game systems (reset, SHIFT ramps, nitro, race manager)
    /// talk to this, never to the physics package directly, so the vehicle package can be swapped.
    /// </summary>
    public interface IVehicle
    {
        Transform Transform { get; }
        Rigidbody Body { get; }

        /// <summary>Signed forward speed in km/h.</summary>
        float SpeedKmh { get; }

        /// <summary>True when at least one wheel touches the ground.</summary>
        bool AnyWheelGrounded { get; }

        /// <summary>True when every wheel touches the ground.</summary>
        bool AllWheelsGrounded { get; }

        /// <summary>Steering input, -1..1, as the player is currently pressing it.</summary>
        float SteerInput { get; }

        /// <summary>Throttle minus brake, -1..1.</summary>
        float PitchInput { get; }

        void SetControllable(bool controllable);

        /// <summary>Move the car instantly, optionally keeping a forward speed (km/h).</summary>
        void Teleport(Vector3 position, Quaternion rotation, float forwardSpeedKmh);
    }
}
