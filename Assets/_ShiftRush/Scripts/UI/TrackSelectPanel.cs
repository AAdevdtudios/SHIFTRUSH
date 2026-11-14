using System.Collections.Generic;
using ShiftRush.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShiftRush.UI
{
    /// <summary>
    /// Track select: one card per catalog track, built from a template card. Left/Right (or hover) picks,
    /// Enter (or click) starts the race behind the loading screen.
    /// </summary>
    public class TrackSelectPanel : MonoBehaviour
    {
        [SerializeField] CanvasGroup visibleGroup;
        [SerializeField] RectTransform cardParent;
        [Tooltip("Inactive template: needs TrackCard on it.")]
        [SerializeField] TrackCard cardTemplate;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text description;
        [SerializeField] TMP_Text details;
        [Tooltip("Big soft-masked shot of the selected track behind the cards.")]
        [SerializeField] Image hero;
        [Header("Right column")]
        [Tooltip("Picture of the car picked in the garage.")]
        [SerializeField] Image carPicture;
        [SerializeField] TMP_Text carName;
        [Tooltip("The selected track's circuit as a grey line.")]
        [SerializeField] Image map;
        RaceCatalog.Car shownCar;
        [Header("Difficulty")]
        [SerializeField] TMP_Text difficultyValue;
        [SerializeField] TMP_Text difficultyBlurb;
        [Tooltip("One per level, lit up to the chosen one.")]
        [SerializeField] Image[] difficultyPips;
        [SerializeField] Button difficultyDown, difficultyUp;
        [SerializeField] Color pipOff = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] Color[] levelColours = { new Color32(56, 225, 255, 255), new Color32(255, 64, 217, 255), new Color32(255, 120, 40, 255) };
        float difficultyPunch;
        [Tooltip("Canvas behind the showroom car that holds the hero band; follows this panel's visibility.")]
        [SerializeField] CanvasGroup backdrop;
        float heroAlpha;

        readonly List<TrackCard> cards = new List<TrackCard>();
        int index;

        void Start()
        {
            var cat = RaceCatalog.Instance;
            if (cat == null || cardTemplate == null) return;
            for (int i = 0; i < cat.tracks.Count; i++)
            {
                var card = Instantiate(cardTemplate, cardParent);
                card.gameObject.SetActive(true);
                card.Bind(this, i, cat.tracks[i]);
                cards.Add(card);
            }
            index = RaceSession.TrackIndex;
            if (difficultyDown) difficultyDown.onClick.AddListener(() => StepDifficulty(-1));
            if (difficultyUp) difficultyUp.onClick.AddListener(() => StepDifficulty(1));
            Refresh();
            ShowDifficulty();
        }

        bool Accepting { get { return visibleGroup == null || (visibleGroup.alpha > 0.95f && visibleGroup.interactable); } }

        void OnDisable() { if (backdrop != null) backdrop.alpha = 0f; }

        void LateUpdate()
        {
            if (backdrop != null) backdrop.alpha = visibleGroup != null && visibleGroup.gameObject.activeInHierarchy ? visibleGroup.alpha : 0f;
            ShowCar();
            if (difficultyValue != null)
            {
                difficultyPunch = Mathf.MoveTowards(difficultyPunch, 0f, Time.unscaledDeltaTime * 4f);
                difficultyValue.transform.localScale = Vector3.one * (1f + 0.15f * difficultyPunch * difficultyPunch);
            }
            if (hero == null) return;
            heroAlpha = Mathf.MoveTowards(heroAlpha, 1f, Time.unscaledDeltaTime * 3f);
            var c = hero.color; c.a = heroAlpha; hero.color = c;
            hero.rectTransform.localScale = Vector3.one * (1.06f - 0.06f * heroAlpha);
        }

        void Update()
        {
            if (!Accepting || SceneLoader.IsLoading) return;
            if (MenuInput.Left) Select(index - 1, true);
            else if (MenuInput.Right) Select(index + 1, true);
            else if (MenuInput.Up) StepDifficulty(1);
            else if (MenuInput.Down) StepDifficulty(-1);
            else if (MenuInput.Submit) Go(index);
        }

        public void Select(int i, bool sound)
        {
            if (cards.Count == 0) return;
            i = (i + cards.Count) % cards.Count;
            if (i == index) return;
            index = i;
            RaceSession.TrackIndex = i;
            if (sound) MenuSounds.Shift();
            Refresh();
        }

        public void Go(int i)
        {
            if (!Accepting || SceneLoader.IsLoading) return;
            var cat = RaceCatalog.Instance;
            if (cat == null || i < 0 || i >= cat.tracks.Count) return;
            index = i;
            RaceSession.TrackIndex = i;
            MenuSounds.Confirm();
            SceneLoader.LoadTrack(cat.tracks[i]);
        }

        /// <summary>Up a level (+1) or down (-1), clamped: CRUISE, RUSH, OVERDRIVE.</summary>
        public void StepDifficulty(int dir)
        {
            int next = Mathf.Clamp(Difficulty.Chosen + dir, 0, Difficulty.Labels.Length - 1);
            if (next == Difficulty.Chosen) { MenuSounds.Tick(); return; }
            Difficulty.Chosen = next;
            MenuSounds.Shift();
            difficultyPunch = 1f;
            ShowDifficulty();
        }

        void ShowDifficulty()
        {
            int lv = Difficulty.Chosen;
            var col = levelColours != null && lv < levelColours.Length ? levelColours[lv] : Color.white;
            if (difficultyValue) { difficultyValue.text = Difficulty.Labels[lv]; difficultyValue.color = col; }
            if (difficultyBlurb)
                difficultyBlurb.text = GameSettings.Tutorial ? "Your first race runs on CRUISE, with the tutorial." : Difficulty.Of(lv).blurb;
            if (difficultyPips != null)
                for (int i = 0; i < difficultyPips.Length; i++) if (difficultyPips[i]) difficultyPips[i].color = i <= lv ? col : pipOff;
            if (difficultyDown) difficultyDown.interactable = lv > 0;
            if (difficultyUp) difficultyUp.interactable = lv < Difficulty.Labels.Length - 1;
        }

        // The car can change in the garage while this panel stays built, so follow the session's pick.
        void ShowCar()
        {
            var car = RaceSession.Car;
            if (car == shownCar) return;
            shownCar = car;
            if (carName) carName.text = car != null ? car.displayName.ToUpperInvariant() : "";
            if (carPicture)
            {
                carPicture.sprite = car != null ? car.thumbnail : null;
                carPicture.enabled = carPicture.sprite != null;
            }
        }

        void Refresh()
        {
            var cat = RaceCatalog.Instance;
            if (cat == null || cat.tracks.Count == 0) return;
            var t = cat.tracks[Mathf.Clamp(index, 0, cat.tracks.Count - 1)];
            if (title) title.text = t.displayName.ToUpperInvariant();
            if (description) description.text = t.description;
            if (details) details.text = t.LengthLabel;
            if (hero != null && hero.sprite != t.thumbnail)
            {
                hero.sprite = t.thumbnail;
                hero.enabled = t.thumbnail != null;
                heroAlpha = 0f; // fade the new track in
            }
            if (map)
            {
                map.sprite = t.map;
                map.enabled = t.map != null;
            }
            for (int i = 0; i < cards.Count; i++) cards[i].SetSelected(i == index);
        }
    }
}
