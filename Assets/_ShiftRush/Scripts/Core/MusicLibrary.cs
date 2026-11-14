using UnityEngine;

namespace ShiftRush.Core
{
    /// <summary>
    /// Every song in Resources/Audio/Music, and the player's music choice: shuffle them all, silence, or one song
    /// on repeat. The choice is saved by song name, so adding or removing songs never changes it.
    /// </summary>
    public static class MusicLibrary
    {
        public const string Folder = "Audio/Music";
        public const string ChoiceKey = "SR_MusicChoice";
        public const string Shuffle = "*shuffle", Off = "*off";

        static AudioClip[] clips;

        public static event System.Action Changed;

        public static AudioClip[] Clips
        {
            get
            {
                if (clips == null)
                {
                    clips = Resources.LoadAll<AudioClip>(Folder);
                    System.Array.Sort(clips, (a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
                }
                return clips;
            }
        }

        /// <summary>"starting_line-final" → "STARTING LINE FINAL".</summary>
        public static string Pretty(string fileName)
        {
            return fileName.Replace('_', ' ').Replace('-', ' ').Trim().ToUpperInvariant();
        }

        /// <summary>Settings labels: SHUFFLE ALL, OFF, then each song.</summary>
        public static string[] Labels
        {
            get
            {
                var c = Clips;
                var l = new string[c.Length + 2];
                l[0] = "SHUFFLE ALL"; l[1] = "OFF";
                for (int i = 0; i < c.Length; i++) l[i + 2] = Pretty(c[i].name);
                return l;
            }
        }

        public static string Choice
        {
            get
            {
                string s = PlayerPrefs.GetString(ChoiceKey, Shuffle);
                if (s == Shuffle || s == Off) return s;
                foreach (var c in Clips) if (c.name == s) return s;
                return Shuffle; // that song was removed
            }
        }

        public static int ChoiceIndex
        {
            get
            {
                string s = Choice;
                if (s == Shuffle) return 0;
                if (s == Off) return 1;
                var c = Clips;
                for (int i = 0; i < c.Length; i++) if (c[i].name == s) return i + 2;
                return 0;
            }
            set
            {
                var c = Clips;
                int v = Mathf.Clamp(value, 0, c.Length + 1);
                string s = v == 0 ? Shuffle : v == 1 ? Off : c[v - 2].name;
                if (s == PlayerPrefs.GetString(ChoiceKey, Shuffle)) return;
                PlayerPrefs.SetString(ChoiceKey, s);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>The song to repeat, or null for shuffle / off.</summary>
        public static AudioClip RepeatClip
        {
            get { string s = Choice; foreach (var c in Clips) if (c.name == s) return c; return null; }
        }
    }
}
