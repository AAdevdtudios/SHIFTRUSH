using ShiftRush.Core;
using ShiftRush.Vehicles;
using UnityEngine;

namespace ShiftRush.Race
{
    /// <summary>
    /// Lines the field up behind the start line (staggered two-wide) and spawns the rivals, set to the race's
    /// <see cref="Difficulty"/>: the player's grid slot, the rivals' pace and how much they ease off or push.
    /// </summary>
    public static class RaceGrid
    {
        public static Vector3 Slot(RaceLine line, RivalField f, int slot, out Quaternion rot, out float lateral)
        {
            int row = slot / 2, col = slot % 2;
            float back = f.firstRowBack + row * f.rowSpacing + col * f.rowSpacing * 0.5f;
            float d = line.length - back;
            int i = line.IndexAt(d);
            rot = Quaternion.LookRotation(line.Direction(i), Vector3.up);
            lateral = col == 0 ? -f.laneSpacing : f.laneSpacing;
            Vector3 pos = line.PointAt(d) + line.Right(i) * lateral;
            // Sit on the road surface (ignore cars).
            float best = float.MinValue;
            foreach (var hit in Physics.RaycastAll(pos + Vector3.up * 2.5f, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.rigidbody == null && hit.point.y > best) best = hit.point.y;
            if (best > float.MinValue) pos.y = best;
            return pos + Vector3.up * 0.6f;
        }

        public static void Fill(RaceManager race, RaceManager.Racer player)
        {
            var f = RivalField.Load();
            var line = race.Line;
            if (f == null || line == null || line.points == null || line.points.Length < 2) return;

            var level = Difficulty.Current;
            int total = f.rivals.Count + 1;
            int playerSlot = Mathf.Clamp(level.gridSlot, 0, total - 1);
            var raceField = Object.Instantiate(f); // this race's copy: the asset stays as authored
            raceField.pushFromBehind = level.push;
            raceField.easeOutFront = level.ease;
            raceField.nitroPerSecond = level.rivalNitro;
            if (player != null)
            {
                Quaternion pr; float pl;
                var pp = Slot(line, f, playerSlot, out pr, out pl);
                player.vehicle.Teleport(pp, pr, 0f);
            }
            if (f.carPrefab == null) return;

            int slot = 0;
            foreach (var rival in f.rivals)
            {
                if (slot == playerSlot) slot++;
                Quaternion rot; float lateral;
                var pos = Slot(line, f, slot, out rot, out lateral);
                slot++;
                var v = VehicleSpawn.Rival(f.carPrefab, pos, rot);
                if (v == null) continue;
                var go = v.Transform.gameObject;
                go.name = "Rival - " + rival.name;
                Paint(go, f.paintToReplace, rival.livery);
                var reset = go.GetComponent<VehicleReset>();
                if (reset != null) reset.listenForInput = false;
                if (level.carBoost != 1f) VehicleSpawn.Boost(go, level.carBoost);
                var racer = race.AddRacer(v, rival.name, false);
                racer.color = rival.color;
                go.AddComponent<RivalDriver>().Init(race, racer, Paced(rival, level.pace), raceField, lateral);
            }
        }

        static RivalField.Rival Paced(RivalField.Rival r, float pace)
        {
            return new RivalField.Rival
            {
                name = r.name, livery = r.livery, color = r.color, shortcutChance = r.shortcutChance,
                grip = r.grip * pace, topSpeedKmh = r.topSpeedKmh * pace,
            };
        }

        static void Paint(GameObject car, Material from, Material to)
        {
            if (from == null || to == null) return;
            foreach (var r in car.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++) if (mats[i] == from) { mats[i] = to; changed = true; }
                if (changed) r.sharedMaterials = mats;
            }
        }
    }
}
