using System.Collections;
using ShiftRush.Race;
using ShiftRush.UI;
using ShiftRush.Vehicles;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShiftRush.Shift
{
    /// <summary>
    /// Runs the SHIFT moment. Leaving a SHIFT ramp drops the game into slow motion and shows the timing diamond: a
    /// magenta diamond closes in on a fixed target diamond, easing as it gets there, three tries; Shift pressed as
    /// they line up is a hit. Two hits warp the car into a random SHIFT world for about 10 seconds. Reach its finish in time and
    /// the car comes back running in a random top place set by the difficulty (1st-3rd on RUSH; never worse than where it left from); run out
    /// of time and it keeps a share of a win's gain by how far it got (never lower than where it left). Nitro is paid in proportion to the world's score. Fewer than two
    /// hits: time comes back, the jump carries on, and a little nitro. The race clock keeps running.
    /// Spawns itself in any scene that has a <see cref="ShiftRamp"/>.
    /// </summary>
    public class ShiftDirector : MonoBehaviour
    {
        public enum State { Idle, Selecting, Away }

        [Header("Timing diamond")]
        [Tooltip("Real seconds for the diamond to close on each try, first to last (a little quicker each time).")]
        [SerializeField] float[] closeSeconds = { 1.6f, 1.45f, 1.3f };
        [Tooltip("How far off the target size still counts (0.16 = within 16%).")]
        [SerializeField] float tolerance = 0.16f;
        [Tooltip("Size the diamond starts at, relative to the target.")]
        [SerializeField] float startScale = 2.6f;
        [SerializeField] int hitsNeeded = 2;
        [Tooltip("Nitro for missing the timing bar (fraction of a tank).")]
        [SerializeField] float consolationNitro = 0.25f;
        [SerializeField] float slowMotion = 0.12f;
        [Tooltip("Speed the car enters a SHIFT world at (km/h), clamped.")]
        [SerializeField] Vector2 arenaSpeed = new Vector2(90f, 130f);
        [Tooltip("Where the SHIFT worlds sit, far from any track.")]
        [SerializeField] Vector3 arenaOrigin = new Vector3(9000f, 40f, 9000f);

        public static ShiftDirector Instance { get; private set; }

        /// <summary>For effects: the timing bar opened / closed, the car warped into a world, the car came back.</summary>
        public static event System.Action TimingStarted, TimingEnded, Warped;
        public static event System.Action<IVehicle> Returned;
        /// <summary>Each timing try: hits so far (after this one) and whether this one landed.</summary>
        public static event System.Action<int, bool> TimingPress;
        /// <summary>Nitro handed out after a SHIFT (0..1 of the tank).</summary>
        public static event System.Action<float> NitroAwarded;
        public State CurrentState { get; private set; }
        /// <summary>The closing diamond is on the target right now (a press would hit).</summary>
        public bool InWindow { get; private set; }

        ShiftHUD hud;
        ShiftArena[] arenas;
        float baseFixedDelta;
        float cooldownUntil;
        INitroTank mutedTank;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded += (s, m) => { if (m == LoadSceneMode.Single) Spawn(s); };
            Spawn(SceneManager.GetActiveScene());
        }

        static void Spawn(Scene scene)
        {
            if (Instance != null || FindFirstObjectByType<ShiftRamp>() == null) return;
            var go = new GameObject("SHIFT Director");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<ShiftDirector>();
        }

        void Awake()
        {
            Instance = this;
            baseFixedDelta = Time.fixedDeltaTime;
            var hudPrefab = Resources.Load<ShiftHUD>("SR_ShiftHUD");
            if (hudPrefab != null) { hud = Instantiate(hudPrefab, transform); hud.name = "SR_ShiftHUD"; }
            arenas = new ShiftArena[2];
            arenas[0] = MakeArena("SR_Arena_RingRush");
            arenas[1] = MakeArena("SR_Arena_TrapRun");
        }

        ShiftArena MakeArena(string name)
        {
            var prefab = Resources.Load<ShiftArena>(name);
            if (prefab == null) return null;
            var a = Instantiate(prefab, arenaOrigin, Quaternion.identity, transform);
            a.name = name;
            a.gameObject.SetActive(false);
            return a;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = baseFixedDelta;
            PauseMenu.Blocked = false;
            if (mutedTank != null) mutedTank.SetNitroInput(true);
        }

        public void Launch(ShiftRamp ramp, IVehicle vehicle)
        {
            if (CurrentState != State.Idle || Time.unscaledTime < cooldownUntil || PauseMenu.IsPaused) return;
            var race = RaceManager.Current;
            if (race != null && (race.CurrentState != RaceManager.State.Racing || race.Player == null || race.Player.vehicle != vehicle)) return;
            StartCoroutine(Run(ramp, vehicle, race));
        }

        IEnumerator Run(ShiftRamp ramp, IVehicle vehicle, RaceManager race)
        {
            CurrentState = State.Selecting;
            PauseMenu.Blocked = true;
            float takeoff = Mathf.Abs(vehicle.SpeedKmh);
            var tank = vehicle as INitroTank;
            mutedTank = tank;
            if (tank != null) tank.SetNitroInput(false); // Shift is the timing key for now, not nitro
            SetSlow(true);
            if (hud != null) hud.ShowSelector();
            TimingStarted?.Invoke();

            // ---- timing diamond: it closes on the target and slows as it gets there; press as they line up.
            int tries = closeSeconds.Length, hits = 0, misses = 0;
            // Tutorial: until the first SHIFT lands, the diamond closes slower and the window is wider.
            bool assist = RaceCoach.AssistShift;
            float slower = assist ? 1.4f : 1f, window = (assist ? tolerance + 0.08f : tolerance) * Core.Difficulty.Current.shiftWindow;
            const float endScale = 0.55f; // it carries on shrinking past the target, so waiting too long misses
            for (int t = 0; t < tries && hits < hitsNeeded && misses <= tries - hitsNeeded; t++)
            {
                bool pressed = false;
                float span = closeSeconds[t] * slower;
                for (float u = 0f; u < span; u += Time.unscaledDeltaTime)
                {
                    float k = u / span;
                    float ease = 1f - (1f - k) * (1f - k); // fast at first, easing in towards the target
                    float scale = Mathf.Lerp(startScale, endScale, ease);
                    bool inside = Mathf.Abs(scale - 1f) <= window;
                    InWindow = inside;
                    if (hud != null) hud.SetDiamond(scale, inside);
                    if (ShiftPressed())
                    {
                        if (inside) hits++; else misses++;
                        if (hud != null) hud.MarkPip(t, inside);
                        TimingPress?.Invoke(hits, inside);
                        pressed = true;
                        break;
                    }
                    yield return null;
                }
                InWindow = false;
                if (!pressed) { misses++; if (hud != null) hud.MarkPip(t, false); TimingPress?.Invoke(hits, false); }
                yield return new WaitForSecondsRealtime(0.25f);
            }

            TimingEnded?.Invoke();
            if (hits < hitsNeeded)
            {
                SetSlow(false);
                if (hud != null) hud.HideSelector();
                if (tank != null) tank.SetNitroInput(true);
                Reward(vehicle, consolationNitro, "+" + Mathf.RoundToInt(consolationNitro * 100f) + "% NITRO", "MISSED THE SHIFT  //  " + hits + " OF " + hitsNeeded);
                Done();
                yield break;
            }

            // ---- warp into a random world
            int pick = Random.Range(0, arenas.Length);
            if (arenas[pick] == null) pick = 1 - pick;
            var arena = arenas[pick];
            int leftFrom = race != null && race.Player != null ? race.PositionOf(race.Player) : 0;
            if (hud != null) hud.Picked(arena.title);
            Warped?.Invoke();
            yield return new WaitForSecondsRealtime(0.3f); // flash peaks, warp under it
            SetSlow(false);
            if (tank != null) tank.SetNitroInput(true);
            if (race != null) race.SetSuspended(race.Player, true);
            arena.gameObject.SetActive(true);
            arena.Begin(vehicle);
            float speed = Mathf.Clamp(takeoff, arenaSpeed.x, arenaSpeed.y);
            vehicle.Teleport(arena.start.position, arena.start.rotation, speed);
            CurrentState = State.Away;
            PauseMenu.Blocked = false;
            if (hud != null) hud.ShowChallenge(arena);

            while (!arena.Done) yield return null;

            PauseMenu.Blocked = true;
            if (hud != null) hud.EndChallenge();
            yield return new WaitForSecondsRealtime(0.25f);
            arena.gameObject.SetActive(false);

            // ---- back into the race: in the top three on a win, second-to-last if late
            float backSpeed = Mathf.Max(takeoff, 60f);
            string headline;
            bool placed = false;
            if (race != null && race.Player != null && race.Racers.Count > 1)
            {
                int from = leftFrom > 0 ? leftFrom : race.Racers.Count;
                int target;
                var level = Core.Difficulty.Current;
                if (arena.Won) target = Mathf.Min(Random.Range(level.warpBest, level.warpWorst + 1), from); // a win never sets you back
                else
                {
                    // Late: never lower than where you left; the further you got, the more of a win's gain you keep
                    // (a win lands around P2). 80% of the way ≈ most of it; 20% ≈ where you were.
                    int best = Mathf.Min(level.warpBest + 1, from);
                    target = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(from, best, arena.Progress)), 1, from);
                }
                placed = race.PlaceAtPosition(race.Player, target, backSpeed);
                headline = arena.Won ? "WARPED TO P" + target
                    : target < from ? "SO CLOSE  //  UP TO P" + target
                    : "TOO LATE  //  HELD P" + target;
            }
            else headline = arena.Won ? "CLEARED" : "TOO LATE";
            if (!placed)
            {
                var back = ramp.returnPoint != null ? ramp.returnPoint : ramp.transform;
                vehicle.Teleport(back.position, back.rotation, backSpeed);
                if (race != null) race.SetSuspended(race.Player, false);
            }
            Returned?.Invoke(vehicle);
            Reward(vehicle, arena.Score, headline, arena.title + "  //  " + arena.Summary + "  //  +" + Mathf.RoundToInt(arena.Score * 100f) + "% NITRO");
            Done();
        }

        void Reward(IVehicle vehicle, float fill, string headline, string detail)
        {
            if (vehicle is INitroTank tank && fill > 0f) { tank.AddNitro(fill); NitroAwarded?.Invoke(fill); }
            if (hud != null) hud.ShowReward(fill, headline, detail);
        }

        void Done()
        {
            PauseMenu.Blocked = false;
            CurrentState = State.Idle;
            cooldownUntil = Time.unscaledTime + 1.5f;
        }

        void SetSlow(bool on)
        {
            Time.timeScale = on ? slowMotion : 1f;
            Time.fixedDeltaTime = baseFixedDelta * (on ? slowMotion : 1f);
        }

        static bool ShiftPressed()
        {
            var kb = Keyboard.current; var pad = Gamepad.current;
            return (kb != null && (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame))
                || (pad != null && pad.buttonWest.wasPressedThisFrame);
        }
    }
}
