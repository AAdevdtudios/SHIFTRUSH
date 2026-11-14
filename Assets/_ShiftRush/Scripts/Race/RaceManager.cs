using System.Collections.Generic;
using ShiftRush.Vehicles;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftRush.Race
{
    /// <summary>
    /// Runs a race on the scene's <see cref="RaceLine"/>: holds the cars for a 3-2-1 countdown, counts laps when a
    /// car crosses every gate in order, times laps, keeps the best lap per track and ranks racers.
    /// Rivals are spawned onto the grid by <see cref="RaceGrid"/> and driven by <see cref="RivalDriver"/>.
    /// Uses scaled time, so the pause menu stops the clock.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class RaceManager : MonoBehaviour
    {
        public enum State { Countdown, Racing, Finished }

        public class Racer
        {
            public string name;
            public IVehicle vehicle;
            public bool isPlayer;
            public int lap = 1;                 // lap being driven, 1-based
            public int nextGate;                // gate that must be crossed next
            public bool started;                // crossed the start line once (standing start behind it)
            public float lapStartTime;
            public readonly List<float> lapTimes = new List<float>();
            public bool finished;
            public float finishTime;
            public int nearest;
            public float progress;              // metres into the current lap, for ranking
            public float[] sides;               // last signed distance to each gate plane
            public float wrongWayTime;
            public bool suspended;              // away in a SHIFT world: not tracked
            public Color color = Color.white;   // minimap dot

            public float Score(float lapLength)
            {
                return finished ? 1e9f - finishTime : (lap - 1) * lapLength + progress;
            }
        }

        [SerializeField] float countdownStep = 1f;
        [SerializeField] float goHold = 0.8f;

        public static RaceManager Current { get; private set; }

        public RaceLine Line { get; private set; }
        public State CurrentState { get; private set; }
        public int TotalLaps { get { return Line != null ? Line.laps : 3; } }
        public float RaceTime { get { return CurrentState == State.Countdown ? 0f : (Player != null && Player.finished ? Player.finishTime : Time.time - startTime); } }
        public int CountdownNumber { get; private set; }  // 3, 2, 1, then 0 = GO
        public bool ShowGo { get { return CurrentState == State.Racing && Time.time - startTime < goHold; } }
        public Racer Player { get; private set; }
        public readonly List<Racer> Racers = new List<Racer>();
        public float BestLap { get; private set; }

        public event System.Action<int> CountdownTick;      // 3,2,1,0(GO)
        public event System.Action<Racer, float, bool> LapCompleted; // racer, lap time, new best
        public event System.Action<Racer> FinalLap;
        public event System.Action<Racer> Finished;

        float startTime, countdownStart;
        float[] gateDistance;
        string BestKey { get { return "SR_BestLap_" + SceneManager.GetActiveScene().name; } }

        void Awake()
        {
            Current = this;
            Line = GetComponent<RaceLine>();
            if (Line == null) Line = FindFirstObjectByType<RaceLine>();
            BestLap = PlayerPrefs.GetFloat(BestKey, 0f);
            if (Line != null && Line.gates != null)
            {
                gateDistance = new float[Line.gates.Length];
                for (int g = 1; g < gateDistance.Length; g++) gateDistance[g] = Line.distances[Line.Nearest(Line.gates[g].position)];
            }
        }

        void OnDestroy() { if (Current == this) Current = null; }

        void Start()
        {
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (mb is IVehicle v) { AddRacer(v, "YOU", true); break; }
            RaceGrid.Fill(this, Player); // grid slots and the rivals
            CurrentState = State.Countdown;
            countdownStart = Time.time;
            CountdownNumber = -1;
        }

        /// <summary>Registers a car in the race (the player, or later an AI opponent).</summary>
        public Racer AddRacer(IVehicle vehicle, string displayName, bool isPlayer)
        {
            var r = new Racer { name = displayName, vehicle = vehicle, isPlayer = isPlayer };
            if (Line != null && Line.gates != null)
            {
                r.sides = new float[Line.gates.Length];
                for (int g = 0; g < r.sides.Length; g++) r.sides[g] = Side(Line.gates[g], vehicle.Transform.position);
            }
            Racers.Add(r);
            if (isPlayer) Player = r;
            // Nitro is earned from SHIFT ramps in a race: start empty, no regeneration.
            if (vehicle is INitroTank tank) { tank.SetRegeneration(false); tank.SetNitro(0f); }
            if (CurrentState == State.Countdown) vehicle.SetControllable(false);
            return r;
        }

        void Update()
        {
            if (Line == null || Line.gates == null || Line.gates.Length == 0) return;

            if (CurrentState == State.Countdown)
            {
                foreach (var r in Racers) r.vehicle.SetControllable(false);
                int n = 3 - Mathf.FloorToInt((Time.time - countdownStart - 0.6f) / countdownStep);
                if (Time.time - countdownStart < 0.6f) n = 4; // a breath before "3"
                if (n <= 3 && n != CountdownNumber && n > 0) { CountdownNumber = n; CountdownTick?.Invoke(n); }
                if (n <= 0)
                {
                    CountdownNumber = 0;
                    CurrentState = State.Racing;
                    startTime = Time.time;
                    foreach (var r in Racers) { r.vehicle.SetControllable(true); r.lapStartTime = startTime; }
                    CountdownTick?.Invoke(0);
                }
                return;
            }

            foreach (var r in Racers.ToArray()) Track(r); // a finish re-sorts the list mid-loop
            Racers.Sort((a, b) => b.Score(Line.length).CompareTo(a.Score(Line.length)));
        }

        /// <summary>Stop tracking a racer while it is away (SHIFT world); on return, pick up where it reappears,
        /// counting any checkpoints between where it left and where it came back.</summary>
        public void SetSuspended(Racer r, bool on)
        {
            if (r == null) return;
            r.suspended = on;
            if (on || Line == null) return;
            Vector3 p = r.vehicle.Transform.position;
            r.nearest = Line.Nearest(p);
            float d = Line.distances[r.nearest];
            if (r.started && gateDistance != null)
                for (int guard = 0; guard < gateDistance.Length && r.nextGate != 0 && gateDistance[r.nextGate] <= d; guard++)
                    r.nextGate = (r.nextGate + 1) % gateDistance.Length;
            for (int g = 0; g < r.sides.Length; g++) r.sides[g] = Side(Line.gates[g], p);
            r.wrongWayTime = 0f;
        }

        public int PositionOf(Racer r) { return Racers.IndexOf(r) + 1; }

        /// <summary>
        /// Puts a racer back on the circuit so it is running in a given position (1 = leading): just ahead of the
        /// car that should be behind it, beside the road's centre on the side away from nearby cars, at a speed.
        /// Never further back along the track than where the racer left from (a leader keeps its gap).
        /// Lap, checkpoints and progress follow the new spot. Ends a SHIFT world's suspension. False when there's
        /// nobody to place against (the caller then puts the car back where it left).
        /// </summary>
        public bool PlaceAtPosition(Racer r, int position, float speedKmh)
        {
            if (r == null || Line == null || Line.points == null) return false;
            float len = Line.length;
            var running = new List<Racer>();
            int finishedAhead = 0;
            foreach (var o in Racers)
            {
                if (o == r) continue;
                if (o.finished) finishedAhead++; else running.Add(o);
            }
            if (running.Count == 0) return false;
            running.Sort((a, b) => b.Score(len).CompareTo(a.Score(len)));

            // Cars that must stay ahead of us, among those still racing.
            int ahead = Mathf.Clamp(position - 1 - finishedAhead, 0, running.Count);
            float target;
            if (ahead == 0) target = running[0].Score(len) + 25f;
            else if (ahead >= running.Count) target = running[running.Count - 1].Score(len) - 22f;
            else
            {
                float front = running[ahead - 1].Score(len), back = running[ahead].Score(len);
                target = Mathf.Min(back + 25f, (front + back) * 0.5f);
            }
            // Never back down the road from where this racer left (it is suspended, so its score is still the
            // spot it jumped from): a big lead stays a big lead. It comes back at least past its own jump.
            target = Mathf.Max(target, r.Score(len) + 40f);
            target = Mathf.Clamp(target, 10f, TotalLaps * len - 80f);

            // Not on top of a SHIFT ramp (it would launch the car again).
            foreach (var ramp in FindObjectsByType<Shift.ShiftRamp>(FindObjectsSortMode.None))
            {
                float rd = Line.distances[Line.Nearest(ramp.transform.position)];
                float rel = Mathf.Repeat(target - rd + 20f, len);
                if (rel < 60f) target += 60f - rel;
            }

            int lap = Mathf.FloorToInt(target / len) + 1;
            float d = target - (lap - 1) * len;
            int i = Line.IndexAt(d);

            // Lane: away from whoever is closest to the spot.
            float lateral = 0f, closest = 30f;
            Vector3 centre = Line.PointAt(d);
            foreach (var o in Racers)
            {
                if (o == r) continue;
                var op = o.vehicle.Transform.position;
                float dist = Vector3.Distance(op, centre);
                if (dist < closest) { closest = dist; lateral = -Mathf.Sign(Line.Lateral(op, i)) * 5f; }
            }
            Vector3 pos = centre + Line.Right(i) * lateral;
            float best = float.MinValue;
            foreach (var hit in Physics.RaycastAll(pos + Vector3.up * 2.5f, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.rigidbody == null && hit.point.y > best) best = hit.point.y;
            if (best > float.MinValue) pos.y = best;
            r.vehicle.Teleport(pos + Vector3.up * 0.6f, Quaternion.LookRotation(Line.Direction(i), Vector3.up), speedKmh);

            int oldLap = r.lap;
            r.lap = lap;
            r.started = true;
            r.suspended = false;
            r.nearest = i;
            r.progress = d;
            r.wrongWayTime = 0f;
            r.nextGate = 0;
            if (gateDistance != null)
                for (int g = 1; g < gateDistance.Length; g++) if (gateDistance[g] > d) { r.nextGate = g; break; }
            for (int g = 0; g < r.sides.Length; g++) r.sides[g] = Side(Line.gates[g], pos);
            if (lap != oldLap && lap == TotalLaps) FinalLap?.Invoke(r);
            Racers.Sort((a, b) => b.Score(len).CompareTo(a.Score(len)));
            return true;
        }

        public bool WrongWay { get { return Player != null && Player.wrongWayTime > 1.2f && CurrentState == State.Racing; } }

        void Track(Racer r)
        {
            if (r.finished || r.suspended) return;
            Vector3 p = r.vehicle.Transform.position;
            r.nearest = Line.Nearest(p, r.nearest);
            float d = Line.distances[r.nearest];
            r.progress = r.started ? d : d - Line.length; // still behind the line: negative

            // Wrong way: moving against the line's direction at speed.
            var vel = r.vehicle.Body != null ? r.vehicle.Body.linearVelocity : Vector3.zero;
            vel.y = 0f;
            bool onShortcut = ShortcutPath.All.Count > 0 && ShortcutPath.OnAny(p);
            bool against = !onShortcut && vel.magnitude > 4f && Vector3.Dot(vel.normalized, Line.Direction(r.nearest)) < -0.4f;
            r.wrongWayTime = against ? r.wrongWayTime + Time.deltaTime : 0f;

            // Gate crossings (only the next expected gate counts, so cutting the track doesn't).
            for (int g = 0; g < Line.gates.Length; g++)
            {
                var gate = Line.gates[g];
                float side = Side(gate, p);
                float prev = r.sides[g];
                r.sides[g] = side;
                if (!(prev < 0f && side >= 0f)) continue;
                var right = Vector3.Cross(Vector3.up, gate.forward).normalized;
                if (Mathf.Abs(Vector3.Dot(p - gate.position, right)) > gate.halfWidth) continue;
                if (g != r.nextGate) continue;
                Crossed(r, g);
            }
        }

        void Crossed(Racer r, int g)
        {
            r.nextGate = (g + 1) % Line.gates.Length;
            if (g != 0) return;
            if (!r.started) { r.started = true; return; } // standing start: first pass of the line starts lap 1

            float lapTime = Time.time - r.lapStartTime;
            r.lapTimes.Add(lapTime);
            r.lapStartTime = Time.time;
            bool best = false;
            if (r.isPlayer && (BestLap <= 0f || lapTime < BestLap))
            {
                BestLap = lapTime; best = true;
                PlayerPrefs.SetFloat(BestKey, lapTime);
                PlayerPrefs.Save();
            }
            LapCompleted?.Invoke(r, lapTime, best);

            if (r.lap >= TotalLaps)
            {
                r.finished = true;
                r.finishTime = Time.time - startTime;
                if (r.isPlayer)
                {
                    Racers.Sort((a, b) => b.Score(Line.length).CompareTo(a.Score(Line.length))); // final standings now
                    CurrentState = State.Finished;
                    Finished?.Invoke(r);
                }
                return;
            }
            r.lap++;
            if (r.lap == TotalLaps) FinalLap?.Invoke(r);
        }

        public float CurrentLapTime(Racer r)
        {
            if (r == null || CurrentState == State.Countdown) return 0f;
            if (r.finished) return r.lapTimes.Count > 0 ? r.lapTimes[r.lapTimes.Count - 1] : 0f;
            return Time.time - r.lapStartTime;
        }

        static float Side(RaceLine.Gate gate, Vector3 p) { return Vector3.Dot(p - gate.position, gate.forward); }

        public static string FormatTime(float t)
        {
            if (t <= 0f) return "--:--.---";
            int m = (int)(t / 60f);
            float s = t - m * 60f;
            return m.ToString("00") + ":" + s.ToString("00.000");
        }
    }
}
