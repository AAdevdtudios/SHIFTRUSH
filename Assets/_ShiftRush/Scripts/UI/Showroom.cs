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
        [Tooltip("Where the camera should frame each screen: Home shows the car to the right, Garage centres it.")]
        [SerializeField] Vector3 homeOffset = new Vector3(-1.4f, 0f, 0f);
        [SerializeField] Vector3 garageOffset = new Vector3(0f, 0f, 0f);

        GameObject current;
        float baseFov, kick, baseYaw;
        Vector3 framing, framingTarget;

        void Awake()
        {
            if (cam == null) cam = Camera.main;
            baseFov = cam != null ? cam.fieldOfView : 40f;
            baseYaw = orbitPivot != null ? orbitPivot.eulerAngles.y : 0f;
            framing = framingTarget = homeOffset;
        }

        void Start()
        {
            var car = RaceSession.Car;
            if (car != null) Show(car);
        }

        public void FrameHome() { framingTarget = homeOffset; }
        public void FrameGarage() { framingTarget = garageOffset; }

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
            if (turntable != null) turntable.Rotate(0f, turntableSpeed * dt, 0f, Space.World);

            kick = Mathf.MoveTowards(kick, 0f, dt * 2.5f);
            framing = Vector3.Lerp(framing, framingTarget, 1f - Mathf.Exp(-dt * 3f));

            if (orbitPivot != null)
            {
                float yaw = baseYaw + Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / orbitPeriod) * orbitSway;
                orbitPivot.rotation = Quaternion.Euler(orbitPivot.eulerAngles.x, yaw, 0f);
                orbitPivot.position = turntable.position + orbitPivot.rotation * framing;
            }
            if (cam != null)
            {
                float k = kick * kick;
                cam.fieldOfView = baseFov + k * kickFov;
                cam.transform.localRotation = Quaternion.Euler(-k * 0.6f, 0f, 0f);
            }
        }
    }
}
