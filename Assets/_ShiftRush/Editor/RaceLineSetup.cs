using System.Collections.Generic;
using System.IO;
using ShiftRush.Race;
using SplineArchitect;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// Adds the racing line to a built circuit: a centreline sampled from the road spline starting at the
    /// start/finish line, checkpoint gates every ~120 m, and a minimap image. Runs automatically at the end of
    /// a track build; SHIFT RUSH → Tracks → Add Race Line To Tracks redoes both circuits without rebuilding them.
    /// </summary>
    public static partial class TrackBuilder
    {
        const float LineStep = 4f, GateSpacing = 120f, GateHalfWidth = SurfaceHalf + 4f;

        [MenuItem("SHIFT RUSH/Tracks/Add Race Line To Tracks")]
        static void AddRaceLineToTracks()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (var s in new[] { AlpinePass(), GlacierValley() })
            {
                var scene = EditorSceneManager.OpenScene(s.scene, OpenSceneMode.Single);
                var spline = Object.FindFirstObjectByType<Spline>();
                if (spline == null) { Debug.LogWarning("No road spline in " + s.scene); continue; }
                AddRaceLine(spline, s);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        /// <summary>Your own tracks: adds the race line to the open scene from its road spline (needs a Start Grid
        /// with a start line, like the built tracks), then the SHIFT ramps, and saves.</summary>
        [MenuItem("SHIFT RUSH/Tracks/This Scene: Add Race Line + SHIFT Ramps + Walls")]
        static void AddToThisScene()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode first."); return; }
            var spline = Object.FindFirstObjectByType<Spline>();
            if (spline == null) { EditorUtility.DisplayDialog("SHIFT//RUSH", "This scene has no Spline Architect spline (the road).", "OK"); return; }
            var scene = EditorSceneManager.GetActiveScene();
            var s = new Spec { name = scene.name.Replace("Track_", ""), scene = scene.path };
            AddRaceLine(spline, s);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ShiftKit.RampPath) == null) ShiftKit.Build();
            AddShiftRamps(s);
            AddTrackWalls();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        public static RaceLine AddRaceLine(Spline spline, Spec s)
        {
            foreach (var old in Object.FindObjectsByType<RaceLine>(FindObjectsSortMode.None)) Object.DestroyImmediate(old.gameObject);

            var road = SampleSpline(spline, LineStep);
            int n = road.Count;

            // Start/finish: the painted start line, facing race direction.
            Vector3 startPos = road[0].pos, startFwd = road[0].dir;
            var grid = GameObject.Find("Start Grid");
            if (grid != null)
                foreach (Transform c in grid.transform)
                    if (c.name.Contains("StartLine")) { startPos = c.position; startFwd = c.forward; }

            int startIndex = 0; float bestD = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                float d = (road[i].pos - startPos).sqrMagnitude;
                if (d < bestD) { bestD = d; startIndex = i; }
            }
            bool reversed = Vector3.Dot(road[startIndex].dir, startFwd) < 0f;

            var pts = new Vector3[n];
            for (int k = 0; k < n; k++)
            {
                int i = reversed ? ((startIndex - k) % n + n) % n : (startIndex + k) % n;
                pts[k] = road[i].pos;
            }
            pts[0] = new Vector3(startPos.x, pts[0].y, startPos.z);
            var dist = new float[n];
            for (int k = 1; k < n; k++) dist[k] = dist[k - 1] + Vector3.Distance(pts[k - 1], pts[k]);
            float length = dist[n - 1] + Vector3.Distance(pts[n - 1], pts[0]);

            var go = new GameObject("Race Line");
            var line = go.AddComponent<RaceLine>();
            line.laps = 3;
            line.points = pts;
            line.distances = dist;
            line.length = length;

            int gateCount = Mathf.Max(4, Mathf.RoundToInt(length / GateSpacing));
            var gates = new List<RaceLine.Gate>();
            for (int g = 0; g < gateCount; g++)
            {
                float target = length * g / gateCount;
                int k = 0;
                while (k < n - 1 && dist[k + 1] <= target) k++;
                var fwd = g == 0 ? startFwd : line.Direction(k);
                fwd.y = 0f;
                gates.Add(new RaceLine.Gate { position = g == 0 ? startPos : pts[k], forward = fwd.normalized, halfWidth = GateHalfWidth });
            }
            line.gates = gates.ToArray();

            line.halfWidth = SurfaceHalf;
            LinkShortcuts(line);
            Rect bounds;
            line.mapSprite = DrawMinimap(pts, s.name, out bounds);
            line.mapBounds = bounds;
            Debug.Log("[SHIFT//RUSH] Race line on " + s.name + ": " + length.ToString("0") + " m, " + gateCount + " gates, " + line.laps + " laps");
            return line;
        }

        /// <summary>White-on-clear track outline with a start tick; the HUD tints it.</summary>
        internal static Sprite DrawMinimap(Vector3[] pts, string trackName, out Rect bounds)
        {
            const int size = 1024; const float pad = 0.08f, thick = 7f;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var p in pts) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z); }
            float span = Mathf.Max(maxX - minX, maxZ - minZ) * (1f + pad * 2f);
            float cx = (minX + maxX) * 0.5f, cz = (minZ + maxZ) * 0.5f;
            var b = new Rect(cx - span * 0.5f, cz - span * 0.5f, span, span);
            bounds = b;

            var px = new float[size * size];
            System.Func<Vector3, Vector2> toPx = p => new Vector2((p.x - b.xMin) / span * size, (p.z - b.yMin) / span * size);
            for (int i = 0; i < pts.Length; i++)
                Segment(px, size, toPx(pts[i]), toPx(pts[(i + 1) % pts.Length]), thick);
            // Start/finish tick across the road.
            Vector2 s0 = toPx(pts[0]), s1 = toPx(pts[1]);
            Vector2 dir = (s1 - s0).normalized, perp = new Vector2(-dir.y, dir.x);
            Segment(px, size, s0 - perp * 22f, s0 + perp * 22f, 4f);
            // Shortcuts: thinner, dashed.
            foreach (var cut in Object.FindObjectsByType<ShortcutPath>(FindObjectsSortMode.None))
                for (int i = 0; i < cut.points.Length - 1; i++)
                    if ((i / 2) % 2 == 0) Segment(px, size, toPx(cut.points[i]), toPx(cut.points[i + 1]), 3.5f);

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var cols = new Color32[size * size];
            for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(px[i]) * 255));
            tex.SetPixels32(cols);
            string path = ArtTrack + "T_Map_" + trackName.Replace(" ", "") + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void Segment(float[] px, int size, Vector2 a, Vector2 b, float r)
        {
            int x0 = Mathf.Max(0, (int)(Mathf.Min(a.x, b.x) - r - 2)), x1 = Mathf.Min(size - 1, (int)(Mathf.Max(a.x, b.x) + r + 2));
            int y0 = Mathf.Max(0, (int)(Mathf.Min(a.y, b.y) - r - 2)), y1 = Mathf.Min(size - 1, (int)(Mathf.Max(a.y, b.y) + r + 2));
            Vector2 ab = b - a; float len2 = Mathf.Max(ab.sqrMagnitude, 1e-4f);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                    float d = Vector2.Distance(p, a + ab * t);
                    float v = Mathf.Clamp01(r - d + 0.5f);
                    int i = y * size + x;
                    if (v > px[i]) px[i] = v;
                }
        }
    }
}
