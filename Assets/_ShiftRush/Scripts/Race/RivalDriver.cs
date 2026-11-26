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
    /// They use the player's moves too, by their profile: drift the tight corners (<see cref="RivalField.Rival.driftSkill"/>),
    /// spiral roll into a car alongside or forward-roll into one just ahead (<see cref="RivalField.Rival.aggression"/>),
    /// and roll out of the way of a roll coming at them.
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

        // moves
        GripMoves moves;
        DriftMoves drift;
        float nextMoveCheck, lastRollAt = -10f, dodgeAt = -1f;
        int dodgeDir, driftCornerAt = -1000, driftDir;
        bool driftThisCorner;
        float driftSince;
        const float RollGap = 4f; // seconds between a rival's own rolls
        float raceStartedAt;

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

            moves = GetComponent<GripMoves>();
            if (moves == null) moves = gameObject.AddComponent<GripMoves>();
            moves.playerControlled = false;
            drift = GetComponent<DriftMoves>();
            if (drift == null) drift = gameObject.AddComponent<DriftMoves>();
            drift.playerControlled = false;
            if (GetComponent<RivalGrounding>() == null) gameObject.AddComponent<RivalGrounding>();
            GripMoves.Leapt -= OnSomeoneRolled;
            GripMoves.Leapt += OnSomeoneRolled;
        }

        void OnDestroy() { GripMoves.Leapt -= OnSomeoneRolled; }

        /// <summary>A roll coming this way: a sharp rival rolls out of its path.</summary>
        void OnSomeoneRolled(GripMoves roller, int dir)
        {
            if (roller == moves || moves == null || dir == 0 || profile == null) return;
            Vector3 rel = roller.transform.InverseTransformPoint(transform.position); // me, from the roller
            if (rel.magnitude > 9f || Mathf.Abs(rel.z) > 3.5f) return;
            if (Mathf.Sign(rel.x) != dir) return; // rolling away from me
            if (Random.value > 0.25f + profile.driftSkill * 0.6f) return;
            dodgeDir = (int)Mathf.Sign(rel.x); // away from the roller: further the way it was rolling
            dodgeAt = Time.time + Random.Range(0.06f, 0.16f); // reaction time
        }

        void FixedUpdate()
        {
            if (race == null || line == null || input == null) return;
            float dt = Time.fixedDeltaTime;

            if (race.CurrentState == RaceManager.State.Countdown) { input.SetDriverInput(0f, 0f, 1f, 1f, false); raceStartedAt = Time.time; return; }

            Vector3 p = transform.position;
            Vector3 vel = vehicle.Body != null ? vehicle.Body.linearVelocity : Vector3.zero;
            float speed = new Vector3(vel.x, 0f, vel.z).magnitude;
            nearest = line.Nearest(p, nearest);
            float d = line.distances[nearest];
            // Corner speeds come from the line's curvature at 1 g; real cars hold a bit less, so keep a margin.
            float sqrtGrip = Mathf.Sqrt(profile.grip * 0.82f);
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
                // Anticipate: everything within reaction distance (about half a second) ahead, not just the next
                // few points. The line's speeds already carry the braking needed for what comes after.
                float reach = Mathf.Max(12f, speed * 0.55f);
                for (float ahead = 0f; ahead <= reach; ahead += 4f)
                    vLimit = Mathf.Min(vLimit, line.CornerSpeed(line.IndexAt(d + ahead)) * sqrtGrip);
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
            // In a drift the nose points into the corner, so steer by the direction of travel instead.
            if (drift != null && drift.Drifting && speed > 3f)
                ang = Vector3.SignedAngle(new Vector3(vel.x, 0f, vel.z), Vector3.ProjectOnPlane(target - p, Vector3.up), Vector3.up);
            float steer = Mathf.Clamp(ang / MaxSteerAngle, -1f, 1f);
            // Yaw damping: if the car is already rotating towards the target, ease off, so it doesn't weave.
            float yawRate = vehicle.Body != null ? vehicle.Body.angularVelocity.y : 0f;
            if (drift == null || !drift.Drifting) steer -= yawRate * Mathf.Lerp(0.05f, 0.22f, Mathf.InverseLerp(10f, 50f, speed));
            // Less lock at speed: small corrections on the straights, full lock only in slow corners.
            steer *= Mathf.Lerp(1f, 0.55f, Mathf.InverseLerp(25f, 65f, speed));
            steer = Mathf.Clamp(steer, -1f, 1f);
            steerSmooth = Mathf.MoveTowards(steerSmooth, steer, dt * 5f);

            // ---- throttle and brake
            float err = vLimit - speed, throttle, brake = 0f;
            if (err > 1.5f) throttle = 1f;
            else if (err > -1f) throttle = Mathf.Lerp(0.3f, 0.9f, (err + 1f) / 2.5f);
            else { throttle = 0f; brake = Mathf.Clamp01(-err * 0.35f); } // firm: a few m/s over is a real stop
            if (Mathf.Abs(steerSmooth) > 0.8f && speed > 25f) throttle *= 0.6f;
            // Traction: sliding sideways without meaning to drift, ease off until the tyres bite again.
            float slide = Mathf.Abs(Vector3.Dot(vel, transform.right));
            if ((drift == null || !drift.Drifting) && slide > 4f) throttle *= Mathf.Clamp01(1.4f - slide * 0.1f);

            // ---- nitro: earned on SHIFT ramps, fired on straights
            CheckRamps(p);
            if (tank != null && field.nitroPerSecond > 0f && !me.finished) tank.AddNitro(field.nitroPerSecond * dt);
            bool nitro = tank != null && tank.Nitro01 > 0.05f && err > 8f && Mathf.Abs(steerSmooth) < 0.2f && !me.finished;

            // ---- stuck: back on the line
            bool racing = race.CurrentState != RaceManager.State.Countdown && !me.suspended;
            stuckTime = racing && speed < 2f ? stuckTime + dt : 0f;
            if (stuckTime > 3f && reset != null) { stuckTime = 0f; onShortcut = null; reset.ResetNow(); }

            // ---- moves: drift the tight corners, roll to knock or dodge
            bool drifting = DriftCorner(speed, sqrtGrip);
            if (drifting) { throttle = 1f; brake = 0f; }
            UseRolls(p, speed);

            input.SetDriverInput(steerSmooth, throttle, brake, 0f, nitro);
        }

        /// <summary>Drift a corner that's tighter than the speed allows, if this rival is the drifting kind.</summary>
        bool DriftCorner(float speed, float sqrtGrip)
        {
            if (drift == null || me.finished) return false;
            float cornerLimit = float.MaxValue;
            for (int k = 2; k < 8; k++) cornerLimit = Mathf.Min(cornerLimit, line.CornerSpeed(nearest + k) * sqrtGrip);
            bool tight = cornerLimit < speed * 0.92f && Mathf.Abs(steerSmooth) > 0.35f;
            // One decision per corner, so a rival commits or doesn't.
            if (tight && Mathf.Abs(nearest - driftCornerAt) > 25) { driftCornerAt = nearest; driftThisCorner = Random.value < profile.driftSkill * drift.DriftEagerness; }
            bool hold;
            if (drift.Drifting)
            {
                // Like the player: the handbrake held while the corner keeps bending, let go as it straightens.
                bool bending = cornerLimit < speed * 1.1f;
                float least = Mathf.Lerp(0.2f, 0.5f, profile.driftSkill);
                hold = Time.time - driftSince < least || (bending && Time.time - driftSince < 3f);
                if (!hold) driftThisCorner = false; // one drift per corner
            }
            else
            {
                hold = driftThisCorner && tight && speed * 3.6f > drift.minKmh + 5f && Mathf.Abs(nearest - driftCornerAt) < 40
                       && Time.time - driftSince > 1.5f;
                if (hold) { driftDir = steerSmooth > 0f ? 1 : -1; driftSince = Time.time; }
            }
            drift.SetAIDrift(hold, driftDir);
            return drift.Drifting;
        }

        /// <summary>Spiral roll into a car alongside, forward-roll into one just ahead, or dodge a roll coming in.</summary>
        void UseRolls(Vector3 p, float speed)
        {
            if (moves == null || me.finished || race.CurrentState == RaceManager.State.Countdown) return;
            if (dodgeAt > 0f && Time.time >= dodgeAt)
            {
                dodgeAt = -1f;
                if (RoomToRoll(p, dodgeDir) && moves.TryLeap(dodgeDir)) lastRollAt = Time.time;
                return;
            }
            if (Time.time < nextMoveCheck || Time.time - lastRollAt < RollGap || speed < 12f) return;
            // Never mid-corner: a roll there throws the car off the road and looks like a crash.
            if (Mathf.Abs(steerSmooth) > 0.25f) return;
            // Not while the pack is still bunched up from the start: rolls there tangle two cars and both stop.
            if (Time.time - raceStartedAt < 8f) return;
            nextMoveCheck = Time.time + 0.25f;
            if (drift != null && drift.Drifting) return;

            foreach (var r in race.Racers)
            {
                if (r == me || r.suspended || r.vehicle == null) continue;
                Vector3 rel = transform.InverseTransformPoint(r.vehicle.Transform.position);
                // Alongside: roll into them. Chance per check scales with aggression.
                if (Mathf.Abs(rel.z) < 2.5f && Mathf.Abs(rel.x) > 1.8f && Mathf.Abs(rel.x) < 5.5f)
                {
                    int dir = rel.x > 0f ? 1 : -1;
                    if (Random.value < profile.aggression * 0.08f && RoomToRoll(p, dir) && moves.TryLeap(dir)) { lastRollAt = Time.time; return; }
                }
                // Just ahead and slower: forward roll into them.
                else if (rel.z > 3f && rel.z < 9f && Mathf.Abs(rel.x) < 1.6f)
                {
                    float theirs = r.vehicle.Body != null ? Vector3.Dot(r.vehicle.Body.linearVelocity, transform.forward) : speed;
                    if (speed - theirs > 3f && speed > 18f && Random.value < profile.aggression * 0.06f && moves.TryLeap(0)) { lastRollAt = Time.time; return; }
                }
            }
        }

        /// <summary>True if a sideways roll that way stays on the road.</summary>
        bool RoomToRoll(Vector3 p, int dir)
        {
            float lat = line.Lateral(p, nearest);
            float lateralDir = Vector3.Dot(transform.right * dir, line.Right(nearest));
            float landing = lat + lateralDir * (moves != null ? moves.rollSide : 8f);
            return Mathf.Abs(landing) < line.halfWidth - 1.5f;
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
