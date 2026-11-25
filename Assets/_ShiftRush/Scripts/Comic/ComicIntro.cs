using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ShiftRush.Comic
{
    /// <summary>
    /// The origin comic, played over the main menu the first time the game starts. Panels are captured from ComicStage
    /// (cameras named intro_01, intro_02, ...) and shown one at a time on an ink page with a
    /// magenta hard shadow. Each one slams in with a little tilt, drifts while it holds, then flicks off to the left.
    /// Click, Enter, Space or A turns the page. Hold Esc (or B) to skip. When it ends, the flag is set and it never
    /// plays again unless SHIFT RUSH → Comic → Replay Intro clears it.
    /// Panels and order come from Resources/SR_ComicIntro, filled by SHIFT RUSH → Comic → Capture Intro Panels.
    /// </summary>
    public class ComicIntro : MonoBehaviour
    {
        public const string SeenKey = "GripPro.IntroSeen";
        const string DataPath = "SR_ComicIntro";
        const float SkipHold = 0.9f;
        const float AutoTurn = 9f;

        static readonly Color Ink = new Color32(8, 5, 16, 255);
        static readonly Color Paper = new Color32(250, 247, 240, 255);
        static readonly Color Magenta = new Color32(255, 46, 154, 255);
        static readonly Color Cyan = new Color32(46, 242, 255, 255);

        public static bool Playing { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (PlayerPrefs.GetInt(SeenKey, 0) == 1) return;
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MainMenu") return;
            var data = Resources.Load<ComicIntroData>(DataPath);
            if (data == null) return;
            data.pages.RemoveAll(p => p == null);
            if (data.pages.Count == 0) return;
            var go = new GameObject("Comic Intro");
            var intro = go.AddComponent<ComicIntro>();
            intro.pages = data.pages;
            intro.mono = data.mono;
        }

        List<Texture2D> pages;
        TMP_FontAsset mono;
        CanvasGroup root;
        RectTransform frame, sheet;
        RawImage art;
        Image skipFill;
        TMP_Text counter;
        EventSystem events;
        bool eventsWereOn;

        void Start()
        {
            Playing = true;
            events = EventSystem.current;
            if (events != null) { eventsWereOn = events.enabled; events.enabled = false; }
            Build();
            StartCoroutine(Run());
        }

        void OnDestroy()
        {
            Playing = false;
            if (events != null) events.enabled = eventsWereOn;
        }

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>();
            root = gameObject.AddComponent<CanvasGroup>();
            root.alpha = 0f;

            var bg = Rect("Ink", transform);
            Stretch(bg);
            bg.gameObject.AddComponent<Image>().color = Ink;

            // Halftone dots in the corners of the page, the same screen as the panels.
            var dots = Rect("Dots", bg);
            Stretch(dots);
            var dotImg = dots.gameObject.AddComponent<RawImage>();
            dotImg.texture = DotTexture();
            dotImg.color = new Color(Magenta.r, Magenta.g, Magenta.b, 0.14f);
            dotImg.uvRect = new Rect(0, 0, 1920f / 24f, 1080f / 24f);

            frame = Rect("Frame", transform);
            frame.sizeDelta = new Vector2(1600, 978);
            frame.anchoredPosition = new Vector2(0, 24);

            var shadow = Rect("Shadow", frame);
            Stretch(shadow);
            shadow.offsetMin = new Vector2(16, -16); shadow.offsetMax = new Vector2(16, -16);
            shadow.gameObject.AddComponent<Image>().color = Magenta;

            sheet = Rect("Sheet", frame);
            Stretch(sheet);
            sheet.gameObject.AddComponent<Image>().color = Paper;

            var artRt = Rect("Art", sheet);
            Stretch(artRt);
            artRt.offsetMin = new Vector2(10, 10); artRt.offsetMax = new Vector2(-10, -10);
            artRt.gameObject.AddComponent<RectMask2D>();
            var inner = Rect("Image", artRt);
            Stretch(inner);
            art = inner.gameObject.AddComponent<RawImage>();

            counter = Label("Counter", transform, mono, 22, TextAlignmentOptions.Left, Cyan);
            var cr = counter.rectTransform;
            cr.anchorMin = cr.anchorMax = new Vector2(0, 0); cr.pivot = new Vector2(0, 0);
            cr.anchoredPosition = new Vector2(160, 18); cr.sizeDelta = new Vector2(700, 40);

            var hint = Label("Hint", transform, mono, 20, TextAlignmentOptions.Right, new Color(Paper.r, Paper.g, Paper.b, 0.7f));
            hint.text = "CLICK / ENTER  NEXT      HOLD ESC  SKIP";
            var hr = hint.rectTransform;
            hr.anchorMin = hr.anchorMax = new Vector2(1, 0); hr.pivot = new Vector2(1, 0);
            hr.anchoredPosition = new Vector2(-210, 18); hr.sizeDelta = new Vector2(800, 40);

            var ring = Rect("Skip", transform);
            ring.anchorMin = ring.anchorMax = new Vector2(1, 0); ring.pivot = new Vector2(1, 0);
            ring.anchoredPosition = new Vector2(-160, 16); ring.sizeDelta = new Vector2(40, 40);
            var ringBg = ring.gameObject.AddComponent<Image>();
            ringBg.sprite = Circle(); ringBg.color = new Color(1, 1, 1, 0.12f);
            var fill = Rect("Fill", ring);
            Stretch(fill);
            skipFill = fill.gameObject.AddComponent<Image>();
            skipFill.sprite = Circle(); skipFill.color = Magenta;
            skipFill.type = Image.Type.Filled; skipFill.fillMethod = Image.FillMethod.Radial360;
            skipFill.fillOrigin = (int)Image.Origin360.Top; skipFill.fillAmount = 0f;
        }

        IEnumerator Run()
        {
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.5f) { root.alpha = t; yield return null; }
            root.alpha = 1f;

            for (int i = 0; i < pages.Count; i++)
            {
                Show(pages[i], i);
                yield return Slide(new Vector2(1900, 0), Vector2.zero, -4f, 0f, 0.28f, true);
                Shake();
                float held = 0f;
                bool turn = false;
                while (!turn)
                {
                    held += Time.unscaledDeltaTime;
                    // A slow push in while the panel holds.
                    float s = 1f + Mathf.Min(held, AutoTurn) * 0.005f;
                    art.rectTransform.localScale = new Vector3(s, s, 1f);
                    if (Skipping()) { yield return Finish(); yield break; }
                    turn = Next() || held > AutoTurn;
                    yield return null;
                }
                UI.MenuSounds.Select();
                if (i < pages.Count - 1) yield return Slide(Vector2.zero, new Vector2(-1900, 60), 0f, 6f, 0.2f, false);
            }
            yield return Finish();
        }

        void Show(Texture2D tex, int i)
        {
            art.texture = tex;
            art.rectTransform.localScale = Vector3.one;
            counter.text = "CHANNEL 13  //  ORIGIN   " + (i + 1).ToString("00") + " / " + pages.Count.ToString("00");
        }

        IEnumerator Slide(Vector2 from, Vector2 to, float rotFrom, float rotTo, float time, bool easeOut)
        {
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / time)
            {
                float k = easeOut ? 1f - Mathf.Pow(1f - t, 3f) : t * t;
                frame.anchoredPosition = Vector2.LerpUnclamped(from, to, k) + new Vector2(0, 24);
                frame.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(rotFrom, rotTo, k));
                yield return null;
            }
            frame.anchoredPosition = to + new Vector2(0, 24);
            frame.localRotation = Quaternion.Euler(0, 0, rotTo);
        }

        void Shake() { StartCoroutine(ShakeRoutine()); }

        IEnumerator ShakeRoutine()
        {
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.18f)
            {
                float a = (1f - t) * 10f;
                sheet.anchoredPosition = new Vector2(Random.Range(-a, a), Random.Range(-a, a));
                yield return null;
            }
            sheet.anchoredPosition = Vector2.zero;
        }

        float skipHeld;

        bool Skipping()
        {
            bool down = (Keyboard.current != null && Keyboard.current.escapeKey.isPressed)
                     || (Gamepad.current != null && Gamepad.current.buttonEast.isPressed);
            skipHeld = down ? skipHeld + Time.unscaledDeltaTime : Mathf.Max(0f, skipHeld - Time.unscaledDeltaTime * 3f);
            skipFill.fillAmount = skipHeld / SkipHold;
            return skipHeld >= SkipHold;
        }

        static bool Next()
        {
            var k = Keyboard.current;
            if (k != null && (k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame)) return true;
            var m = Mouse.current;
            if (m != null && m.leftButton.wasPressedThisFrame) return true;
            var g = Gamepad.current;
            return g != null && g.buttonSouth.wasPressedThisFrame;
        }

        IEnumerator Finish()
        {
            PlayerPrefs.SetInt(SeenKey, 1);
            PlayerPrefs.Save();
            for (float t = 1; t > 0f; t -= Time.unscaledDeltaTime / 0.6f) { root.alpha = t; yield return null; }
            Destroy(gameObject);
        }

        // ---------- small builders ----------

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }

        static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, float size, TextAlignmentOptions align, Color col)
        {
            var t = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.color = col;
            t.raycastTarget = false;
            return t;
        }

        static Texture2D DotTexture()
        {
            const int n = 24;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                    byte a = (byte)(Mathf.Clamp01(4.5f - d) * 255);
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        static Sprite circle;

        static Sprite Circle()
        {
            if (circle != null) return circle;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                    byte a = (byte)(Mathf.Clamp01(n / 2f - d) * 255);
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return circle;
        }
    }
}
