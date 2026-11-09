using UnityEngine;

namespace ShiftRush.Vehicles
{
    /// <summary>
    /// The only bridge between SHIFT//RUSH and Realistic Car Controller Pro (a paid Asset Store package,
    /// git-ignored in the public repo). Everything else in the game uses <see cref="IVehicle"/>.
    /// </summary>
    [RequireComponent(typeof(RCCP_CarController))]
    public class RCCPVehicleAdapter : MonoBehaviour, IVehicle
    {
        RCCP_CarController car;
        Rigidbody body;

        public Transform Transform { get { return transform; } }
        public Rigidbody Body { get { return body; } }
        public float SpeedKmh { get { return car.speed; } }

        public bool AnyWheelGrounded
        {
            get
            {
                var wheels = car.AllWheelColliders;
                if (wheels == null) return false;
                for (int i = 0; i < wheels.Length; i++) if (wheels[i] != null && wheels[i].isGrounded) return true;
                return false;
            }
        }

        public bool AllWheelsGrounded
        {
            get
            {
                var wheels = car.AllWheelColliders;
                if (wheels == null || wheels.Length == 0) return false;
                for (int i = 0; i < wheels.Length; i++) if (wheels[i] == null || !wheels[i].isGrounded) return false;
                return true;
            }
        }

        public float SteerInput { get { return car.Inputs != null ? car.Inputs.steerInput : 0f; } }

        public float PitchInput
        {
            get { return car.Inputs != null ? car.Inputs.throttleInput - car.Inputs.brakeInput : 0f; }
        }

        void Awake()
        {
            car = GetComponent<RCCP_CarController>();
            body = GetComponent<Rigidbody>();
        }

        public void SetControllable(bool controllable)
        {
            RCCP.SetControl(car, controllable);
        }

        public void Teleport(Vector3 position, Quaternion rotation, float forwardSpeedKmh)
        {
            RCCP.Transport(car, position, rotation, true);
            if (forwardSpeedKmh != 0f)
                body.linearVelocity = rotation * Vector3.forward * (forwardSpeedKmh / 3.6f);
        }
    }
}
