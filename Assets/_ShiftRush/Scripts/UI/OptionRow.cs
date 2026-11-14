using ShiftRush.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShiftRush.UI
{
    /// <summary>
    /// One setting in the in-race options list. Writes the same PlayerPrefs keys as the main menu's Heat
    /// widgets, so a change here shows up in Settings and vice versa.
    /// </summary>
    public class OptionRow : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public enum Kind { Choice, Toggle, Volume }

        [SerializeField] Kind kind;
        [Tooltip("Heat save key, e.g. SR_SpeedFX, SR_CameraShake, MasterVolume.")]
        [SerializeField] string key;
        [SerializeField] string[] choices = new string[0];
        [Tooltip("Choice row listing the songs in Resources/Audio/Music (the Music setting).")]
        [SerializeField] bool musicChoice;
        [SerializeField] int defaultIndex;
        [SerializeField] bool defaultOn = true;
        [Tooltip("Volume rows: mixer channel (Master, Music, SFX, UI).")]
        [SerializeField] string channel;
        [SerializeField] TMP_Text label;
        [SerializeField] TMP_Text value;
        [SerializeField] Image highlight;
        [SerializeField] Image[] segments = new Image[0];

        [HideInInspector] public OptionsList list;
        [HideInInspector] public int rowIndex;

        static readonly Color Magenta = new Color(1f, 0.25f, 0.85f);
        static readonly Color Empty = new Color(1f, 1f, 1f, 0.12f);
        bool selected;
        float glow;

        void OnEnable() { Refresh(); }

        public void SetSelected(bool on) { selected = on; }

        /// <summary>Left/Right (or click, which steps right and wraps).</summary>
        public void Step(int dir)
        {
            switch (kind)
            {
                case Kind.Choice when musicChoice:
                    MusicLibrary.ChoiceIndex = Mathf.Clamp(MusicLibrary.ChoiceIndex + dir, 0, MusicLibrary.Labels.Length - 1);
                    break;
                case Kind.Choice:
                {
                    int i = PlayerPrefs.GetInt("HorizontalSelector_" + key, defaultIndex);
                    i = Mathf.Clamp(i + dir, 0, choices.Length - 1);
                    PlayerPrefs.SetInt("HorizontalSelector_" + key, i);
                    break;
                }
                case Kind.Toggle:
                    PlayerPrefs.SetString("Switch_" + key, IsOn ? "false" : "true");
                    break;
                case Kind.Volume:
                {
                    float v = Mathf.Clamp(Mathf.Round(Volume * 10f) / 10f + dir * 0.1f, 0f, 1f);
                    PlayerPrefs.SetFloat("Slider_" + key, v);
                    AudioBoot.SetVolume(channel, v);
                    break;
                }
            }
            MenuSounds.Tick();
            Refresh();
        }

        bool IsOn
        {
            get
            {
                string s = PlayerPrefs.GetString("Switch_" + key, "");
                return s == "" ? defaultOn : s == "true";
            }
        }

        float Volume { get { return PlayerPrefs.GetFloat("Slider_" + key, 1f); } }

        public void Refresh()
        {
            if (value == null) return;
            switch (kind)
            {
                case Kind.Choice when musicChoice:
                    value.text = "<  " + MusicLibrary.Labels[MusicLibrary.ChoiceIndex] + "  >";
                    break;
                case Kind.Choice:
                    int i = Mathf.Clamp(PlayerPrefs.GetInt("HorizontalSelector_" + key, defaultIndex), 0, Mathf.Max(0, choices.Length - 1));
                    value.text = "<  " + (choices.Length > 0 ? choices[i].ToUpperInvariant() : "") + "  >";
                    break;
                case Kind.Toggle:
                    value.text = IsOn ? "<  ON  >" : "<  OFF  >";
                    break;
                case Kind.Volume:
                    value.text = Mathf.RoundToInt(Volume * 100f) + "%";
                    int lit = Mathf.RoundToInt(Volume * segments.Length);
                    for (int s = 0; s < segments.Length; s++) if (segments[s]) segments[s].color = s < lit ? Magenta : Empty;
                    break;
            }
        }

        void Update()
        {
            glow = Mathf.Lerp(glow, selected ? 1f : 0f, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 16f));
            if (highlight) highlight.color = new Color(Magenta.r, Magenta.g, Magenta.b, 0.85f * glow);
            if (label) label.color = Color.Lerp(new Color(1, 1, 1, 0.55f), Color.white, glow);
        }

        public void OnPointerEnter(PointerEventData eventData) { if (list) list.Select(rowIndex, true); }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (list) list.Select(rowIndex, false);
            Step(eventData.button == PointerEventData.InputButton.Right ? -1 : 1);
        }
    }
}
