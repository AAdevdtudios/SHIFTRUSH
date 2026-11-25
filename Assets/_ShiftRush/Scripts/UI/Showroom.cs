using System.Collections.Generic;
using ShiftRush.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ShiftRush.UI
{
    /// <summary>
    /// The live car behind the menu: sits on a slowly turning plinth while the camera drifts around it.
    /// Menu actions "kick" the camera (a quick FOV punch and nudge) so shifting gears feels physical.
    /// Drivable RCC prefabs are shown with their physics and scripts stripped, so nothing drives or makes noise.
    /// </summary>
    public class Showroom : MonoBehaviour
    {
        [SerializeField] Transform turntable;
        [SerializeField] Camera cam;
        [Tooltip("The camera orbits this point.")]
        [SerializeField] Transform orbitPivot;
        [SerializeField] float turntableSpeed = 9f;
        [Tooltip("With the driver on stage the turntable stops and holds them facing the camera, turned this far (degrees) for a three-quarter look.")]
        [SerializeField] float driverFacing = -14f;
        [SerializeField] float orbitSway = 14f;
        [SerializeField] float orbitPeriod = 22f;
        [SerializeField] float kickFov = 5f;
        [Tooltip("Handheld camera drift, degrees (0 = locked off).")]
        [SerializeField] float handheld = 0.35f;
        [Tooltip("How quickly the handheld drift wanders.")]
        [SerializeField] float handheldSpeed = 0.35f;
        [Header("Look around")]
        [Tooltip("Drag with the mouse (on empty screen, not on a button) or push the right stick to orbit the car.")]
        [SerializeField] bool userOrbit = true;
        [Tooltip("Degrees of orbit per pixel dragged.")]
        [SerializeField] float dragSensitivity = 0.25f;
        [Tooltip("Degrees a second at full right stick.")]
        [SerializeField] float stickSpeed = 90f;
        [Tooltip("Lowest and highest camera tilt the player can drag to, degrees.")]
        [SerializeField] Vector2 pitchLimits = new Vector2(1f, 32f);
        [Tooltip("The camera never goes lower than this, metres above the ground (the tarmac is at 0).")]
        [SerializeField] float minCameraHeight = 0.6f;
        [Tooltip("Seconds after letting go before the camera drifts back to its framing.")]
        [SerializeField] float returnDelay = 5f;
        [Header("Car size")]
        [Tooltip("A car this long (m) uses the screen's distance as set. Longer cars, like trucks, push the camera back.")]
        [SerializeField] float referenceLength = 4.6f;
        [Header("Home")]
        [Tooltip("Camera framing on Home. X more negative = car further right, Y more negative = car higher.")]
        [SerializeField] Vector3 homeOffset = new Vector3(-1.4f, 0f, 0f);
        [Tooltip("Camera distance from the car on Home (bigger = smaller car).")]
        [SerializeField] float homeDistance = 13f;
        [Header("Garage")]
        [Tooltip("Camera framing on Garage. X more negative = car further right, Y more negative = car higher.")]
        [SerializeField] Vector3 garageOffset = new Vector3(0f, 0f, 0f);
        [Tooltip("Camera distance from the car on Garage (bigger = smaller car).")]
        [SerializeField] float garageDistance = 13f;
        [Tooltip("Camera tilt on Garage, degrees down. Higher = looking down on the car.")]
        [SerializeField] float garagePitch = 13f;
        [Tooltip("Camera tilt on Home, degrees down.")]
        [SerializeField] float homePitch = 4f;
        [Header("Track select pose")]
        [Tooltip("Track select: where the camera frames the posed car (right of the text, above the cards).")]
        [SerializeField] Vector3 tracksOffset = new Vector3(-3.8f, -0.2f, 0f);
        [Tooltip("Camera distance from the car on track select (bigger = smaller car).")]
        [SerializeField] float tracksDistance = 9f;
        [Tooltip("Car heading relative to the camera on track select: 90 / -90 = side on, 180 = nose to camera.")]
        [SerializeField] float tracksYaw = -90f;
        [Tooltip("Hidden on track select (the car too: that screen shows a picture of it instead).")]
        [SerializeField] GameObject[] hideWhenPosed;
        [Header("Driver (garage Driver tab)")]
        [Tooltip("Pivot offset for the driver. The camera sits 1.1 m above its pivot, so this frames the whole body above the garage's piece row.")]
        [SerializeField] Vector3 driverOffset = new Vector3(0f, -0.3f, 0f);
        [SerializeField] float driverDistance = 6.6f;
        [SerializeField] float driverPitch = 5f;
        [Tooltip("Brightness of the soft light in front of the driver.")]
        [SerializeField] float driverFill = 2.2f;

        GameObject current;
        RaceCatalog.Car shownCar;
        GameObject driverHolder;
        bool driverMode;

        /// <summary>The driver model on the stage (null unless the garage's Driver tab is open).</summary>
        public GameObject ShownDriver { get; private set; }

        /// <summary>The car on the turntable and its model, for previewing parts in the garage.</summary>
        public RaceCatalog.Car ShownCar { get { return shownCar; } }
        public GameObject ShownModel { get { return current != null && current.transform.childCount > 0 ? current.transform.GetChild(0).gameObject : null; } }
        float baseFov, kick, baseYaw;
        Vector3 framing, framingTarget;
        enum MenuScreen { Home, Garage, Tracks }
        MenuScreen screen = MenuScreen.Home;
        bool posed => screen == MenuScreen.Tracks;
        float pose;       // 0 = turning showroom, 1 = posed close-up (eased)
        float distance;
        float pitch, userYaw, userPitch, idle = 99f, sizeScale = 1f;
        bool dragging;
        Vector2 lastMouse;

        void Awake()
        {
            if (cam == null) cam = Camera.main;
            baseFov = cam != null ? cam.fieldOfView : 40f;
            baseYaw = orbitPivot != null ? orbitPivot.eulerAngles.y : 0f;
            framing = framingTarget = homeOffset;
            distance = homeDistance;
            pitch = homePitch;
        }

        void Start()
        {
            var car = RaceSession.Car;
            if (car != null) Show(car);
        }

        /// <summary>The car on the plinth right now (null between cars).</summary>
        public GameObject CurrentCar { get { return current; } }

        public void FrameHome() { SetScreen(MenuScreen.Home); }
        public void FrameGarage() { SetScreen(MenuScreen.Garage); }
        /// <summary>Track select: the turntable eases to a fixed side-on angle and stops, the camera moves in close
        /// and stops swaying, and the floor, plinth, ring and car itself disappear. Any other screen undoes it.</summary>
        public void FrameTracks() { SetScreen(MenuScreen.Tracks); }

        void SetScreen(MenuScreen s)
        {
            screen = s;
            if (s != MenuScreen.Garage && driverMode) HideDriver();
            if (hideWhenPosed != null) foreach (var g in hideWhenPosed) if (g != null) g.SetActive(!posed);
            if (current != null) current.SetActive(!posed && !driverMode); // track select shows a picture of the car instead
        }

        /// <summary>
        /// Mouse drag on empty screen (not on a button or the chat) or the right stick orbits the camera around the
        /// car. A while after you let go it drifts back to the screen's own framing.
        /// </summary>
        void LookAround(float dt)
        {
            if (!userOrbit || posed) { dragging = false; return; }
            var mouse = Mouse.current;
            bool moved = false;
            if (mouse != null)
            {
                Vector2 pos = mouse.position.ReadValue();
                if (mouse.leftButton.wasPressedThisFrame && !OverUi(pos)) { dragging = true; lastMouse = pos; }
                if (!mouse.leftButton.isPressed) dragging = false;
                if (dragging)
                {
                    Vector2 d = pos - lastMouse;
                    lastMouse = pos;
                    userYaw += d.x * dragSensitivity;
                    userPitch -= d.y * dragSensitivity * 0.6f;
                    moved = d.sqrMagnitude > 0f;
                }
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 st = pad.rightStick.ReadValue();
                if (st.sqrMagnitude > 0.04f)
                {
                    userYaw += st.x * stickSpeed * dt;
                    userPitch -= st.y * stickSpeed * 0.5f * dt;
                    moved = true;
                }
            }
            userPitch = Mathf.Clamp(userPitch, pitchLimits.x - pitch, pitchLimits.y - pitch);
            idle = moved || dragging ? 0f : idle + dt;
            if (idle > returnDelay)
            {
                float k = 1f - Mathf.Exp(-dt * 0.8f);
                userYaw = Mathf.Lerp(userYaw, 0f, k);
                userPitch = Mathf.Lerp(userPitch, 0f, k);
            }
        }

        /// <summary>True when the pointer is on something you can click (a button, menu panel or the chat).</summary>
        static bool OverUi(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var data = new PointerEventData(es) { position = screenPos };
            var hits = new List<RaycastResult>();
            es.RaycastAll(data, hits);
            foreach (var h in hits)
            {
                if (h.gameObject == null) continue;
                if (h.gameObject.GetComponentInParent<UnityEngine.UI.Selectable>() != null) return true;
                if (h.gameObject.GetComponentInParent<BroadcastMenuItem>() != null) return true;
                if (h.gameObject.GetComponentInParent<TheFeed>() != null) return true;
            }
            return false;
        }

        public void Kick(float amount) { kick = Mathf.Max(kick, amount); }

        public void Show(RaceCatalog.Car car)
        {
            if (current != null) Destroy(current);
            if (car == null) return;
            GameObject source = car.showroomPrefab != null ? car.showroomPrefab : car.prefab;
            if (source == null) return;

            // Instantiate under an inactive holder: no Awake/OnEnable runs until the scripts are gone.
            var holder = new GameObject("Showroom Car");
            holder.SetActive(false);
            holder.transform.SetParent(turntable, false);
            var model = Instantiate(source, holder.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            if (car.showroomPrefab == null) Strip(model);
            holder.SetActive(true); // bounds are only valid on active renderers

            // Sit the car on the plinth, centred on its visual bounds.
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                Vector3 offset = turntable.position - new Vector3(b.center.x, b.min.y, b.center.z);
                model.transform.position += offset;
                // Big cars (trucks) get a camera that stands further back.
                float length = Mathf.Max(b.size.x, b.size.z);
                sizeScale = Mathf.Clamp(length / Mathf.Max(0.5f, referenceLength), 1f, 1.8f);
            }
            current = holder;
            shownCar = car;
            Vehicles.CarLoadout.ApplyFitted(model, car, false); // the parts fitted in the garage, looks only
            current.SetActive(!posed && !driverMode);
            Kick(1f);
        }

        /// <summary>Puts a driver body on the stage in place of the car and frames it at person height.</summary>
        public GameObject ShowDriver(GameObject prefab)
        {
            if (driverHolder != null) Destroy(driverHolder);
            ShownDriver = null;
            if (prefab == null) return null;
            driverHolder = new GameObject("Showroom Driver");
            driverHolder.SetActive(false);
            driverHolder.transform.SetParent(turntable, false);
            var model = Instantiate(prefab, driverHolder.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            // The pack's own customisation script would reset the outfit in its Start: strip every script first.
            foreach (var mb in model.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null) DestroyImmediate(mb);
            model.AddComponent<DriverIdle>();
            // A soft front light that turns with the driver, so clothes read against the night set.
            var fill = new GameObject("Driver Fill").AddComponent<Light>();
            fill.transform.SetParent(driverHolder.transform, false);
            fill.transform.localPosition = new Vector3(0.4f, 1.7f, 1.6f);
            fill.type = LightType.Point;
            fill.range = 4.5f;
            fill.intensity = driverFill;
            fill.color = new Color(1f, 0.94f, 0.9f);
            fill.shadows = LightShadows.None;
            driverHolder.SetActive(true);
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                model.transform.position += new Vector3(turntable.position.x - b.center.x, turntable.position.y - b.min.y, turntable.position.z - b.center.z);
            }
            driverMode = true;
            if (current != null) current.SetActive(false);
            ShownDriver = model;
            Kick(0.6f);
            return model;
        }

        /// <summary>Back to the car.</summary>
        public void HideDriver()
        {
            if (driverHolder != null) Destroy(driverHolder);
            driverHolder = null;
            ShownDriver = null;
            if (!driverMode) return;
            driverMode = false;
            if (current != null) current.SetActive(!posed);
            Kick(0.6f);
        }

        /// <summary>Removes everything but visuals, scripts first (others may depend on what they require).</summary>
        public static void Strip(GameObject root)
        {
            // Newest first on each object: a script added later usually [RequireComponent]s an earlier one.
            for (int pass = 0; pass < 4; pass++)
            {
                bool any = false;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var list = t.GetComponents<MonoBehaviour>();
                    for (int i = list.Length - 1; i >= 0; i--)
                        if (list[i] != null) { DestroyImmediate(list[i]); any = true; }
                }
                if (!any) break;
            }
            var kill = new List<Component>();
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c is Transform || c is MeshFilter || c is MeshRenderer || c is SkinnedMeshRenderer || c is LODGroup) continue;
                kill.Add(c);
            }
            // Joints before bodies, colliders and audio after.
            kill.Sort((a, b) => Rank(a).CompareTo(Rank(b)));
            foreach (var c in kill) if (c != null) DestroyImmediate(c);
        }

        static int Rank(Component c)
        {
            if (c is Joint) return 0;
            if (c is Collider) return 1;
            if (c is Rigidbody) return 2;
            return 3;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            // Read every frame so Inspector edits in Play mode show straight away.
            framingTarget = driverMode ? driverOffset : screen == MenuScreen.Tracks ? tracksOffset : screen == MenuScreen.Garage ? garageOffset : homeOffset;
            float targetDistance = driverMode ? driverDistance : (screen == MenuScreen.Tracks ? tracksDistance : screen == MenuScreen.Garage ? garageDistance : homeDistance) * sizeScale;
            distance = Mathf.Lerp(distance, targetDistance, 1f - Mathf.Exp(-dt * 3f));
            float targetPitch = driverMode ? driverPitch : screen == MenuScreen.Garage ? garagePitch : homePitch;
            pitch = Mathf.Lerp(pitch, targetPitch, 1f - Mathf.Exp(-dt * 3f));
            LookAround(dt);
            pose = Mathf.MoveTowards(pose, posed ? 1f : 0f, dt * 1.6f);
            float ease = pose * pose * (3f - 2f * pose);
            if (turntable != null)
            {
                if (posed)
                {
                    // Glide to the pose and hold it.
                    var target = Quaternion.Euler(0f, baseYaw + tracksYaw, 0f);
                    turntable.rotation = Quaternion.Slerp(turntable.rotation, target, 1f - Mathf.Exp(-dt * 4f));
                }
                else if (driverMode)
                {
                    // The driver doesn't spin: the stage turns them to face the camera and holds while they idle.
                    var target = Quaternion.Euler(0f, baseYaw + 180f + driverFacing, 0f);
                    turntable.rotation = Quaternion.Slerp(turntable.rotation, target, 1f - Mathf.Exp(-dt * 3f));
                }
                else if (!dragging) turntable.Rotate(0f, turntableSpeed * (1f - ease) * dt, 0f, Space.World);
            }

            kick = Mathf.MoveTowards(kick, 0f, dt * 2.5f);
            framing = Vector3.Lerp(framing, framingTarget, 1f - Mathf.Exp(-dt * 3f));

            if (orbitPivot != null)
            {
                float yaw = baseYaw + Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / orbitPeriod) * orbitSway * (1f - ease) + userYaw * (1f - ease);
                float tilt = Mathf.Clamp(pitch + userPitch * (1f - ease), pitchLimits.x, pitchLimits.y);
                orbitPivot.rotation = Quaternion.Euler(tilt, yaw, 0f);
                orbitPivot.position = turntable.position + orbitPivot.rotation * framing;
            }
            if (cam != null)
            {
                float k = kick * kick;
                cam.fieldOfView = baseFov + k * kickFov;
                // A slow handheld drift so the shot reads as a live camera, not a turntable render.
                float tt = Time.unscaledTime * handheldSpeed;
                float hx = (Mathf.PerlinNoise(tt, 1.7f) - 0.5f) * 2f * handheld;
                float hy = (Mathf.PerlinNoise(4.3f, tt) - 0.5f) * 2f * handheld;
                float hz = (Mathf.PerlinNoise(tt * 0.7f, 9.1f) - 0.5f) * 2f * handheld * 0.6f;
                cam.transform.localRotation = Quaternion.Euler(-k * 0.6f + hx, hy, hz);
                var lp = cam.transform.localPosition;
                lp.z = -distance;
                cam.transform.localPosition = lp;
                // Keep the lens above the tarmac however far the player drags: lift it and aim back at the car.
                var wp = cam.transform.position;
                if (wp.y < minCameraHeight)
                {
                    var rot = cam.transform.rotation;
                    var aim = turntable != null ? turntable.position + Vector3.up * 0.8f : wp + cam.transform.forward;
                    cam.transform.position = new Vector3(wp.x, minCameraHeight, wp.z);
                    var look = Quaternion.LookRotation(aim - cam.transform.position);
                    cam.transform.rotation = Quaternion.Slerp(rot, look, 0.6f);
                }
            }
        }
    }
}
