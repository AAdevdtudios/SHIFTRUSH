using UnityEngine;

namespace ShiftRush.Core
{
    /// <summary>
    /// The three race levels: CRUISE (gentle), RUSH (the race as designed, default) and OVERDRIVE (no mercy).
    /// Chosen on the track select screen or in Settings → Gameplay (one saved value, Heat selector style).
    /// A first race with the tutorial on always runs on CRUISE.
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
            /// <summary>Nitro rivals gain per second on top of SHIFT ramps (share of a tank).</summary>
            public float rivalNitro;
            /// <summary>Your start: 0 = pole, 5 = last of six.</summary>
            public int gridSlot;
            /// <summary>The SHIFT timing window, × this.</summary>
            public float shiftWindow;
            /// <summary>Where a cleared SHIFT world drops you: a random place from warpBest to warpWorst.</summary>
            public int warpBest, warpWorst;
            /// <summary>Slipstream, close call and wall skim nitro, × this.</summary>
            public float bonusNitro;
            public string blurb;
        }

        static readonly Tuning[] Levels =
        {
            new Tuning { pace = 0.9f, push = 0.04f, ease = 0.14f, carBoost = 1f, rivalNitro = 0f, gridSlot = 2, warpBest = 1, warpWorst = 2, shiftWindow = 1.3f, bonusNitro = 1.25f,
                blurb = "Gentler rivals, a wider SHIFT window, more nitro. You start 3rd." },
            new Tuning { pace = 1f, push = 0.06f, ease = 0.06f, carBoost = 1f, rivalNitro = 0f, gridSlot = 4, warpBest = 1, warpWorst = 3, shiftWindow = 1f, bonusNitro = 1f,
                blurb = "The race as it's meant to be. You start 5th." },
            new Tuning { pace = 1.12f, push = 0.15f, ease = 0f, carBoost = 1.12f, rivalNitro = 0.025f, gridSlot = 5, warpBest = 2, warpWorst = 4, shiftWindow = 0.8f, bonusNitro = 0.8f,
                blurb = "Faster rivals who never wait, a tight SHIFT window, a SHIFT lands you P2-P4. You start last." },
        };

        /// <summary>The level the player picked.</summary>
        public static int Chosen
        {
            get { return Mathf.Clamp(PlayerPrefs.GetInt("HorizontalSelector_" + Key, Default), 0, Levels.Length - 1); }
            set { PlayerPrefs.SetInt("HorizontalSelector_" + Key, Mathf.Clamp(value, 0, Levels.Length - 1)); }
        }

        /// <summary>The level this race runs on: CRUISE for a tutorial first race, else the pick.</summary>
        public static int Level { get { return GameSettings.Tutorial ? 0 : Chosen; } }
        public static Tuning Current { get { return Levels[Level]; } }
        public static string Label { get { return Labels[Level]; } }
        public static Tuning Of(int level) { return Levels[Mathf.Clamp(level, 0, Levels.Length - 1)]; }
    }
}
