using System.Collections.Generic;
using System.IO;
using ShiftRush.Core;
using UnityEditor;
using UnityEngine;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// SHIFT RUSH → Story → Build Story Catalog. Act 1's twelve episodes (Resources/SR_Story). Episodes whose event
    /// isn't in the game yet are marked as still filming. Rerun to reset the text; progress is in PlayerPrefs.
    /// </summary>
    public static class StoryCatalogBuilder
    {
        const string AssetPath = "Assets/_ShiftRush/Resources/SR_Story.asset";
        const string LiftChopperPath = "Assets/ARCADE - Ultimate Vehicles Pack/Prefabs (Meshes Only)/Air/Aglaea.prefab";

        [MenuItem("SHIFT RUSH/Story/Build Story Catalog")]
        public static StoryCatalog Build()
        {
            var cat = AssetDatabase.LoadAssetAtPath<StoryCatalog>(AssetPath);
            bool fresh = cat == null;
            if (fresh) cat = ScriptableObject.CreateInstance<StoryCatalog>();
            cat.actTitle = "ACT 1 · THE LEAGUE";
            cat.chapters = new[] { "THE DROP", "GOING LIVE", "HEAT" };
            cat.episodes = new List<StoryCatalog.Episode>
            {
                Ep("ep01", 1, 0, "FIRST DROP", "RACE", "alpine", 5, 600,
                    "The crew's plane drops you over Alpine Pass on night one. Land it, find the line, finish the run.",
                    "Night one. Nobody expects anything. Finish the lap and they'll remember the name.",
                    "Unregistered aircraft over the pass again.", "Hold one long drift", "Land one clean knock", "", "", 1f,
                    "Hex wants to see what you do with a car you didn't build."),
                Ep("ep02", 2, 0, "SHOP TEST", "RACE", "valley", 4, 800,
                    "Hex wants to see what you do with a car you didn't build. Glacier Valley, the whole crew on the pit wall.",
                    "Hex is watching from the wall. Don't scratch her paint. Actually, do. Chat loves it.",
                    "Same black car on three feeds.", "Two drifts past tier 1", "Knock a rival into the snow", "", "", 1f,
                    "Slim puts the rookie on the stream. Vex is waiting."),
                Ep("ep03", 3, 0, "ON AIR", "RACE", "alpine", 3, 1200,
                    "Slim puts the rookie on the stream for the first time. Alpine Pass, six cars, the league watching.",
                    "Forty thousand people are about to watch a test driver race the league champ. No pressure, rookie. Actually, all of the pressure.",
                    "Who is driving that car?", "Hold two tier 2 drifts", "Knock Vex twice", "VEX", "Champion. Grips, never drifts.", 1.04f,
                    "A second rookie shows up on Glacier Valley. Nobody invited him."),
                Ep("ep04", 4, 0, "KAITO", "DUEL", "valley", 2, 1500,
                    "A second rookie shows up on Glacier Valley. Nobody invited him, and he drives like he has nothing to lose.",
                    "Kaito says he's the real rookie. Chat is split. Settle it.",
                    "Second driver. Same plane. Who's paying them?", "Out-drift Kaito", "Put Kaito in a wall", "KAITO", "Cyan hoodie. Drives everything sideways.", 1.06f,
                    "Highland roads, real ones. The feed goes public."),
                Ep("ep05", 5, 1, "OPEN ROAD", "RACE", "stormbreak", 3, 1600,
                    "Stormbreak Highlands, a real road with a real view. The Feed wants speed and a little chaos.",
                    "We're on a public road now. Smile for the traffic cameras. Kidding. Don't.",
                    "Feed is public now. Clips everywhere.", "Clean lap, no knocks", "Five knocks in one race", "", "", 1f,
                    "Faction night: the Feed picks the event."),
                Ep("ep06", 6, 1, "FACTION NIGHT", "VOTE", "alpine", 3, 1800,
                    "The Feed votes on tonight's event: a drift showcase for GLOSS or a wreck run for GRIT.",
                    "Chat is choosing tonight. I'm just the guy with the camera.",
                    "Two fan camps. Both love the rookie.", "Win the drift showcase", "Win the wreck run", "", "", 1f, ""),
                Ep("ep07", 7, 1, "PORTAL RUN", "PORTAL", "alpine", 3, 2000,
                    "SHIFT through the portals into the challenge worlds and come back with the lead.",
                    "Hex calls it spell casting. I call it great television.",
                    "Cars vanish off the road and come back ahead.", "Clear a world without a scratch", "Wreck your way through", "", "", 1f, ""),
                Ep("ep08", 8, 1, "CALLED OUT", "RACE", "alpine", 1, 2500,
                    "Vex calls the rookie out on stream. Back to Alpine Pass, and this time only first place counts.",
                    "Vex called you out live. You know what happens now.",
                    "Vex left Grip Pro. Why?", "Win it clean", "Knock Vex three times", "VEX", "Wants the crew back. Wants you gone.", 1.08f,
                    "The police find the airstrip."),
                Ep("ep09", 9, 2, "INTERCEPT", "INTERCEPT", "stormbreak", 3, 2800, "", "", "", "Shake units with drifts", "Spin out the police", "", "", 1f, "", false, true),
                Ep("ep10", 10, 2, "THE RAID", "RACE", "valley", 3, 3000, "", "", "", "", "", "", "", 1f, "", false, true),
                Ep("ep11", 11, 2, "SIGNAL LOST", "RACE", "stormbreak", 3, 3200, "", "", "", "", "", "", "", 1f, "", false, true),
                Ep("ep12", 12, 2, "THE CATCH", "FINALE", "alpine", 1, 5000,
                    "The finale: beat Vex, outrun Kowal's roadblock and make the plane's catch.",
                    "", "", "Win it clean", "Win it loud", "VEX", "", 1.1f, "", false, false),
            };
            if (cat.liftChopper == null) cat.liftChopper = AssetDatabase.LoadAssetAtPath<GameObject>(LiftChopperPath);
            if (fresh) { Directory.CreateDirectory(Path.GetDirectoryName(AssetPath)); AssetDatabase.CreateAsset(cat, AssetPath); }
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            return cat;
        }

        static StoryCatalog.Episode Ep(string id, int n, int chapter, string title, string kind, string track, int within, int pays,
            string synopsis, string slim, string kowal, string gloss, string grit, string rival, string rivalLine, float rivalPace, string teaser,
            bool playable = true, bool redacted = false)
        {
            // Only plain races are in the game so far: the other event types are still being filmed.
            bool filmed = playable && (kind == "RACE" || kind == "DUEL");
            return new StoryCatalog.Episode
            {
                id = id, number = n, chapter = chapter, title = title, eventType = kind, trackId = track, finishWithin = within, payout = pays,
                synopsis = synopsis, slimLine = slim, kowalNote = kowal, glossBonus = gloss, gritBonus = grit,
                rival = rival, rivalLine = rivalLine, rivalPace = rivalPace, teaser = teaser, playable = filmed, redacted = redacted,
            };
        }
    }
}
