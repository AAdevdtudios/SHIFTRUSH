using Michsky.UI.Heat;
using ShiftRush.Core;
using UnityEngine;

namespace ShiftRush.UI
{
    /// <summary>
    /// Settings → Audio → Music as a drop-down list: SHUFFLE ALL, OFF, then every song in Resources/Audio/Music.
    /// The list is filled when the menu opens, so songs dropped into that folder show up without rebuilding the
    /// menu, and a long list scrolls instead of crowding the row. Runs before the Heat dropdown builds itself;
    /// a pick goes straight to <see cref="MusicLibrary"/>.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [RequireComponent(typeof(Dropdown))]
    public class MusicChoiceDropdown : MonoBehaviour
    {
        Dropdown dropdown;

        void Awake()
        {
            dropdown = GetComponent<Dropdown>();
            dropdown.items.Clear();
            foreach (var label in MusicLibrary.Labels) dropdown.CreateNewItem(label, false);
            dropdown.saveSelected = false;
            dropdown.selectedItemIndex = Mathf.Clamp(MusicLibrary.ChoiceIndex, 0, dropdown.items.Count - 1);
            dropdown.onValueChanged.AddListener(i => MusicLibrary.ChoiceIndex = i);
            MusicLibrary.Changed += Sync;
        }

        // Open only as tall as the songs need, up to a cap; longer lists scroll.
        const float MaxListHeight = 300f;

        void Start()
        {
            if (dropdown.items.Count == 0 || dropdown.items[0].itemButton == null) return;
            float itemHeight = ((RectTransform)dropdown.items[0].itemButton.transform).sizeDelta.y;
            if (itemHeight <= 0f) return;
            int n = dropdown.items.Count;
            float need = dropdown.itemPaddingTop + dropdown.itemPaddingBottom + n * itemHeight + (n - 1) * dropdown.itemSpacing + 2f;
            dropdown.panelSize = Mathf.Min(MaxListHeight, need);
        }

        void OnDestroy() { MusicLibrary.Changed -= Sync; }

        void OnEnable() { Sync(); }

        /// <summary>Show the current choice (it can change from the pause menu too).</summary>
        void Sync()
        {
            if (dropdown == null || dropdown.items.Count == 0) return;
            int i = Mathf.Clamp(MusicLibrary.ChoiceIndex, 0, dropdown.items.Count - 1);
            if (dropdown.items[i].itemButton == null) { dropdown.selectedItemIndex = i; return; } // not built yet
            if (dropdown.selectedItemIndex != i) dropdown.SetDropdownIndex(i);
        }
    }
}
