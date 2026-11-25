using System.Collections.Generic;
using System.IO;
using SplineArchitect;
using SplineArchitect.Utility;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// Builds the SHIFT//RUSH circuits. Each track is a looping Spline Architect spline (drag its points to reshape
    /// the road, guardrails and posts follow), a generated terrain shaped around the road, and Synty Alpine scenery.
    /// SHIFT RUSH → Tracks → Build Alpine Pass / Build Glacier Valley. Rebuilding regenerates the scene.
    /// </summary>
    public static partial class TrackBuilder
    {
        const string Root = "Assets/_ShiftRush/";
        const string ArtTrack = Root + "Art/Track/";
        const string Synty = "Assets/Synty/PolygonNatureBiomes/PNB_Alpine_Mountain/";
        /// <summary>Asphalt width. Arcade racing needs room for 3–4 cars abreast.</summary>
        public const float RoadWidth = 18f;
        /// <summary>Gravel run-off each side of the asphalt, part of the road mesh, inside the rails.</summary>
        public const float ShoulderWidth = 3f;
        /// <summary>Half the drivable surface (asphalt + shoulders); rails stand just outside it.</summary>
        public const float SurfaceHalf = RoadWidth * 0.5f + ShoulderWidth;

        // ------------------------------------------------------------------ track definitions

        public class Spec
        {
            public string name, scene;
            public Vector3[] sketch;        // x, z from the layout sketch (pixels), y = elevation in metres
            public float targetLength;      // metres; the sketch is scaled to this
            public bool snowy;
            public Vector2 galleryRange;    // fraction of the lap covered by the avalanche gallery (0 = none)
            public Vector2 bridgeRange;     // fraction of the lap on the bridge
            public Vector2 lakeSketch; public float lakeRadiusSketch;
            public Vector2[] riverSketch;
            public Vector2[] villageSketch;
            public int seed;
            public string kroma; // KROMA sky profile asset name
        }

        public static Spec AlpinePass()
        {
            return new Spec
            {
                name = "Alpine Pass", scene = Root + "Scenes/Track_AlpinePass.unity", snowy = true, seed = 11, kroma = "Kroma_Profile_30_ColdBreath",
                targetLength = 2600f, galleryRange = new Vector2(0.80f, 0.90f),
                sketch = new[]
                {
                    new Vector3(95, 0, 330), new Vector3(165, 0, 330), new Vector3(235, 2, 330), new Vector3(282, 6, 305),
                    new Vector3(290, 10, 260), new Vector3(258, 16, 224), new Vector3(220, 20, 205), new Vector3(170, 25, 195),
                    new Vector3(122, 30, 186), new Vector3(100, 35, 162), new Vector3(125, 40, 145), new Vector3(165, 44, 143),
                    new Vector3(205, 48, 138), new Vector3(222, 52, 118), new Vector3(200, 55, 100), new Vector3(155, 55, 100),
                    new Vector3(110, 52, 105), new Vector3(74, 46, 125), new Vector3(62, 40, 160), new Vector3(64, 28, 210),
                    new Vector3(70, 16, 260), new Vector3(78, 6, 312),
                },
            };
        }

        public static Spec GlacierValley()
        {
            return new Spec
            {
                name = "Glacier Valley", scene = Root + "Scenes/Track_GlacierValley.unity", snowy = false, seed = 23, kroma = "Kroma_Profile_33_LowPolyMorning",
                targetLength = 2400f, bridgeRange = new Vector2(0.155f, 0.19f),
                lakeSketch = new Vector2(498, 175), lakeRadiusSketch = 52f,
                riverSketch = new[] { new Vector2(540, 195), new Vector2(580, 222), new Vector2(615, 240), new Vector2(660, 252), new Vector2(720, 262) },
                villageSketch = new[] { new Vector2(560, 300), new Vector2(585, 295), new Vector2(600, 308), new Vector2(548, 312) },
                sketch = new[]
                {
                    new Vector3(420, 0, 330), new Vector3(490, 0, 330), new Vector3(560, 0, 330), new Vector3(605, 1, 325),
                    new Vector3(628, 2, 290), new Vector3(620, 3, 258), new Vector3(600, 3.5f, 238), new Vector3(590, 3, 215),
                    new Vector3(600, 3, 180), new Vector3(606, 5, 140), new Vector3(590, 7, 105), new Vector3(565, 8, 90),
                    new Vector3(510, 7, 80), new Vector3(455, 6, 88), new Vector3(415, 5, 105), new Vector3(400, 4, 140),
                    new Vector3(402, 3, 180), new Vector3(415, 2, 215), new Vector3(410, 1, 255), new Vector3(400, 0.5f, 295),
                    new Vector3(405, 0, 322),
                },
            };
        }

        [MenuItem("SHIFT RUSH/Tracks/Build Alpine Pass")]
        public static void BuildAlpine() { Build(AlpinePass()); }

        [MenuItem("SHIFT RUSH/Tracks/Build Glacier Valley")]
        public static void BuildValley() { Build(GlacierValley()); }

        // ------------------------------------------------------------------ geometry helpers

        /// <summary>Sketch → world: centred on the origin, flipped so sketch-down is world-back, scaled to length.</summary>
        public static List<Vector3> WorldPoints(Spec s, out float scale, out Vector3 offset)
        {
            Vector3 c = Vector3.zero;
            foreach (var p in s.sketch) c += new Vector3(p.x, 0, p.z);
            c /= s.sketch.Length;
            float perimeter = 0f;
            for (int i = 0; i < s.sketch.Length; i++)
            {
                var a = s.sketch[i]; var b = s.sketch[(i + 1) % s.sketch.Length];
                perimeter += Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
            }
            scale = s.targetLength / (perimeter * 1.04f); // curves run a little longer than the polygon
            offset = c;
            var list = new List<Vector3>();
            foreach (var p in s.sketch) list.Add(ToWorld(new Vector2(p.x, p.z), scale, offset, p.y));
            return list;
        }

        public static Vector3 ToWorld(Vector2 sketch, float scale, Vector3 offset, float y = 0f)
        {
            return new Vector3((sketch.x - offset.x) * scale, y, -(sketch.y - offset.z) * scale);
        }

        /// <summary>Evenly spaced samples along the built spline (position + direction), every `step` metres.</summary>
        public static List<Sample> SampleSpline(Spline spline, float step)
        {
            spline.RebuildCache();
            var list = new List<Sample>();
            int n = Mathf.Max(8, Mathf.CeilToInt(spline.Length / step));
            for (int i = 0; i < n; i++)
            {
                float fixedTime = spline.TimeToFixedTime(i / (float)n);
                list.Add(new Sample
                {
                    pos = spline.GetPosition(fixedTime),
                    dir = spline.GetDirection(fixedTime).normalized,
                    t = i / (float)n,
                    dist = spline.Length * i / n,
                });
            }
            return list;
        }

        public struct Sample { public Vector3 pos, dir; public float t, dist; }

        /// <summary>Distance from a point to the road centreline (XZ), plus the road height and lap fraction there.</summary>
        public static float NearestOnRoad(List<Sample> road, Vector3 p, out float roadY, out float t)
        {
            float best = float.MaxValue; roadY = 0f; t = 0f;
            int n = road.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = road[i].pos, b = road[(i + 1) % n].pos;
                Vector2 a2 = new Vector2(a.x, a.z), b2 = new Vector2(b.x, b.z), p2 = new Vector2(p.x, p.z);
                Vector2 ab = b2 - a2;
                float k = Mathf.Clamp01(Vector2.Dot(p2 - a2, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                float d = (p2 - (a2 + ab * k)).sqrMagnitude;
                if (d < best) { best = d; roadY = Mathf.Lerp(a.y, b.y, k); t = Mathf.Lerp(road[i].t, road[(i + 1) % n].t == 0 ? 1f : road[(i + 1) % n].t, k); }
            }
            return Mathf.Sqrt(best);
        }

        public static bool InRange(float t, Vector2 range)
        {
            return range.y > range.x && t >= range.x && t <= range.y;
        }

        // ------------------------------------------------------------------ build

        public static void Build(Spec s)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode first."); return; }
            Directory.CreateDirectory(ArtTrack);
            var kit = TrackKit.Load();

            // Start from TestArena's setup (camera rig, dashboard, sky, post), then replace the ground and car.
            AssetDatabase.SaveAssets();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(s.scene);
            AssetDatabase.CopyAsset(Root + "Scenes/TestArena.unity", s.scene);
            var scene = EditorSceneManager.OpenScene(s.scene, OpenSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == "Plane" || go.GetComponent<RCCP_CarController>() != null) Object.DestroyImmediate(go);
            }

            var root = new GameObject("Track - " + s.name);
            float scale; Vector3 offset;
            var points = WorldPoints(s, out scale, out offset);

            var spline = BuildRoad(root.transform, points, s, kit);
            var road = SampleSpline(spline, 4f);
            if (s.riverSketch != null) s.bridgeRange = FindBridge(road, s, scale, offset);
            var cuts = FindShortcuts(road, s, scale, offset);
            AddRails(spline, road, cuts, kit);
            var lanes = cuts.ConvertAll(c => c.path);
            var terrain = BuildTerrain(root.transform, s, road, scale, offset, kit, lanes);
            BuildWater(root.transform, s, scale, offset, kit);
            BuildScenery(root.transform, s, road, terrain, scale, offset, kit, lanes);
            BuildShortcuts(root.transform, cuts, road, s, kit);
            PlaceStartAndCar(root.transform, s, road, kit);
            AddRaceLine(spline, s);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ShiftKit.RampPath) == null) ShiftKit.Build();
            AddShiftRamps(s);
            AddTrackWalls();

            ApplyKroma(s.kroma);
            VFXKitBuilder.AddAmbience(s.snowy);
            var setup = new GameObject("RCCP Session Setup");
            setup.AddComponent<ShiftRush.Vehicles.RCCPSessionSetup>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SHIFT//RUSH] Built " + s.name + ": " + spline.Length.ToString("0") + " m lap, " + s.scene);
        }

        /// <summary>The stretch of road where the river passes under it, padded to bridge length.</summary>
        static Vector2 FindBridge(List<Sample> road, Spec s, float scale, Vector3 offset)
        {
            var river = new List<Vector2>();
            foreach (var r in s.riverSketch) { var p = ToWorld(r, scale, offset); river.Add(new Vector2(p.x, p.z)); }
            float best = float.MaxValue, bestT = 0f, len = road[road.Count - 1].dist + 4f;
            foreach (var smp in road)
            {
                float d = DistToPolyline(new Vector2(smp.pos.x, smp.pos.z), river);
                if (d < best) { best = d; bestT = smp.t; }
            }
            float half = 32f / len;
            return new Vector2(bestT - half, bestT + half);
        }

        /// <summary>Applies a KROMA sky/lighting profile through KROMA's own editor logic (reflection: optional pack).</summary>
        internal static void ApplyKroma(string profileName)
        {
            if (string.IsNullOrEmpty(profileName)) return;
            var guids = AssetDatabase.FindAssets(profileName);
            if (guids.Length == 0) { Debug.LogWarning("[SHIFT//RUSH] KROMA profile not found: " + profileName); return; }
            var profile = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guids[0]));
            var logicType = System.Type.GetType("FayLabs.Kroma.Editor.KromaBrowserLogic, FayLabs.Kroma.Editor");
            if (logicType == null || profile == null) return;
            var logic = System.Activator.CreateInstance(logicType);
            logicType.GetMethod("RefreshProfileList").Invoke(logic, null);
            var apply = logicType.GetMethod("ApplyToSelected");
            apply.Invoke(logic, new object[] { profile, null });
            var lighting = Object.FindFirstObjectByType<FayLabs.Kroma.KromaSceneLighting>();
            if (lighting != null)
            {
                var so = new SerializedObject(lighting);
                var p = so.FindProperty("kromaProfile");
                if (p != null) { p.objectReferenceValue = profile; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
        }

        static Spline BuildRoad(Transform root, List<Vector3> points, Spec s, TrackKit kit)
        {
            var go = new GameObject("Road Spline (drag points to reshape)");
            go.transform.SetParent(root, false);
            // Spline Architect wants anchor, tangent A (behind) and tangent B (ahead) per point.
            var triples = new List<Vector3>();
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 prev = points[(i - 1 + points.Count) % points.Count], p = points[i], next = points[(i + 1) % points.Count];
                Vector3 dir = (next - prev).normalized;
                float tl = 0.33f * Mathf.Min(Vector3.Distance(prev, p), Vector3.Distance(p, next));
                triples.Add(p); triples.Add(p - dir * tl); triples.Add(p + dir * tl);
            }
            var spline = SplineUtility.Create(go, triples, new Color(1f, 0.25f, 0.85f), Space.World);
            spline.SetLoop(true);
            for (int i = 0; i < spline.SegmentCount; i++)
            {
                var seg = spline.GetSegmentAtIndex(i);
                seg.ApplyAutoSmooth();
                seg.ZRotation = 0f;
            }
            // Must stay ACTIVE: Spline Architect refuses to create deformations on a non-active spline.
            spline.ComponentMode = ComponentMode.ACTIVE;
            spline.RebuildCache();

            float len = spline.Length;
            AddPopulation(spline, kit.roadPiece, 0f, 0f, 0f, 0f, true, "Road");
            // Guardrails and posts go on after the shortcuts are chosen (AddRails), so they can open for them.
            AddKerbs(spline, kit);

            if (s.galleryRange.y > s.galleryRange.x)
                AddPopulation(spline, kit.galleryPiece, 0f, len * s.galleryRange.x, 0f, len * (1f - s.galleryRange.y), true, "Avalanche Gallery");
            return spline;
        }

        /// <summary>Red/white kerbs on both edges of the asphalt wherever the road bends tightly.</summary>
        static void AddKerbs(Spline spline, TrackKit kit)
        {
            var road = SampleSpline(spline, 4f);
            int n = road.Count;
            float len = spline.Length, step = len / n;
            var corner = new bool[n];
            for (int i = 0; i < n; i++)
            {
                // Turn over ~24 m centred here, in degrees per metre.
                float turn = Vector3.Angle(Flat(road[(i - 3 + n) % n].dir), Flat(road[(i + 3) % n].dir)) / (6f * step);
                corner[i] = turn > 0.45f; // tighter than ~130 m radius
            }
            int made = 0;
            for (int i = 0; i < n; i++)
            {
                if (!corner[i] || corner[(i - 1 + n) % n]) continue; // start of a corner run
                int j = i;
                while (corner[(j + 1) % n] && j - i < n) j++;
                float a = Mathf.Max(0f, i * step - 10f), b = Mathf.Min(len, (j + 1) * step + 10f);
                if (b - a < 24f) continue;
                AddPopulation(spline, kit.kerb, RoadWidth * 0.5f + 0.6f, a, 0f, len - b, true, "Kerb L");
                AddPopulation(spline, kit.kerb, -(RoadWidth * 0.5f + 0.6f), a, 0f, len - b, true, "Kerb R");
                made++;
            }
            Debug.Log("[SHIFT//RUSH] Kerbs on " + made + " corners");
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.normalized; }

        static void AddPopulation(Spline spline, GameObject prefab, float xOffset, float startPad, float spacing, float endPad, bool deform, string name, bool mirror = false)
        {
            // Pieces must be direct children of the spline: a grouping object under the spline becomes a SplineObject
            // itself and its children get deformed in its (offset) space.
            var pop = new Population(prefab, deform, true, null, false);
            pop.XOffset = xOffset;
            pop.Spacing = spacing;
            pop.StartPadding = startPad;
            pop.EndPadding = endPad;
            pop.FillMode = deform ? PopulationFillMode.FIT_USING_SCALE : PopulationFillMode.FIT_USING_SPACING;
            if (deform) { pop.AutoResolution = true; pop.ResolutionBias = 1f; }
            if (mirror) pop.PrefabRotationOffset = Quaternion.Euler(0f, 0f, 0f) * Quaternion.Euler(0, 180f, 0);
            // Bake straight away without registering the population: the pieces become ordinary spline deformations
            // (saved in the scene, still bending when points move) and nothing keeps respawning them.
            var made = new List<SplineObject>();
            spline.BakePopulation(pop, made);
            foreach (var so in made) if (so != null) so.gameObject.name = name;
            spline.DeformSplineObjectsNow();
        }
    }
}
