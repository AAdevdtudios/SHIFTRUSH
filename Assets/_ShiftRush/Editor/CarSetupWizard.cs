using System.Collections.Generic;
using ShiftRush.Core;
using ShiftRush.Vehicles;
using ShiftRush.VFX;
using UnityEditor;
using UnityEngine;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// Car Setup wizard: puts every Grip Pro feature on a car prefab in one go (the link to RCC Pro, reset, air control,
    /// spiral rolls and knocks, wall riding, the held drift and its effects, the wind aura) and sets its drift character.
    /// Auto works the character out from the drivetrain: rear drive drifty, all-wheel balanced, front drive grippy.
    /// Pick a car from the catalog or drop any RCC prefab in, see what it has and what it's missing, then set it up,
    /// or set up every car in the catalog at once. Safe to run again: it only adds what's missing and updates settings.
    /// SHIFT RUSH → Cars → Car Setup. The car builder runs it on every car it builds.
    /// </summary>
    public class CarSetupWizard : EditorWindow
    {
        public class Options
        {
            public DriftCharacter drift = DriftCharacter.Auto;
            public bool rolls = true, driftMoves = true, driftFx = true, windAura = true, reset = true, airControl = true;
        }

        struct Feature
        {
            public string name, about;
            public System.Type type;
            public Feature(string name, System.Type type, string about) { this.name = name; this.type = type; this.about = about; }
        }

        static readonly Feature[] Features =
        {
            new Feature("Car link (RCC Pro)", typeof(RCCPVehicleAdapter), "Lets the game drive and read the car. Needed by everything below."),
            new Feature("Reset", typeof(VehicleReset), "Puts the car back on the road when it's stuck, flipped or fallen off."),
            new Feature("Air control", typeof(AirControl), "Keeps the car level in the air so it lands on its wheels."),
            new Feature("Spiral rolls, knocks, wall ride", typeof(GripMoves), "Q / E spiral rolls, Q + E forward roll, knocking rivals, riding grip walls."),
            new Feature("Held drift", typeof(DriftMoves), "Space + steer drift with charge tiers and an exit boost."),
            new Feature("Drift effects", typeof(DriftFX), "Smoke, sparks, tail trails and tyre screech, coloured by tier."),
            new Feature("Wind aura", typeof(SpeedAura), "Wind rings round the car during rolls and top-tier drifts (PixPlays pack)."),
        };

        GameObject car;
        Options options = new Options();
        Vector2 scroll;
        string report = "";
        RaceCatalog catalog;

        /// <summary>Selects the live drift tuning asset (creates it the first time).</summary>
        [MenuItem("SHIFT RUSH/Cars/Drift Tuning")]
        public static void SelectDriftTuning()
        {
            const string path = "Assets/_ShiftRush/Resources/SR_DriftTuning.asset";
            var t = AssetDatabase.LoadAssetAtPath<DriftTuning>(path);
            if (t == null)
            {
                t = CreateInstance<DriftTuning>();
                AssetDatabase.CreateAsset(t, path);
                AssetDatabase.SaveAssets();
            }
            Selection.activeObject = t;
            EditorGUIUtility.PingObject(t);
        }

        [MenuItem("SHIFT RUSH/Cars/Car Setup")]
        static void Open()
        {
            var w = GetWindow<CarSetupWizard>("Car Setup");
            w.minSize = new Vector2(420f, 520f);
        }

        void OnEnable()
        {
            catalog = AssetDatabase.LoadAssetAtPath<RaceCatalog>("Assets/_ShiftRush/Resources/RaceCatalog.asset");
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Grip Pro car setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Adds every Grip Pro feature to a car and sets how it drifts. Pick a car below, or drop any RCC Pro car prefab in.", MessageType.None);

            // ---- pick a car
            if (catalog != null && catalog.cars.Count > 0)
            {
                EditorGUILayout.LabelField("Garage", EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();
                int n = 0;
                foreach (var c in catalog.cars)
                {
                    if (c.prefab == null) continue;
                    GUI.backgroundColor = car == c.prefab ? new Color(1f, 0.5f, 0.85f) : Color.white;
                    if (GUILayout.Button(c.displayName, GUILayout.MinWidth(70f))) { car = c.prefab; options.drift = CarTypes.Drift(c.type); }
                    if (++n % 4 == 0) { EditorGUILayout.EndHorizontal(); EditorGUILayout.BeginHorizontal(); }
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }
            car = (GameObject)EditorGUILayout.ObjectField("Car prefab", car, typeof(GameObject), false);

            if (car != null)
            {
                EditorGUILayout.Space(6f);
                var rb = car.GetComponent<Rigidbody>();
                string drive = Drivetrain(car);
                DriftCharacter auto = AutoCharacter(car);
                EditorGUILayout.LabelField("Weight", rb != null ? rb.mass.ToString("F0") + " kg" : "no Rigidbody");
                EditorGUILayout.LabelField("Drivetrain", drive + "   (Auto drift: " + auto + ")");
                if (car.GetComponent<RCCP_CarController>() == null)
                    EditorGUILayout.HelpBox("This isn't an RCC Pro car (no RCCP_CarController). The features need one.", MessageType.Warning);

                // ---- features
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("Features", EditorStyles.boldLabel);
                foreach (var f in Features)
                {
                    bool has = car.GetComponent(f.type) != null;
                    EditorGUILayout.BeginHorizontal();
                    var old = GUI.color;
                    GUI.color = has ? new Color(0.4f, 1f, 0.7f) : new Color(1f, 0.6f, 0.4f);
                    GUILayout.Label(has ? "ON" : "MISSING", EditorStyles.miniBoldLabel, GUILayout.Width(58f));
                    GUI.color = old;
                    bool want = Wanted(f.type);
                    bool locked = f.type == typeof(RCCPVehicleAdapter);
                    using (new EditorGUI.DisabledScope(locked))
                        want = EditorGUILayout.ToggleLeft(new GUIContent(f.name, f.about), locked || want);
                    SetWanted(f.type, want);
                    EditorGUILayout.EndHorizontal();
                }

                // ---- car type (garage cars) and drift
                EditorGUILayout.Space(6f);
                var entry = catalog != null ? catalog.cars.Find(x => x.prefab == car) : null;
                if (entry != null)
                {
                    EditorGUILayout.LabelField("Car type", EditorStyles.boldLabel);
                    var t = (CarType)EditorGUILayout.EnumPopup("Type", entry.type);
                    if (t != entry.type) { entry.type = t; options.drift = CarTypes.Drift(t); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); }
                    EditorGUILayout.LabelField(" ", "A " + CarTypes.Name(entry.type).ToLowerInvariant() + " drifts " + CarTypes.Drift(entry.type).ToString().ToLowerInvariant() + ".");
                }
                EditorGUILayout.LabelField("Drift character", EditorStyles.boldLabel);
                options.drift = (DriftCharacter)EditorGUILayout.EnumPopup("Character", options.drift);
                if (entry != null && options.drift != CarTypes.Drift(entry.type))
                    EditorGUILayout.HelpBox("Not what a " + CarTypes.Name(entry.type).ToLowerInvariant() + " usually is (" + CarTypes.Drift(entry.type) + "). Fine if it's on purpose.", MessageType.Warning);
                var dm = car.GetComponent<DriftMoves>();
                if (dm != null) EditorGUILayout.LabelField("Now", dm.character + (dm.character == DriftCharacter.Auto ? " (" + dm.autoResolvesTo + ")" : "") + (dm.customTuning ? ", custom tuning" : ""));
                EditorGUILayout.HelpBox(About(options.drift == DriftCharacter.Auto ? auto : options.drift), MessageType.None);

                EditorGUILayout.Space(8f);
                GUI.backgroundColor = new Color(1f, 0.5f, 0.85f);
                if (GUILayout.Button("Set up " + car.name, GUILayout.Height(30f))) report = Apply(car, options);
                GUI.backgroundColor = Color.white;
            }

            if (catalog != null && GUILayout.Button("Set up every car in the garage (drift from each car's type)", GUILayout.Height(24f)))
            {
                var sb = new System.Text.StringBuilder();
                foreach (var c in catalog.cars)
                {
                    if (c.prefab == null) continue;
                    var o = new Options { drift = CarTypes.Drift(c.type) };
                    sb.AppendLine(Apply(c.prefab, o));
                }
                report = sb.ToString();
            }

            if (!string.IsNullOrEmpty(report)) EditorGUILayout.HelpBox(report.Trim(), MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        bool Wanted(System.Type t)
        {
            if (t == typeof(VehicleReset)) return options.reset;
            if (t == typeof(AirControl)) return options.airControl;
            if (t == typeof(GripMoves)) return options.rolls;
            if (t == typeof(DriftMoves)) return options.driftMoves;
            if (t == typeof(DriftFX)) return options.driftFx;
            if (t == typeof(SpeedAura)) return options.windAura;
            return true;
        }

        void SetWanted(System.Type t, bool v)
        {
            if (t == typeof(VehicleReset)) options.reset = v;
            else if (t == typeof(AirControl)) options.airControl = v;
            else if (t == typeof(GripMoves)) options.rolls = v;
            else if (t == typeof(DriftMoves)) options.driftMoves = v;
            else if (t == typeof(DriftFX)) options.driftFx = v;
            else if (t == typeof(SpeedAura)) options.windAura = v;
        }

        static DriftCharacter CurrentCharacter(GameObject prefab)
        {
            var dm = prefab.GetComponent<DriftMoves>();
            return dm != null ? dm.character : DriftCharacter.Auto;
        }

        static string About(DriftCharacter c)
        {
            switch (c)
            {
                case DriftCharacter.Drifty: return "Drifty: steps out from 38 km/h with a light touch of Space, opens up to 55°, barely loses speed, charges fast.";
                case DriftCharacter.Grippy: return "Grippy: needs 55 km/h and a firmer touch, opens only to 28°, bleeds speed, charges slowly, but a 40% bigger boost.";
                default: return "Balanced: steps out from 45 km/h, opens up to 42°, normal charge and boost.";
            }
        }

        /// <summary>Adds the chosen features to a car prefab and sets its drift character. Returns a one-line report.</summary>
        public static string Apply(GameObject prefabAsset, Options o)
        {
            string path = AssetDatabase.GetAssetPath(prefabAsset);
            if (string.IsNullOrEmpty(path)) return prefabAsset.name + ": not a prefab asset, skipped.";
            var root = PrefabUtility.LoadPrefabContents(path);
            var added = new List<string>();
            try
            {
                if (root.GetComponent<RCCP_CarController>() == null) return prefabAsset.name + ": not an RCC Pro car, skipped.";
                Add<RCCPVehicleAdapter>(root, true, added, "car link");
                Add<VehicleReset>(root, o.reset, added, "reset");
                Add<AirControl>(root, o.airControl, added, "air control");
                Add<GripMoves>(root, o.rolls, added, "rolls");
                var dm = Add<DriftMoves>(root, o.driftMoves, added, "drift");
                Add<DriftFX>(root, o.driftFx && o.driftMoves, added, "drift effects");
                Add<SpeedAura>(root, o.windAura, added, "wind aura");

                if (dm != null)
                {
                    dm.character = o.drift;
                    dm.addEffects = o.driftFx;
                    dm.autoResolvesTo = AutoCharacter(root);
                    if (!dm.customTuning) dm.ApplyCharacter();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                string drift = dm != null ? ", drift " + dm.character + (dm.character == DriftCharacter.Auto ? " (" + dm.autoResolvesTo + ")" : "") : "";
                return prefabAsset.name + ": " + (added.Count > 0 ? "added " + string.Join(", ", added) : "had everything") + drift + ".";
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static T Add<T>(GameObject root, bool want, List<string> added, string label) where T : Component
        {
            var c = root.GetComponent<T>();
            if (want && c == null) { c = root.AddComponent<T>(); added.Add(label); }
            else if (!want && c != null && typeof(T) != typeof(RCCPVehicleAdapter)) { Object.DestroyImmediate(c, true); c = null; }
            return c;
        }

        /// <summary>"Rear drive", "All-wheel drive", "Front drive" or "Unknown", from which axles get power.</summary>
        public static string Drivetrain(GameObject root)
        {
            bool front, rear;
            PoweredEnds(root, out front, out rear);
            if (front && rear) return "All-wheel drive";
            if (rear) return "Rear drive";
            if (front) return "Front drive";
            return "Unknown";
        }

        public static DriftCharacter AutoCharacter(GameObject root)
        {
            bool front, rear;
            PoweredEnds(root, out front, out rear);
            if (rear && !front) return DriftCharacter.Drifty;
            if (front && !rear) return DriftCharacter.Grippy;
            return DriftCharacter.Balanced;
        }

        static void PoweredEnds(GameObject root, out bool front, out bool rear)
        {
            front = rear = false;
            var powered = new HashSet<RCCP_Axle>();
            foreach (var d in root.GetComponentsInChildren<RCCP_Differential>(true)) if (d.connectedAxle != null) powered.Add(d.connectedAxle);
            foreach (var a in root.GetComponentsInChildren<RCCP_Axle>(true)) if (a.isPower) powered.Add(a);
            foreach (var a in powered)
            {
                Transform w = a.leftWheelCollider != null ? a.leftWheelCollider.transform : a.transform;
                float z = root.transform.InverseTransformPoint(w.position).z;
                if (z >= 0f) front = true; else rear = true;
            }
        }
    }
}
