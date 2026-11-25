using Michsky.UI.Heat;
using UnityEngine;

namespace ShiftRush.UI
{
    /// <summary>
    /// Moves between the menu's screens on top of Heat's PanelManager: Home → Garage → Tracks, plus Settings
    /// and Credits. Back (Esc / B) goes one screen up; on Home it asks to quit. Also tells the showroom
    /// camera how to frame the car for each screen.
    /// </summary>
    public class MenuRouter : MonoBehaviour
    {
        public const string Home = "Home", Garage = "Garage", Tracks = "Tracks", Settings = "Settings", Story = "Story", Briefing = "Briefing";

        [SerializeField] PanelManager panels;
        [SerializeField] Showroom showroom;
        [SerializeField] CreditsManager credits;
        [SerializeField] ModalWindowManager quitWindow;

        string current = Home;
        string garageBackTo = Home;

        /// <summary>The episode the briefing shows.</summary>
        public string BriefingEpisode { get; private set; }

        void Awake()
        {
            // So Settings → Controls shows the keys a race will actually use.
            ShiftRush.Vehicles.ControlBindings.Apply();
        }

        System.Collections.IEnumerator Start()
        {
            // Back from a story race: straight to the case board.
            if (!ShiftRush.Core.StoryProgress.TakeReturnToBoard()) yield break;
            yield return null;
            OpenStory();
        }

        public void OpenHome() { Open(Home); }
        public void OpenGarage() { garageBackTo = current == Briefing ? Briefing : Home; Open(Garage); }
        public void OpenStory() { Open(Story); }

        /// <summary>The briefing for one episode.</summary>
        public void OpenBriefing(string episodeId)
        {
            BriefingEpisode = episodeId;
            if (current == Briefing) { var b = FindFirstObjectByType<BriefingPanel>(); if (b != null) b.Show(); return; }
            Open(Briefing);
        }
        public void OpenTracks() { Open(Tracks); }
        public void OpenSettings() { Open(Settings); }

        public void Open(string panel)
        {
            if (panel == current) return;
            current = panel;
            panels.OpenPanel(panel);
            if (showroom != null)
            {
                if (panel == Garage) showroom.FrameGarage();
                else if (panel == Tracks || panel == Story || panel == Briefing) showroom.FrameTracks(); // full-screen boards: the set rests
                else showroom.FrameHome();
            }
        }

        /// <summary>Straight into a race: the car and track picked last time.</summary>
        public void QuickRace()
        {
            // Only a car you own goes on the grid.
            if (!ShiftRush.Core.PlayerProfile.OwnsCar(ShiftRush.Core.RaceSession.Car))
                ShiftRush.Core.RaceSession.CarIndex = ShiftRush.Core.PlayerProfile.FirstOwnedIndex();
            var track = ShiftRush.Core.RaceSession.Track;
            if (track == null) { OpenGarage(); return; }
            ShiftRush.Core.StoryProgress.Active = ""; // a quick race: no story drop, no episode result
            ShiftRush.Core.SceneLoader.LoadTrack(track);
        }

        /// <summary>The practice yard: no race, no rules, every move to play with.</summary>
        public void FreeRoam()
        {
            ShiftRush.Core.SceneLoader.Load("MovesLab", "Free Roam", "THE YARD  //  NO CAMERAS, NO RULES",
                "Q and E roll you sideways. Hold Space through a corner to drift, let go to boost.");
        }

        public void OpenCredits()
        {
            if (credits != null) credits.OpenPanel();
        }

        public void AskQuit()
        {
#if UNITY_WEBGL
            return;
#else
            if (quitWindow != null) quitWindow.OpenWindow();
            else Quit();
#endif
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Wired to each screen's Back hotkey.</summary>
        public void Back()
        {
            if (BindingRow.IsListening) return;
            MenuSounds.Back();
            switch (current)
            {
                case Tracks: Open(Garage); break;
                case Garage: Open(garageBackTo); garageBackTo = Home; break;
                case Briefing: Open(Story); break;
                case Story:
                case Settings: Open(Home); break;
                default: AskQuit(); break;
            }
        }
    }
}
