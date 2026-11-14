using System.Collections.Generic;
using ShiftRush.Shift;
using ShiftRush.Vehicles;
using UnityEngine;

namespace ShiftRush.Race
{
    /// <summary>
    /// Drives a rival round the race line. Steering chases a point a speed-dependent distance ahead (offset into
    /// the rival's lane); speed comes from the line's corner limits, scaled by the rival's grip, so they brake
    /// for bends the way a driver would. They pull out to pass cars in front, sometimes take shortcuts, earn
    /// nitro when they jump a SHIFT ramp and fire it on straights. A rival that gets stuck is put back on the line.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class RivalDriver : MonoBehaviour
    {
        const float MaxSteerAngle = 26f;

        RaceManager race;
        RaceManager.Racer me;
        RaceLine line;
        IVehicle vehicle;
        IDriverInput input;
        INitroTank tank;
        VehicleReset reset;
        RivalField.Rival profile;
        RivalField field;

        int nearest = -1;
        float lane, baseLane, stuckTime, steerSmooth, wantLane = float.NaN;
        ShortcutPath onShortcut, decidedFor;
        bool decidedTake;
        readonly List<Transform> ramps = new List<Transform>();
        Transform lastRamp;

        public void Init(RaceManager race, RaceManager.Racer me, RivalField.Rival profile, RivalField field, float startLane)
        {
            this.race = race; this.me = me; this.profile = profile; this.field = field;
            line = race.Line;
            vehicle = me.vehicle;
            input = vehicle as IDriverInput;
            tank = vehicle as INitroTank;
            reset = GetComponent<VehicleReset>();
            lane = startLane;
            // Each rival keeps to its own part of the road, so they don't all queue on the centreline.
            baseLane = Mathf.Clamp(startLane * 0.5f + Random.Range(-2f, 2f), -5f, 5f);
            foreach (var r in FindObjectsByType<ShiftRamp>(FindObjectsSortMode.None)) ramps.Add(r.transform);
        }

        void FixedUpdate()
        {
            if (race == null || line == null || input == null) return;
            float dt = Time.fixedDeltaTime;

            if (race.CurrentState == RaceManager.State.Countdown) { input.SetDriverInput(0f, 0f, 1f, 1f, false); return; }

            Vector3 p = transform.position;
            Vector3 vel = vehicle.Body != null ? vehicle.Body.linearVelocity : Vector3.zero;
            float speed = new Vector3(vel.x, 0f, vel.z).magnitude;
            nearest = line.Nearest(p, nearest);
            float d = line.distances[nearest];
            float sqrtGrip = Mathf.Sqrt(profile.grip);
            float look = Mathf.Clamp(6f + speed * 0.5f, 9f, 42f);

            // ---- where to aim, and how fast that part of the road can be taken
            Vector3 target;
            float vLimit = profile.topSpeedKmh / 3.6f;
            wantLane = float.NaN;
            ChooseShortcut(p, d, speed, ref vLimit, sqrtGrip);
            if (onShortcut != null)
            {
                int si; float along;
                onShortcut.DistanceTo(p, out si, out along);
                target = onShortcut.PointAlong(along + look);
                for (int k = 0; k < 4; k++) vLimit = Mathf.Min(vLimit, onShortcut.CornerSpeed(si + k) * sqrtGrip);
                if (along > onShortcut.Length - 10f) onShortcut = null; // back on the main road
            }
            else
            {
                float laneTarget = Avoid(p, ref vLimit);
                if (!float.IsNaN(wantLane)) laneTarget = wantLane;
                lane = Mathf.MoveTowards(lane, laneTarget, dt * 3.5f);
                float td = d + look;
                int ti = line.IndexAt(td);
                target = line.PointAt(td) + line.Right(ti) * lane;
                for (int k = 0; k < 4; k++) vLimit = Mathf.Min(vLimit, line.CornerSpeed(nearest + k) * sqrtGrip);
            }

            // ---- rubber band: a little help from behind, a little drag out front
            if (race.Player != null && !me.finished)
            {
                float gap = race.Player.Score(line.length) - me.Score(line.length);
                float band = Mathf.Clamp(gap / 300f, -1f, 1f); // + = the player is ahead of this rival
                float push = field.pushFromBehind >= 0f ? field.pushFromBehind : field.catchUp;
                float ease = field.easeOutFront >= 0f ? field.easeOutFront : field.catchUp;
                vLimit *= 1f + band * (band > 0f ? push : ease);
            }
            if (me.finished) vLimit *= 0.55f; // cool-down lap

            // ---- steering
            Vector3 local = transform.InverseTransformPoint(target);
            float ang = Mathf.Atan2(local.x, Mathf.Max(local.z, 0.1f)) * Mathf.Rad2Deg;
            float steer = Mathf.Clamp(ang / MaxSteerAngle, -1f, 1f);
            steerSmooth = Mathf.MoveTowards(steerSmooth, steer, dt * 6f);

            // ---- throttle and brake
            float err = vLimit - speed, throttle, brake = 0f;
            if (err > 1.5f) throttle = 1f;
            else if (err > -1f) throttle = Mathf.Lerp(0.3f, 0.9f, (err + 1f) / 2.5f);
            else { throttle = 0f; brake = Mathf.Clamp01(-err * 0.12f); }
            if (Mathf.Abs(steerSmooth) > 0.8f && speed > 25f) throttle *= 0.6f;

            // ---- nitro: earned on SHIFT ramps, fired on straights
            CheckRamps(p);
            if (tank != null && field.nitroPerSecond > 0f && !me.finished) tank.AddNitro(field.nitroPerSecond * dt);
            bool nitro = tank != null && tank.Nitro01 > 0.05f && err > 8f && Mathf.Abs(steerSmooth) < 0.2f && !me.finished;

            // ---- stuck: back on the line
            bool racing = race.CurrentState != RaceManager.State.Countdown && !me.suspended;
            stuckTime = racing && speed < 2f ? stuckTime + dt : 0f;
            if (stuckTime > 3f && reset != null) { stuckTime = 0f; onShortcut = null; reset.ResetNow(); }

            input.SetDriverInput(steerSmooth, throttle, brake, 0f, nitro);
        }

        /// <summary>Lane to aim for: own lane, or round the side of a car in front. Slows if boxed in.</summary>
        float Avoid(Vector3 p, ref float vLimit)
        {
            float edge = line.halfWidth - 3f;
            float laneTarget = baseLane, closest = float.MaxValue;
            foreach (var r in race.Racers)
            {
                if (r == me || r.suspended) continue;
                Vector3 op = r.vehicle.Transform.position;
                Vector3 rel = transform.InverseTransformPoint(op);
                if (rel.z <= 0f || rel.z > 24f || Mathf.Abs(rel.x) > 3.4f) continue;
                float otherLane = line.Lateral(op, nearest);
                float side = Mathf.Abs(otherLane) < 1.5f ? (lane >= 0f ? 1f : -1f) : -Mathf.Sign(otherLane);
                float pass = otherLane + side * 4.6f;
                if (Mathf.Abs(pass) > edge) pass = otherLane - side * 4.6f; // no room that side: the other
                laneTarget = Mathf.Clamp(pass, -edge, edge);
                if (rel.z < closest)
                {
                    closest = rel.z;
                    float theirs = r.vehicle.Body != null ? Vector3.Dot(r.vehicle.Body.linearVelocity, transform.forward) : 0f;
                    if (rel.z < 10f) vLimit = Mathf.Min(vLimit, theirs + rel.z * 0.4f);
                }
            }
            return laneTarget;
        }

        void ChooseShortcut(Vector3 p, float d, float speed, ref float vLimit, float sqrtGrip)
        {
            if (onShortcut != null) return;
            foreach (var sc in ShortcutPath.All)
            {
                float ahead = Mathf.Repeat(sc.entryDistance - d, line.length);
                if (ahead > 90f) continue;
                if (decidedFor != sc) { decidedFor = sc; decidedTake = Random.value < profile.shortcutChance; }
                if (!decidedTake) continue;
                // Brake in time for the turn-in, and move over to that side of the road.
                float entry = sc.EntrySpeed * sqrtGrip;
                vLimit = Mathf.Min(vLimit, Mathf.Sqrt(entry * entry + 2f * 7.5f * ahead));
                if (ahead < 70f) wantLane = sc.Side * (line.halfWidth - 4f);
                if (ahead < 4f || sc.DistanceTo(p) < 2.5f) onShortcut = sc;
            }
        }

        void CheckRamps(Vector3 p)
        {
            if (tank == null) return;
            foreach (var r in ramps)
            {
                if (r == null || r == lastRamp) continue;
                if ((r.position - p).sqrMagnitude < 8f * 8f)
                {
                    lastRamp = r;
                    tank.AddNitro(Random.Range(field.rampNitro.x, field.rampNitro.y));
                }
            }
            // Forget the last ramp once well past it, so it pays again next lap.
            if (lastRamp != null && (lastRamp.position - p).sqrMagnitude > 200f * 200f) lastRamp = null;
        }

        void OnDisable() { if (input != null) input.ReleaseDriverInput(); }
    }
}
