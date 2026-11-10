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
            Refresh();
        }

        bool Accepting { get { return visibleGroup == null || (visibleGroup.alpha > 0.95f && visibleGroup.interactable); } }

        void Update()
        {
            if (!Accepting || SceneLoader.IsLoading) return;
            if (MenuInput.Left || MenuInput.Up) Select(index - 1, true);
            else if (MenuInput.Right || MenuInput.Down) Select(index + 1, true);
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

        void Refresh()
        {
            var cat = RaceCatalog.Instance;
            if (cat == null || cat.tracks.Count == 0) return;
            var t = cat.tracks[Mathf.Clamp(index, 0, cat.tracks.Count - 1)];
            if (title) title.text = t.displayName.ToUpperInvariant();
            if (description) description.text = t.description;
            if (details) details.text = t.LengthLabel;
            for (int i = 0; i < cards.Count; i++) cards[i].SetSelected(i == index);
        }
    }
}
