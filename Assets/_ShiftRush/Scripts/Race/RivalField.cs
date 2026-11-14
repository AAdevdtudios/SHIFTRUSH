using System.Collections.Generic;
using UnityEngine;

namespace ShiftRush.Race
{
    /// <summary>
    /// The rivals who line up against the player on every circuit: their car, paint and driving style, and the
    /// grid. Lives at Resources/SR_RivalField (made by SHIFT RUSH → Race → Build Rival Field). Edit it to rename
    /// rivals or make them faster (grip = how hard they corner, top speed = how fast on the straights).
    /// </summary>
    [CreateAssetMenu(menuName = "SHIFT RUSH/Rival Field", fileName = "SR_RivalField")]
    public class RivalField : ScriptableObject
    {
        [System.Serializable]
        public class Rival
        {
            public string name = "KESTREL";
            [Tooltip("Paint swapped in for the car's own body paint.")]
            public Material livery;
            [Tooltip("Dot colour on the minimap.")]
            public Color color = Color.white;
            [Tooltip("How hard they take corners. 1 = cautious, 1.5 = on the limit.")]
            [Range(0.8f, 1.8f)] public float grip = 1.25f;
            [Range(120f, 320f)] public float topSpeedKmh = 230f;
            [Tooltip("Chance they dive into a shortcut when they reach one.")]
            [Range(0f, 1f)] public float shortcutChance = 0.5f;
        }

        [Tooltip("The drivable car the rivals race (an RCC prefab).")]
        public GameObject carPrefab;
        [Tooltip("The body paint on that prefab that each rival's livery replaces.")]
        public Material paintToReplace;
        public List<Rival> rivals = new List<Rival>();

        [Header("Grid")]
        [Tooltip("Where the player starts: 0 = pole, 5 = back of a six-car grid.")]
        [Range(0, 7)] public int playerGridSlot = 4;
        [Tooltip("Metres behind the start line to the front row.")]
        public float firstRowBack = 10f;
        public float rowSpacing = 10f;
        [Tooltip("Sideways distance of each grid column from the centre.")]
        public float laneSpacing = 4.5f;

        [Header("Pace")]
        [Tooltip("Rivals far behind the player find a few % more speed, rivals far ahead lose a few %.")]
        [Range(0f, 0.2f)] public float catchUp = 0.06f;
        // Set per race from the difficulty (RaceGrid): split rubber band and a nitro trickle. -1 = use catchUp.
        [System.NonSerialized] public float pushFromBehind = -1f, easeOutFront = -1f, nitroPerSecond;
        [Tooltip("Nitro a rival earns when it jumps a SHIFT ramp (min, max of a full tank).")]
        public Vector2 rampNitro = new Vector2(0.35f, 0.9f);

        public static RivalField Load() { return Resources.Load<RivalField>("SR_RivalField"); }
    }
}
