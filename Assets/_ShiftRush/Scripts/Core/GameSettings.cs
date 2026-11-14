using UnityEngine;

namespace ShiftRush.Core
{
    /// <summary>
    /// Player settings the game itself reads (as opposed to graphics and volume, which Heat UI applies).
    /// Values live in PlayerPrefs under the same keys Heat's settings widgets save to, so a Heat switch with
    /// save key "SR_CameraShake" and this class always agree, in the menu and in a race scene started directly.
    /// </summary>
    public static class GameSettings
    {
        // Heat prefixes: switches save "Switch_" + key as "true"/"false", selectors "HorizontalSelector_" + key as an index.
        public const string SpeedFxKey = "SR_SpeedFX";
        public const string CameraShakeKey = "SR_CameraShake";
        public const string ReducedMotionKey = "SR_ReducedMotion";
        public const string UnitsKey = "SR_Units";
        public const string ScrollSoundKey = "SR_ScrollSound";
        public const string MinimapKey = "SR_Minimap";
        public const string AutoAccelerateKey = "SR_AutoAccelerate";
        public const string TutorialKey = "SR_Tutorial";
        public const string ToonLookKey = "SR_ToonLook";

        /// <summary>Coach hints in the first races (see Race.RaceCoach). On until the player has done it all.</summary>
        public static bool Tutorial { get { return GetSwitch(TutorialKey, true); } }
        /// <summary>Toon shading on the cars and ink outlines over the scene.</summary>
        public static bool ToonLook { get { return GetSwitch(ToonLookKey, true); } }

        /// <summary>The car keeps the throttle down by itself; the player steers, brakes and fires nitro.</summary>
        public static bool AutoAccelerate { get { return GetSwitch(AutoAccelerateKey, false); } }

        /// <summary>Race minimap: a faded patch of track around the car, the whole circuit, or hidden.</summary>
        public static readonly string[] MinimapLabels = { "Local", "Full", "Off" };
        /// <summary>0 local (follows and turns with the car), 1 full circuit, 2 off.</summary>
        public static int Minimap { get { return PlayerPrefs.GetInt("HorizontalSelector_" + MinimapKey, 0); } }

        /// <summary>Sound when moving between menu items.</summary>
        public static readonly string[] ScrollSoundLabels = { "Soft", "Clunk", "Off" };
        /// <summary>0 soft tick, 1 mechanical clunk, 2 silent.</summary>
        public static int ScrollSound { get { return PlayerPrefs.GetInt("HorizontalSelector_" + ScrollSoundKey, 0); } }

        /// <summary>Labels for the speed-effects selector, matching <see cref="SpeedFxLevels"/>.</summary>
        public static readonly string[] SpeedFxLabels = { "Off", "Subtle", "Full", "Max" };
        static readonly float[] SpeedFxLevels = { 0f, 0.5f, 1f, 1.3f };
        const int DefaultSpeedFx = 2;

        /// <summary>Multiplier for camera pull-back, FOV kick, streaks and lens effects. 1 = as designed.</summary>
        public static float SpeedFxStrength
        {
            get
            {
                int i = PlayerPrefs.GetInt("HorizontalSelector_" + SpeedFxKey, DefaultSpeedFx);
                return SpeedFxLevels[Mathf.Clamp(i, 0, SpeedFxLevels.Length - 1)];
            }
        }

        public static bool CameraShake { get { return GetSwitch(CameraShakeKey, true); } }

        /// <summary>Turns off speed streaks, lens distortion and fringing, and calms the camera.</summary>
        public static bool ReducedMotion { get { return GetSwitch(ReducedMotionKey, false); } }

        public static bool UseMph { get { return PlayerPrefs.GetInt("HorizontalSelector_" + UnitsKey, 0) == 1; } }
        public static string SpeedUnit { get { return UseMph ? "MPH" : "KM/H"; } }
        public static float ToDisplaySpeed(float kmh) { return UseMph ? kmh * 0.621371f : kmh; }

        static bool GetSwitch(string key, bool fallback)
        {
            string v = PlayerPrefs.GetString("Switch_" + key, "");
            if (v == "true") return true;
            if (v == "false") return false;
            return fallback;
        }
    }
}
