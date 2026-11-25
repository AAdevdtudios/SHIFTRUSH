using ShiftRush.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ShiftRush.UI
{
    /// <summary>
    /// "Tonight on Channel 13": the briefing before an episode. Slim's intro, the main goal and the two Feed bonuses,
    /// the rival to beat, the track and your car. Enter goes live (loads the race), G opens the garage, Esc goes back
    /// to the case board.
    /// </summary>
    public class BriefingPanel : MonoBehaviour
    {
        [SerializeField] CanvasGroup visibleGroup;
        [SerializeField] MenuRouter router;
        [SerializeField] TMP_Text tag;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text titleEcho;
        [SerializeField] TMP_Text slimLine;
        [SerializeField] TMP_Text goalText;
        [SerializeField] TMP_Text goalPays;
        [SerializeField] TMP_Text glossText;
        [SerializeField] TMP_Text gritText;
        [SerializeField] GameObject rivalCard;
        [SerializeField] TMP_Text rivalName;
        [SerializeField] TMP_Text rivalLine;
        [SerializeField] TMP_Text trackName;
        [SerializeField] TMP_Text trackInfo;
        [SerializeField] TMP_Text carName;
        [SerializeField] TMP_Text carInfo;
        [SerializeField] Image photo;
        [SerializeField] TMP_Text photoLabel;
        [SerializeField] Button goButton;
        [SerializeField] Button boardButton;
        [SerializeField] Button garageButton;

        StoryCatalog.Episode episode;

        void Awake()
        {
            if (goButton) goButton.onClick.AddListener(GoLive);
            if (boardButton) boardButton.onClick.AddListener(() => { if (Accepting && router) { MenuSounds.Back(); router.OpenStory(); } });
            if (garageButton) garageButton.onClick.AddListener(() => { if (Accepting && router) { MenuSounds.Select(); router.OpenGarage(); } });
        }

        bool Accepting { get { return visibleGroup == null || (visibleGroup.alpha > 0.95f && visibleGroup.interactable); } }

        void OnEnable() { Show(); }

        void Update()
        {
            if (!Accepting) return;
            var kb = Keyboard.current; var pad = Gamepad.current;
            if (MenuInput.Submit) GoLive();
            else if ((kb != null && kb.gKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame)) { MenuSounds.Select(); if (router) router.OpenGarage(); }
        }

        /// <summary>Fills the briefing for the episode the router picked.</summary>
        public void Show()
        {
            var cat = StoryCatalog.Instance;
            episode = cat != null && router != null ? cat.Find(router.BriefingEpisode) : null;
            if (episode == null && cat != null) episode = StoryProgress.Current;
            if (episode == null) return;
            var e = episode;
            var race = RaceCatalog.Instance;
            var track = race != null ? race.tracks.Find(t => t.id == e.trackId) : null;
            var car = RaceSession.Car;
            if (car != null && !PlayerProfile.OwnsCar(car) && race != null) car = race.cars[PlayerProfile.FirstOwnedIndex()];

            if (tag) tag.text = (cat.actTitle + "  ·  CHAPTER " + (e.chapter + 1) + "  ·  EPISODE " + e.number.ToString("00") + " OF " + cat.episodes.Count).ToUpperInvariant();
            if (title) title.text = e.title;
            if (titleEcho) titleEcho.text = e.title;
            if (slimLine) slimLine.text = "“" + e.slimLine + "”";
            if (goalText) goalText.text = CaseBoardPanel.Goal(e);
            if (goalPays) goalPays.text = StoryProgress.IsClosed(e.id) ? "PAID" : "+" + e.payout.ToString("N0");
            if (glossText) glossText.text = e.glossBonus;
            if (gritText) gritText.text = e.gritBonus;
            bool rival = !string.IsNullOrEmpty(e.rival);
            if (rivalCard) rivalCard.SetActive(rival);
            if (rivalName) rivalName.text = e.rival;
            if (rivalLine) rivalLine.text = e.rivalLine;
            if (trackName) trackName.text = track != null ? track.displayName.ToUpperInvariant() : e.trackId.ToUpperInvariant();
            if (trackInfo) trackInfo.text = track != null ? "3 LAPS  ·  " + track.LengthLabel + "  ·  6 CARS" : "";
            if (carName) carName.text = car != null ? car.displayName.ToUpperInvariant() : "";
            if (carInfo) carInfo.text = car != null ? Vehicles.CarTypes.Name(car.type) : "";
            if (photo) { photo.sprite = track != null ? track.thumbnail : null; photo.color = photo.sprite != null ? Color.white : new Color32(30, 25, 48, 255); }
            if (photoLabel) photoLabel.text = "CAM 01  //  DRONE OVER " + (track != null ? track.displayName.ToUpperInvariant() : "THE TRACK");
        }

        void GoLive()
        {
            if (!Accepting || episode == null) return;
            var st = StoryProgress.StateOf(episode);
            if (st != StoryProgress.State.Open && st != StoryProgress.State.Closed) { MenuSounds.Back(); return; }
            MenuSounds.Confirm();
            // The ride to the race first (the Heavy Lifter snatches the car off the runway), then the race loads.
            var e = episode;
            if (!StoryLiftOff.Run(e, () => StoryProgress.Play(e))) StoryProgress.Play(e);
        }
    }
}
