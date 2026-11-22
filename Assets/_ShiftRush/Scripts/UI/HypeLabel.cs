using TMPro;
using UnityEngine;

namespace ShiftRush.UI
{
    /// <summary>Shows the player's Hype ("4,250 HYPE") in the text under it, refreshed whenever it appears.</summary>
    public class HypeLabel : MonoBehaviour
    {
        public const string Key = "GripPro.Hype";
        TMP_Text text;

        void OnEnable()
        {
            if (text == null) text = GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.text = PlayerPrefs.GetInt(Key, 0).ToString("N0") + " HYPE";
        }
    }
}
