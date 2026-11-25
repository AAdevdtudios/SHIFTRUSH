using ShiftRush.Race;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// SHIFT RUSH → Tracks → Clean Up Open Track: fixes the things that look wrong on the open-country tracks.
    /// Removes the shortcut lanes (they overlap the main road where they peel off), rebuilds the invisible walls
    /// without their openings, repaints snow and snow-cliff terrain as river rock and grass on tracks that aren't
    /// snowy, and drops anything floating near the circuit back onto the ground. Safe to rerun.
    /// </summary>
    public static class TrackCleanup
    {
        [MenuItem("SHIFT RUSH/Tracks/Clean Up Open Track")]
        public static string Run()
        {
            string report = "";
            report += RemoveShortcuts();
            report += RepaintSnow();
            report += GroundProps();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[TRACK] " + report);
            return report;
        }

        static string RemoveShortcuts()
        {
            int n = 0;
            foreach (var c in Object.FindObjectsByType<ShortcutPath>(FindObjectsSortMode.None)) { Object.DestroyImmediate(c.gameObject); n++; }
            var group = GameObject.Find("Shortcuts");
            if (group != null && group.transform.childCount == 0) Object.DestroyImmediate(group);
            var line = Object.FindFirstObjectByType<RaceLine>();
            if (n > 0 && line != null)
            {
                TrackBuilder.LinkShortcuts(line);
                TrackBuilder.AddTrackWalls(); // closes the openings the shortcuts had
            }
            return "shortcuts removed " + n + "; ";
        }

        /// <summary>Snow (and the snow-cliff rock) moved onto river rock on slopes and grass elsewhere.</summary>
        static string RepaintSnow()
        {
            var t = Object.FindFirstObjectByType<Terrain>();
            if (t == null) return "no terrain; ";
            var data = t.terrainData;
            var layers = data.terrainLayers;
            int snow = -1, cliff = -1, rock = -1, grass = -1;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] == null) continue;
                string n = layers[i].name.ToLowerInvariant();
                if (n.Contains("snowcliff")) cliff = i;
                else if (n.Contains("snow")) snow = i;
                else if (n.Contains("riverrock")) rock = i;
                else if (grass < 0 && n.Contains("grass")) grass = i;
            }
            if (rock < 0 || grass < 0 || (snow < 0 && cliff < 0)) return "terrain needs no repaint; ";
            int w = data.alphamapWidth, h = data.alphamapHeight;
            var maps = data.GetAlphamaps(0, 0, w, h);
            int changed = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float moved = 0f;
                    if (cliff >= 0) { float c = maps[y, x, cliff]; if (c > 0f) { maps[y, x, rock] += c; maps[y, x, cliff] = 0f; moved += c; } }
                    if (snow >= 0) { float s = maps[y, x, snow]; if (s > 0f) { maps[y, x, grass] += s; maps[y, x, snow] = 0f; moved += s; } }
                    if (moved > 0f) changed++;
                }
            data.SetAlphamaps(0, 0, maps);
            EditorUtility.SetDirty(data);
            return "terrain repainted " + changed + " cells; ";
        }

        /// <summary>Props near the circuit hanging above the ground (the terrain was lowered under them) put back down.</summary>
        static string GroundProps()
        {
            var line = Object.FindFirstObjectByType<RaceLine>();
            if (line == null) return "";
            int moved = 0;
            foreach (var prop in Object.FindObjectsByType<BreakableProp>(FindObjectsSortMode.None))
            {
                var go = prop.gameObject;
                var rs = go.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                float best = float.MinValue;
                foreach (var hit in Physics.RaycastAll(new Vector3(b.center.x, b.max.y + 50f, b.center.z), Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.transform.IsChildOf(go.transform)) continue;
                    if (hit.collider.GetComponentInParent<BreakableProp>() != null) continue;
                    if (hit.point.y > best) best = hit.point.y;
                }
                if (best == float.MinValue) continue;
                float gap = b.min.y - best;
                if (gap > 0.35f) { go.transform.position -= Vector3.up * (gap - 0.05f); moved++; }
            }
            return "props grounded " + moved + "; ";
        }
    }
}
