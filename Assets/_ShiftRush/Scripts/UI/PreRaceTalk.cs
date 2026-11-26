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
    /// straight on. Above the call, a face-off: your driver on the left, whoever's talking on the right, each a live
    /// portrait (<see cref="PortraitStudio"/>) in a tilted comic panel. The speaker's panel is lit and full size, the
    /// other dims back; each line sets the speaker's mood ("!" angry, "?" shocked, rivals smug by default).
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

        void OnDestroy()
        {
            Running = false;
            foreach (var st in studios.Values) if (st != null) Destroy(st.gameObject);
        }

        // ------------------------------------------------------------------ face-off portraits

        class Panel
        {
            public RectTransform root;
            public RawImage face;
            public Image back, dots, shadow, tagBar;
            public TMP_Text tag;
            public CanvasGroup group;
            public RectTransform marks;
            public float home, tilt, lit;
            public string who;
        }

        readonly System.Collections.Generic.Dictionary<string, PortraitStudio> studios = new System.Collections.Generic.Dictionary<string, PortraitStudio>();
        Panel left, right;
        string speaking;
        static Sprite dotSprite;

        PortraitStudio Studio(string who)
        {
            string key = who.ToLowerInvariant();
            if (!studios.TryGetValue(key, out var st) || st == null) { st = PortraitStudio.Film(key == "you" ? "YOU" : key, FaceColour(who)); studios[key] = st; }
            return st;
        }

        static Color FaceColour(string who)
        {
            string k = who.ToLowerInvariant();
            if (k == "you") return Cyan;
            if (k == "slim") return Magenta;
            var c = RivalCast.Instance != null ? RivalCast.Instance.Find(k) : null;
            return c != null ? c.color : new Color32(255, 184, 46, 255);
        }

        static PortraitStudio.Mood MoodOf(string who, string text)
        {
            if (text.Contains("?")) return PortraitStudio.Mood.Shocked;
            if (text.Contains("!")) return PortraitStudio.Mood.Angry;
            string k = who.ToLowerInvariant();
            return k == "you" || k == "slim" ? PortraitStudio.Mood.Neutral : PortraitStudio.Mood.Smug;
        }

        /// <summary>Who's talking now: their panel lights, their mood is set, the other dims.</summary>
        void Speak(string who, string text)
        {
            speaking = who;
            bool me = who == "YOU";
            var p = me ? left : right;
            if (!me && right.who != who) Show(right, who);
            Studio(who).SetMood(MoodOf(who, text));
            Marks(p, MoodOf(who, text));
        }

        void Show(Panel p, string who)
        {
            p.who = who;
            var st = Studio(who);
            p.face.texture = st.Texture;
            var c = FaceColour(who);
            p.back.color = Color.Lerp(c, Ink, 0.35f);
            p.dots.color = new Color(c.r, c.g, c.b, 0.55f);
            // The drop shadow in the opposite broadcast colour, so a cyan character still pops.
            Color.RGBToHSV(c, out float hue, out _, out _);
            p.shadow.color = hue > 0.4f && hue < 0.6f ? Magenta : Cyan;
            p.tag.text = Display(who);
            p.tagBar.color = c;
            // Only the two portraits on screen keep rendering.
            foreach (var kv in studios)
                if (kv.Value != null) kv.Value.Live = (left != null && left.who != null && kv.Key == left.who.ToLowerInvariant())
                                                    || (right != null && right.who != null && kv.Key == right.who.ToLowerInvariant());
        }

        Panel MakePanel(bool isLeft)
        {
            var p = new Panel();
            p.root = Rect(isLeft ? "You Panel" : "Caller Panel", transform);
            p.root.anchorMin = p.root.anchorMax = new Vector2(0.5f, 0.38f);
            p.root.sizeDelta = new Vector2(400, 400);
            p.home = isLeft ? -330f : 330f;
            p.tilt = isLeft ? -4f : 4f;
            p.root.anchoredPosition = new Vector2(p.home + (isLeft ? -900f : 900f), 400f);
            p.root.localEulerAngles = new Vector3(0, 0, p.tilt);
            p.group = p.root.gameObject.AddComponent<CanvasGroup>();

            var sh = Rect("Shadow", p.root); Fill(sh); sh.offsetMin = new Vector2(14, -14); sh.offsetMax = new Vector2(14, -14);
            p.shadow = sh.gameObject.AddComponent<Image>();
            var ink = Rect("Ink", p.root); Fill(ink); ink.gameObject.AddComponent<Image>().color = Ink;
            var inner = Rect("Inner", p.root); Fill(inner); inner.offsetMin = new Vector2(8, 8); inner.offsetMax = new Vector2(-8, -8);
            inner.gameObject.AddComponent<RectMask2D>();
            p.back = inner.gameObject.AddComponent<Image>();
            var dots = Rect("Halftone", inner); Fill(dots);
            p.dots = dots.gameObject.AddComponent<Image>(); p.dots.sprite = Dots(); p.dots.type = Image.Type.Tiled; p.dots.raycastTarget = false;
            p.dots.pixelsPerUnitMultiplier = 1.6f;
            // Speed lines behind the head, shown when shocked.
            p.marks = Rect("Marks", inner); Fill(p.marks);
            var face = Rect("Face", inner); Fill(face); face.offsetMin = new Vector2(-30, -30); face.offsetMax = new Vector2(30, 30);
            p.face = face.gameObject.AddComponent<RawImage>(); p.face.raycastTarget = false;

            // Name slashed across the bottom corner.
            var bar = Rect("Name", p.root);
            bar.anchorMin = bar.anchorMax = new Vector2(isLeft ? 0f : 1f, 0f); bar.pivot = new Vector2(isLeft ? 0f : 1f, 0f);
            bar.anchoredPosition = new Vector2(isLeft ? -18f : 18f, -22f); bar.sizeDelta = new Vector2(250, 54);
            bar.localEulerAngles = new Vector3(0, 0, isLeft ? 5f : -5f);
            var barInk = Rect("Ink", bar); Fill(barInk); barInk.offsetMin = new Vector2(-5, -5); barInk.offsetMax = new Vector2(5, 5); barInk.gameObject.AddComponent<Image>().color = Ink;
            var barFill = Rect("Fill", bar); Fill(barFill);
            p.tagBar = barFill.gameObject.AddComponent<Image>();
            p.tag = Label(bar, null, 34, Ink, TextAlignmentOptions.Center); Fill(p.tag.rectTransform);
            p.tag.fontStyle = FontStyles.Bold | FontStyles.Italic;
            return p;
        }

        /// <summary>Comic marks for the mood: shocked gets speed lines, angry gets vein marks, smug a sparkle.</summary>
        void Marks(Panel p, PortraitStudio.Mood mood)
        {
            for (int i = p.marks.childCount - 1; i >= 0; i--) Destroy(p.marks.GetChild(i).gameObject);
            switch (mood)
            {
                case PortraitStudio.Mood.Shocked:
                    for (int i = 0; i < 16; i++)
                    {
                        var l = Rect("Line", p.marks); l.anchorMin = l.anchorMax = new Vector2(0.5f, 0.55f);
                        l.pivot = new Vector2(0.5f, 0f); l.sizeDelta = new Vector2(i % 2 == 0 ? 5 : 3, 320);
                        l.localEulerAngles = new Vector3(0, 0, i * 22.5f + 6f);
                        l.anchoredPosition = Vector2.zero;
                        var im = l.gameObject.AddComponent<Image>(); im.color = new Color(1, 1, 1, 0.55f); im.raycastTarget = false;
                    }
                    break;
                case PortraitStudio.Mood.Angry:
                    Vein(p.marks, new Vector2(0.78f, 0.8f), 0f); Vein(p.marks, new Vector2(0.86f, 0.68f), 40f);
                    break;
                case PortraitStudio.Mood.Smug:
                    Sparkle(p.marks, new Vector2(0.22f, 0.78f), 1f); Sparkle(p.marks, new Vector2(0.3f, 0.9f), 0.55f);
                    break;
            }
        }

        static void Vein(RectTransform parent, Vector2 at, float spin)
        {
            // Four short red strokes around a gap: the comic "angry" cross.
            for (int i = 0; i < 4; i++)
            {
                var r = Rect("Vein", parent); r.anchorMin = r.anchorMax = at; r.sizeDelta = new Vector2(30, 8);
                float a = spin + i * 90f;
                r.localEulerAngles = new Vector3(0, 0, a + 20f);
                r.anchoredPosition = (Vector2)(Quaternion.Euler(0, 0, a) * new Vector2(17, 0));
                var im = r.gameObject.AddComponent<Image>(); im.color = new Color32(255, 40, 60, 255); im.raycastTarget = false;
            }
        }

        static void Sparkle(RectTransform parent, Vector2 at, float size)
        {
            // A four-point glint: a tall thin stroke, a shorter cross stroke, and a small diamond in the middle.
            Stroke(parent, at, new Vector2(5, 56) * size, 0f);
            Stroke(parent, at, new Vector2(5, 30) * size, 90f);
            Stroke(parent, at, new Vector2(12, 12) * size, 45f);
        }

        static void Stroke(RectTransform parent, Vector2 at, Vector2 size, float angle)
        {
            var r = Rect("Sparkle", parent); r.anchorMin = r.anchorMax = at; r.sizeDelta = size;
            r.localEulerAngles = new Vector3(0, 0, angle);
            var im = r.gameObject.AddComponent<Image>(); im.color = Color.white; im.raycastTarget = false;
        }

        /// <summary>One halftone dot, tiled across a panel's background.</summary>
        static Sprite Dots()
        {
            if (dotSprite != null) return dotSprite;
            const int n = 24;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                    byte a = (byte)(Mathf.Clamp01(5.5f - d) * 255f);
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px); tex.Apply();
            dotSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return dotSprite;
        }

        void Update()
        {
            if (left == null || right == null) return;
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 9f);
            foreach (var p in new[] { left, right })
            {
                bool on = speaking == null || (p == left ? speaking == "YOU" : speaking != "YOU");
                p.lit = Mathf.Lerp(p.lit, on ? 1f : 0f, k);
                var pos = p.root.anchoredPosition;
                p.root.anchoredPosition = new Vector2(Mathf.Lerp(pos.x, p.home, k), Mathf.Lerp(pos.y, 400f + p.lit * 14f, k));
                p.root.localScale = Vector3.one * Mathf.Lerp(0.84f, 1f, p.lit);
                float g = Mathf.Lerp(0.38f, 1f, p.lit);
                p.face.color = new Color(g, g, g, 1f);
                p.group.alpha = Mathf.Lerp(0.8f, 1f, p.lit);
                p.root.localEulerAngles = new Vector3(0, 0, p.tilt * Mathf.Lerp(0.6f, 1f, p.lit));
                if (p.marks != null) p.marks.gameObject.SetActive(p.lit > 0.5f);
            }
        }

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
                Speak(l.speaker, l.text);
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

            left = MakePanel(true);
            right = MakePanel(false);
            Show(left, "YOU");
            string caller = null;
            foreach (var l in episode.talk) if (l.speaker != "YOU") { caller = l.speaker; break; }
            Show(right, caller ?? "SLIM");
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
