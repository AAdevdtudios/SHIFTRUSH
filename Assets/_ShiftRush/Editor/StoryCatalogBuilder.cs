using System.Collections.Generic;
using System.IO;
using ShiftRush.Core;
using UnityEditor;
using UnityEngine;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// SHIFT RUSH → Story → Build Story Catalog. The cast (Resources/SR_Cast) and Act 1's twelve episodes
    /// (Resources/SR_Story). Every episode is its own kind of night: a league night against the board, a beef settled
    /// one on one, a crew battle, a test drive, a wreck run with the police, an endurance run, the final. Each names
    /// who you race, in which cars, why, how hard it is, and the talk before Go Live. Rerun to reset the text;
    /// progress is in PlayerPrefs.
    /// </summary>
    public static class StoryCatalogBuilder
    {
        const string AssetPath = "Assets/_ShiftRush/Resources/SR_Story.asset";
        const string CastPath = "Assets/_ShiftRush/Resources/SR_Cast.asset";
        const string LiftChopperPath = "Assets/ARCADE - Ultimate Vehicles Pack/Prefabs (Meshes Only)/Air/Aglaea.prefab";

        // ------------------------------------------------------------------ the cast

        static RivalCast BuildCast()
        {
            var cast = AssetDatabase.LoadAssetAtPath<RivalCast>(CastPath);
            bool fresh = cast == null;
            if (fresh) cast = ScriptableObject.CreateInstance<RivalCast>();
            cast.characters = new List<RivalCast.Character>
            {
                Who("kaito", "KAITO", "tetsuo", new Color(0.18f, 0.95f, 1f), "Drifts everything", 1.12f, 235f, 0.5f, 0.95f, 0.6f,
                    "The other rookie. Cyan hoodie, no fear, no plan. Says the rookie spot was his first."),
                Who("hex", "HEX", "fireburst", new Color(1f, 0.55f, 0.1f), "Clean and technical", 1.3f, 230f, 0.15f, 0.4f, 0.2f,
                    "Grip Pro's builder. Half the cars on the grid went through her shop. Races to see who deserves them."),
                Who("vex", "VEX", "blackout", new Color(0.95f, 0.15f, 0.25f), "Grips, never drifts", 1.4f, 245f, 0.45f, 0.1f, 0.3f,
                    "League champion. Left the Grip Pro crew and wants it back, without you in it."),
                Who("diesel", "DIESEL", "phobos", new Color(1f, 0.85f, 0.1f), "Rams. Paid to.", 0.9f, 215f, 0.85f, 0.2f, 0.5f, // the Phobos weighs 2.4 t: slower into corners
                    "Heavy car, heavy hands. Takes money to put people in walls and doesn't ask who's paying."),
                Who("nova", "NOVA", "predator", new Color(0.65f, 0.35f, 1f), "Fastest car, makes mistakes", 1.2f, 262f, 0.3f, 0.55f, 0.4f,
                    "Top of the Channel 13 board. Fastest thing in the league, and she knows it."),
                Who("mamba", "MAMBA", "zephyr", new Color(0.3f, 0.95f, 0.4f), "Blocks, dirty lines", 1.22f, 232f, 0.8f, 0.45f, 0.7f,
                    "Kaito's crew. Does the dirty work: blocks the inside, shuts doors, never says sorry."),
            };
            if (fresh) AssetDatabase.CreateAsset(cast, CastPath);
            EditorUtility.SetDirty(cast);
            return cast;
        }

        static RivalCast.Character Who(string id, string name, string car, Color col, string style, float grip, float top, float aggro, float drift, float shortcut, string bio)
        {
            return new RivalCast.Character { id = id, name = name, carId = car, color = col, style = style, grip = grip, topSpeedKmh = top, aggression = aggro, driftSkill = drift, shortcutChance = shortcut, bio = bio };
        }

        // ------------------------------------------------------------------ the episodes

        [MenuItem("SHIFT RUSH/Story/Build Story Catalog")]
        public static StoryCatalog Build()
        {
            BuildCast();
            var cat = AssetDatabase.LoadAssetAtPath<StoryCatalog>(AssetPath);
            bool fresh = cat == null;
            if (fresh) cat = ScriptableObject.CreateInstance<StoryCatalog>();
            cat.actTitle = "ACT 1 · THE LEAGUE";
            cat.chapters = new[] { "THE DROP", "GOING LIVE", "HEAT" };
            cat.episodes = new List<StoryCatalog.Episode>
            {
                // ---- Chapter 1: THE DROP
                Ep("ep01", 1, 0, "FIRST DROP", "LEAGUE NIGHT", "alpine", 5, 600,
                    "Night one on Alpine Pass. Five names already on the Channel 13 board, and you're the sixth.",
                    "Night one. Nobody expects anything. Finish the lap and they'll remember the name.",
                    "Unregistered aircraft over the pass again.", "Hold one long drift", "Land one clean knock",
                    new[] { "kaito", "hex", "mamba", "diesel", "nova" }, 0.85f, 0.92f,
                    "The whole board. Finish anywhere and you're on it.",
                    new[] { "SLIM|Night one. Five drivers already on the board. You're the sixth name.", "DEE|Nobody expects anything from you. Use that.", "YOU|Then I'll give them something to expect." },
                    "Hex wants to see what you do with a car you didn't build."),
                Ep("ep02", 2, 0, "SHOP TEST", "TEST DRIVE", "valley", 1, 800,
                    "Hex built half the car you're driving. Glacier Valley, one on one: beat her and you keep the keys.",
                    "Hex is watching from the wall. Don't scratch her paint. Actually, do. Chat loves it.",
                    "Same black car on three feeds.", "Two drifts past tier 1", "Knock Hex into the snow",
                    new[] { "hex" }, 0.9f, 0.95f,
                    "Builds the cars. Wants to see if you deserve one.",
                    new[] { "HEX|That car you're in? Half of it came out of my shop.", "HEX|Finish ahead of me and it's yours to keep driving.", "YOU|And if I'm behind?", "HEX|Then it comes back to the shop. With you walking." },
                    "Slim puts the rookie on the stream. Vex is waiting."),
                Ep("ep03", 3, 0, "ON AIR", "LEAGUE NIGHT", "alpine", 3, 1200,
                    "Slim puts you on the stream for the first time. Alpine Pass, the league champion on the grid.",
                    "Forty thousand people are about to watch a test driver race the league champ. No pressure, rookie.",
                    "Who is driving that car?", "Hold two tier 2 drifts", "Knock Vex twice",
                    new[] { "vex", "kaito", "hex", "mamba", "nova" }, 0.95f, 1f,
                    "Vex leads the board. Grips, never drifts.",
                    new[] { "SLIM|Forty thousand watching. Vex is on the grid tonight.", "VEX|Rookie. Stay out of my mirrors.", "YOU|Check them anyway." },
                    "A second rookie shows up on Glacier Valley. Nobody invited him."),
                Ep("ep04", 4, 0, "KAITO", "BEEF", "valley", 1, 1500,
                    "A second rookie says the spot is his. Glacier Valley, one on one, loser stops calling themselves the rookie.",
                    "Kaito says he's the real rookie. Chat is split. Settle it.",
                    "Second driver. Same plane. Who's paying them?", "Out-drift Kaito", "Put Kaito in a wall",
                    new[] { "kaito" }, 1f, 1.02f,
                    "Says he's the real rookie. Chat is split.",
                    new[] { "KAITO|Two rookies is one too many.", "KAITO|Glacier Valley. Loser stops calling themselves the rookie.", "YOU|Bring a coat. It's a long walk home from second." },
                    "Kaito brought friends to the city."),

                // ---- Chapter 2: GOING LIVE
                Ep("ep05", 5, 1, "OPEN ROAD", "CREW BATTLE", "downtown", 1, 1600,
                    "Kaito didn't take losing well. Downtown Neon, him and his crew against you, and the police are on the radio.",
                    "We're on a public road now. Smile for the traffic cameras. Kidding. Don't.",
                    "Feed is public now. Clips everywhere.", "Clean lap, no knocks", "Five knocks in one race",
                    new[] { "kaito", "mamba", "diesel" }, 1f, 1.03f,
                    "Kaito brought his crew. Mamba blocks, Diesel rams.",
                    new[] { "MAMBA|Kaito sent us.", "DIESEL|Nothing personal. Somebody paid for your bumper.", "DEE|Three of them, one of you, and the cops are already listening. Go." },
                    "Faction night: the Feed picks sides.", police: true),
                Ep("ep06", 6, 1, "FACTION NIGHT", "SHOWCASE", "stormbreak", 3, 1800,
                    "The Feed split in two: GLOSS wants drifts, GRIT wants wrecks. Stormbreak Highlands, and Nova tops the board.",
                    "Chat is choosing tonight. I'm just the guy with the camera.",
                    "Two fan camps. Both love the rookie.", "Drift the switchbacks", "Wreck the crates",
                    new[] { "nova", "hex", "kaito", "mamba", "diesel" }, 1.03f, 1.03f,
                    "Nova is top of the board. Thinks you're beneath her.",
                    new[] { "SLIM|GLOSS wants drifts. GRIT wants wrecks. Give them both.", "NOVA|Top of the board, rookie. Look up.", "YOU|Looking. Coming." },
                    "Nova wants it one on one."),
                Ep("ep07", 7, 1, "TOP OF THE BOARD", "SPRINT", "valley", 1, 2000,
                    "Nova calls you out for the top spot. Glacier Valley, one on one, against the fastest car in the league.",
                    "Fastest car in the league against the rookie. I'd bet on the car. Prove me wrong.",
                    "Two cars, one spot.", "Out-drift Nova", "Make Nova spin",
                    new[] { "nova" }, 1.05f, 1.04f,
                    "Fastest car in the league. Makes mistakes when pushed.",
                    new[] { "NOVA|Fastest car in the league. You've got a turn signal and hope.", "YOU|I've got the line. You've got to brake sometime.", "NOVA|We'll see who brakes first." },
                    "Vex calls you out live."),
                Ep("ep08", 8, 1, "CALLED OUT", "BEEF", "alpine", 1, 2500,
                    "Vex calls you out on stream. Back to Alpine Pass, one on one, and the winner keeps the crew.",
                    "Vex called you out live. You know what happens now.",
                    "Vex left Grip Pro. Why?", "Win it clean", "Knock Vex three times",
                    new[] { "vex" }, 1.07f, 1.06f,
                    "Wants the crew back. Wants you gone.",
                    new[] { "VEX|I put Grip Pro on the map before you had a licence.", "VEX|Alpine Pass. Winner keeps the crew.", "DEE|She's not bluffing.", "YOU|Neither am I." },
                    "The police find the highland road."),

                // ---- Chapter 3: HEAT
                Ep("ep09", 9, 2, "INTERCEPT", "HEAT", "stormbreak", 2, 2800,
                    "The police found the highland route. Nova and Kaito on the grid, units on your tail the whole way.",
                    "They're on the highland road. Don't stop for anything.",
                    "Units deployed to Stormbreak.", "Shake units with drifts", "Spin out the police",
                    new[] { "nova", "kaito" }, 1.07f, 1.05f,
                    "Nova and Kaito, and the police are on the highland road.",
                    new[] { "SLIM|Units on the highland road. Don't stop for anything.", "KAITO|Truce till the cops are gone?", "NOVA|No truce. Race." },
                    "Diesel's back. So are the police.", police: true),
                Ep("ep10", 10, 2, "THE RAID", "WRECK RUN", "downtown", 2, 3000,
                    "The police raid the harbour mid race. Diesel's been paid again, and he brought company.",
                    "They found the warehouse. Doesn't matter. Drive.",
                    "Raid on the harbour. Units on every corner.", "Shake them with a drift", "Knock a cop car",
                    new[] { "diesel", "mamba", "hex" }, 1.09f, 1.06f,
                    "Diesel's paid to wreck you. The harbour's crawling with cops.",
                    new[] { "DIESEL|Same job as last time. Better pay.", "HEX|I'm only here to see my car survive.", "SLIM|Raid on the harbour. Make it loud." },
                    "Out past the city, the signal drops.", police: true, cops: 3),
                Ep("ep11", 11, 2, "SIGNAL LOST", "ENDURANCE", "countyline", 2, 3200,
                    "Out past the city the stream drops to one bar. County Line, six kilometres a lap, everyone left on the board.",
                    "Signal's bad out here. If I go quiet, keep driving.",
                    "Lost the feed somewhere past the river.", "Drift the woods descent", "Knock a rival off the ridge",
                    new[] { "vex", "nova", "kaito", "hex", "mamba" }, 1.1f, 1.07f,
                    "Six kilometres a lap. Everyone left on the board.",
                    new[] { "SLIM|Signal's bad out here. If I go quiet, keep driving.", "VEX|Nowhere to hide on the ridge.", "YOU|Good. I want them to see." },
                    "Last one."),
                Ep("ep12", 12, 2, "THE CATCH", "FINAL", "alpine", 1, 5000,
                    "The final: Vex, one on one, on Alpine Pass with the police closing in. Only first place makes the catch.",
                    "Last one. Win it and the chopper's waiting.", "Roadblock planned on the pass.", "Win it clean", "Win it loud",
                    new[] { "vex" }, 1.15f, 1.1f,
                    "One last race. Winner takes the board.",
                    new[] { "SLIM|Last one. Win it and the chopper's waiting.", "VEX|Just you and me. And them.", "YOU|Then let's give them a show." },
                    "", police: true, cops: 3),
            };
            if (cat.liftChopper == null) cat.liftChopper = AssetDatabase.LoadAssetAtPath<GameObject>(LiftChopperPath);
            if (fresh) { Directory.CreateDirectory(Path.GetDirectoryName(AssetPath)); AssetDatabase.CreateAsset(cat, AssetPath); }
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            return cat;
        }

        static StoryCatalog.Episode Ep(string id, int n, int chapter, string title, string format, string track, int within, int pays,
            string synopsis, string slim, string kowal, string gloss, string grit, string[] opponents, float heat, float pace, string why,
            string[] talk, string teaser, bool police = false, int cops = 2)
        {
            var lines = new List<StoryCatalog.TalkLine>();
            foreach (var t in talk)
            {
                int bar = t.IndexOf('|');
                lines.Add(new StoryCatalog.TalkLine { speaker = bar > 0 ? Speaker(t.Substring(0, bar)) : "SLIM", text = bar > 0 ? t.Substring(bar + 1) : t });
            }
            // A one-on-one names the rival for the goal line ("Win it, beat KAITO").
            var cast = RivalCast.Instance;
            string rival = opponents.Length == 1 && cast != null && cast.Find(opponents[0]) != null ? cast.Find(opponents[0]).name : "";
            return new StoryCatalog.Episode
            {
                id = id, number = n, chapter = chapter, title = title, eventType = format, trackId = track, finishWithin = within, payout = pays,
                synopsis = synopsis, slimLine = slim, kowalNote = kowal, glossBonus = gloss, gritBonus = grit,
                rival = rival, rivalLine = why, rivalPace = pace, teaser = teaser, playable = true, redacted = false,
                police = police, policeCars = cops, opponents = opponents, heat = heat, talk = lines,
            };
        }

        /// <summary>Cast members are stored by id (lowercase), everyone else by their on-screen name.</summary>
        static string Speaker(string s)
        {
            string id = s.ToLowerInvariant();
            foreach (var c in new[] { "kaito", "hex", "vex", "diesel", "nova", "mamba" }) if (id == c) return id;
            return s.ToUpperInvariant();
        }
    }
}
