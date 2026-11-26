using UnityEngine;

namespace ShiftRush.Core
{
    /// <summary>
    /// The three race levels: CRUISE (gentle), RUSH (the race as designed, default) and OVERDRIVE (no mercy).
    /// Chosen on the track select screen or in Settings → Gameplay (one saved value, Heat selector style).
    /// Only the very first race (tutorial on, never raced) runs on CRUISE; after that the pick always applies, hints
    /// on or off.
    /// </summary>
    public static class Difficulty
    {
        public const string Key = "SR_Difficulty";
        public const int Default = 1;
        public static readonly string[] Labels = { "CRUISE", "RUSH", "OVERDRIVE" };

        public struct Tuning
        {
            /// <summary>Rivals' top speed and cornering grip, × this.</summary>
            public float pace;
            /// <summary>Rubber band, rivals behind you: up to this much faster (0.15 = +15%) when far back.</summary>
            public float push;
            /// <summary>Rubber band, rivals ahead of you: up to this much slower when far ahead (0 = they never wait).</summary>
            public float ease;
            /// <summary>Rivals' cars: engine torque, top speed and tyre grip, × this (their real ability, not just intent).</summary>
            public float carBoost;
            /// <summary>Extra nitro rivals gain per second (share of a tank).</summary>
            public float rivalNitro;
            /// <summary>Your start: 0 = pole, 5 = last of six.</summary>
            public int gridSlot;
            /// <summary>The SHIFT timing window, × this.</summary>
            public float shiftWindow;
            /// <summary>Where a cleared SHIFT world drops you: a random place from warpBest to warpWorst.</summary>
            public int warpBest, warpWorst;
            /// <summary>Drift, slipstream, close call and wall skim nitro, × this.</summary>
            public float bonusNitro;
            /// <summary>Seconds for an empty tank to refill on its own.</summary>
            public float rechargeSeconds;
            public string blurb;
        }

        static readonly Tuning[] Levels =
        {
            new Tuning { pace = 0.9f, push = 0.04f, ease = 0.14f, carBoost = 1f, rivalNitro = 0f, gridSlot = 2, warpBest = 1, warpWorst = 2, shiftWindow = 1.3f, bonusNitro = 1.25f, rechargeSeconds = 32f,
                blurb = "Gentler rivals who wait for you, more nitro from drifts, a faster recharge. You start 3rd." },
            new Tuning { pace = 1f, push = 0.06f, ease = 0.06f, carBoost = 1f, rivalNitro = 0f, gridSlot = 4, warpBest = 1, warpWorst = 3, shiftWindow = 1f, bonusNitro = 1f, rechargeSeconds = 45f,
                blurb = "The race as it's meant to be. You start 5th." },
            new Tuning { pace = 1.12f, push = 0.15f, ease = 0f, carBoost = 1.12f, rivalNitro = 0.025f, gridSlot = 5, warpBest = 2, warpWorst = 4, shiftWindow = 0.8f, bonusNitro = 0.8f, rechargeSeconds = 60f,
                blurb = "Faster rivals who never wait and run nitro of their own, less nitro for you. You start last." },
        };

        /// <summary>The level the player picked.</summary>
        public static int Chosen
        {
            get { return Mathf.Clamp(PlayerPrefs.GetInt("HorizontalSelector_" + Key, Default), 0, Levels.Length - 1); }
            set { PlayerPrefs.SetInt("HorizontalSelector_" + Key, Mathf.Clamp(value, 0, Levels.Length - 1)); }
        }

        const string RacedKey = "SR_RacedOnce";

        /// <summary>True until the first race with the tutorial is done (that one runs on CRUISE).</summary>
        public static bool FirstRace
        {
            get
            {
                if (PlayerPrefs.HasKey(RacedKey)) return false;
                if (PlayerPrefs.GetString("Switch_" + GameSettings.TutorialKey, "") == "false") { MarkRaced(); return false; }
                return GameSettings.Tutorial;
            }
        }

        public static void MarkRaced() { PlayerPrefs.SetInt(RacedKey, 1); PlayerPrefs.Save(); }

        /// <summary>The level this race runs on: CRUISE for the very first race, else the pick.</summary>
        public static int Level { get { return FirstRace ? 0 : Chosen; } }
        public static Tuning Current { get { return Levels[Level]; } }
        public static string Label { get { return Labels[Level]; } }
        public static Tuning Of(int level) { return Levels[Mathf.Clamp(level, 0, Levels.Length - 1)]; }
    }
}
