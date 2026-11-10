using UnityEngine;
using UnityEngine.Audio;

namespace ShiftRush.Core
{
    /// <summary>
    /// Applies saved volumes to the game mixer when the game starts (from any scene) and routes every RCC Pro
    /// vehicle sound into its SFX group, so the menu's volume sliders also control engines, tyres and crashes.
    /// The mixer is Resources/SR_Mixer with exposed parameters Master, Music, SFX and UI (in dB). Heat's
    /// UIManagerAudio in the menu drives the same mixer and the same PlayerPrefs keys.
    /// </summary>
    public static class AudioBoot
    {
        public const string MixerResource = "SR_Mixer";
        static readonly string[] Channels = { "Master", "Music", "SFX", "UI" };
        static readonly string[] SaveKeys = { "MasterVolume", "MusicVolume", "SFXVolume", "UIVolume" };

        static AudioMixer mixer;

        public static AudioMixer Mixer
        {
            get
            {
                if (mixer == null) mixer = Resources.Load<AudioMixer>(MixerResource);
                return mixer;
            }
        }

        public static AudioMixerGroup Group(string name)
        {
            if (Mixer == null) return null;
            var found = Mixer.FindMatchingGroups(name);
            return found.Length > 0 ? found[0] : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Mixer == null)
            {
                Debug.LogWarning("[SHIFT//RUSH] Resources/" + MixerResource + " not found; volumes not applied.");
                return;
            }
            ApplySavedVolumes();

            var sfx = Group("SFX");
            if (sfx != null) RCCP.SetAudioOutputBus(sfx);
        }

        public static void ApplySavedVolumes()
        {
            for (int i = 0; i < Channels.Length; i++)
                SetVolume(Channels[i], PlayerPrefs.GetFloat("Slider_" + SaveKeys[i], 1f));
        }

        /// <summary>Sets a channel from a 0..1 slider value (logarithmic, so the slider feels even).</summary>
        public static void SetVolume(string channel, float linear01)
        {
            if (Mixer == null) return;
            Mixer.SetFloat(channel, Mathf.Log10(Mathf.Clamp(linear01, 0.0001f, 1f)) * 20f);
        }
    }
}
