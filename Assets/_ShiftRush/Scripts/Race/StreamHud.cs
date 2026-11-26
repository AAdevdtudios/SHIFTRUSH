using System.Collections.Generic;
using ShiftRush.Core;
using ShiftRush.Vehicles;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftRush.Race
{
    /// <summary>
    /// The stream half of the race HUD (Channel 13 is broadcasting your race):
    /// the viewer count, a Hype streak that builds with style and cools when you drive dull, chat lines that pop
    /// up after big moves, move callouts with their Hype, the drift tier meter, roll cooldowns, the power-up
    /// slots, and the odd fan dare. Hype earned in a race is banked when you finish and shown on the results.
    /// Lives on SR_RaceHUD next to RaceHUD, built by SHIFT RUSH → Build Race HUD.
    /// </summary>
    public class StreamHud : MonoBehaviour
    {
        public const string HypeKey = "GripPro.Hype";

        [Header("Stream corner")]
        [SerializeField] TMP_Text viewersText;
        [SerializeField] TMP_Text streakText;
        [SerializeField] Image[] streakSegments;
        [SerializeField] TMP_Text sessionHypeText;
        [SerializeField] RectTransform chatParent;
        [SerializeField] TMP_FontAsset chatFont;

        [Header("Callout")]
        [SerializeField] CanvasGroup calloutGroup;
        [SerializeField] RectTransform calloutBody;
        [SerializeField] TMP_Text calloutTitle;
        [SerializeField] TMP_Text calloutSub;

        [Header("Drift meter")]
        [SerializeField] CanvasGroup driftGroup;
        [SerializeField] TMP_Text driftTitle;
        [SerializeField] TMP_Text driftInfo;
        [SerializeField] RectTransform[] driftFills = new RectTransform[3];

        [Header("Cooldowns (roll, forward roll, drift)")]
        [SerializeField] RectTransform[] cooldownFills = new RectTransform[3];
        [SerializeField] Image[] cooldownFillImages = new Image[3];
        [SerializeField] TMP_Text[] cooldownStatus = new TMP_Text[3];

        [Header("Fan dare")]
        [SerializeField] CanvasGroup dareGroup;
        [SerializeField] TMP_Text dareFrom, dareTitle, dareReward, dareTimer, dareVotes;
        [SerializeField] RectTransform dareTimerFill;

        [Header("Results")]
        [Tooltip("Everything live (stream corner, callouts, dash, power-ups): faded out when the results come up.")]
        [SerializeField] CanvasGroup liveLayer;
        [SerializeField] TMP_Text payoutHeadline;
        [SerializeField] TMP_Text payoutLines;
        [SerializeField] TMP_Text payoutTotal;
        [SerializeField] TMP_Text payoutBalance;
        [SerializeField] TMP_Text clipTag;
        [SerializeField] TMP_Text peakViewersText;
        [Tooltip("Top three at the finish: name and gap, the player's row inverted.")]
        [SerializeField] TMP_Text[] standingNames = new TMP_Text[3];
        [SerializeField] TMP_Text[] standingGaps = new TMP_Text[3];
        [SerializeField] TMP_Text[] standingRanks = new TMP_Text[3];
        [SerializeField] Image[] standingBacks = new Image[3];
        [SerializeField] TMP_Text[] resultChat = new TMP_Text[2];

        [Header("Colours")]
        [SerializeField] Color pink = new Color32(255, 46, 154, 255);
        [SerializeField] Color cyan = new Color32(46, 242, 255, 255);
        [SerializeField] Color gold = new Color32(255, 197, 61, 255);
        [SerializeField] Color ready = new Color32(124, 242, 154, 255);
        [SerializeField] Color off = new Color32(42, 37, 64, 255);

        // Hype per move, before the streak multiplier.
        static readonly int[] DriftTierHype = { 0, 100, 220, 380 };
        const int KnockHype = 150, LandingHype = 40, PlaceHype = 1200;

        RaceManager race;
        GripMoves grip;
        DriftMoves drift;
        IVehicle vehicle;

        int sessionHype, driftHype, knockHype, rollHype, dareHype, streakHype, dares;
        int driftCount, bestTier, knockCount, bestChain, landingCount, daresDone;
        float peakViewers;
        float streak = 1f, streakPeak = 1f, coolOff, viewers = 48200f, viewersTarget = 48200f;
        float calloutUntil, calloutPunch;
        bool banked;

        // Dares.
        enum DareKind { None, DriftTier3, Knock, CleanLandings, TopSpeed, Rings }
        FanRings rings;
        DareKind dare;
        float dareEnds, dareNext, dareProgress, dareSince;
        int dareCount, dareGoal, dareReward01;
        string dareResult; float dareResultUntil;

        readonly List<(CanvasGroup g, float born)> chat = new List<(CanvasGroup, float)>();

        static readonly string[] Users = { "slim_m", "nitroqueen", "gridghost", "rollcage_rita", "drift_dad", "kaito", "pitwall_pete", "boostbabe", "tetsuo_stan", "lowkey_lou" };
        static readonly string[] NameColours = { "#2EF2FF", "#FF2E9A", "#FFC53D", "#7CF29A", "#B9A4FF", "#FF8A5B" };
        static readonly string[] DriftLines = { "THAT SLIDE", "tyres are screaming", "SIDEWAYS SIDEWAYS", "clip it clip it", "tier 3 or uninstall" };
        static readonly string[] KnockLines = { "SENT HIM", "chain it chain it", "bonk", "no mercy on channel 13", "insurance is crying" };
        static readonly string[] RollLines = { "THE ROLL", "stuck the landing", "barrel roll merchant", "clean", "how is that legal" };
        static readonly string[] DareLines = { "do the dare", "chat voted, go", "we believe", "easy money" };

        void Awake()
        {
            var line = FindFirstObjectByType<RaceLine>();
            if (line != null) race = line.GetComponent<RaceManager>();
            if (calloutGroup) calloutGroup.alpha = 0f;
            if (dareGroup) dareGroup.alpha = 0f;
            if (driftGroup) driftGroup.alpha = 0f;
            dareNext = Random.Range(25f, 40f);
        }

        void OnEnable()
        {
            DriftMoves.TierUp += OnTierUp;
            DriftMoves.Ended += OnDriftEnded;
            KnockTumble.Knocked += OnKnocked;
            GripMoves.Landed += OnLanded;
            if (race != null) race.Finished += OnFinished;
        }

        void OnDisable()
        {
            DriftMoves.TierUp -= OnTierUp;
            DriftMoves.Ended -= OnDriftEnded;
            KnockTumble.Knocked -= OnKnocked;
            GripMoves.Landed -= OnLanded;
            if (race != null) race.Finished -= OnFinished;
        }

        void FindPlayer()
        {
            if (grip != null && drift != null) return;
            foreach (var g in FindObjectsByType<GripMoves>(FindObjectsSortMode.None)) if (g.IsPlayer) { grip = g; break; }
            foreach (var d in FindObjectsByType<DriftMoves>(FindObjectsSortMode.None)) if (d.IsPlayer) { drift = d; break; }
            if (vehicle == null)
                foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                    if (mb is IVehicle v && v.PlayerDriven) { vehicle = v; break; }
        }

        bool Racing { get { return race == null || race.CurrentState == RaceManager.State.Racing; } }
        float RaceClock { get { return race != null ? race.RaceTime : Time.timeSinceLevelLoad; } }

        // ------------------------------------------------------------------ events

        void OnTierUp(DriftMoves d, int tier)
        {
            if (d != drift) return;
            Callout("DRIFT TIER " + tier, null, false);
            if (tier >= 3) Chat(DriftLines);
            if (dare == DareKind.DriftTier3 && tier >= 3) DareDone();
        }

        void OnDriftEnded(DriftMoves d, int tier, float held)
        {
            if (d != drift || tier <= 0) return;
            driftCount++; bestTier = Mathf.Max(bestTier, tier);
            int h = Award(DriftTierHype[Mathf.Clamp(tier, 0, 3)], ref driftHype, 0.35f * tier);
            Callout(tier >= 3 ? "MONSTER DRIFT" : "DRIFT", "+" + h + " HYPE", true);
            if (tier >= 2) Chat(DriftLines);
        }

        void OnKnocked(GameObject attacker, GameObject victim, float damage, int chain)
        {
            if (grip == null || attacker != grip.gameObject) return;
            knockCount++; bestChain = Mathf.Max(bestChain, chain);
            int h = Award(KnockHype * chain, ref knockHype, 0.4f * chain);
            Callout(chain > 1 ? "CHAIN KNOCK x" + chain : "KNOCK", "+" + h + " HYPE", true);
            Chat(KnockLines);
            if (dare == DareKind.Knock) DareDone();
        }

        void OnLanded(GripMoves m, bool clean, float air)
        {
            if (m != grip || !clean) return;
            landingCount++;
            int h = Award(LandingHype, ref rollHype, 0.2f);
            Callout("CLEAN LANDING", "+" + h + " HYPE", true);
            if (Random.value < 0.4f) Chat(RollLines);
            if (dare == DareKind.CleanLandings) { dareCount++; if (dareCount >= dareGoal) DareDone(); }
        }

        /// <summary>Adds Hype for a move with the current streak, and builds the streak.</summary>
        int Award(int baseHype, ref int bucket, float streakGain)
        {
            int h = Mathf.RoundToInt(baseHype * streak);
            bucket += h;
            sessionHype += h;
            streakHype += h - baseHype;
            streak = Mathf.Min(3f, streak + streakGain);
            streakPeak = Mathf.Max(streakPeak, streak);
            coolOff = 0f;
            viewersTarget += h * 6f;
            return h;
        }

        void Callout(string title, string sub, bool punch)
        {
            if (calloutTitle) calloutTitle.text = title;
            if (calloutSub) { calloutSub.text = sub ?? ""; calloutSub.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(sub)); } // the chip behind it too
            calloutUntil = Time.unscaledTime + 1.6f;
            if (punch) calloutPunch = 1f;
        }

        void Chat(string[] lines)
        {
            if (chatParent == null) return;
            string user = Users[Random.Range(0, Users.Length)];
            string colour = NameColours[Mathf.Abs(user.GetHashCode()) % NameColours.Length];
            var go = new GameObject("Chat", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(chatParent, false);
            var img = go.GetComponent<Image>();
            img.color = new Color32(11, 10, 18, 205);
            img.raycastTarget = false;
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 5, 6);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            var fit = go.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var t = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            t.transform.SetParent(go.transform, false);
            t.font = chatFont;
            t.fontSize = 22;
            t.raycastTarget = false;
            t.text = "<b><color=" + colour + ">" + user + "</color></b>  <color=#F4F1FA>" + lines[Random.Range(0, lines.Length)] + "</color>";
            go.transform.SetAsFirstSibling(); // newest on top
            var g = go.GetComponent<CanvasGroup>();
            g.alpha = 0f;
            chat.Add((g, Time.unscaledTime));
            while (chat.Count > 3) { if (chat[0].g) Destroy(chat[0].g.gameObject); chat.RemoveAt(0); }
        }

        // ------------------------------------------------------------------ dares

        void StartDare()
        {
            dares++;
            dareCount = 0;
            // Rings come up most: the crowd loves them, and they're a dare you can see on the road.
            var options = new List<DareKind> { DareKind.Rings, DareKind.Rings, DareKind.DriftTier3, DareKind.Knock, DareKind.CleanLandings, DareKind.TopSpeed };
            dare = options[Random.Range(0, options.Count)];
            if (dare == DareKind.Rings)
            {
                rings = race != null && vehicle != null ? FanRings.Spawn(race.Line, vehicle, 5) : null;
                if (rings == null) dare = DareKind.DriftTier3;
            }
            float time = 20f;
            string title;
            switch (dare)
            {
                case DareKind.DriftTier3: title = "HIT DRIFT TIER 3"; dareReward01 = 600; break;
                case DareKind.Knock: title = "KNOCK A RIVAL"; dareReward01 = 500; break;
                case DareKind.CleanLandings: title = "LAND 2 CLEAN ROLLS"; dareGoal = 2; dareReward01 = 450; break;
                case DareKind.Rings: title = "THREAD 4 OF 5 RINGS"; dareGoal = 4; dareReward01 = 550; time = 30f; break;
                default: title = "HOLD " + Mathf.RoundToInt(GameSettings.ToDisplaySpeed(180f)) + "+ " + GameSettings.SpeedUnit + " FOR 3 S"; dareReward01 = 400; time = 25f; break;
            }
            dareEnds = Time.unscaledTime + time;
            dareProgress = 0f;
            dareSince = Time.unscaledTime;
            if (dareFrom) dareFrom.text = "<b><color=#2EF2FF>" + Users[Random.Range(0, Users.Length)] + "</color></b> asks";
            if (dareTitle) dareTitle.text = title;
            if (dareReward) dareReward.text = "+" + dareReward01 + " HYPE";
            if (dareVotes) dareVotes.text = Random.Range(800, 4200).ToString("N0") + " VOTES";
            Chat(DareLines);
        }

        /// <summary>The rings fade out a moment after their dare is over.</summary>
        void ClearRings()
        {
            if (rings != null) Destroy(rings.gameObject, 1.5f);
            rings = null;
        }

        void DareDone()
        {
            if (dare == DareKind.None) return;
            dare = DareKind.None;
            ClearRings();
            daresDone++;
            dareHype += dareReward01;
            sessionHype += dareReward01;
            viewersTarget += 4000f;
            Callout("DARE DONE", "+" + dareReward01 + " HYPE", true);
            dareResult = "DONE"; dareResultUntil = Time.unscaledTime + 1.5f;
            dareNext = RaceClock + Random.Range(40f, 60f);
        }

        void UpdateDare(float dt)
        {
            bool finalLap = race != null && race.Player != null && race.Player.lap >= race.TotalLaps;
            float raceTime = RaceClock;
            if (dare == DareKind.None)
            {
                if (dares < 2 && Racing && !finalLap && raceTime > 20f && raceTime > dareNext)
                    StartDare();
            }
            else
            {
                if (dare == DareKind.TopSpeed && vehicle != null)
                {
                    dareProgress = Mathf.Abs(vehicle.SpeedKmh) >= 180f ? dareProgress + dt : 0f;
                    if (dareProgress >= 3f) DareDone();
                }
                if (dare == DareKind.Rings && rings != null)
                {
                    if (rings.Passed >= dareGoal) DareDone();
                    else if (rings.Behind || rings.Missed > rings.Count - dareGoal) dareEnds = Mathf.Min(dareEnds, Time.unscaledTime); // can't make it now
                    if (dareVotes && dare == DareKind.Rings) dareVotes.text = rings.Passed + " / " + rings.Count + " RINGS";
                }
                if (dare != DareKind.None && Time.unscaledTime > dareEnds)
                {
                    ClearRings();
                    dare = DareKind.None; // missed: nothing lost
                    dareResult = "MISSED"; dareResultUntil = Time.unscaledTime + 1.2f;
                    dareNext = raceTime + Random.Range(35f, 55f);
                }
            }

            if (dareGroup == null) return;
            bool show = dare != DareKind.None || Time.unscaledTime < dareResultUntil;
            dareGroup.alpha = Mathf.MoveTowards(dareGroup.alpha, show ? 1f : 0f, dt * 4f);
            if (dare != DareKind.None)
            {
                float left = Mathf.Max(0f, dareEnds - Time.unscaledTime);
                if (dareTimer) dareTimer.text = "0:" + Mathf.CeilToInt(left).ToString("00");
                if (dareTimerFill) dareTimerFill.anchorMax = new Vector2(Mathf.Clamp01(left / Mathf.Max(1f, dareEnds - dareSince)), 1f);
            }
            else if (show && dareTimer) dareTimer.text = dareResult;
        }

        // ------------------------------------------------------------------ results

        void OnFinished(RaceManager.Racer r)
        {
            if (!r.isPlayer || banked) return;
            banked = true;
            ClearRings();
            int place = race.PositionOf(r);
            int placeHype = Mathf.RoundToInt(PlaceHype * Mathf.Clamp01(1f - (place - 1) / Mathf.Max(1f, race.Racers.Count)));
            sessionHype += placeHype;
            // Story mode: close the episode if the place is good enough (drifts tilt the Feed GLOSS, knocks GRIT).
            var story = ShiftRush.Core.StoryProgress.Report(place, driftHype, knockHype);
            sessionHype += story.payout;
            ShiftRush.Core.PlayerProfile.AddHype(sessionHype);
            int balance = ShiftRush.Core.PlayerProfile.Hype;

            if (payoutLines)
                payoutLines.text =
                    Line("FINISHED " + place + Suffix(place), null, placeHype) +
                    Line("DRIFTS", driftCount > 0 ? driftCount + ", best tier " + bestTier : null, driftHype) +
                    Line("KNOCKS", knockCount > 0 ? knockCount + (bestChain > 1 ? ", one chain of " + bestChain : "") : null, knockHype) +
                    Line("ROLLS AND CLEAN LANDINGS", landingCount > 0 ? landingCount.ToString() : null, rollHype) +
                    Line("FAN DARES", dares > 0 ? daresDone + " of " + dares : null, dareHype) +
                    Line("VIEWER STREAK", "x" + streakPeak.ToString("0.0") + " peak", streakHype) +
                    (story.story ? Line(story.closed ? "EPISODE " + story.episode.number.ToString("00") + " CLOSED" : "EPISODE " + story.episode.number.ToString("00") + " STILL OPEN",
                        story.closed ? (story.alreadyClosed ? "replay" : story.episode.title) : "needed " + ShiftRush.UI.CaseBoardPanel.Goal(story.episode).ToLowerInvariant(), story.payout) : "");

            // The clip tag, peak viewers, the top three and two chat reactions.
            // The track actually raced (by scene), not just the last one picked in the menu.
            ShiftRush.Core.RaceCatalog.Track track = null;
            var cat = ShiftRush.Core.RaceCatalog.Instance;
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (cat != null) foreach (var t in cat.tracks) if (t.scene == scene) { track = t; break; }
            if (clipTag) clipTag.text = "CLIP SAVED  //  " + (track != null ? track.displayName.ToUpperInvariant() : UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.ToUpperInvariant()) + "  //  " + Difficulty.Label;
            if (peakViewersText) peakViewersText.text = (peakViewers / 1000f).ToString("0.0") + "K";
            var leader = race.Racers.Count > 0 ? race.Racers[0] : null;
            for (int i = 0; i < 3; i++)
            {
                bool has = i < race.Racers.Count;
                if (standingBacks[i]) standingBacks[i].gameObject.SetActive(has);
                if (!has) continue;
                var o = race.Racers[i];
                if (standingRanks[i]) standingRanks[i].text = (i + 1).ToString();
                if (standingNames[i]) standingNames[i].text = o.isPlayer ? "YOU" : o.name.ToUpperInvariant();
                string gap = i == 0 ? RaceManager.FormatTime(o.finished ? o.finishTime : race.RaceTime)
                    : o.finished && leader != null && leader.finished ? "+" + (o.finishTime - leader.finishTime).ToString("0.00") : "RACING";
                if (standingGaps[i]) standingGaps[i].text = gap;
                if (standingBacks[i]) standingBacks[i].color = o.isPlayer ? new Color32(11, 10, 18, 255) : new Color(0, 0, 0, 0);
                var c = o.isPlayer ? new Color32(244, 241, 250, 255) : new Color32(11, 10, 18, 255);
                if (standingNames[i]) standingNames[i].color = c;
                if (standingGaps[i]) standingGaps[i].color = c;
                if (standingRanks[i]) standingRanks[i].color = c;
            }
            string[] good = { "clip of the night. cops never had a chance", "that tier 3 into the tunnel", "the rookie is HIM", "running it back, chat?" };
            string[] meh = { "the stream keeps rolling, next one", "tyres were cold, we move", "rookie mistakes, still entertaining", "run it back" };
            var pool = place <= 3 ? good : meh;
            string[] who = { "slim_m", "nitroqueen", "kaito", "gridghost" };
            for (int i = 0; i < resultChat.Length; i++)
                if (resultChat[i]) resultChat[i].text = "<b><color=" + (i == 0 ? "#C4157A" : "#2EF2FF") + ">" + who[(place + i) % who.Length] + "</color></b>  " + pool[(place * 3 + i) % pool.Length];
            if (payoutTotal) payoutTotal.text = "+" + sessionHype.ToString("N0");
            if (payoutHeadline)
                payoutHeadline.text = story.story ? (story.closed ? "EP " + story.episode.number.ToString("00") + " CLOSED. KOWAL ADDS A PHOTO." : "EP " + story.episode.number.ToString("00") + " STILL OPEN. RUN IT BACK.")
                    : place == 1 ? "YOU OUTRAN THE LEAGUE" : place <= 3 ? "ON THE PODIUM. CHAT IS LOUD." : "THE STREAM KEEPS ROLLING";
            if (payoutBalance) payoutBalance.text = "BALANCE  " + balance.ToString("N0") + " HYPE";
        }

        static string Line(string label, string detail, int value)
        {
            return label + (string.IsNullOrEmpty(detail) ? "" : "  <size=70%><color=#8C86A3>" + detail + "</color></size>")
                + "<pos=82%><color=#FFC53D>+" + value.ToString("N0") + "</color>\n";
        }

        static string Suffix(int n)
        {
            return (n % 100 >= 11 && n % 100 <= 13) ? "TH" : (n % 10 == 1 ? "ST" : n % 10 == 2 ? "ND" : n % 10 == 3 ? "RD" : "TH");
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            FindPlayer();
            if (liveLayer) liveLayer.alpha = Mathf.MoveTowards(liveLayer.alpha, banked ? 0f : 1f, dt * 3f);

            // Streak cools off after a few seconds of dull driving.
            coolOff += dt;
            if (coolOff > 6f) streak = Mathf.MoveTowards(streak, 1f, dt * 0.25f);
            if (streakText) streakText.text = "x" + streak.ToString("0.0") + " STREAK";
            if (streakSegments != null)
            {
                float lit = (streak - 1f) / 2f * streakSegments.Length;
                for (int i = 0; i < streakSegments.Length; i++)
                    if (streakSegments[i]) streakSegments[i].color = i + 1 <= lit ? gold : (i < lit ? new Color(gold.r, gold.g, gold.b, 0.45f) : off);
            }
            if (sessionHypeText) sessionHypeText.text = "+" + sessionHype.ToString("N0");

            viewersTarget = Mathf.Max(20000f, viewersTarget - dt * 120f);
            viewers = Mathf.Lerp(viewers, viewersTarget, 1f - Mathf.Exp(-dt * 1.2f));
            peakViewers = Mathf.Max(peakViewers, viewers);
            if (viewersText) viewersText.text = (viewers / 1000f).ToString("0.0") + "K";

            // Chat lines fade in, then out after five seconds.
            for (int i = chat.Count - 1; i >= 0; i--)
            {
                var c = chat[i];
                if (c.g == null) { chat.RemoveAt(i); continue; }
                float age = Time.unscaledTime - c.born;
                c.g.alpha = age < 0.2f ? age / 0.2f : age > 5f ? Mathf.Max(0f, 1f - (age - 5f) * 2f) : 1f;
                if (age > 5.6f) { Destroy(c.g.gameObject); chat.RemoveAt(i); }
            }

            // Callout punches in and fades.
            calloutPunch = Mathf.MoveTowards(calloutPunch, 0f, dt * 4f);
            if (calloutGroup) calloutGroup.alpha = Mathf.MoveTowards(calloutGroup.alpha, Time.unscaledTime < calloutUntil ? 1f : 0f, dt * 5f);
            if (calloutBody) calloutBody.localScale = Vector3.one * (1f + 0.18f * calloutPunch * calloutPunch);

            UpdateDrift(dt);
            UpdateCooldowns();
            UpdateDare(dt);
        }

        void UpdateDrift(float dt)
        {
            if (driftGroup == null) return;
            bool on = drift != null && drift.Drifting;
            driftGroup.alpha = Mathf.MoveTowards(driftGroup.alpha, on ? 1f : 0f, dt * (on ? 8f : 3f));
            if (drift == null) return;
            if (driftTitle) driftTitle.text = drift.Tier > 0 ? "DRIFT TIER " + drift.Tier : "DRIFT";
            if (driftInfo) driftInfo.text = Mathf.RoundToInt(drift.SlipAngle) + "°";
            for (int i = 0; i < driftFills.Length; i++)
            {
                if (driftFills[i] == null) continue;
                float f = i < drift.Tier ? 1f : i == drift.Tier ? drift.NextTier01 : 0f;
                driftFills[i].anchorMax = new Vector2(Mathf.Clamp01(f), 1f);
            }
        }

        void UpdateCooldowns()
        {
            float roll = grip != null ? grip.LeapReady01 : 1f;
            bool busy = grip != null && grip.Leaping;
            for (int i = 0; i < 2; i++) SetChip(i, busy ? 0f : roll, busy ? "AIR" : roll >= 1f ? "READY" : ((1f - roll) * (grip != null ? grip.leapCooldown : 1f)).ToString("0.0") + "s");
            if (drift != null)
            {
                bool d = drift.Drifting;
                SetChip(2, d ? drift.NextTier01 : 1f, d ? "T" + drift.Tier : "READY");
            }
        }

        void SetChip(int i, float fill, string status)
        {
            if (i >= cooldownFills.Length) return;
            bool full = fill >= 1f && (status == "READY");
            if (cooldownFills[i]) cooldownFills[i].anchorMax = new Vector2(Mathf.Clamp01(fill), 1f);
            if (cooldownFillImages[i]) cooldownFillImages[i].color = full ? ready : (status.StartsWith("T") ? cyan : gold);
            if (cooldownStatus[i]) { cooldownStatus[i].text = status; cooldownStatus[i].color = full ? ready : (status.StartsWith("T") ? cyan : gold); }
        }
    }
}
