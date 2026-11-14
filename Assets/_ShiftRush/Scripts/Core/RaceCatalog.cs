using System.Collections.Generic;
using UnityEngine;

namespace ShiftRush.Core
{
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
