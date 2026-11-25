using System.Collections.Generic;
using ShiftRush.Race;
using ShiftRush.Vehicles;
using UnityEngine;

namespace ShiftRush.Lab
{
    /// <summary>
    /// A tuning aid, added by hand (or by the editor) to a race: per rival it counts resets (off track, flipped,
    /// stuck), hard wall hits and the average and best speed, and how far round the lap it is. Read it with
    /// <see cref="Report"/>. Not used in the game.
    /// </summary>
    public class RaceProbe : MonoBehaviour
    {
        class Stat { public int resets, hits; public float speedSum, best; public int samples; public float progress; }
        readonly Dictionary<string, Stat> stats = new Dictionary<string, Stat>();
        float t0;

        void OnEnable() { VehicleReset.Respawned += OnReset; t0 = Time.time; }
        void OnDisable() { VehicleReset.Respawned -= OnReset; }

        Stat Of(string n) { Stat s; if (!stats.TryGetValue(n, out s)) { s = new Stat(); stats[n] = s; } return s; }

        void OnReset(VehicleReset r)
        {
            var race = RaceManager.Current; if (race == null) return;
            foreach (var x in race.Racers) if (x.vehicle != null && x.vehicle.Transform == r.transform) Of(x.name).resets++;
        }

        void FixedUpdate()
        {
            var race = RaceManager.Current;
            if (race == null || race.CurrentState != RaceManager.State.Racing) return;
            foreach (var x in race.Racers)
            {
                if (x.isPlayer || x.vehicle == null) continue;
                var s = Of(x.name);
                float kmh = Mathf.Abs(x.vehicle.SpeedKmh);
                s.speedSum += kmh; s.samples++; s.best = Mathf.Max(s.best, kmh);
                s.progress = (x.lap - 1) + (race.Line != null ? x.progress / race.Line.length : 0f);
                // A sudden loss of speed above 60 km/h is a wall or a car.
                var b = x.vehicle.Body;
                if (b != null)
                {
                    float last;
                    if (lastSpeed.TryGetValue(x.name, out last) && last - kmh > 35f && last > 60f) s.hits++;
                    lastSpeed[x.name] = kmh;
                }
            }
        }

        readonly Dictionary<string, float> lastSpeed = new Dictionary<string, float>();

        public string Report()
        {
            var sb = new System.Text.StringBuilder("probe " + (Time.time - t0).ToString("F0") + "s: ");
            foreach (var kv in stats)
                sb.Append(kv.Key + " resets " + kv.Value.resets + " hits " + kv.Value.hits + " avg " + (kv.Value.samples > 0 ? kv.Value.speedSum / kv.Value.samples : 0).ToString("F0")
                    + " best " + kv.Value.best.ToString("F0") + " laps " + kv.Value.progress.ToString("F2") + "; ");
            return sb.ToString();
        }
    }
}
