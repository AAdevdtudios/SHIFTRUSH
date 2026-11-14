using ShiftRush.Race;
using ShiftRush.Shift;
using ShiftRush.Vehicles;
using UnityEngine;

namespace ShiftRush.Audio
{
    /// <summary>
    /// Hooks the game's sounds to what happens: the countdown, laps and the finish, the SHIFT timing (a bell that
    /// climbs a fifth for the second hit, static for a miss), the warp out and the thump back in, nitro handed
    /// out, arena rings and traps, respawns and overtakes. Only the player's car makes these calls.
    /// </summary>
    public class SfxDirector : MonoBehaviour
    {
        static SfxDirector instance;
        static bool hooked;

        RaceManager race;
        int lastPosition;
        float positionCooldown;

        // Ring pitches climb up the A major scale, ring by ring.
        static readonly float[] Scale = { 1f, 1.1225f, 1.2599f, 1.3348f, 1.4983f, 1.6818f, 1.8877f, 2f };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance == null)
            {
                var go = new GameObject("SFX Director");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<SfxDirector>();
            }
            if (hooked) return; // static events: subscribe once even without a domain reload
            hooked = true;
            ShiftDirector.TimingStarted += () => GameSfx.Play(Sfx.ShiftOpen);
            ShiftDirector.TimingPress += (hits, hit) =>
            {
                if (hit) GameSfx.Play(Sfx.ShiftHit, hits >= 2 ? 1.4983f : 1f); // second hit a fifth up
                else GameSfx.Play(Sfx.ShiftMiss);
            };
            ShiftDirector.Warped += () => GameSfx.Play(Sfx.ShiftWarp);
            ShiftDirector.Returned += v => GameSfx.Play(Sfx.ShiftArrive);
            ShiftDirector.NitroAwarded += fill => GameSfx.Play(Sfx.NitroGain, 1f, Mathf.Lerp(0.7f, 1f, fill));
            ShiftArena.Feedback += (arena, ring) =>
            {
                if (ring) GameSfx.Play(Sfx.RingPass, Scale[Mathf.Clamp(arena.RingsPassed - 1, 0, Scale.Length - 1)]);
                else GameSfx.Play(Sfx.TrapHit, Random.Range(0.92f, 1.06f));
            };
            VehicleReset.Respawned += r =>
            {
                var rm = RaceManager.Current;
                bool player = rm == null || rm.Player == null || rm.Player.vehicle.Transform == r.transform;
                if (player) GameSfx.Play(Sfx.Respawn);
            };
        }

        void Start() { GameSfx.Warm(); }

        void OnDestroy()
        {
            Unhook();
            if (instance == this) instance = null;
        }

        void Update()
        {
            var current = RaceManager.Current;
            if (current != race)
            {
                Unhook();
                race = current;
                if (race != null)
                {
                    race.CountdownTick += OnCountdown;
                    race.LapCompleted += OnLap;
                    race.FinalLap += OnFinalLap;
                    race.Finished += OnFinished;
                    lastPosition = 0;
                }
            }
            if (race != null) WatchPosition();
        }

        void Unhook()
        {
            if (race == null) return;
            race.CountdownTick -= OnCountdown;
            race.LapCompleted -= OnLap;
            race.FinalLap -= OnFinalLap;
            race.Finished -= OnFinished;
            race = null;
        }

        // 3, 2, 1 step up a tone each; GO an octave above.
        void OnCountdown(int n)
        {
            if (n == 0)
            {
                GameSfx.Play(Sfx.CountGo);
                positionCooldown = Time.time + 4f; // the grid shuffles in the first seconds: no overtake ticks yet
            }
            else GameSfx.Play(Sfx.CountTick, n == 3 ? 1f : n == 2 ? 1.1225f : 1.2599f);
        }

        void OnLap(RaceManager.Racer r, float time, bool best)
        {
            if (!r.isPlayer || r.lap >= race.TotalLaps) return; // the finish has its own sound
            if (r.lap + 1 == race.TotalLaps && !best) return;   // the final-lap call covers it
            GameSfx.Play(best ? Sfx.BestLap : Sfx.Lap);
        }

        void OnFinalLap(RaceManager.Racer r)
        {
            if (r.isPlayer) GameSfx.Play(Sfx.FinalLap);
        }

        void OnFinished(RaceManager.Racer r)
        {
            if (r.isPlayer) GameSfx.Play(race.PositionOf(r) <= 3 ? Sfx.FinishPodium : Sfx.Finish);
        }

        // A soft tick when you take a place (not when you lose one; not while away in a SHIFT world).
        void WatchPosition()
        {
            var p = race.Player;
            if (p == null || race.CurrentState != RaceManager.State.Racing || p.suspended || p.finished
                || (ShiftDirector.Instance != null && ShiftDirector.Instance.CurrentState != ShiftDirector.State.Idle))
            {
                lastPosition = 0;
                return;
            }
            int pos = race.PositionOf(p);
            if (lastPosition == 0) { lastPosition = pos; return; }
            if (pos < lastPosition && Time.time >= positionCooldown)
            {
                GameSfx.Play(Sfx.Overtake);
                positionCooldown = Time.time + 1.2f;
            }
            lastPosition = pos;
        }
    }
}
