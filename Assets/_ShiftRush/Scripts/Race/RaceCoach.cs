using ShiftRush.Audio;
using ShiftRush.Core;
using ShiftRush.Shift;
using ShiftRush.UI;
using ShiftRush.Vehicles;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShiftRush.Race
{
    /// <summary>
    /// The tutorial: a coach card along the bottom of the screen in the player's first races (Resources/SR_Coach,
    /// built with the menu). No tutorial level. Each lesson shows up at the moment it matters and clears when the
    /// player actually does it:
    ///  1. DRIVE: during the countdown, with the player's own keys (or what auto accelerate leaves them to do).
    ///  2. RAMP: as a SHIFT ramp comes up, its distance counting down on the key cap.
    ///  3. TIMING: in the slow motion. The cap flashes NOW while the diamonds meet, and the timing is gentler until
    ///     the player lands a SHIFT (<see cref="AssistShift"/>).
    ///  4. WORLD: what to do in Ring Rush or Trap Run.
    ///  5. NITRO: once nitro has been earned, until it's been used.
    /// A "stuck? press R" card comes up when needed. It's for first-timers: it switches itself off (Settings → Gameplay
    /// → Tutorial) after the first race, or as soon as all five are done; switching it back on replays it.
    /// </summary>
    public class RaceCoach : MonoBehaviour
    {
        enum Lesson { Drive, Ramp, Timing, World, Nitro, Stuck, Missed, Done, None }
        const int Counted = 5;
        const int AllDone = (1 << Counted) - 1;
        const string ProgressKey = "SR_TutorialProgress";

        [SerializeField] CanvasGroup group;
        [SerializeField] RectTransform card;
        [SerializeField] Image frame, keyCap;
        [SerializeField] TMP_Text keyText, tagText, headline, detail;
        [SerializeField] Image[] pips;
        [SerializeField] Color accent = new Color32(255, 64, 217, 255);
        [SerializeField] Color green = new Color32(61, 255, 138, 255);
        [Tooltip("Metres before a SHIFT ramp that the ramp lesson comes up.")]
        [SerializeField] float rampWarning = 240f;

        /// <summary>True until the player has landed their first SHIFT with the tutorial on: the timing is gentler.</summary>
        public static bool AssistShift { get; private set; }

        RaceManager race;
        IVehicleDash dash;
        int progress;
        bool active;
        Lesson showing = Lesson.None;
        float shownAt, clearAt = -1f, cleared, appear, slowTime, nitroHeld, nitroReadyAt = -1f, hideAt = -1f;
        float[] rampDistances;
        ShiftArena arena;
        bool missedPending;
        float racedSeconds; // counted here: on leaving the scene the race may already be gone

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded += (s, m) => { if (m == LoadSceneMode.Single) Spawn(s); };
            Spawn(SceneManager.GetActiveScene());
        }

        static void Spawn(Scene scene)
        {
            AssistShift = false;
            if (FindFirstObjectByType<RaceCoach>() != null || FindFirstObjectByType<RaceLine>() == null) return;
            var prefab = Resources.Load<RaceCoach>("SR_Coach");
            if (prefab == null) return;
            var coach = Instantiate(prefab);
            coach.name = "SR_Coach";
            SceneManager.MoveGameObjectToScene(coach.gameObject, scene);
        }

        void Awake()
        {
            if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false; }
        }

        void OnEnable()
        {
            ShiftDirector.TimingStarted += OnTimingStarted;
            ShiftDirector.TimingEnded += OnTimingEnded;
            ShiftDirector.Warped += OnWarped;
            ShiftDirector.Returned += OnReturned;
            ShiftDirector.NitroAwarded += OnNitro;
            VehicleReset.Respawned += OnRespawned;
        }

        void OnDisable()
        {
            // Left the first race part-way (restart, back to menu): it still counts once they've really raced.
            if (racedSeconds > 30f) EndTutorial();
            ShiftDirector.TimingStarted -= OnTimingStarted;
            ShiftDirector.TimingEnded -= OnTimingEnded;
            ShiftDirector.Warped -= OnWarped;
            ShiftDirector.Returned -= OnReturned;
            ShiftDirector.NitroAwarded -= OnNitro;
            VehicleReset.Respawned -= OnRespawned;
            AssistShift = false;
        }

        void Start()
        {
            active = GameSettings.Tutorial;
            progress = PlayerPrefs.GetInt(ProgressKey, 0);
            if (active && progress == AllDone) { progress = 0; PlayerPrefs.SetInt(ProgressKey, 0); } // switched back on: replay
            AssistShift = active && !IsDone(Lesson.Timing);
        }

        void FollowSetting()
        {
            bool want = GameSettings.Tutorial;
            if (want == active) return;
            active = want;
            if (want && progress == AllDone) { progress = 0; PlayerPrefs.SetInt(ProgressKey, 0); }
            if (!want) Hide();
            AssistShift = active && !IsDone(Lesson.Timing);
            RefreshPips();
        }

        bool IsDone(Lesson l) { return (progress & (1 << (int)l)) != 0; }

        int DoneCount
        {
            get { int n = 0; for (int i = 0; i < Counted; i++) if ((progress & (1 << i)) != 0) n++; return n; }
        }

        // ------------------------------------------------------------------ SHIFT events

        void OnTimingStarted()
        {
            if (!active) return;
            Complete(Lesson.Ramp);
            missedPending = false;
            if (!IsDone(Lesson.Timing)) Show(Lesson.Timing);
            else if (showing != Lesson.Done) Hide();
        }

        void OnTimingEnded()
        {
            if (!active || showing != Lesson.Timing) return;
            missedPending = true; // Warped follows straight away on a hit; if it doesn't, it was a miss
        }

        void OnWarped()
        {
            if (!active) return;
            missedPending = false;
            Complete(Lesson.Timing);
            AssistShift = false;
            arena = null; // found once the car is in it
        }

        void OnReturned(IVehicle v)
        {
            if (!active) return;
            if (arena != null) Complete(Lesson.World);
            arena = null;
        }

        void OnNitro(float fill)
        {
            if (!active || IsDone(Lesson.Nitro)) return;
            nitroReadyAt = Time.unscaledTime + 2.4f; // after the reward panel has had its moment
        }

        void OnRespawned(VehicleReset r)
        {
            if (showing == Lesson.Stuck) Complete(Lesson.Stuck);
            slowTime = 0f;
        }

        // ------------------------------------------------------------------ the lessons

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (race == null && RaceManager.Current != null)
            {
                race = RaceManager.Current;
                if (race.Player != null) dash = race.Player.vehicle as IVehicleDash;
                CacheRamps();
            }

            if (PauseMenu.IsPaused && showing != Lesson.Done) FollowSetting(); // the pause menu has the switch too
            if (hideAt > 0f && Time.unscaledTime >= hideAt) { hideAt = -1f; Hide(); }
            if (race != null && race.CurrentState == RaceManager.State.Racing) racedSeconds += Time.deltaTime;
            if (active && race != null && race.Player != null) Think();
            Animate(dt);
        }

        void Think()
        {
            var p = race.Player;
            if (dash == null) dash = p.vehicle as IVehicleDash;
            var shift = ShiftDirector.Instance;
            var shiftState = shift != null ? shift.CurrentState : ShiftDirector.State.Idle;
            float speed = Mathf.Abs(p.vehicle.SpeedKmh);

            if (race.CurrentState == RaceManager.State.Finished || p.finished)
            {
                if (showing != Lesson.None && showing != Lesson.Done) Hide();
                EndTutorial(); // first-timers only: one race and it's done
                return;
            }

            // A lesson waiting to clear (green flash), or the sign-off running its time.
            if (clearAt > 0f)
            {
                if (Time.unscaledTime >= clearAt) { clearAt = -1f; if (showing != Lesson.Done) Hide(); }
                return;
            }
            switch (showing)
            {
                case Lesson.Drive:
                    if (race.CurrentState == RaceManager.State.Racing && speed > 70f) Complete(Lesson.Drive);
                    return;
                case Lesson.Ramp:
                {
                    float ahead = RampAhead(p);
                    if (shiftState == ShiftDirector.State.Idle && ahead > rampWarning + 30f) Hide(); // drove past it
                    else if (keyText != null) keyText.text = Mathf.Max(0, Mathf.RoundToInt(ahead / 10f) * 10) + "M";
                    return;
                }
                case Lesson.Timing:
                {
                    bool now = shift != null && shift.InWindow;
                    if (headline != null) headline.text = now ? "<color=#3DFF8A>NOW!</color>  PRESS SHIFT" : "PRESS SHIFT AS THE DIAMONDS MEET";
                    if (keyCap != null) keyCap.color = now ? green : new Color(1f, 1f, 1f, 0.1f);
                    if (keyText != null) keyText.color = now ? Color.black : Color.white;
                    if (missedPending && shiftState == ShiftDirector.State.Idle) { missedPending = false; Show(Lesson.Missed); hideAt = Time.unscaledTime + 4.5f; }
                    return;
                }
                case Lesson.World:
                    return; // cleared when the car comes back
                case Lesson.Nitro:
                    if (dash != null && dash.NitroActive) nitroHeld += Time.unscaledDeltaTime;
                    if (nitroHeld > 0.5f) Complete(Lesson.Nitro);
                    return;
                case Lesson.Stuck:
                    if (speed > 30f) Hide();
                    return;
                case Lesson.Missed:
                    if (shiftState == ShiftDirector.State.Idle && RampAhead(p) < rampWarning && Time.unscaledTime - shownAt > 2f) Show(Lesson.Ramp);
                    return;
                case Lesson.Done:
                    return;
            }

            // Nothing on screen: what's next?
            if (race.CurrentState == RaceManager.State.Countdown)
            {
                if (!IsDone(Lesson.Drive)) Show(Lesson.Drive);
                return;
            }
            if (!IsDone(Lesson.Drive)) { Show(Lesson.Drive); return; }

            if (shiftState == ShiftDirector.State.Away)
            {
                if (arena == null) arena = ActiveArena();
                if (arena != null && !IsDone(Lesson.World)) Show(Lesson.World);
                return;
            }
            if (shiftState != ShiftDirector.State.Idle) return;

            if (!IsDone(Lesson.Nitro) && nitroReadyAt > 0f && Time.unscaledTime >= nitroReadyAt && dash != null && dash.Nitro01 > 0.05f)
            {
                Show(Lesson.Nitro);
                return;
            }
            if ((!IsDone(Lesson.Ramp) || !IsDone(Lesson.Timing)) && RampAhead(p) < rampWarning)
            {
                Show(Lesson.Ramp);
                return;
            }

            // Stuck: barely moving for a few seconds while the race is on (scaled time, so the pause doesn't count).
            bool controllable = !p.suspended && !PauseMenu.IsPaused;
            slowTime = controllable && speed < 8f ? slowTime + Time.deltaTime : 0f;
            if (slowTime > 3.5f || race.WrongWay && p.wrongWayTime > 4f) { slowTime = 0f; Show(Lesson.Stuck); }
        }

        void Complete(Lesson l)
        {
            if ((int)l < Counted && !IsDone(l))
            {
                progress |= 1 << (int)l;
                PlayerPrefs.SetInt(ProgressKey, progress);
                PlayerPrefs.Save();
            }
            if (showing != l) return;
            cleared = 1f;
            clearAt = Time.unscaledTime + 0.9f;
            if (keyCap != null) keyCap.color = green;
            if (keyText != null) { keyText.text = "OK"; keyText.color = Color.black; }
            RefreshPips();
            GameSfx.Play(Sfx.RingPass, 1.4983f, 0.8f);
            if (progress == AllDone) Invoke(nameof(SignOff), 1.1f);
        }

        void SignOff()
        {
            clearAt = -1f;
            Show(Lesson.Done);
            hideAt = Time.unscaledTime + 6f;
            EndTutorial(); // the card stays up until hideAt, then this race is hint-free
            GameSfx.Play(Sfx.BestLap, 1f, 0.8f);
        }

        /// <summary>
        /// The tutorial is for first-timers: after one race (or all five lessons) it switches itself off. Switching it
        /// back on in Settings replays every lesson.
        /// </summary>
        void EndTutorial()
        {
            if (!active) return;
            active = false;
            AssistShift = false;
            progress = AllDone;
            PlayerPrefs.SetInt(ProgressKey, AllDone);
            PlayerPrefs.SetString("Switch_" + GameSettings.TutorialKey, "false");
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------ card

        void Show(Lesson l)
        {
            if (showing == l && l != Lesson.Missed) return;
            showing = l;
            shownAt = Time.unscaledTime;
            appear = 0f;
            cleared = 0f;
            clearAt = -1f;
            hideAt = -1f;
            if (l == Lesson.Nitro) nitroHeld = 0f;
            if (keyCap != null) keyCap.color = new Color(1f, 1f, 1f, 0.1f);
            if (keyText != null) keyText.color = Color.white;

            string key, head, body;
            Words(l, out key, out head, out body);
            if (keyText != null) keyText.text = key;
            if (headline != null) headline.text = head;
            if (detail != null) detail.text = body;
            if (tagText != null)
            {
                int step = (int)l < Counted ? DoneCount + 1 : DoneCount;
                tagText.text = l == Lesson.Done ? "TUTORIAL  //  COMPLETE"
                    : (int)l < Counted ? "TUTORIAL  //  " + step.ToString("00") + " / " + Counted.ToString("00")
                    : "TUTORIAL  //  TIP";
            }
            RefreshPips();
            if (l != Lesson.Timing) GameSfx.Play(Sfx.Overtake, 0.75f, 1.4f);
        }

        void Hide()
        {
            showing = Lesson.None;
            clearAt = -1f;
            hideAt = -1f;
        }

        void Words(Lesson l, out string key, out string head, out string body)
        {
            const string M = "<color=#FF40D9>", E = "</color>";
            string left = Key("Steering", "wasd", "negative", "A"), right = Key("Steering", "wasd", "positive", "D");
            string brake = Key("Brake", "wasd", "positive", "S"), hand = Key("Handbrake", "keyboard", "positive", "SPACE");
            string nitro = Key("NOS", "keyboard", "positive", "SHIFT");
            switch (l)
            {
                case Lesson.Drive:
                    if (GameSettings.AutoAccelerate)
                    {
                        key = "AUTO";
                        head = "THE THROTTLE'S ON YOU";
                        body = M + left + " " + right + E + " steer  //  " + M + brake + E + " brakes  //  " + M + hand + E + " handbrake to drift";
                    }
                    else
                    {
                        key = Key("Throttle", "wasd", "positive", "W");
                        head = "HOLD " + key + " TO DRIVE";
                        body = M + left + " " + right + E + " steer  //  " + M + brake + E + " brakes, then reverses  //  " + M + hand + E + " handbrake to drift";
                    }
                    return;
                case Lesson.Ramp:
                    key = "---";
                    head = "MAGENTA RAMP AHEAD: HIT IT";
                    body = "A SHIFT ramp. Take it fast and the world slows down. That's your shot at a shortcut.";
                    return;
                case Lesson.Timing:
                    key = "SHIFT";
                    head = "PRESS SHIFT AS THE DIAMONDS MEET";
                    body = "Land " + M + "2 of 3" + E + " to warp out. The timing's gentler until you land your first.";
                    return;
                case Lesson.Missed:
                    key = "AGAIN";
                    head = "MISSED. NEXT RAMP, GO AGAIN";
                    body = "A miss still pays some nitro. Watch the magenta diamond and press as it touches the white one.";
                    return;
                case Lesson.World:
                    if (arena != null && arena.kind == ShiftArena.Kind.TrapRun)
                    {
                        key = "DODGE";
                        head = "REACH THE ARCH. DODGE THE TRAPS.";
                        body = "Every hit costs nitro. Make it in time and you come back in the " + M + "top three" + E + ".";
                    }
                    else
                    {
                        key = "RINGS";
                        head = "DRIVE THROUGH THE RINGS";
                        body = "More rings, more nitro. Reach the arch in time and you come back in the " + M + "top three" + E + ".";
                    }
                    return;
                case Lesson.Nitro:
                    key = nitro;
                    head = "NITRO'S LOADED: HOLD " + nitro;
                    body = "Nitro only comes from SHIFT ramps, so spend it on the straights.";
                    return;
                case Lesson.Stuck:
                    key = "R";
                    head = "STUCK? PRESS R";
                    body = "Puts you back on the racing line, facing the right way.";
                    return;
                case Lesson.Done:
                    key = "GO";
                    head = "THAT'S EVERYTHING. GO WIN IT.";
                    body = "Hints are off now. Turn them back on in Settings  >  Gameplay  >  Tutorial.";
                    return;
            }
            key = head = body = "";
        }

        static string Key(string action, string composite, string part, string fallback)
        {
            string k = ControlBindings.KeyName(action, composite, part);
            return string.IsNullOrEmpty(k) ? fallback : k;
        }

        void RefreshPips()
        {
            if (pips == null) return;
            for (int i = 0; i < pips.Length; i++)
            {
                if (pips[i] == null) continue;
                bool done = i < Counted && (progress & (1 << i)) != 0;
                bool now = i == (int)showing;
                pips[i].color = done ? green : now ? accent : new Color(1f, 1f, 1f, 0.18f);
            }
        }

        void Animate(float dt)
        {
            if (group == null) return;
            bool visible = showing != Lesson.None && !PauseMenu.IsPaused;
            group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, dt * (visible ? 6f : 4f));
            appear = Mathf.MoveTowards(appear, 1f, dt * 4f);
            float e = 1f - (1f - appear) * (1f - appear) * (1f - appear);
            if (card != null) card.anchoredPosition = new Vector2(0f, Mathf.Lerp(-40f, 0f, e));
            cleared = Mathf.MoveTowards(cleared, 0f, dt * 1.5f);
            if (frame != null) frame.color = Color.Lerp(accent, green, cleared);
            if (card != null) card.localScale = Vector3.one * (1f + 0.04f * cleared);
            if (showing == Lesson.Timing && keyCap != null)
                keyCap.transform.localScale = Vector3.one * (ShiftDirector.Instance != null && ShiftDirector.Instance.InWindow ? 1.12f : 1f);
            else if (keyCap != null) keyCap.transform.localScale = Vector3.one;
        }

        // ------------------------------------------------------------------ helpers

        void CacheRamps()
        {
            var line = race.Line;
            var ramps = FindObjectsByType<ShiftRamp>(FindObjectsSortMode.None);
            if (line == null || line.distances == null) { rampDistances = new float[0]; return; }
            rampDistances = new float[ramps.Length];
            for (int i = 0; i < ramps.Length; i++) rampDistances[i] = line.distances[line.Nearest(ramps[i].transform.position)];
        }

        /// <summary>Metres along the circuit to the next SHIFT ramp ahead of the player.</summary>
        float RampAhead(RaceManager.Racer p)
        {
            if (rampDistances == null || rampDistances.Length == 0 || race.Line == null || p.suspended) return float.MaxValue;
            float len = race.Line.length, d = race.Line.distances[p.nearest], best = float.MaxValue;
            foreach (var rd in rampDistances) best = Mathf.Min(best, Mathf.Repeat(rd - d, len));
            return best;
        }

        static ShiftArena ActiveArena()
        {
            foreach (var a in FindObjectsByType<ShiftArena>(FindObjectsSortMode.None))
                if (a.isActiveAndEnabled) return a;
            return null;
        }
    }
}
