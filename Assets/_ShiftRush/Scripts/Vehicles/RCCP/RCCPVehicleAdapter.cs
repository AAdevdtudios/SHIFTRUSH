using UnityEngine;

namespace ShiftRush.Vehicles
{
    /// <summary>
    /// The only bridge between SHIFT//RUSH and Realistic Car Controller Pro (a paid Asset Store package,
    /// git-ignored in the public repo). Everything else in the game uses <see cref="IVehicle"/>.
    /// </summary>
    [RequireComponent(typeof(RCCP_CarController))]
    [DefaultExecutionOrder(50)] // after RCCP_Input has read the keys this frame (auto-accelerate builds on it)
    public class RCCPVehicleAdapter : MonoBehaviour, IVehicle, IVehicleDash, INitroTank, IDriverInput
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

        public float Rpm01
        {
            get
            {
                float span = Mathf.Max(1f, car.maxEngineRPM - car.minEngineRPM);
                return Mathf.Clamp01((car.engineRPM - car.minEngineRPM) / span);
            }
        }

        public string GearLabel
        {
            get
            {
                var gb = car.Gearbox;
                if (gb != null && gb.currentGearState != null)
                {
                    switch (gb.currentGearState.gearState)
                    {
                        case RCCP_Gearbox.CurrentGearState.GearState.InReverseGear: return "R";
                        case RCCP_Gearbox.CurrentGearState.GearState.Neutral:
                        case RCCP_Gearbox.CurrentGearState.GearState.Park: return "N";
                    }
                }
                return (car.currentGear + 1).ToString();
            }
        }

        public float Nitro01
        {
            get
            {
                var addons = car.OtherAddonsManager;
                var nos = addons != null ? addons.Nos : null;
                return nos != null && nos.enabled ? nos.amount : -1f;
            }
        }

        public bool NitroActive
        {
            get
            {
                var addons = car.OtherAddonsManager;
                var nos = addons != null ? addons.Nos : null;
                return nos != null && nos.nosInUse;
            }
        }

        RCCP_Nos Nos
        {
            get { var a = car.OtherAddonsManager; var n = a != null ? a.Nos : null; return n != null && n.enabled ? n : null; }
        }

        float defaultRegenRate = -1f;

        float INitroTank.Nitro01 { get { var n = Nos; return n != null ? n.amount : -1f; } }

        public void SetNitro(float fill01)
        {
            var n = Nos;
            if (n == null) return;
            n.timer = n.durationTime * Mathf.Clamp01(fill01);
            n.amount = Mathf.Clamp01(fill01);
        }

        public void AddNitro(float fill01)
        {
            var n = Nos;
            if (n != null) SetNitro(n.amount + fill01);
        }

        public void SetNitroInput(bool on) { ControlBindings.SetNitroInput(on); }

        public void SetRegeneration(bool on)
        {
            var n = Nos;
            if (n == null) return;
            if (defaultRegenRate < 0f) defaultRegenRate = n.regenerateRate;
            n.regenerateRate = on ? defaultRegenRate : 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterSpawner()
        {
            VehicleSpawn.Rival = (prefab, pos, rot) =>
            {
                var rc = prefab != null ? prefab.GetComponent<RCCP_CarController>() : null;
                if (rc == null) return null;
                // Not registered as the player: the camera, dashboard and keyboard stay on the player's car.
                var car = RCCP.SpawnRCC(rc, pos, rot, false, true, true);
                RCCP.SetExternalControl(car, true);
                return car.GetComponent<IVehicle>();
            };
        }

        RCCP_Inputs driverInputs;

        public void SetDriverInput(float steer, float throttle, float brake, float handbrake, bool nitro)
        {
            if (car.Inputs == null) return;
            if (driverInputs == null) { driverInputs = new RCCP_Inputs(); car.externalControl = true; }
            driverInputs.steerInput = Mathf.Clamp(steer, -1f, 1f);
            driverInputs.throttleInput = Mathf.Clamp01(throttle);
            driverInputs.brakeInput = Mathf.Clamp01(brake);
            driverInputs.handbrakeInput = Mathf.Clamp01(handbrake);
            driverInputs.nosInput = nitro ? 1f : 0f;
            car.Inputs.OverrideInputs(driverInputs);
        }

        public void ReleaseDriverInput()
        {
            if (driverInputs == null || car.Inputs == null) return;
            car.Inputs.DisableOverrideInputs();
            car.externalControl = false;
            driverInputs = null;
        }

        void Awake()
        {
            car = GetComponent<RCCP_CarController>();
            body = GetComponent<Rigidbody>();
        }

        /// <summary>
        /// Settings → Gameplay → Auto Accelerate: full throttle on the player's car unless the brake is held. Steering,
        /// brake, handbrake and nitro stay with the player. Holds off while the gearbox shifts (RCCP cuts the throttle
        /// then) and while reversing, so braking to a stop still backs the car up.
        /// </summary>
        void Update()
        {
            var inputs = car.Inputs;
            if (inputs == null || !Core.GameSettings.AutoAccelerate || !car.IsControllableByPlayer()) return;
            if (inputs.brakeInput > 0.05f || car.shiftingNow || car.reversingNow) return;
            inputs.throttleInput = 1f;
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
