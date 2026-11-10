using UnityEngine;

namespace ShiftRush.Core
{
    /// <summary>
    /// Applies the saved graphics quality level at startup (any scene) and when the Settings selector changes.
    /// The selector saves its index under Heat's "HorizontalSelector_SR_Quality" key; index = Quality level.
    /// </summary>
    public class QualityBoot : MonoBehaviour
    {
        public const string Key = "SR_Quality";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (!PlayerPrefs.HasKey("HorizontalSelector_" + Key)) return;
            Apply(PlayerPrefs.GetInt("HorizontalSelector_" + Key));
        }

        /// <summary>Hooked to the Settings selector's value changed event.</summary>
        public void SetLevel(int level) { Apply(level); }

        static void Apply(int level)
        {
            level = Mathf.Clamp(level, 0, QualitySettings.names.Length - 1);
            if (QualitySettings.GetQualityLevel() != level) QualitySettings.SetQualityLevel(level, true);
        }
    }
}
