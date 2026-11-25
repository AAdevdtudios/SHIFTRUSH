using UnityEngine;

namespace ShiftRush.Core
{
    /// <summary>
    /// Applies the Settings → Visuals choices (window mode, frame rate, VSync, texture quality, filtering) from the
    /// values Heat's widgets save, so they take effect when the game starts, not only once the Visuals tab has been
    /// opened, and survive a Graphics Quality change (switching quality level resets VSync, textures and filtering).
    /// Also fixes VSync: Heat's handler syncs every second refresh (30 fps on a 60 Hz screen); this syncs every one.
    /// </summary>
    public static class DisplaySettings
    {
        // Heat save keys and option orders, as set up in the menu (Settings → Visuals).
        const string WindowModeKey = "HorizontalSelector_WindowMode";          // Borderless, Fullscreen, Windowed
        const string FrameRateKey = "HorizontalSelector_FrameRate";            // Unlimited, 30, 60, 144, 240
        const string TextureKey = "HorizontalSelector_TextureQuality";         // Low, Medium, High, Ultra
        const string AnisoKey = "HorizontalSelector_AnisotropicFiltering";     // Low, Medium, High
        const string VSyncKey = "Switch_VSync";                                // "true" / "false"

        static readonly int[] FrameRates = { -1, 30, 60, 144, 240 };

        public static void Apply()
        {
            QualitySettings.vSyncCount = PlayerPrefs.GetString(VSyncKey, "false") == "true" ? 1 : 0;

            int fr = Mathf.Clamp(PlayerPrefs.GetInt(FrameRateKey, 0), 0, FrameRates.Length - 1);
            Application.targetFrameRate = FrameRates[fr];

            int tex = Mathf.Clamp(PlayerPrefs.GetInt(TextureKey, 3), 0, 3);
            QualitySettings.globalTextureMipmapLimit = 3 - tex; // Ultra = full resolution

            int an = Mathf.Clamp(PlayerPrefs.GetInt(AnisoKey, 2), 0, 2);
            QualitySettings.anisotropicFiltering = an == 0 ? AnisotropicFiltering.Disable
                : an == 1 ? AnisotropicFiltering.Enable : AnisotropicFiltering.ForceEnable;

#if !UNITY_EDITOR
            FullScreenMode mode;
            switch (Mathf.Clamp(PlayerPrefs.GetInt(WindowModeKey, 0), 0, 2))
            {
                case 1: mode = Application.platform == RuntimePlatform.WindowsPlayer ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.FullScreenWindow; break;
                case 2: mode = FullScreenMode.Windowed; break;
                default: mode = FullScreenMode.FullScreenWindow; break;
            }
            if (Screen.fullScreenMode != mode) Screen.fullScreenMode = mode;
            CapRenderHeight(mode == FullScreenMode.Windowed ? Screen.height : Screen.currentResolution.height);
#endif
        }

        /// <summary>
        /// The look is mostly full-screen passes (ambient occlusion, outlines, post), so cost grows with pixel count.
        /// On screens taller than this, render at this height and let FSR sharpen it up to full size: a 1440p screen
        /// draws about 30% fewer pixels and looks nearly the same. 1080p and below render at full resolution.
        /// Build only: changing it in the editor would save into the pipeline asset.
        /// </summary>
        const int MaxRenderHeight = 1200;

        static void CapRenderHeight(int screenHeight)
        {
            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (urp == null || screenHeight <= 0) return;
            float scale = Mathf.Clamp(MaxRenderHeight / (float)screenHeight, 0.5f, 1f);
            urp.renderScale = scale;
            urp.upscalingFilter = scale < 1f ? UnityEngine.Rendering.Universal.UpscalingFilterSelection.FSR : UnityEngine.Rendering.Universal.UpscalingFilterSelection.Auto;
        }
    }
}
