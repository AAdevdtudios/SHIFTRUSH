using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftRush.UI
{
    /// <summary>
    /// "The Feed", Channel 13's live chat down the right of the home screen. New lines arrive every few seconds:
    /// viewers talking, tips, rival trash talk, rumours and the odd news flash, so the menu feels like a stream
    /// that's already running. Also ticks the viewer count and shows the player's Hype.
    /// Lines are written here for now. Later they can come from the story and, optionally, a real stream's chat.
    /// </summary>
    public class TheFeed : MonoBehaviour
    {
        public enum Kind { Chat, Tip, Rumour, News, System }

        [System.Serializable]
        public struct Line
        {
            public Kind kind;
            public string user;
            public string text;
            public Line(Kind kind, string user, string text) { this.kind = kind; this.user = user; this.text = text; }
        }

        [SerializeField] RectTransform messages;
        [SerializeField] TMP_FontAsset bodyFont, monoFont;
        [SerializeField] TMP_Text viewersText, hypeText;
        [SerializeField] int maxLines = 9;
        [SerializeField] Vector2 gap = new Vector2(2.2f, 4.6f);
        [SerializeField] int baseViewers = 48200;

        static readonly string[] NameColours = { "#2EF2FF", "#FF2E9A", "#FFC53D", "#7CF29A", "#B9A4FF", "#FF8A5B" };

        readonly List<Line> pool = new List<Line>
        {
            new Line(Kind.Chat, "nitroqueen", "blackout again?? brave"),
            new Line(Kind.Chat, "slim_m", "feed is clean, cops two blocks out"),
            new Line(Kind.Chat, "vex_official", "1:38.90 on the pass. come get it"),
            new Line(Kind.Tip, "tetsuo_stan", "for new tyres, do it"),
            new Line(Kind.Rumour, null, "a sixth car is coming to the league"),
            new Line(Kind.Chat, "gridghost", "who let the rookie on channel 13"),
            new Line(Kind.Chat, "rollcage_rita", "Q E roll over vex please i am begging"),
            new Line(Kind.Chat, "drift_dad", "hold the handbrake LONGER"),
            new Line(Kind.News, null, "Police raid the docks event. Crew escapes by plane."),
            new Line(Kind.Chat, "kaito", "tyres warm. who is next"),
            new Line(Kind.Chat, "nitroqueen", "that plane is a whole vibe"),
            new Line(Kind.Tip, "lowkey_lou", "spend it on the pink wrap"),
            new Line(Kind.Rumour, null, "the league is moving to the toll road at midnight"),
            new Line(Kind.Chat, "pitwall_pete", "tier 3 drift into the tunnel or nothing"),
            new Line(Kind.Chat, "slim_m", "remember, chat. we never saw anything"),
            new Line(Kind.Chat, "boostbabe", "garage has new parts btw"),
            new Line(Kind.News, null, "Vex claims the Alpine Pass record."),
            new Line(Kind.Chat, "gridghost", "phobos would eat that car alive"),
            new Line(Kind.Chat, "rollcage_rita", "chain knock or uninstall"),
            new Line(Kind.Tip, "anon_7741", "keep the stream alive"),
            new Line(Kind.Chat, "drift_dad", "rain on the strip tonight, perfect for sliding"),
            new Line(Kind.Rumour, null, "someone saw a second plane over the glacier"),
            new Line(Kind.Chat, "kaito", "the rookie drives like they stole it. respect"),
            new Line(Kind.Chat, "pitwall_pete", "camera 2 is crooked again slim"),
        };

        readonly List<GameObject> rows = new List<GameObject>();
        readonly List<CanvasGroup> fading = new List<CanvasGroup>();
        int next;
        float wait, viewers, viewerTarget, retarget;

        void Start()
        {
            viewers = viewerTarget = baseViewers;
            // Start with a screen of history so it never looks empty.
            for (int i = 0; i < 5; i++) Post(pool[next++ % pool.Count], true);
            wait = Random.Range(gap.x, gap.y);
            RefreshHype();
        }

        void OnEnable() { RefreshHype(); ShiftRush.Core.PlayerProfile.Changed += RefreshHype; }
        void OnDisable() { ShiftRush.Core.PlayerProfile.Changed -= RefreshHype; }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            wait -= dt;
            if (wait <= 0f)
            {
                Post(pool[next++ % pool.Count], false);
                wait = Random.Range(gap.x, gap.y);
            }

            for (int i = fading.Count - 1; i >= 0; i--)
            {
                if (fading[i] == null) { fading.RemoveAt(i); continue; }
                fading[i].alpha = Mathf.MoveTowards(fading[i].alpha, 1f, dt * 3f);
                if (fading[i].alpha >= 1f) fading.RemoveAt(i);
            }

            retarget -= dt;
            if (retarget <= 0f) { viewerTarget = Mathf.Max(1000f, viewerTarget + Random.Range(-600f, 900f)); retarget = Random.Range(1.5f, 3.5f); }
            viewers = Mathf.Lerp(viewers, viewerTarget, 1f - Mathf.Exp(-dt * 1.5f));
            if (viewersText != null) viewersText.text = (viewers / 1000f).ToString("0.0") + "K WATCHING";
        }

        public void RefreshHype()
        {
            if (hypeText != null) hypeText.text = ShiftRush.Core.PlayerProfile.Hype.ToString("N0") + " HYPE";
        }

        /// <summary>A message from the channel itself, e.g. when a locked menu entry is picked.</summary>
        public void PostSystem(string text) { Post(new Line(Kind.System, "CH13", text), false); }

        void Post(Line line, bool instant)
        {
            if (messages == null) return;
            var row = new GameObject(line.kind + " Line", typeof(RectTransform), typeof(CanvasGroup));
            row.transform.SetParent(messages, false);
            var group = row.GetComponent<CanvasGroup>();
            group.alpha = instant ? 1f : 0f;
            if (!instant) fading.Add(group);

            bool boxed = line.kind == Kind.Tip || line.kind == Kind.News || line.kind == Kind.System;
            var layout = row.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = boxed ? new RectOffset(12, 12, 6, 7) : new RectOffset(0, 0, 0, 0);
            layout.spacing = 1f;
            if (boxed)
            {
                var bg = row.AddComponent<Image>();
                bg.raycastTarget = false;
                bg.color = line.kind == Kind.Tip ? new Color32(42, 33, 6, 255)
                         : line.kind == Kind.News ? new Color32(42, 21, 32, 255)
                         : new Color32(40, 14, 34, 255);
            }

            switch (line.kind)
            {
                case Kind.Chat:
                    Text(row, "<b><color=" + Colour(line.user) + ">" + line.user + "</color></b>  <color=#BDB7D1>" + line.text + "</color>", bodyFont, 25f);
                    break;
                case Kind.Tip:
                    Text(row, "<b><color=#FFC53D>" + line.user + "</color></b>  <size=19><color=#FFC53D>TIPPED " + Random.Range(1, 11) * 50 + " HYPE</color></size>", bodyFont, 25f);
                    Text(row, line.text, bodyFont, 24f, new Color32(244, 241, 250, 255));
                    break;
                case Kind.Rumour:
                    Text(row, "RUMOUR  ·  " + line.text, monoFont, 17f, new Color32(140, 134, 163, 255));
                    break;
                case Kind.News:
                    Text(row, "NEWS", monoFont, 15f, new Color32(255, 107, 122, 255));
                    Text(row, line.text, bodyFont, 25f, new Color32(244, 241, 250, 255));
                    break;
                case Kind.System:
                    Text(row, "CH13", monoFont, 15f, new Color32(255, 46, 154, 255));
                    Text(row, line.text, bodyFont, 24f, new Color32(244, 241, 250, 255));
                    break;
            }

            rows.Add(row);
            while (rows.Count > maxLines) { Destroy(rows[0]); rows.RemoveAt(0); }
        }

        TMP_Text Text(GameObject row, string text, TMP_FontAsset font, float size, Color? colour = null)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(row.transform, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = font;
            t.fontSize = size;
            t.text = text;
            t.color = colour ?? Color.white;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.richText = true;
            return t;
        }

        static string Colour(string user)
        {
            int h = 0;
            foreach (char c in user) h = h * 31 + c;
            return NameColours[Mathf.Abs(h) % NameColours.Length];
        }
    }
}
