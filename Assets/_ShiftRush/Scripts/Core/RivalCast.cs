using System.Collections.Generic;
using UnityEngine;

namespace ShiftRush.Core
{
    /// <summary>
    /// The people you race in story mode (Resources/SR_Cast, filled by SHIFT RUSH → Story → Build Story Catalog).
    /// Each has a name, their own car, a colour for the standings and minimap, a bio, and how they drive:
    /// grip and top speed for pace, aggression for knocks and rolls, drift skill, how often they take shortcuts.
    /// Story episodes list who's on the grid by id.
    /// </summary>
    [CreateAssetMenu(menuName = "SHIFT RUSH/Rival Cast", fileName = "SR_Cast")]
    public class RivalCast : ScriptableObject
    {
        [System.Serializable]
        public class Character
        {
            public string id = "kaito";
            public string name = "KAITO";
            [Tooltip("Race catalog car id.")]
            public string carId = "tetsuo";
            public Color color = Color.white;
            [Tooltip("One line: how they drive.")]
            public string style = "";
            [TextArea] public string bio = "";
            [Range(0.8f, 1.8f)] public float grip = 1.25f;
            [Range(120f, 320f)] public float topSpeedKmh = 230f;
            [Range(0f, 1f)] public float aggression = 0.5f;
            [Range(0f, 1f)] public float driftSkill = 0.5f;
            [Range(0f, 1f)] public float shortcutChance = 0.4f;
        }

        public List<Character> characters = new List<Character>();

        static RivalCast instance;
        public static RivalCast Instance { get { if (instance == null) instance = Resources.Load<RivalCast>("SR_Cast"); return instance; } }

        public Character Find(string id)
        {
            foreach (var c in characters) if (c.id == id) return c;
            return null;
        }
    }
}
