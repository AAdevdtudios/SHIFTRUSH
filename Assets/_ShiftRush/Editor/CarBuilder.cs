using System.Collections.Generic;
using ShiftRush.Core;
using ShiftRush.Vehicles;
using UnityEditor;
using UnityEngine;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// Builds the garage's cars from the ARCADE pack: each one is the Zephyr's RCC Pro setup (engine, gearbox,
    /// axles, stability, nitro, the game's own scripts) with another model's body and wheels dropped in, then tuned
    /// to its role. Registers them in the RaceCatalog with stats, a nitro effect and a picture. Once a car has a
    /// nitro effect, rebuilding leaves its nitro alone (tune it in SHIFT RUSH → Cars → Nitro Bench).
    /// SHIFT RUSH → Build Cars. Safe to run again: prefabs are rebuilt, catalog entries updated in place.
    /// </summary>
    public static class CarBuilder
    {
        const string Base = "Assets/_ShiftRush/Prefabs/Vehicles/Car_Zephyr.prefab";
        const string Out = "Assets/_ShiftRush/Prefabs/Vehicles/";
        const string Pack = "Assets/ARCADE - Ultimate Vehicles Pack/Prefabs (Meshes Only)/Land/";
        const string Thrusters = "Assets/GabrielAguiarProductions/UniqueSciFiVol_1/Prefabs/vfx_SciFiThruster";

        class Spec
        {
            public string id, name, model, tagline, thruster;
            [Tooltip("Kept for later: not built, and taken out of the catalog.")]
            public bool shelved;
            [Tooltip("Rear tyre grip, if different from the front (lower = slides more). 0 = same as grip.")]
            public float rearGrip;
            public int top, accel, handling, boost;
            public Color accent;
            public float mass, torque, maxKmh, steer, grip = 1f;
            public float nosMultiplier = 2.5f, nosSeconds = 3f, nitroScale = 0.3f;
            public float suspension = 0.2f, spring = 35000f, damper = 4500f;
            [Tooltip("Height of the centre of mass (car space; the ground is about -0.65). Lower = harder to roll.")]
            public float comY = -0.26f;
            public float antiroll = 500f;
            public float cameraDistance, cameraHeight; // 0 = the track's own
            [Tooltip("What kind of car it is. Sets its drift character.")]
            public CarType type = CarType.AllRounder;

        }

        static List<Spec> Specs()
        {
            return new List<Spec>
            {
                new Spec { id = "fireburst", name = "Fireburst", model = "Racing/R Class/Fireburst",
                    tagline = "Flat out or nothing. Twitchy at the limit.",
                    top = 9, accel = 7, handling = 5, boost = 5, accent = new Color32(56, 225, 255, 255),
                    mass = 1180f, torque = 520f, maxKmh = 215f, steer = 37f, grip = 0.95f, thruster = "02", type = CarType.Supercar },
                new Spec { id = "predator", name = "Predator", model = "Racing/R Class/Predator",
                    tagline = "A race car on a leash. Corners like it's on rails.",
                    top = 6, accel = 6, handling = 9, boost = 5, accent = new Color32(255, 150, 30, 255),
                    mass = 1300f, torque = 440f, maxKmh = 182f, steer = 42f, grip = 1.25f, thruster = "10", type = CarType.RaceCar },
                new Spec { id = "blackout", name = "Blackout", model = "Racing/B Class/Blackout",
                    tagline = "All muscle. The biggest, longest nitro burn.",
                    top = 7, accel = 8, handling = 4, boost = 9, accent = new Color32(40, 110, 255, 255),
                    mass = 1520f, torque = 560f, maxKmh = 196f, steer = 34f, grip = 0.92f,
                    nosMultiplier = 3.3f, nosSeconds = 4.5f, nitroScale = 0.36f, thruster = "03", type = CarType.Muscle },
                new Spec { id = "tetsuo", name = "Tetsuo", model = "Racing/R Class/Tetsuo",
                    tagline = "Born sideways. Throw it in and hold the slide.",
                    top = 7, accel = 7, handling = 7, boost = 6, accent = new Color32(255, 60, 80, 255),
                    mass = 1220f, torque = 470f, maxKmh = 192f, steer = 45f, grip = 1.08f, rearGrip = 0.86f, thruster = "07", type = CarType.Tuner },
                // The heavy type: slow to turn, impossible to stop, wins every knock.
                new Spec { id = "phobos", name = "Phobos", model = "Racing/X Class/Phobos", type = CarType.Heavy,
                    tagline = "Monster truck. Huge air off SHIFT ramps, shrugs off bumps.",
                    top = 4, accel = 5, handling = 5, boost = 6, accent = new Color32(150, 255, 60, 255),
                    mass = 2400f, torque = 760f, maxKmh = 160f, steer = 33f, grip = 1.0f, nitroScale = 0.42f,
                    suspension = 0.45f, spring = 70000f, damper = 7000f, comY = -0.7f, antiroll = 16000f,
                    cameraDistance = 7.2f, cameraHeight = 2.7f, thruster = "04" },
            };
        }

        [MenuItem("SHIFT RUSH/Build Cars")]
        public static void BuildAll()
        {
            var cat = AssetDatabase.LoadAssetAtPath<RaceCatalog>("Assets/_ShiftRush/Resources/RaceCatalog.asset");
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Base);
            if (cat == null || basePrefab == null) { Debug.LogError("[SHIFT//RUSH] Catalog or Car_Zephyr missing."); return; }
            int built = 0;
            foreach (var s in Specs())
            {
                if (s.shelved) { cat.cars.RemoveAll(c => c.id == s.id); continue; }
                var prefab = BuildCar(s, basePrefab);
                if (prefab == null) continue;
                Register(cat, s, prefab);
                built++;
            }
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            Debug.Log("[SHIFT//RUSH] Built " + built + " cars. Rebuild the main menu for their pictures.");
        }

        static GameObject BuildCar(Spec s, GameObject basePrefab)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + s.model + ".prefab");
            if (source == null) { Debug.LogWarning("[SHIFT//RUSH] No pack model " + s.model); return null; }

            var car = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            PrefabUtility.UnpackPrefabInstance(car, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            car.name = "Car_" + s.name;
            car.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            try
            {
                // Swap the Zephyr's model for this one, its origin on the ground as before.
                var old = car.transform.Find("Zephyr");
                Vector3 offset = old.localPosition;
                Object.DestroyImmediate(old.gameObject);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.transform.SetParent(car.transform, false);
                model.transform.localPosition = offset;
                model.name = s.name;

                // Wheels: each RCC wheel collider follows the model's wheel of the same corner.
                var wheels = new Dictionary<string, Transform>();
                foreach (var t in model.GetComponentsInChildren<Transform>(true)) wheels[t.name] = t;
                foreach (var wc in car.GetComponentsInChildren<RCCP_WheelCollider>(true))
                {
                    bool front = wc.transform.parent.name.Contains("Front");
                    bool left = wc.name.EndsWith("_L");
                    string key = (front ? "Front" : "Rear") + (left ? "Left" : "Right") + "Wheel";
                    Transform w;
                    if (!wheels.TryGetValue(key, out w)) { Debug.LogWarning("[SHIFT//RUSH] " + s.name + " has no " + key); continue; }
                    wc.wheelModel = w;
                    var mf = w.GetComponent<MeshFilter>();
                    float radius = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.extents.y * w.lossyScale.y : 0.37f;
                    Vector3 centre = car.transform.InverseTransformPoint(mf != null && mf.sharedMesh != null ? w.TransformPoint(mf.sharedMesh.bounds.center) : w.position);
                    var col = wc.GetComponent<WheelCollider>();
                    col.radius = radius;
                    col.suspensionDistance = s.suspension;
                    var spring = col.suspensionSpring; spring.spring = s.spring; spring.damper = s.damper; col.suspensionSpring = spring;
                    // Rest the wheel about half way into its travel, like the Zephyr.
                    wc.transform.position = car.transform.TransformPoint(centre + Vector3.up * s.suspension * 0.49f);
                    wc.grip = !front && s.rearGrip > 0f ? s.rearGrip : s.grip;
                }

                // The body's collider: the cabin box, clear of the wheels underneath.
                var body = model.transform.Find("Body");
                if (body != null)
                {
                    var mf = body.GetComponent<MeshFilter>();
                    var b = mf.sharedMesh.bounds;
                    var box = body.gameObject.AddComponent<BoxCollider>();
                    float bottom = b.min.y + b.size.y * 0.32f, top = b.max.y - b.size.y * 0.08f;
                    box.center = new Vector3(b.center.x, (bottom + top) * 0.5f, b.center.z);
                    box.size = new Vector3(b.size.x, top - bottom, b.size.z * 0.98f);
                }

                // Tuning.
                car.GetComponent<Rigidbody>().mass = s.mass;
                var engine = car.GetComponentInChildren<RCCP_Engine>(true);
                engine.maximumTorqueAsNM = s.torque;
                engine.maximumSpeed = s.maxKmh;
                foreach (var axle in car.GetComponentsInChildren<RCCP_Axle>(true))
                {
                    if (axle.isSteer) axle.maxSteerAngle = s.steer;
                    axle.antirollForce = s.antiroll;
                }
                var com = car.transform.Find("RCCP_Modules/RCCP_Aero/COM");
                if (com != null) com.localPosition = new Vector3(0f, s.comY, 0f);
                var nos = car.GetComponentInChildren<RCCP_Nos>(true);
                if (nos != null) { nos.torqueMultiplier = s.nosMultiplier; nos.durationTime = s.nosSeconds; nos.timer = s.nosSeconds; }

                var prefab = PrefabUtility.SaveAsPrefabAsset(car, Out + "Car_" + s.name + ".prefab");
                // Every Grip Pro feature, with this car's drift character.
                Debug.Log("[SHIFT//RUSH] " + CarSetupWizard.Apply(prefab, new CarSetupWizard.Options { drift = CarTypes.Drift(s.type) }));
                return prefab;
            }
            finally { Object.DestroyImmediate(car); }
        }

        static void Register(RaceCatalog cat, Spec s, GameObject prefab)
        {
            var c = cat.cars.Find(x => x.id == s.id);
            bool fresh = c == null;
            if (fresh) { c = new RaceCatalog.Car(); cat.cars.Add(c); }
            c.id = s.id; c.displayName = s.name; c.tagline = s.tagline; c.prefab = prefab; c.type = s.type;
            c.topSpeed = s.top; c.acceleration = s.accel; c.handling = s.handling; c.boost = s.boost; c.accent = s.accent;
            c.cameraDistance = s.cameraDistance; c.cameraHeight = s.cameraHeight;
            if (!fresh && c.nitroVfx != null) return; // nitro is tuned in the Nitro Bench after the first build: keep it
            c.nitroVfx = AssetDatabase.LoadAssetAtPath<GameObject>(Thrusters + s.thruster + ".prefab");
            c.nitroRotation = new Vector3(0f, -90f, 0f);
            c.nitroScale = s.nitroScale;
            if (c.nitroHide == null || c.nitroHide.Length == 0) c.nitroHide = new[] { "SpaceshipMesh" };
        }
    }
}
