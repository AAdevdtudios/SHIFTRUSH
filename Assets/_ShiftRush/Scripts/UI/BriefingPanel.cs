using ShiftRush.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ShiftRush.UI
{
    /// <summary>
    /// "Tonight on Channel 13": the briefing before an episode. Slim's intro, the main goal and the two Feed bonuses,
    /// the rival to beat, the track and your car. Arrows (or the d-pad) move between GO LIVE, CASE BOARD and GARAGE,
    /// with a focus ring on the one Enter will press; G still opens the garage, Esc goes back to the case board.
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
        KeyNav nav;

        void Awake()
        {
            if (goButton) goButton.onClick.AddListener(GoLive);
            if (boardButton) boardButton.onClick.AddListener(() => { if (Accepting && router) { MenuSounds.Back(); router.OpenStory(); } });
            if (garageButton) garageButton.onClick.AddListener(() => { if (Accepting && router) { MenuSounds.Select(); router.OpenGarage(); } });
            nav = KeyNav.Attach(gameObject, goButton, boardButton, garageButton);
            nav.Gate = () => Accepting;
        }

        bool Accepting { get { return !PreRaceTalk.Running && !StoryLiftOff.Running && (visibleGroup == null || (visibleGroup.alpha > 0.95f && visibleGroup.interactable)); } }

        void OnEnable() { Show(); if (nav != null) nav.Focus(0); }

        void Update()
        {
            if (!Accepting) return;
            var kb = Keyboard.current; var pad = Gamepad.current;
            if (MenuInput.Submit) { if (nav != null) nav.Press(); else GoLive(); }
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
            // Who's on the grid: "1v1 // VS KAITO" (or the field), and why, with their cars.
            var cast = RivalCast.Instance;
            bool named = e.opponents != null && e.opponents.Length > 0 && cast != null;
            bool rival = named || !string.IsNullOrEmpty(e.rival);
            if (rivalCard) rivalCard.SetActive(rival);
            if (named)
            {
                var names = new System.Collections.Generic.List<string>();
                var rides = new System.Collections.Generic.List<string>();
                foreach (var id in e.opponents)
                {
                    var c = cast.Find(id); if (c == null) continue;
                    names.Add(c.name);
                    var rc = race != null ? race.cars.Find(x => x.id == c.carId) : null;
                    rides.Add(c.name + " " + (rc != null ? rc.displayName.ToUpperInvariant() : c.carId.ToUpperInvariant()));
                }
                if (rivalName) rivalName.text = e.Matchup + "  //  VS " + (names.Count <= 2 ? string.Join(" & ", names) : names.Count + " RIVALS");
                // One or two rivals: name and car. A bigger field: just the names, so it fits the card.
                if (rivalLine)
                {
                    rivalLine.text = e.rivalLine + "\n<color=#9AA0B4>" + string.Join("  ·  ", names.Count <= 2 ? rides : names) + "</color>";
                    FitInCard(rivalLine);
                }
            }
            else
            {
                if (rivalName) rivalName.text = e.rival;
                if (rivalLine) { rivalLine.text = e.rivalLine; FitInCard(rivalLine); }
            }
            if (trackName) trackName.text = track != null ? track.displayName.ToUpperInvariant() : e.trackId.ToUpperInvariant();
            int gridSize = named ? e.opponents.Length + 1 : 6;
            if (trackInfo) trackInfo.text = track != null ? e.eventType + "  ·  " + track.LengthLabel + "  ·  " + gridSize + " CARS" + (e.police ? "  ·  POLICE" : "") : "";
            if (carName) carName.text = car != null ? car.displayName.ToUpperInvariant() : "";
            if (carInfo) carInfo.text = car != null ? Vehicles.CarTypes.Name(car.type) : "";
            if (photo) { photo.sprite = track != null ? track.thumbnail : null; photo.color = photo.sprite != null ? Color.white : new Color32(30, 25, 48, 255); }
            if (photoLabel) photoLabel.text = "CAM 01  //  DRONE OVER " + (track != null ? track.displayName.ToUpperInvariant() : "THE TRACK");
        }

        /// <summary>The rival's line shrinks to stay inside its card instead of running out under the buttons.</summary>
        static void FitInCard(TMP_Text t)
        {
            if (!t.enableAutoSizing) { t.fontSizeMax = t.fontSize; t.fontSizeMin = Mathf.Min(16f, t.fontSize); t.enableAutoSizing = true; }
            t.overflowMode = TextOverflowModes.Ellipsis;
        }

        void GoLive()
        {
            if (!Accepting || episode == null) return;
            var st = StoryProgress.StateOf(episode);
            if (st != StoryProgress.State.Open && st != StoryProgress.State.Closed) { MenuSounds.Back(); return; }
            MenuSounds.Confirm();
            // The ride to the race first (the Heavy Lifter snatches the car off the runway), then the race loads.
            var e = episode;
            // The talk first (who you're racing and why), then the ride to the race, then the race.
            System.Action ride = () => { if (!StoryLiftOff.Run(e, () => StoryProgress.Play(e))) StoryProgress.Play(e); };
            if (!PreRaceTalk.Run(e, ride)) ride();
        }
    }
}
