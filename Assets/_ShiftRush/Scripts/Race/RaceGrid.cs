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

            // A story episode with a named cast: only those people, in their own cars, driving their own way.
            var ep = ShiftRush.Core.StoryProgress.ActiveEpisode;
            var cast = ShiftRush.Core.RivalCast.Instance;
            if (ep != null && ep.opponents != null && ep.opponents.Length > 0 && cast != null)
            {
                FillStory(race, player, line, f, raceField, level, ep, cast);
                return;
            }
            if (player != null)
            {
                Quaternion pr; float pl;
                var pp = Slot(line, f, playerSlot, out pr, out pl);
                player.vehicle.Teleport(pp, pr, 0f);
            }
            if (f.carPrefab == null) return;

            int slot = 0, n = 0;
            foreach (var source in f.rivals)
            {
                // A story episode can put its named rival (Vex, Kaito) in the first rival slot, a touch quicker.
                var rival = source;
                if (n++ == 0 && ShiftRush.Core.StoryProgress.RivalName != null)
                {
                    rival = Paced(source, ShiftRush.Core.StoryProgress.RivalPace);
                    rival.name = ShiftRush.Core.StoryProgress.RivalName;
                }
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

        /// <summary>
        /// The episode's opponents on the grid ahead of the player (who starts last: in a 1v1 that's alongside).
        /// Their pace is the difficulty's, times the episode's rival pace and heat; heat also sets how hard they
        /// push from behind and how little they ease off out front.
        /// </summary>
        static void FillStory(RaceManager race, RaceManager.Racer player, RaceLine line, RivalField f, RivalField raceField,
            Difficulty.Tuning level, ShiftRush.Core.StoryCatalog.Episode ep, ShiftRush.Core.RivalCast cast)
        {
            float heat = Mathf.Max(0.5f, ep.heat);
            raceField.pushFromBehind = level.push * heat;
            raceField.easeOutFront = level.ease / heat;
            raceField.nitroPerSecond = level.rivalNitro * heat;
            int total = ep.opponents.Length + 1;
            if (player != null)
            {
                Quaternion pr; float pl;
                var pp = Slot(line, f, total - 1, out pr, out pl);
                player.vehicle.Teleport(pp, pr, 0f);
            }
            var catalog = ShiftRush.Core.RaceCatalog.Instance;
            float pace = level.pace * ep.rivalPace * heat;
            for (int i = 0; i < ep.opponents.Length; i++)
            {
                var who = cast.Find(ep.opponents[i]);
                if (who == null) continue;
                GameObject prefab = null;
                if (catalog != null) foreach (var c in catalog.cars) if (c.id == who.carId) { prefab = c.prefab; break; }
                if (prefab == null) prefab = f.carPrefab;
                if (prefab == null) continue;
                Quaternion rot; float lateral;
                var pos = Slot(line, f, i, out rot, out lateral);
                var v = VehicleSpawn.Rival(prefab, pos, rot);
                if (v == null) continue;
                var go = v.Transform.gameObject;
                go.name = "Rival - " + who.name;
                var reset = go.GetComponent<VehicleReset>();
                if (reset != null) reset.listenForInput = false;
                if (level.carBoost != 1f) VehicleSpawn.Boost(go, level.carBoost);
                var racer = race.AddRacer(v, who.name, false);
                racer.color = who.color;
                var profile = new RivalField.Rival
                {
                    name = who.name, color = who.color, grip = who.grip * pace, topSpeedKmh = who.topSpeedKmh * pace,
                    aggression = who.aggression, driftSkill = who.driftSkill, shortcutChance = who.shortcutChance,
                };
                go.AddComponent<RivalDriver>().Init(race, racer, profile, raceField, lateral);
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
