using System.Collections.Generic;
using ShiftRush.Core;
using UnityEngine;

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
        [SerializeField] float orbitSway = 14f;
        [SerializeField] float orbitPeriod = 22f;
        [SerializeField] float kickFov = 5f;
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
        [Header("Track select pose")]
        [Tooltip("Track select: where the camera frames the posed car (right of the text, above the cards).")]
        [SerializeField] Vector3 tracksOffset = new Vector3(-3.8f, -0.2f, 0f);
        [Tooltip("Camera distance from the car on track select (bigger = smaller car).")]
        [SerializeField] float tracksDistance = 9f;
        [Tooltip("Car heading relative to the camera on track select: 90 / -90 = side on, 180 = nose to camera.")]
        [SerializeField] float tracksYaw = -90f;
        [Tooltip("Hidden on track select (the car too: that screen shows a picture of it instead).")]
        [SerializeField] GameObject[] hideWhenPosed;

        GameObject current;
        float baseFov, kick, baseYaw;
        Vector3 framing, framingTarget;
        enum MenuScreen { Home, Garage, Tracks }
        MenuScreen screen = MenuScreen.Home;
        bool posed => screen == MenuScreen.Tracks;
        float pose;       // 0 = turning showroom, 1 = posed close-up (eased)
        float distance;

        void Awake()
        {
            if (cam == null) cam = Camera.main;
            baseFov = cam != null ? cam.fieldOfView : 40f;
            baseYaw = orbitPivot != null ? orbitPivot.eulerAngles.y : 0f;
            framing = framingTarget = homeOffset;
            distance = homeDistance;
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
            if (hideWhenPosed != null) foreach (var g in hideWhenPosed) if (g != null) g.SetActive(!posed);
            if (current != null) current.SetActive(!posed); // track select shows a picture of the car instead
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
            }
            current = holder;
            current.SetActive(!posed);
            Kick(1f);
        }

        /// <summary>Removes everything but visuals, scripts first (others may depend on what they require).</summary>
        static void Strip(GameObject root)
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
            framingTarget = screen == MenuScreen.Tracks ? tracksOffset : screen == MenuScreen.Garage ? garageOffset : homeOffset;
            float targetDistance = screen == MenuScreen.Tracks ? tracksDistance : screen == MenuScreen.Garage ? garageDistance : homeDistance;
            distance = Mathf.Lerp(distance, targetDistance, 1f - Mathf.Exp(-dt * 3f));
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
                else turntable.Rotate(0f, turntableSpeed * (1f - ease) * dt, 0f, Space.World);
            }

            kick = Mathf.MoveTowards(kick, 0f, dt * 2.5f);
            framing = Vector3.Lerp(framing, framingTarget, 1f - Mathf.Exp(-dt * 3f));

            if (orbitPivot != null)
            {
                float yaw = baseYaw + Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / orbitPeriod) * orbitSway * (1f - ease);
                orbitPivot.rotation = Quaternion.Euler(orbitPivot.eulerAngles.x, yaw, 0f);
                orbitPivot.position = turntable.position + orbitPivot.rotation * framing;
            }
            if (cam != null)
            {
                float k = kick * kick;
                cam.fieldOfView = baseFov + k * kickFov;
                cam.transform.localRotation = Quaternion.Euler(-k * 0.6f, 0f, 0f);
                var lp = cam.transform.localPosition;
                lp.z = -distance;
                cam.transform.localPosition = lp;
            }
        }
    }
}
