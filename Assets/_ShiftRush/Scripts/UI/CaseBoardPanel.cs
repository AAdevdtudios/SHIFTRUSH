using ShiftRush.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftRush.UI
{
    /// <summary>
    /// Story mode's map: Detective Kowal's case board on the rookie. Each episode is a pinned photo on red string,
    /// four to a chapter. Arrows (or W A S D) move between them, Enter opens the briefing for an open one, and the
    /// file on the right says what the episode asks, what it pays, how the Feed leans and how strong the case is.
    /// </summary>
    public class CaseBoardPanel : MonoBehaviour
    {
        [System.Serializable]
        public class Card
        {
            public RectTransform root;
            public Image paper;
            public Image photo;
            public Image ring;
            public Image pin;
            public TMP_Text number;
            public TMP_Text title;
            public TMP_Text status;
            public TMP_Text kind;
            public Button button;
        }

        [SerializeField] CanvasGroup visibleGroup;
        [SerializeField] MenuRouter router;
        [SerializeField] Card[] cards = new Card[0];
        [SerializeField] TMP_Text actLabel;
        [SerializeField] TMP_Text fileTag;
        [SerializeField] TMP_Text fileTitle;
        [SerializeField] TMP_Text fileSynopsis;
        [SerializeField] TMP_Text fileGoal;
        [SerializeField] TMP_Text fileGloss;
        [SerializeField] TMP_Text fileGrit;
        [SerializeField] TMP_Text filePays;
        [SerializeField] TMP_Text kowalNote;
        [SerializeField] RectTransform leanFill;
        [SerializeField] TMP_Text leanLabel;
        [SerializeField] Image[] strength = new Image[0];
        [SerializeField] TMP_Text strengthLabel;
        [SerializeField] Button openButton;
        [SerializeField] TMP_Text openLabel;
        [SerializeField] Image openFill;
        [SerializeField] TMP_Text scanner;

        static readonly Color Paper = new Color32(242, 238, 227, 255);
        static readonly Color PaperDim = new Color32(217, 211, 195, 255);
        static readonly Color Redacted = new Color32(28, 32, 40, 255);
        static readonly Color Ink = new Color32(20, 18, 24, 255);
        static readonly Color CaseRed = new Color32(181, 18, 31, 255);
        static readonly Color Pink = new Color32(255, 46, 154, 255);
        static readonly Color Green = new Color32(30, 107, 52, 255);
        static readonly Color Grey = new Color32(85, 82, 74, 255);
        static readonly Color Pale = new Color32(154, 157, 168, 255);
        static readonly Color StrengthOn = new Color32(181, 18, 31, 255);
        static readonly Color StrengthOff = new Color32(201, 195, 179, 255);

        int index;
        float shake;
        RectTransform openRect; Vector2 openHome;
        static readonly string[] ScannerLines =
        {
            "KOWAL: pull every clip with that black car in it",
            "DISPATCH: plane seen over the pass, no flight plan",
            "UNIT 4: feed traced to a relay on the airstrip, gone before we got there",
            "KOWAL: the rookie's on every stream now. Find out who they are.",
        };

        void Awake()
        {
            for (int i = 0; i < cards.Length; i++) { int k = i; if (cards[i].button) cards[i].button.onClick.AddListener(() => Click(k)); }
            if (openButton) { openButton.onClick.AddListener(Open); openRect = (RectTransform)openButton.transform; openHome = openRect.anchoredPosition; }
        }

        void OnEnable()
        {
            var cat = StoryCatalog.Instance;
            var cur = StoryProgress.Current;
            index = cat != null && cur != null ? Mathf.Max(0, cat.IndexOf(cur.id)) : 0;
            Refresh();
        }

        bool Accepting { get { return visibleGroup == null || (visibleGroup.alpha > 0.95f && visibleGroup.interactable); } }

        void Update()
        {
            if (openRect != null)
            {
                shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 3f);
                openRect.anchoredPosition = openHome + new Vector2(Mathf.Sin(Time.unscaledTime * 70f) * 10f * shake * shake, 0f);
            }
            if (scanner) scanner.text = "<color=#E8233A><b>SCANNER</b></color>    " + ScannerLines[(int)(Time.unscaledTime / 7f) % ScannerLines.Length];
            if (!Accepting) return;
            if (MenuInput.Left) Move(-1);
            else if (MenuInput.Right) Move(1);
            else if (MenuInput.Up) Move(-4);
            else if (MenuInput.Down) Move(4);
            else if (MenuInput.Submit) Open();
        }

        int Count { get { var cat = StoryCatalog.Instance; return cat != null ? Mathf.Min(cat.episodes.Count, cards.Length) : 0; } }

        void Move(int by)
        {
            int n = Count;
            if (n == 0) return;
            int next = Mathf.Clamp(index + by, 0, n - 1);
            if (next == index) { MenuSounds.Tick(); return; }
            index = next;
            MenuSounds.Hover();
            Refresh();
        }

        void Click(int i)
        {
            if (!Accepting || i >= Count) return;
            if (i == index) { Open(); return; }
            index = i;
            MenuSounds.Hover();
            Refresh();
        }

        /// <summary>Into the briefing for an open or closed (replayable) episode.</summary>
        void Open()
        {
            if (!Accepting) return;
            var cat = StoryCatalog.Instance;
            if (cat == null || index >= cat.episodes.Count) return;
            var e = cat.episodes[index];
            var st = StoryProgress.StateOf(e);
            if (st == StoryProgress.State.Open || st == StoryProgress.State.Closed)
            {
                MenuSounds.Select();
                if (router) router.OpenBriefing(e.id);
            }
            else { shake = 1f; MenuSounds.Back(); }
        }

        void Refresh()
        {
            var cat = StoryCatalog.Instance;
            if (cat == null) return;
            var race = RaceCatalog.Instance;
            if (actLabel) actLabel.text = cat.actTitle + "  ·  " + StoryProgress.ClosedCount + " OF " + cat.episodes.Count + " CLOSED";

            for (int i = 0; i < cards.Length; i++)
            {
                var c = cards[i];
                bool has = i < cat.episodes.Count;
                if (c.root) c.root.gameObject.SetActive(has);
                if (!has) continue;
                var e = cat.episodes[i];
                var st = StoryProgress.StateOf(e);
                bool on = i == index;
                bool hidden = st == StoryProgress.State.Sealed && e.redacted;
                var track = race != null ? race.tracks.Find(t => t.id == e.trackId) : null;

                if (c.paper) c.paper.color = hidden ? Redacted : st == StoryProgress.State.Sealed || st == StoryProgress.State.Filming ? PaperDim : Paper;
                if (c.photo)
                {
                    c.photo.sprite = track != null && !hidden ? track.thumbnail : null;
                    c.photo.color = c.photo.sprite != null ? (st == StoryProgress.State.Open || st == StoryProgress.State.Closed ? Color.white : new Color(0.45f, 0.45f, 0.5f)) : new Color32(32, 36, 44, 255);
                }
                if (c.ring) c.ring.enabled = on;
                if (c.pin) c.pin.color = st == StoryProgress.State.Closed ? Green : st == StoryProgress.State.Open ? CaseRed : Grey;
                if (c.number) { c.number.text = "EP " + e.number.ToString("00"); c.number.color = hidden ? Pale : Ink; }
                if (c.title) { c.title.text = hidden ? "████████" : e.title; c.title.color = hidden ? new Color32(60, 64, 74, 255) : Ink; }
                if (c.kind) { c.kind.text = hidden ? "" : e.eventType; c.kind.color = hidden ? Pale : Grey; }
                if (c.status)
                {
                    c.status.text = st == StoryProgress.State.Closed ? "CLOSED" : st == StoryProgress.State.Open ? "OPEN" : st == StoryProgress.State.Filming ? "FILMING" : "SEALED";
                    c.status.color = st == StoryProgress.State.Closed ? Green : st == StoryProgress.State.Open ? CaseRed : hidden ? Pale : Grey;
                }
            }

            // The open file.
            var ep = index < cat.episodes.Count ? cat.episodes[index] : null;
            if (ep == null) return;
            var state = StoryProgress.StateOf(ep);
            bool redacted = state == StoryProgress.State.Sealed && ep.redacted;
            var tr = race != null ? race.tracks.Find(t => t.id == ep.trackId) : null;
            if (fileTag) fileTag.text = "OPEN FILE  ·  EP " + ep.number.ToString("00") + "  ·  " + cat.chapters[Mathf.Clamp(ep.chapter, 0, cat.chapters.Length - 1)];
            if (fileTitle) fileTitle.text = redacted ? "REDACTED" : ep.title;
            if (fileSynopsis) fileSynopsis.text = redacted ? "Kowal hasn't got this far. Close the episodes before it." : ep.synopsis;
            if (fileGoal) fileGoal.text = redacted ? "" : "MAIN<pos=34%><b>" + Goal(ep) + "</b>" + (tr != null ? "\n<color=#55524A>WHERE</color><pos=34%>" + tr.displayName.ToUpperInvariant() + "  ·  " + tr.LengthLabel : "");
            if (fileGloss) fileGloss.text = redacted ? "" : "<color=#0A6E7A><b>GLOSS</b></color><pos=34%>" + ep.glossBonus;
            if (fileGrit) fileGrit.text = redacted ? "" : "<color=#A44A00><b>GRIT</b></color><pos=34%>" + ep.gritBonus;
            if (filePays) filePays.text = redacted ? "" : "PAYS<pos=34%><b>" + ep.payout.ToString("N0") + " HYPE</b>" + (state == StoryProgress.State.Closed ? "  <color=#1E6B34>(PAID)</color>" : "");
            if (kowalNote) kowalNote.text = string.IsNullOrEmpty(ep.kowalNote) || redacted ? "Who is driving that car?" : ep.kowalNote;

            float g = StoryProgress.GlossShare;
            if (leanFill) leanFill.anchorMax = new Vector2(g, 1f);
            if (leanLabel) leanLabel.text = "<color=#0A6E7A>GLOSS " + Mathf.RoundToInt(g * 100f) + "%</color><pos=60%><color=#A44A00>GRIT " + (100 - Mathf.RoundToInt(g * 100f)) + "%</color>";
            int lit = Mathf.CeilToInt(StoryProgress.ClosedCount / (float)Mathf.Max(1, cat.episodes.Count) * strength.Length);
            for (int i = 0; i < strength.Length; i++) if (strength[i]) strength[i].color = i < lit ? StrengthOn : StrengthOff;
            if (strengthLabel) strengthLabel.text = "CASE STRENGTH  " + StoryProgress.ClosedCount + " / " + cat.episodes.Count;

            string label; bool can;
            switch (state)
            {
                case StoryProgress.State.Open: label = "OPEN EPISODE"; can = true; break;
                case StoryProgress.State.Closed: label = "REPLAY EPISODE"; can = true; break;
                case StoryProgress.State.Filming: label = "STILL FILMING"; can = false; break;
                default: label = "CLOSE EP " + (ep.number - 1).ToString("00") + " FIRST"; can = false; break;
            }
            if (openLabel) openLabel.text = label;
            if (openFill) openFill.color = can ? Pink : new Color32(60, 52, 82, 255);
        }

        public static string Goal(StoryCatalog.Episode e)
        {
            string g = e.finishWithin <= 1 ? "Win it" : "Finish top " + e.finishWithin;
            return string.IsNullOrEmpty(e.rival) ? g : g + ", beat " + e.rival;
        }
    }
}
