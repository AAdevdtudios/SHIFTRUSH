using System.Collections.Generic;
using UnityEngine;

namespace ShiftRush.Core
{
    /// <summary>
    /// Story mode, Act 1: the case against Channel 13 (Resources/SR_Story, filled by SHIFT RUSH → Story → Build
    /// Story Catalog). Twelve episodes in three chapters, shown as Detective Kowal's case board. An episode is
    /// playable once it's filmed (its event type exists in the game) and the one before it is closed.
    /// </summary>
    [CreateAssetMenu(menuName = "SHIFT RUSH/Story Catalog", fileName = "SR_Story")]
    public class StoryCatalog : ScriptableObject
    {
        [System.Serializable]
        public class Episode
        {
            public string id = "ep01";
            public int number = 1;
            [Tooltip("0 = The Drop, 1 = Going Live, 2 = Heat.")]
            public int chapter;
            public string title = "FIRST DROP";
            [Tooltip("What the event plays as (shown on the card and briefing).")]
            public string eventType = "RACE";
            [Tooltip("Race catalog track id.")]
            public string trackId = "alpine";
            [TextArea] public string synopsis = "";
            [Tooltip("Slim's line on the briefing.")]
            [TextArea] public string slimLine = "";
            [Tooltip("Kowal's sticky note on the case board.")]
            public string kowalNote = "";
            [Tooltip("Finish in this place or better to close the episode.")]
            [Range(1, 6)] public int finishWithin = 3;
            [Tooltip("Hype for closing it.")]
            public int payout = 1000;
            public string glossBonus = "Out-drift the knocks";
            public string gritBonus = "Out-knock the drifts";
            [Tooltip("Named rival put on the grid in the first rival slot (empty = the usual field).")]
            public string rival = "";
            public string rivalLine = "";
            [Tooltip("Pace of the named rival (1 = like the rest).")]
            public float rivalPace = 1f;
            [Tooltip("Off = not filmed yet: shows as coming soon.")]
            public bool playable = true;
            [Tooltip("Shown instead of the title until the episode is reached.")]
            public bool redacted;
            [Tooltip("Next episode's teaser after this one closes.")]
            public string teaser = "";
        }

        public string actTitle = "ACT 1 · THE LEAGUE";
        public string[] chapters = { "THE DROP", "GOING LIVE", "HEAT" };
        public List<Episode> episodes = new List<Episode>();
        [Tooltip("The Heavy Lifter flown in the ride to each story race (the garage's parked one is static and can't move).")]
        public GameObject liftChopper;

        public Episode Find(string id)
        {
            foreach (var e in episodes) if (e.id == id) return e;
            return null;
        }

        public int IndexOf(string id) { return episodes.FindIndex(e => e.id == id); }

        static StoryCatalog instance;
        public static StoryCatalog Instance { get { if (instance == null) instance = Resources.Load<StoryCatalog>("SR_Story"); return instance; } }
    }
}
