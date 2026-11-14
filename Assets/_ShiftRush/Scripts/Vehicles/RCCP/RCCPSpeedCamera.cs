using UnityEngine;
using ShiftRush.CameraFX;

namespace ShiftRush.Vehicles
{
    /// <summary>
    /// Connects RCCP's chase camera to <see cref="SpeedFeel"/>. Runs after RCCP_Camera (execution order 5) so the
    /// rig is already placed for this frame; SpeedFeel then offsets the child camera and this sets the chase FOV.
    /// </summary>
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(RCCP_Camera))]
    public class RCCPSpeedCamera : MonoBehaviour
    {
        [SerializeField] SpeedFeel speedFeel;
        [Tooltip("Chase FOV standing still. RCCP's own speed FOV is replaced by this plus SpeedFeel's extra.")]
        [SerializeField] float baseFov = 55f;

        RCCP_Camera rig;
        RCCP_CarController car;
        IVehicle vehicle;
        RCCP_Nos nos;
        float trackDistance = -1f, trackHeight;

        void Awake()
        {
            rig = GetComponent<RCCP_Camera>();
            if (speedFeel == null) speedFeel = GetComponent<SpeedFeel>();
        }

        void LateUpdate()
        {
            if (speedFeel == null) return;
            // Paused or in SHIFT slow motion: no rumble left running.
            if (rig.actualCamera == null || Time.timeScale < 0.5f) speedFeel.StopRumble();
            if (rig.actualCamera == null || Time.timeScale <= 0f) return;

            var current = rig.cameraTarget.playerVehicle;
            if (current != car)
            {
                car = current;
                vehicle = car != null ? car.GetComponent<IVehicle>() : null;
                nos = car != null ? car.GetComponentInChildren<RCCP_Nos>(true) : null;
                FrameFor(car);
            }

            bool chase = car != null && rig.cameraMode == RCCP_Camera.CameraMode.TPS;
            bool boosting = nos != null && nos.nosInUse;
            speedFeel.Tick(vehicle, boosting, rig.actualCamera, chase, Time.deltaTime);

            // Overwrite (not add to) RCCP's chase FOV each frame, so nothing accumulates.
            if (chase) rig.targetFieldOfView = baseFov + speedFeel.ExtraFov;
        }

        // Each car can ask for its own chase framing (a monster truck needs the camera higher and further back to
        // see over its own roof); otherwise the track's setting stands.
        void FrameFor(RCCP_CarController c)
        {
            if (trackDistance < 0f) { trackDistance = rig.TPSDistance; trackHeight = rig.TPSHeight; }
            var cat = Core.RaceCatalog.Instance;
            var spec = c != null && cat != null ? cat.FindCar(c.gameObject) : null;
            rig.TPSDistance = spec != null && spec.cameraDistance > 0f ? spec.cameraDistance : trackDistance;
            rig.TPSHeight = spec != null && spec.cameraHeight > 0f ? spec.cameraHeight : trackHeight;
        }
    }
}
