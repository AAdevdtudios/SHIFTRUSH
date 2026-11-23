using System.Collections.Generic;
using UnityEngine;

namespace ShiftRush.Core
{
    /// <summary>
    /// What kind of car it is. Each type makes a promise the look and the handling keep: muscle is heavy and drifty,
    /// a race car grips, a tuner is playful, a supercar is the fastest but bites, the all-rounder forgives, the heavy
    /// wins every knock. The type sets the car's drift character (see Vehicles.CarTypes).
    /// </summary>
    public enum CarType { AllRounder, Supercar, RaceCar, Muscle, Tuner, Heavy }

    /// <summary>
    /// How garage parts go on one car, set by hand in SHIFT RUSH → Cars → Parts Fitting because every car's wheels
    /// and boot are shaped differently. The automatic fit (sized to the car's own wheels and wing) is the starting
    /// point, and these numbers adjust it.
    /// </summary>
    [System.Serializable]
    public class PartFit
    {
        [Tooltip("Off = this car never wears a spoiler (the slot shows NONE).")]
        public bool takesSpoiler = true;
        [Tooltip("Off = no tyre swaps on this car.")]
        public bool takesTyres = true;
        [Tooltip("Part ids this car allows. Empty = every part in the catalog.")]
        public string[] allowedTyres = new string[0];
        public string[] allowedSpoilers = new string[0];

        [Header("Tyres (all four, mirrored left and right)")]
        [Tooltip("X and Y = height of the wheel, Z = width. 1 = the automatic fit.")]
        public Vector3 tyreScale = Vector3.one;
        [Tooltip("Metres. X = forward, Y = up, Z = outwards from the car.")]
        public Vector3 tyreOffset = Vector3.zero;

        [Header("Spoiler")]
        [Tooltip("Metres from the automatic seat. X = forward, Y = up, Z = sideways.")]
        public Vector3 spoilerOffset = Vector3.zero;
        [Tooltip("1 = the automatic size (the width of the car's own wing).")]
        public float spoilerScale = 1f;
        [Tooltip("Degrees. Positive tips the trailing edge up.")]
        public float spoilerTilt = 0f;

        public bool Allows(string[] list, string id)
        {
            if (id == "stock" || list == null || list.Length == 0) return true;
            return System.Array.IndexOf(list, id) >= 0;
        }
    }

    /// <summary>
    /// Every car and track the menu offers. Add a car or track here and it appears in the select screens.
    /// Lives at Resources/RaceCatalog so the race scenes can look up the player's choice too.
    /// </summary>
    [CreateAssetMenu(menuName = "SHIFT RUSH/Race Catalog", fileName = "RaceCatalog")]
    public class RaceCatalog : ScriptableObject
    {
        [System.Serializable]
        public class Car
        {
            public string id = "zephyr";
            public string displayName = "Zephyr";
            [Tooltip("Short tagline under the name.")]
            public string tagline = "";
            [Tooltip("What kind of car it is. Sets its drift character and shows on the car select card.")]
            public CarType type = CarType.AllRounder;
            [Tooltip("Hype to buy it in the garage. 0 = owned from the start.")]
            public int price = 0;
            [Tooltip("Which garage parts this car takes and how they sit on it. Set with SHIFT RUSH → Cars → Parts Fitting.")]
            public PartFit fit = new PartFit();
            [Tooltip("The drivable RCC prefab.")]
            public GameObject prefab;
            [Tooltip("Optional model-only prefab for the showroom. If empty, the drivable prefab is shown with its physics and scripts stripped.")]
            public GameObject showroomPrefab;
            [Range(0, 10)] public int topSpeed = 6;
            [Range(0, 10)] public int acceleration = 6;
            [Range(0, 10)] public int handling = 6;
            [Range(0, 10)] public int boost = 6;
            [Tooltip("A picture of the car for the track select screen. Rendered by the menu builder.")]
            public Sprite thumbnail;
            [Tooltip("Paint colour used for the card accent.")]
            public Color accent = new Color(1f, 0.25f, 0.85f);

            [Header("Chase camera")]
            [Tooltip("Metres behind the car. 0 = the track's own setting. Raise it for big vehicles.")]
            public float cameraDistance = 0f;
            [Tooltip("Metres above the car. 0 = the track's own setting. Raise it so a tall vehicle doesn't hide the road.")]
            public float cameraHeight = 0f;

            [Header("Nitro")]
            [Tooltip("The effect at each exhaust while nitro burns: any particle prefab (a GabrielAguiar Sci-Fi Thruster, say). Empty = the game's own neon flame.")]
            public GameObject nitroVfx;
            [Tooltip("Exhaust points in the car's own space, in metres (x right, y up, z forward). Empty = two, low at the back either side of centre.")]
            public Vector3[] nitroExhausts = new Vector3[0];
            [Tooltip("Turns the effect at each exhaust, in degrees, after it is pointed straight back.")]
            public Vector3 nitroRotation = Vector3.zero;
            [Tooltip("Size of the effect.")]
            [Range(0.05f, 3f)] public float nitroScale = 1f;
            [Tooltip("Children of the effect to hide, by name (the thruster demos carry a spaceship).")]
            public string[] nitroHide = { "SpaceshipMesh" };
        }

        [System.Serializable]
        public class Track
        {
            public string id = "arena";
            public string displayName = "Test Arena";
            [Tooltip("Scene name as listed in Build Profiles.")]
            public string scene = "TestArena";
            [TextArea] public string description = "";
            [Tooltip("Lap length. 0 for open areas.")]
            public float lengthKm = 0f;
            public Sprite thumbnail;
            [Tooltip("The circuit drawn as a single line, for track select. Made from the minimap by the menu builder.")]
            public Sprite map;
            [Tooltip("Shown while loading.")]
            public string[] loadingTips = new string[0];

            /// <summary>"3.4 KM", or "FREE ROAM" when no length is set.</summary>
            public string LengthLabel { get { return lengthKm > 0f ? lengthKm.ToString("0.0") + " KM" : "FREE ROAM"; } }
        }

        public List<Car> cars = new List<Car>();
        public List<Track> tracks = new List<Track>();

        /// <summary>The catalog car a spawned car was made from (matched by prefab name), or null.</summary>
        public Car FindCar(GameObject instance)
        {
            if (instance == null) return null;
            string n = instance.name.Replace("(Clone)", "").Trim();
            foreach (var c in cars)
            {
                if (c.prefab != null && (n == c.prefab.name || n.StartsWith(c.prefab.name))) return c;
                if (c.showroomPrefab != null && n == c.showroomPrefab.name) return c;
            }
            return null;
        }

        static RaceCatalog instance;
        public static RaceCatalog Instance
        {
            get
            {
                if (instance == null) instance = Resources.Load<RaceCatalog>("RaceCatalog");
                return instance;
            }
        }
    }

    /// <summary>The player's current choices, carried from the menu into the race.</summary>
    public static class RaceSession
    {
        const string CarKey = "SR_LastCar", TrackKey = "SR_LastTrack";

        public static int CarIndex
        {
            get { return Mathf.Clamp(PlayerPrefs.GetInt(CarKey, 0), 0, Mathf.Max(0, CarCount - 1)); }
            set { PlayerPrefs.SetInt(CarKey, value); }
        }

        public static int TrackIndex
        {
            get { return Mathf.Clamp(PlayerPrefs.GetInt(TrackKey, 0), 0, Mathf.Max(0, TrackCount - 1)); }
            set { PlayerPrefs.SetInt(TrackKey, value); }
        }

        static int CarCount { get { var c = RaceCatalog.Instance; return c != null ? c.cars.Count : 0; } }
        static int TrackCount { get { var c = RaceCatalog.Instance; return c != null ? c.tracks.Count : 0; } }

        public static RaceCatalog.Car Car
        {
            get { var c = RaceCatalog.Instance; return c != null && c.cars.Count > 0 ? c.cars[CarIndex] : null; }
        }

        public static RaceCatalog.Track Track
        {
            get { var c = RaceCatalog.Instance; return c != null && c.tracks.Count > 0 ? c.tracks[TrackIndex] : null; }
        }
    }
}
