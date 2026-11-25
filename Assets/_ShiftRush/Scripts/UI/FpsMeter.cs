using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ShiftRush.UI
{
    /// <summary>
    /// Frame-rate strip on the bottom edge, styled like the broadcast HUD: average FPS over the last half
    /// second, frame time in ms, and the 1% low (the worst frames, which you feel as hitches). Cyan when smooth,
    /// amber under 50, magenta under 30, plus the screen refresh rate, which caps FPS on a slow monitor. F3 shows or hides it and the choice is remembered. Lives in every scene.
    /// </summary>
    public class FpsMeter : MonoBehaviour
    {
        const string Key = "GripPro.ShowFps";
        static readonly Color Cyan = new Color32(46, 242, 255, 255);
        static readonly Color Amber = new Color32(255, 184, 46, 255);
        static readonly Color Magenta = new Color32(255, 46, 154, 255);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<FpsMeter>() != null) return;
            var go = new GameObject("FPS Meter");
            DontDestroyOnLoad(go);
            go.AddComponent<FpsMeter>();
        }

        readonly float[] frames = new float[240];
        int head, count;
        float window, windowTime;
        int windowFrames;
        GameObject panel;
        TMP_Text fps, detail;
        Image bar;

        void Start()
        {
            Build();
            panel.SetActive(PlayerPrefs.GetInt(Key, 1) == 1);
        }

        void Update()
        {
            var k = Keyboard.current;
            if (k != null && k.f3Key.wasPressedThisFrame)
            {
                bool on = !panel.activeSelf;
                panel.SetActive(on);
                PlayerPrefs.SetInt(Key, on ? 1 : 0);
            }

            float dt = Time.unscaledDeltaTime;
            frames[head] = dt; head = (head + 1) % frames.Length; if (count < frames.Length) count++;
            windowTime += dt; windowFrames++;
            if (windowTime < 0.5f || !panel.activeSelf) return;

            float avg = windowFrames / windowTime;
            windowTime = 0f; windowFrames = 0;

            // 1% low: average of the slowest 1% of recent frames, shown as FPS.
            var sorted = new float[count];
            System.Array.Copy(frames, sorted, count);
            System.Array.Sort(sorted);
            int worst = Mathf.Max(1, count / 100);
            float slow = 0f; for (int i = count - worst; i < count; i++) slow += sorted[i];
            float low = worst / Mathf.Max(slow, 0.0001f);

            var col = avg >= 50f ? Cyan : avg >= 30f ? Amber : Magenta;
            fps.text = Mathf.RoundToInt(avg).ToString();
            fps.color = col; bar.color = col;
            detail.text = (1000f / avg).ToString("0.0") + " MS    1% LOW " + Mathf.RoundToInt(low) + "    " + Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value) + "HZ" + RenderTag();
        }

        /// <summary>The height the game actually renders at, when the resolution cap is lowering it.</summary>
        static string RenderTag()
        {
            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (urp == null || urp.renderScale >= 0.999f) return "";
            // The display's own height: on a Retina Mac Screen.height can report double the drawable.
            int h = Screen.fullScreenMode == FullScreenMode.Windowed ? Screen.height : Screen.currentResolution.height;
            return "  " + Mathf.RoundToInt(h * urp.renderScale) + "P";
        }

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 2000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1f;

            var data = Resources.Load<Comic.ComicIntroData>("SR_ComicIntro");
            var mono = data != null ? data.mono : null;

            // One slim strip on the bottom edge, centred: clear of the feed, the menu row and the race HUD.
            var p = new GameObject("Strip", typeof(RectTransform));
            p.transform.SetParent(transform, false);
            var r = (RectTransform)p.transform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0f);
            r.anchoredPosition = new Vector2(0, 4); r.sizeDelta = new Vector2(400, 26);
            p.AddComponent<Image>().color = new Color(0.03f, 0.02f, 0.06f, 0.72f);
            panel = p;

            var b = new GameObject("Bar", typeof(RectTransform)); b.transform.SetParent(r, false);
            var br = (RectTransform)b.transform;
            br.anchorMin = new Vector2(0, 0); br.anchorMax = new Vector2(0, 1); br.pivot = new Vector2(0, 0.5f);
            br.sizeDelta = new Vector2(4, 0);
            bar = b.AddComponent<Image>(); bar.color = Cyan;

            fps = Label(r, mono, 20, TextAlignmentOptions.MidlineRight, new Vector2(10, 0), new Vector2(46, 26));
            fps.fontStyle = FontStyles.Bold; fps.text = "--";
            var tag = Label(r, mono, 13, TextAlignmentOptions.MidlineLeft, new Vector2(60, 0), new Vector2(32, 26));
            tag.text = "FPS"; tag.color = new Color(1, 1, 1, 0.55f);
            detail = Label(r, mono, 15, TextAlignmentOptions.MidlineLeft, new Vector2(96, 0), new Vector2(298, 26));
            detail.color = new Color(1, 1, 1, 0.85f);
        }

        static TMP_Text Label(RectTransform parent, TMP_FontAsset font, float size, TextAlignmentOptions align, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject("Text", typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size; t.alignment = align; t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }
    }
}
