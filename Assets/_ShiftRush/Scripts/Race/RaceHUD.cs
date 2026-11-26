using ShiftRush.Core;
using ShiftRush.Vehicles;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShiftRush.Race
{
    /// <summary>
    /// The in-race HUD (Resources/SR_RaceHUD, built by SHIFT RUSH → Build Race HUD). Spawns itself in every
    /// scene with a car except the menu. On a circuit (scene has a <see cref="RaceLine"/>) it starts a
    /// <see cref="RaceManager"/> and shows position, laps, times, minimap, countdown and the results screen;
    /// in free roam it shows just the speedometer.
    /// </summary>
    public class RaceHUD : MonoBehaviour
    {
        [System.Serializable]
        public class LeaderRow
        {
            public GameObject root;
            public Image highlight;
            public TMP_Text rank, name, gap;
        }

        const string MenuScene = "MainMenu";

        [Header("Race panels")]
        [SerializeField] CanvasGroup raceGroup;
        [SerializeField] GameObject freeRoamTag;
        [SerializeField] TMP_Text positionValue;
        [SerializeField] TMP_Text lapValue;
        [SerializeField] TMP_Text timeValue, lapTimeValue, bestValue;
        [SerializeField] LeaderRow[] rows;

        [Header("Centre")]
        [SerializeField] TMP_Text countdownText;
        [SerializeField] Image countdownRing;
        [SerializeField] CanvasGroup messageGroup;
        [SerializeField] TMP_Text messageText;

        [Header("Speedometer")]
        [SerializeField] TMP_Text speedValue;
        [SerializeField] TMP_Text unitText;
        [SerializeField] TMP_Text gearText;
        [SerializeField] Image rpmFill;
        [SerializeField] RectTransform nitroFill;
        [SerializeField] Image nitroFillImage;
        [SerializeField] GameObject nitroGroup;
        [Tooltip("Share of the full circle the RPM arc covers.")]
        [SerializeField] float rpmArc = 0.75f;

        [Header("Minimap")]
        [Tooltip("Soft-masked window the map is drawn in.")]
        [SerializeField] RectTransform mapView;
        [Tooltip("The edge-fade mask on the map window (on in Local, off in Full so the whole circuit shows).")]
        [SerializeField] Behaviour mapFade;
        [SerializeField] Image mapImage;
        [SerializeField] RectTransform mapDot;
        [Tooltip("Sprite for rival dots on the map (tinted with each rival's colour).")]
        [SerializeField] Sprite rivalDot;
        [SerializeField] float rivalDotSize = 15f;
        [Tooltip("Local mode: metres of track visible across the window.")]
        [SerializeField] float localSpan = 480f;
        [Tooltip("Full mode: share of the window the circuit fills.")]
        [SerializeField] float fullFill = 0.86f;

        [Header("Results")]
        [SerializeField] CanvasGroup finishGroup;
        [SerializeField] TMP_Text finishPosition, finishTotal, finishBest, finishLaps;

        [Header("Sound")]
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip beep, go, lap;

        [Header("Colours")]
        [SerializeField] Color accent = new Color32(255, 64, 217, 255);
        [SerializeField] Color steel = new Color32(154, 160, 180, 255);

        RaceManager race;
        IVehicle vehicle;
        IVehicleDash dash;
        float shownSpeed, countdownPunch, tickTime, messageUntil, finishShownAt = -1f;
        int lastCount = -1;
        bool hasMap;
        readonly System.Collections.Generic.Dictionary<RaceManager.Racer, RectTransform> dots = new System.Collections.Generic.Dictionary<RaceManager.Racer, RectTransform>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded += (s, m) => { if (m == LoadSceneMode.Single) Spawn(s); };
            Spawn(SceneManager.GetActiveScene());
        }

        static void Spawn(Scene scene)
        {
            if (scene.name == MenuScene || FindFirstObjectByType<RaceHUD>() != null) return;
            PlayerCarSwap.Apply(scene); // the garage's car onto the grid first
            bool hasCar = false;
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)) if (mb is IVehicle) { hasCar = true; break; }
            if (!hasCar) return;
            var prefab = Resources.Load<RaceHUD>("SR_RaceHUD");
            if (prefab == null) return;
            var hud = Instantiate(prefab);
            hud.name = "SR_RaceHUD";
            SceneManager.MoveGameObjectToScene(hud.gameObject, scene);
        }

        void Awake()
        {
            var line = FindFirstObjectByType<RaceLine>();
            if (line != null)
            {
                race = line.GetComponent<RaceManager>();
                if (race == null) race = line.gameObject.AddComponent<RaceManager>();
            }
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (mb is IVehicle v) { vehicle = v; dash = mb as IVehicleDash; break; }

            if (raceGroup != null) raceGroup.alpha = race != null ? 1f : 0f;
            if (freeRoamTag != null) freeRoamTag.SetActive(race == null);
            if (finishGroup != null) { finishGroup.alpha = 0f; finishGroup.blocksRaycasts = false; }
            if (messageGroup != null) messageGroup.alpha = 0f;
            if (countdownText != null) countdownText.text = "";
            if (countdownRing != null) countdownRing.enabled = false;
            if (mapImage != null && line != null && line.mapSprite != null) mapImage.sprite = line.mapSprite;
            hasMap = line != null && line.mapSprite != null;
            if (mapView != null) mapView.gameObject.SetActive(hasMap);
        }

        void OnEnable()
        {
            if (race == null) return;
            race.CountdownTick += OnCountdown;
            race.LapCompleted += OnLap;
            race.FinalLap += OnFinalLap;
            race.Finished += OnFinished;
        }

        void OnDisable()
        {
            if (race == null) return;
            race.CountdownTick -= OnCountdown;
            race.LapCompleted -= OnLap;
            race.FinalLap -= OnFinalLap;
            race.Finished -= OnFinished;
        }

        void OnCountdown(int n)
        {
            lastCount = n;
            countdownPunch = 1f;
            tickTime = Time.unscaledTime;
            if (countdownText != null)
            {
                countdownText.text = n > 0 ? n.ToString() : "GO";
                countdownText.color = n > 0 ? Color.white : accent;
            }
            Play(n > 0 ? beep : go, n > 0 ? 0.8f : 1f);
        }

        void OnLap(RaceManager.Racer r, float time, bool best)
        {
            if (!r.isPlayer || r.finished) return;
            Show(best ? "NEW BEST LAP  " + RaceManager.FormatTime(time) : "LAP  " + RaceManager.FormatTime(time), 2.2f);
            Play(lap, 0.9f);
        }

        void OnFinalLap(RaceManager.Racer r)
        {
            if (!r.isPlayer) return;
            Show("FINAL LAP", 2.4f);
        }

        void OnFinished(RaceManager.Racer r)
        {
            Show("FINISH", 1.6f);
            Play(go, 1f);
            if (r.vehicle != null) r.vehicle.SetControllable(false);
            finishShownAt = Time.unscaledTime + 1.6f;
            if (finishPosition != null) finishPosition.text = Ordinal(race.PositionOf(r));
            if (finishTotal != null) finishTotal.text = RaceManager.FormatTime(r.finishTime);
            if (finishBest != null) finishBest.text = RaceManager.FormatTime(race.BestLap);
            if (finishLaps != null)
            {
                float fastest = float.MaxValue;
                foreach (var t in r.lapTimes) fastest = Mathf.Min(fastest, t);
                var sb = new System.Text.StringBuilder();
                sb.Append("<color=#0B0A12>" + Difficulty.Label + "</color>\n");
                for (int i = 0; i < r.lapTimes.Count; i++)
                {
                    bool f = Mathf.Approximately(r.lapTimes[i], fastest);
                    sb.Append("<color=#6B1440>LAP " + (i + 1) + "</color>    ");
                    sb.Append(f ? "<color=#FFFFFF>" + RaceManager.FormatTime(r.lapTimes[i]) + "</color>" : RaceManager.FormatTime(r.lapTimes[i]));
                    if (i < r.lapTimes.Count - 1) sb.Append('\n');
                }
                finishLaps.text = sb.ToString();
            }
        }

        void Show(string text, float seconds)
        {
            if (messageText != null) messageText.text = text;
            messageUntil = Time.unscaledTime + seconds;
        }

        void Play(AudioClip clip, float volume)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip, volume);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            UpdateSpeedo(dt);
            if (race == null) return;

            var p = race.Player;
            if (positionValue != null && p != null)
                positionValue.text = race.PositionOf(p) + "<size=55%><color=#9AA0B4>/" + race.Racers.Count + "</color></size>";
            if (lapValue != null && p != null)
                lapValue.text = Mathf.Min(p.lap, race.TotalLaps) + "<size=55%><color=#9AA0B4>/" + race.TotalLaps + "</color></size>";
            if (timeValue != null) timeValue.text = RaceManager.FormatTime(race.RaceTime);
            if (lapTimeValue != null) lapTimeValue.text = RaceManager.FormatTime(race.CurrentLapTime(p));
            if (bestValue != null) bestValue.text = RaceManager.FormatTime(race.BestLap);
            UpdateRows();
            UpdateCountdown(dt);
            UpdateMessage(dt);
            UpdateMap();
            UpdateFinish(dt);
        }

        void UpdateSpeedo(float dt)
        {
            if (vehicle == null) return;
            float kmh = Mathf.Abs(vehicle.SpeedKmh);
            shownSpeed = Mathf.Lerp(shownSpeed, GameSettings.ToDisplaySpeed(kmh), 1f - Mathf.Exp(-dt * 12f));
            if (speedValue != null) speedValue.text = Mathf.RoundToInt(shownSpeed).ToString("000");
            if (unitText != null) unitText.text = GameSettings.SpeedUnit;
            if (dash == null) return;
            if (gearText != null) gearText.text = dash.GearLabel;
            if (rpmFill != null)
            {
                float rpm = dash.Rpm01;
                rpmFill.fillAmount = rpm * rpmArc;
                bool red = rpm > 0.88f;
                rpmFill.color = red && Mathf.Repeat(Time.unscaledTime * 8f, 1f) > 0.5f ? Color.white : accent;
            }
            float nitro = dash.Nitro01;
            if (nitroGroup != null) nitroGroup.SetActive(nitro >= 0f);
            if (nitroFill != null && nitro >= 0f)
            {
                nitroFill.anchorMax = new Vector2(Mathf.Clamp01(nitro), 1f);
                if (nitroFillImage != null) nitroFillImage.color = dash.NitroActive ? Color.white : accent;
            }
        }

        // The time the race first reached each 10 m of distance (that's the leader's timeline).
        readonly System.Collections.Generic.Dictionary<int, float> splits = new System.Collections.Generic.Dictionary<int, float>();
        int furthest = -1;

        int Bucket(RaceManager.Racer r)
        {
            // Behind the start line on the grid the lap position reads as nearly a full lap: count it as zero.
            float d = r.finished ? race.TotalLaps * race.Line.length : !r.started ? 0f : (r.lap - 1) * race.Line.length + r.progress;
            return Mathf.FloorToInt(d / 10f);
        }

        void RecordSplits()
        {
            if (race.CurrentState != RaceManager.State.Racing) return;
            foreach (var r in race.Racers)
            {
                int b = Bucket(r);
                while (furthest < b) { furthest++; splits[furthest] = Time.time; }
            }
        }

        void UpdateRows()
        {
            if (rows == null) return;
            RecordSplits();
            var leader = race.Racers.Count > 0 ? race.Racers[0] : null;
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                bool on = i < race.Racers.Count;
                row.root.SetActive(on);
                if (!on) continue;
                var r = race.Racers[i];
                row.rank.text = (i + 1).ToString();
                row.name.text = r.name;
                if (i == 0) row.gap.text = "LEADER";
                else if (r.finished && leader.finished) row.gap.text = "+" + (r.finishTime - leader.finishTime).ToString("0.0");
                else
                {
                    // Real time behind: how long ago the leader was where this car is now (not distance over this
                    // car's speed, which jumps about when it slows for a corner or crashes).
                    float t;
                    row.gap.text = splits.TryGetValue(Bucket(r), out t) ? "+" + Mathf.Max(0f, Time.time - t).ToString("0.0") : "";
                }
                if (row.highlight != null) row.highlight.enabled = r.isPlayer;
                row.name.color = r.isPlayer ? Color.white : steel;
            }
        }

        void UpdateCountdown(float dt)
        {
            if (countdownText == null) return;
            bool counting = race.CurrentState == RaceManager.State.Countdown && lastCount > 0;
            bool showing = counting || race.ShowGo;
            // Each number punches in big and settles; the ring drains over that second.
            countdownPunch = Mathf.MoveTowards(countdownPunch, 0f, dt * 3f);
            float e = countdownPunch * countdownPunch;
            float since = Time.unscaledTime - tickTime;
            countdownText.transform.localScale = Vector3.one * (1f + 0.5f * e);
            var c = countdownText.color; c.a = showing ? 1f : Mathf.MoveTowards(c.a, 0f, dt * 4f);
            countdownText.color = c;
            if (countdownRing != null)
            {
                countdownRing.enabled = showing || c.a > 0.01f;
                countdownRing.fillAmount = counting ? Mathf.Clamp01(1f - since) : 1f;
                var rc = lastCount > 0 ? Color.white : accent; rc.a = c.a * 0.9f;
                countdownRing.color = rc;
                countdownRing.transform.localScale = Vector3.one * (lastCount > 0 ? 1f + 0.08f * e : 1f + 0.35f * Mathf.Clamp01(since));
            }
        }

        void UpdateMessage(float dt)
        {
            if (messageGroup == null) return;
            if (race.WrongWay && race.CurrentState == RaceManager.State.Racing)
            {
                messageText.text = "WRONG WAY";
                messageGroup.alpha = Mathf.Repeat(Time.unscaledTime * 2f, 1f) > 0.3f ? 1f : 0.25f;
                return;
            }
            messageGroup.alpha = Mathf.MoveTowards(messageGroup.alpha, Time.unscaledTime < messageUntil ? 1f : 0f, dt * 5f);
        }

        void UpdateMap()
        {
            var line = race.Line;
            if (!hasMap || mapView == null || mapImage == null || race.Player == null || race.Player.suspended) return;
            int mode = GameSettings.Minimap;
            mapView.gameObject.SetActive(mode != 2);
            if (mode == 2) return;
            if (mapFade != null && mapFade.enabled != (mode == 0)) mapFade.enabled = mode == 0;

            var rt = mapImage.rectTransform;
            var t = race.Player.vehicle.Transform;
            Vector2 uv = line.MapUV(t.position);
            float yaw = t.eulerAngles.y;
            float view = mapView.rect.width;
            if (mode == 1)
            {
                // Full circuit, north up; the arrow moves.
                float size = view * fullFill;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = Vector2.zero;
                rt.localEulerAngles = Vector3.zero;
                mapDot.anchoredPosition = new Vector2((uv.x - 0.5f) * size, (uv.y - 0.5f) * size);
                mapDot.localEulerAngles = new Vector3(0, 0, -yaw);
            }
            else
            {
                // Local: the car stays in the middle pointing up and the track turns around it.
                float size = line.mapBounds.width / localSpan * view;
                rt.pivot = uv;
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = Vector2.zero;
                rt.localEulerAngles = new Vector3(0, 0, yaw);
                mapDot.anchoredPosition = Vector2.zero;
                mapDot.localEulerAngles = Vector3.zero;
            }
            UpdateRivalDots(line);
        }

        /// <summary>Rivals as tinted dots pinned to the map image, so they turn and scale with it.</summary>
        void UpdateRivalDots(RaceLine line)
        {
            if (rivalDot == null) return;
            foreach (var r in race.Racers)
            {
                if (r.isPlayer) continue;
                if (!dots.TryGetValue(r, out var dot) || dot == null)
                {
                    var go = new GameObject("Rival Dot " + r.name, typeof(RectTransform), typeof(Image));
                    dot = (RectTransform)go.transform;
                    dot.SetParent(mapImage.rectTransform, false);
                    dot.sizeDelta = Vector2.one * rivalDotSize;
                    var img = go.GetComponent<Image>();
                    img.sprite = rivalDot; img.raycastTarget = false;
                    img.color = r.color;
                    dots[r] = dot;
                    mapDot.SetAsLastSibling();
                }
                dot.gameObject.SetActive(!r.suspended);
                Vector2 uv = line.MapUV(r.vehicle.Transform.position);
                dot.anchorMin = dot.anchorMax = uv;
                dot.anchoredPosition = Vector2.zero;
                dot.localEulerAngles = Vector3.zero;
            }
        }

        void UpdateFinish(float dt)
        {
            if (finishGroup == null || finishShownAt < 0f || Time.unscaledTime < finishShownAt) return;
            finishGroup.alpha = Mathf.MoveTowards(finishGroup.alpha, 1f, dt * 3f);
            if (raceGroup != null) raceGroup.alpha = Mathf.MoveTowards(raceGroup.alpha, 0.25f, dt * 3f);
            if (finishGroup.alpha < 0.9f || SceneLoader.IsLoading || ShiftRush.UI.PauseMenu.IsPaused) return;

            SetupFinishButtons();
            var kb = Keyboard.current; var pad = Gamepad.current;
            // Arrows, WASD or the d-pad move between the two buttons; Enter, Space or A presses the lit one.
            bool move = (kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame
                            || kb.downArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame))
                        || (pad != null && (pad.dpad.left.wasPressedThisFrame || pad.dpad.right.wasPressedThisFrame || pad.dpad.up.wasPressedThisFrame || pad.dpad.down.wasPressedThisFrame));
            if (move) { finishChoice = 1 - finishChoice; StyleFinishButtons(); ShiftRush.UI.MenuSounds.Tick(); }
            bool press = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            bool menu = (kb != null && kb.mKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame);
            if (press) { if (finishChoice == 0) RaceAgain(); else LeaveRace(); }
            else if (menu) LeaveRace();
        }

        // ------------------------------------------------------------------ results buttons

        int finishChoice;
        bool finishButtonsReady;
        Image againImg, menuImg, againShadow;
        TMP_Text againText, menuText;
        Sprite filledSprite, outlineSprite;
        static readonly Color Pink = new Color32(255, 46, 154, 255), Night = new Color32(11, 10, 18, 255), Paper = new Color32(244, 241, 250, 255);

        /// <summary>Story: RESTART or RETURN TO STORY (the case board). Quick race: RACE AGAIN or MAIN MENU.</summary>
        void SetupFinishButtons()
        {
            if (finishButtonsReady || finishGroup == null) return;
            finishButtonsReady = true;
            foreach (var img in finishGroup.GetComponentsInChildren<Image>(true))
            {
                if (img.name == "Again") againImg = img;
                else if (img.name == "Menu") menuImg = img;
                else if (img.name == "Again Shadow") againShadow = img;
            }
            if (againImg == null || menuImg == null) return;
            againText = againImg.GetComponentInChildren<TMP_Text>(true);
            menuText = menuImg.GetComponentInChildren<TMP_Text>(true);
            filledSprite = againImg.sprite; outlineSprite = menuImg.sprite;
            bool story = !string.IsNullOrEmpty(StoryProgress.Active);
            if (againText != null) againText.text = story ? "RESTART" : "RACE AGAIN";
            if (menuText != null) menuText.text = story ? "RETURN TO STORY" : "MAIN MENU";
            // Same size, side by side, so either can be the lit one.
            var a = againImg.rectTransform; var m = menuImg.rectTransform;
            float w = Mathf.Max(a.sizeDelta.x, 330f);
            m.sizeDelta = new Vector2(w, a.sizeDelta.y);
            m.anchoredPosition = new Vector2(a.anchoredPosition.x + w + 22f, a.anchoredPosition.y);
            if (againText != null) { againText.fontSize = 34f; againText.enableAutoSizing = true; againText.fontSizeMax = 38f; againText.fontSizeMin = 20f; }
            if (menuText != null) { menuText.fontSize = 34f; menuText.enableAutoSizing = true; menuText.fontSizeMax = 38f; menuText.fontSizeMin = 20f; if (againText != null) menuText.font = againText.font; }
            finishChoice = 0;
            StyleFinishButtons();
        }

        /// <summary>The lit button: pink, filled, with the cyan shadow behind it. The other: an outline.</summary>
        void StyleFinishButtons()
        {
            if (againImg == null || menuImg == null) return;
            var lit = finishChoice == 0 ? againImg : menuImg;
            var dim = finishChoice == 0 ? menuImg : againImg;
            lit.sprite = filledSprite; lit.color = Pink;
            dim.sprite = outlineSprite; dim.color = Paper;
            var litText = finishChoice == 0 ? againText : menuText;
            var dimText = finishChoice == 0 ? menuText : againText;
            if (litText != null) litText.color = Night;
            if (dimText != null) dimText.color = Paper;
            if (againShadow != null)
            {
                var sr = againShadow.rectTransform; var lr = lit.rectTransform;
                sr.sizeDelta = lr.sizeDelta;
                sr.anchoredPosition = lr.anchoredPosition + new Vector2(7f, -7f);
                againShadow.transform.SetSiblingIndex(lit.transform.GetSiblingIndex());
            }
            lit.rectTransform.localScale = Vector3.one * 1.04f;
            dim.rectTransform.localScale = Vector3.one;
        }

        static void LeaveRace()
        {
            bool story = !string.IsNullOrEmpty(StoryProgress.Active);
            SceneLoader.Load(MenuScene, story ? "CASE BOARD" : "MAIN MENU", "GRIP PRO", "");
        }

        static void RaceAgain()
        {
            string scene = SceneManager.GetActiveScene().name;
            var cat = RaceCatalog.Instance;
            if (cat != null)
                foreach (var t in cat.tracks)
                    if (t.scene == scene) { SceneLoader.LoadTrack(t); return; }
            SceneLoader.Load(scene, scene.ToUpperInvariant(), "", "");
        }

        static string Ordinal(int n)
        {
            string suffix = (n % 100 >= 11 && n % 100 <= 13) ? "TH" : (n % 10 == 1 ? "ST" : n % 10 == 2 ? "ND" : n % 10 == 3 ? "RD" : "TH");
            return n + suffix;
        }
    }
}
