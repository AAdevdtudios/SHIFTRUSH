using UnityEngine;

namespace ShiftRush.Core
{
    /// <summary>
    /// Story progress in PlayerPrefs: which episodes are closed, how the Feed leans (drift Hype counts for GLOSS,
    /// knock Hype for GRIT), and the episode being played right now so the race result can close it and the menu
    /// can come back to the case board.
    /// </summary>
    public static class StoryProgress
    {
        const string DoneKey = "GripPro.Story.Done";
        const string GlossKey = "GripPro.Story.Gloss";
        const string GritKey = "GripPro.Story.Grit";
        const string ActiveKey = "GripPro.Story.Active";
        const string BoardKey = "GripPro.Story.ReturnToBoard";

        public struct Result
        {
            public bool story, closed, alreadyClosed;
            public StoryCatalog.Episode episode;
            public int payout, place;
        }

        public enum State { Closed, Open, Sealed, Filming }

        public static bool IsClosed(string id)
        {
            foreach (var s in PlayerPrefs.GetString(DoneKey, "").Split(',')) if (s == id) return true;
            return false;
        }

        public static int ClosedCount
        {
            get { int n = 0; foreach (var s in PlayerPrefs.GetString(DoneKey, "").Split(',')) if (!string.IsNullOrEmpty(s)) n++; return n; }
        }

        /// <summary>Closed, open to play, sealed (the one before isn't closed) or still filming.</summary>
        public static State StateOf(StoryCatalog.Episode e)
        {
            var cat = StoryCatalog.Instance;
            if (e == null || cat == null) return State.Sealed;
            if (IsClosed(e.id)) return State.Closed;
            int i = cat.IndexOf(e.id);
            bool reached = i <= 0 || IsClosed(cat.episodes[i - 1].id);
            if (!reached) return State.Sealed;
            return e.playable ? State.Open : State.Filming;
        }

        /// <summary>The first episode that isn't closed (the one the board opens on).</summary>
        public static StoryCatalog.Episode Current
        {
            get
            {
                var cat = StoryCatalog.Instance;
                if (cat == null || cat.episodes.Count == 0) return null;
                foreach (var e in cat.episodes) if (!IsClosed(e.id)) return e;
                return cat.episodes[cat.episodes.Count - 1];
            }
        }

        /// <summary>GLOSS share of the Feed, 0..1 (0.5 before any story race).</summary>
        public static float GlossShare
        {
            get
            {
                float g = PlayerPrefs.GetInt(GlossKey, 0), r = PlayerPrefs.GetInt(GritKey, 0);
                return g + r <= 0f ? 0.5f : g / (g + r);
            }
        }

        // ------------------------------------------------------------------ playing an episode

        /// <summary>The episode being raced (empty when the race isn't a story one).</summary>
        public static string Active
        {
            get { return PlayerPrefs.GetString(ActiveKey, ""); }
            set { PlayerPrefs.SetString(ActiveKey, value ?? ""); PlayerPrefs.Save(); }
        }

        public static StoryCatalog.Episode ActiveEpisode { get { var cat = StoryCatalog.Instance; return cat != null ? cat.Find(Active) : null; } }

        /// <summary>Starts an episode: picks its track and loads it.</summary>
        public static bool Play(StoryCatalog.Episode e)
        {
            var race = RaceCatalog.Instance;
            if (e == null || race == null) return false;
            int t = race.tracks.FindIndex(x => x.id == e.trackId);
            if (t < 0) return false;
            if (!PlayerProfile.OwnsCar(RaceSession.Car)) RaceSession.CarIndex = PlayerProfile.FirstOwnedIndex();
            RaceSession.TrackIndex = t;
            Active = e.id;
            SceneLoader.LoadTrack(race.tracks[t]);
            return true;
        }

        /// <summary>The race finished: closes the episode if the place is good enough and tilts the Feed.</summary>
        public static Result Report(int place, int driftHype, int knockHype)
        {
            var r = new Result { place = place };
            var e = ActiveEpisode;
            if (e == null) return r;
            r.story = true;
            r.episode = e;
            PlayerPrefs.SetInt(GlossKey, PlayerPrefs.GetInt(GlossKey, 0) + Mathf.Max(0, driftHype));
            PlayerPrefs.SetInt(GritKey, PlayerPrefs.GetInt(GritKey, 0) + Mathf.Max(0, knockHype));
            r.alreadyClosed = IsClosed(e.id);
            r.closed = place <= e.finishWithin;
            if (r.closed && !r.alreadyClosed)
            {
                string cur = PlayerPrefs.GetString(DoneKey, "");
                PlayerPrefs.SetString(DoneKey, string.IsNullOrEmpty(cur) ? e.id : cur + "," + e.id);
                r.payout = e.payout;
            }
            PlayerPrefs.SetInt(BoardKey, 1); // the menu opens on the case board next time
            PlayerPrefs.Save();
            return r;
        }

        /// <summary>Called by the menu on load: true once after a story race, and the active episode ends.</summary>
        public static bool TakeReturnToBoard()
        {
            bool back = PlayerPrefs.GetInt(BoardKey, 0) == 1;
            PlayerPrefs.SetInt(BoardKey, 0);
            Active = "";
            return back;
        }

        /// <summary>The named rival for the first grid slot in a story race, or null.</summary>
        public static string RivalName { get { var e = ActiveEpisode; return e != null && !string.IsNullOrEmpty(e.rival) ? e.rival : null; } }
        public static float RivalPace { get { var e = ActiveEpisode; return e != null && !string.IsNullOrEmpty(e.rival) ? e.rivalPace : 1f; } }
    }
}
