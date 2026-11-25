using System.Collections;
using ShiftRush.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ShiftRush.UI
{
    /// <summary>
    /// The talk before a story race: when you press Go Live, the episode's lines play one at a time as an incoming
    /// call over the briefing (who's on the line, their car, then the back and forth), typed out. Enter, Space,
    /// click or A for the next line, Esc or B to skip. Then the lift-off and the race. Episodes with no lines go
    /// straight on.
    /// </summary>
    public class PreRaceTalk : MonoBehaviour
    {
        static readonly Color Ink = new Color32(8, 5, 16, 255);
        static readonly Color Paper = new Color32(250, 247, 240, 255);
        static readonly Color Magenta = new Color32(255, 46, 154, 255);
        static readonly Color Cyan = new Color32(46, 242, 255, 255);

        public static bool Running { get; private set; }

        public static bool Run(StoryCatalog.Episode e, System.Action done)
        {
            if (Running) return true;
            if (e == null || e.talk == null || e.talk.Count == 0) return false;
            var go = new GameObject("Pre-Race Talk");
            var t = go.AddComponent<PreRaceTalk>();
            t.episode = e; t.done = done;
            Running = true;
            return true;
        }

        StoryCatalog.Episode episode;
        System.Action done;
        TMP_Text header, who, line, hint;
        Image ring, nameBar;
        CanvasGroup group;

        void OnDestroy() { Running = false; }

        void Start() { Build(); StartCoroutine(Play()); }

        IEnumerator Play()
        {
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.25f) { group.alpha = t; yield return null; }
            group.alpha = 1f;
            // Ringing first: who's calling.
            string caller = null;
            foreach (var l in episode.talk) if (l.speaker != "YOU") { caller = l.speaker; break; }
            header.text = "INCOMING CALL  //  " + Display(caller ?? "CH13");
            MenuSounds.Tick();
            for (float t = 0; t < 0.9f; t += Time.unscaledDeltaTime)
            {
                ring.transform.localScale = Vector3.one * (1f + Mathf.Repeat(t * 2f, 1f) * 0.5f);
                ring.color = new Color(Cyan.r, Cyan.g, Cyan.b, 1f - Mathf.Repeat(t * 2f, 1f));
                if (Skip()) { Finish(); yield break; }
                yield return null;
            }
            ring.enabled = false;
            header.text = "ON THE LINE  //  " + Display(caller ?? "CH13") + CarOf(caller);

            foreach (var l in episode.talk)
            {
                bool me = l.speaker == "YOU";
                who.text = Display(l.speaker);
                nameBar.color = me ? Cyan : ColourOf(l.speaker);
                who.color = Ink;
                line.text = l.text;
                line.maxVisibleCharacters = 0;
                MenuSounds.Select();
                int total = l.text.Length;
                for (float c = 0; c < total; c += Time.unscaledDeltaTime * 55f)
                {
                    line.maxVisibleCharacters = (int)c;
                    if (Next()) break;
                    if (Skip()) { Finish(); yield break; }
                    yield return null;
                }
                line.maxVisibleCharacters = total;
                yield return null;
                while (!Next())
                {
                    if (Skip()) { Finish(); yield break; }
                    yield return null;
                }
            }
            Finish();
        }

        void Finish()
        {
            StopAllCoroutines();
            StartCoroutine(HandOver());
        }

        IEnumerator HandOver()
        {
            // One frame later, so the key that closed the call isn't read again by what comes next.
            group.alpha = 0f;
            yield return null;
            var go = done; done = null;
            Destroy(gameObject);
            go?.Invoke();
        }

        static string Display(string speaker)
        {
            var c = RivalCast.Instance != null ? RivalCast.Instance.Find(speaker) : null;
            return c != null ? c.name : speaker.ToUpperInvariant();
        }

        static Color ColourOf(string speaker)
        {
            var c = RivalCast.Instance != null ? RivalCast.Instance.Find(speaker) : null;
            return c != null ? c.color : Magenta;
        }

        static string CarOf(string speaker)
        {
            var c = RivalCast.Instance != null ? RivalCast.Instance.Find(speaker) : null;
            if (c == null || RaceCatalog.Instance == null) return "";
            foreach (var car in RaceCatalog.Instance.cars) if (car.id == c.carId) return "  //  " + car.displayName.ToUpperInvariant();
            return "";
        }

        static bool Next()
        {
            var k = Keyboard.current;
            if (k != null && (k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame)) return true;
            var m = Mouse.current; if (m != null && m.leftButton.wasPressedThisFrame) return true;
            var g = Gamepad.current; return g != null && g.buttonSouth.wasPressedThisFrame;
        }

        static bool Skip()
        {
            var k = Keyboard.current; if (k != null && k.escapeKey.wasPressedThisFrame) return true;
            var g = Gamepad.current; return g != null && g.buttonEast.wasPressedThisFrame;
        }

        // ------------------------------------------------------------------ layout

        void Build()
        {
            var data = Resources.Load<Comic.ComicIntroData>("SR_ComicIntro");
            var mono = data != null ? data.mono : null;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 850;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>();
            group = gameObject.AddComponent<CanvasGroup>(); group.alpha = 0f;

            var dim = Rect("Dim", transform); Fill(dim); dim.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.02f, 0.06f, 0.78f);

            var card = Rect("Call", transform);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.38f); card.sizeDelta = new Vector2(1180, 300);
            var shadow = Rect("Shadow", card); Fill(shadow); shadow.offsetMin = new Vector2(14, -14); shadow.offsetMax = new Vector2(14, -14);
            shadow.gameObject.AddComponent<Image>().color = Magenta;
            var ink = Rect("Ink", card); Fill(ink); ink.gameObject.AddComponent<Image>().color = Ink;
            var paper = Rect("Paper", card); Fill(paper); paper.offsetMin = new Vector2(7, 7); paper.offsetMax = new Vector2(-7, -7);
            paper.gameObject.AddComponent<Image>().color = Paper;

            var bar = Rect("Name Bar", paper);
            bar.anchorMin = new Vector2(0, 1); bar.anchorMax = new Vector2(0, 1); bar.pivot = new Vector2(0, 1);
            bar.anchoredPosition = new Vector2(34, -26); bar.sizeDelta = new Vector2(340, 54);
            nameBar = bar.gameObject.AddComponent<Image>(); nameBar.color = Magenta;
            who = Label(bar, null, 36, Ink, TextAlignmentOptions.Center); Fill(who.rectTransform);
            who.fontStyle = FontStyles.Bold | FontStyles.Italic;

            line = Label(paper, null, 44, Ink, TextAlignmentOptions.TopLeft);
            Fill(line.rectTransform); line.rectTransform.offsetMin = new Vector2(38, 26); line.rectTransform.offsetMax = new Vector2(-38, -96);
            line.fontStyle = FontStyles.Bold | FontStyles.UpperCase;

            header = Label(transform, mono, 26, Cyan, TextAlignmentOptions.Left);
            var hr = header.rectTransform; hr.anchorMin = hr.anchorMax = new Vector2(0.5f, 0.38f); hr.sizeDelta = new Vector2(1180, 40);
            hr.anchoredPosition = new Vector2(70, 185);

            var r = Rect("Ring", transform);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.38f); r.sizeDelta = new Vector2(34, 34); r.anchoredPosition = new Vector2(-575, 185);
            ring = r.gameObject.AddComponent<Image>(); ring.color = Cyan;

            hint = Label(transform, mono, 20, new Color(Paper.r, Paper.g, Paper.b, 0.75f), TextAlignmentOptions.Right);
            var nr = hint.rectTransform; nr.anchorMin = nr.anchorMax = new Vector2(0.5f, 0.38f); nr.sizeDelta = new Vector2(1180, 34);
            nr.anchoredPosition = new Vector2(0, -180);
            hint.text = "ENTER / A  NEXT      ESC / B  SKIP";
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
        static void Fill(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; }
        static TMP_Text Label(Transform parent, TMP_FontAsset font, float size, Color col, TextAlignmentOptions align)
        {
            var t = Rect("Text", parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size; t.color = col; t.alignment = align; t.raycastTarget = false;
            return t;
        }
    }
}
