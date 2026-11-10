using ShiftRush.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShiftRush.UI
{
    /// <summary>One track in the track select row. Lifts and lights its slanted frame when selected.</summary>
    public class TrackCard : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [SerializeField] TMP_Text number;
        [SerializeField] TMP_Text trackName;
        [SerializeField] TMP_Text length;
        [SerializeField] Image thumbnail;
        [SerializeField] Image frame;
        [SerializeField] RectTransform body;
        [SerializeField] Color idleFrame = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] Color activeFrame = new Color(1f, 0.25f, 0.85f, 1f);

        TrackSelectPanel panel;
        int index;
        bool selected;
        float lift;

        public void Bind(TrackSelectPanel owner, int i, RaceCatalog.Track track)
        {
            panel = owner;
            index = i;
            if (number) number.text = (i + 1).ToString("00");
            if (trackName) trackName.text = track.displayName.ToUpperInvariant();
            if (length) length.text = track.LengthLabel;
            if (thumbnail)
            {
                thumbnail.sprite = track.thumbnail;
                thumbnail.enabled = track.thumbnail != null;
            }
        }

        public void SetSelected(bool on) { selected = on; }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            lift = Mathf.Lerp(lift, selected ? 1f : 0f, 1f - Mathf.Exp(-dt * 14f));
            if (body) body.anchoredPosition = new Vector2(0f, lift * 18f);
            if (frame) frame.color = Color.Lerp(idleFrame, activeFrame, lift);
            if (trackName) trackName.alpha = 0.55f + 0.45f * lift;
        }

        public void OnPointerEnter(PointerEventData eventData) { if (panel) panel.Select(index, true); }
        public void OnPointerClick(PointerEventData eventData) { if (panel) panel.Go(index); }
    }
}
