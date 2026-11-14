using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ShiftRush.EditorTools
{
    /// <summary>Terrain, water, scenery and the start grid for the track builder.</summary>
    public static partial class TrackBuilder
    {
        const float BaseY = -60f, TerrainHeight = 520f, WaterY = -1.2f, Margin = 520f;
        const int HeightRes = 1025, AlphaRes = 512;

        // ------------------------------------------------------------------ distance to the road (bucketed)

        class RoadIndex
        {
            struct Seg { public Vector3 a, b; public float ta, tb, shrink; }
            readonly List<Seg> segs = new List<Seg>();
            readonly Dictionary<long, List<int>> cells = new Dictionary<long, List<int>>();
            const float Cell = 40f;

            /// <summary>The road loop, plus optional shortcut lanes. A lane reports its distance less the
            /// difference in half-width, so the same flatten/paint/scatter rules give it a narrower corridor;
            /// lanes report t = -1 (never inside the gallery or bridge ranges).</summary>
            public RoadIndex(List<Sample> r, List<List<Vector3>> lanes = null)
            {
                for (int i = 0; i < r.Count; i++)
                {
                    float tb = r[(i + 1) % r.Count].t; if (tb == 0f) tb = 1f;
                    Add(new Seg { a = r[i].pos, b = r[(i + 1) % r.Count].pos, ta = r[i].t, tb = tb });
                }
                if (lanes != null)
                    foreach (var lane in lanes)
                        for (int i = 0; i < lane.Count - 1; i++)
                            Add(new Seg { a = lane[i], b = lane[i + 1], ta = -1f, tb = -1f, shrink = SurfaceHalf - CutHalf - 1f });
            }

            void Add(Seg s)
            {
                int id = segs.Count;
                segs.Add(s);
                int x0 = Mathf.FloorToInt(Mathf.Min(s.a.x, s.b.x) / Cell), x1 = Mathf.FloorToInt(Mathf.Max(s.a.x, s.b.x) / Cell);
                int z0 = Mathf.FloorToInt(Mathf.Min(s.a.z, s.b.z) / Cell), z1 = Mathf.FloorToInt(Mathf.Max(s.a.z, s.b.z) / Cell);
                for (int x = x0; x <= x1; x++)
                    for (int z = z0; z <= z1; z++)
                    {
                        long k = Key(x, z);
                        if (!cells.TryGetValue(k, out var list)) cells[k] = list = new List<int>();
                        list.Add(id);
                    }
            }

            static long Key(int x, int z) { return ((long)x << 32) ^ (uint)z; }

            /// <summary>Distance to the road in XZ (searching up to `reach` metres; beyond that returns reach+), road height and lap fraction.</summary>
            public float Query(Vector3 p, float reach, out float roadY, out float t)
            {
                int cx = Mathf.FloorToInt(p.x / Cell), cz = Mathf.FloorToInt(p.z / Cell);
                int r = Mathf.CeilToInt(reach / Cell);
                float best = float.MaxValue; roadY = 0f; t = 0f;
                Vector2 p2 = new Vector2(p.x, p.z);
                for (int x = cx - r; x <= cx + r; x++)
                    for (int z = cz - r; z <= cz + r; z++)
                    {
                        if (!cells.TryGetValue(Key(x, z), out var list)) continue;
                        foreach (int i in list)
                        {
                            var s = segs[i];
                            Vector2 a2 = new Vector2(s.a.x, s.a.z), ab = new Vector2(s.b.x, s.b.z) - a2;
                            float k = Mathf.Clamp01(Vector2.Dot(p2 - a2, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                            float d = (p2 - (a2 + ab * k)).magnitude - s.shrink;
                            if (d < best)
                            {
                                best = d; roadY = Mathf.Lerp(s.a.y, s.b.y, k);
                                t = s.ta < 0f ? -1f : Mathf.Lerp(s.ta, s.tb, k);
                            }
                        }
                    }
                if (best == float.MaxValue) return reach + 1f;
                return Mathf.Max(0f, best);
            }
        }

        /// <summary>Smooth, coarse distance-to-road and road height everywhere (brute force on a 129² grid).</summary>
        class CoarseField
        {
            const int N = 129;
            readonly float[,] dist = new float[N, N], roadY = new float[N, N];
            readonly Vector3 origin; readonly float size;

            public CoarseField(List<Sample> road, Vector3 origin, float size)
            {
                this.origin = origin; this.size = size;
                var pts = new List<Vector3>();
                for (int i = 0; i < road.Count; i += 4) pts.Add(road[i].pos);
                for (int z = 0; z < N; z++)
                    for (int x = 0; x < N; x++)
                    {
                        float wx = origin.x + size * x / (N - 1), wz = origin.z + size * z / (N - 1);
                        float best = float.MaxValue, by = 0f, wsum = 0f, ysum = 0f;
                        foreach (var p in pts)
                        {
                            float d = (p.x - wx) * (p.x - wx) + (p.z - wz) * (p.z - wz);
                            if (d < best) { best = d; by = p.y; }
                            float w = 1f / (d + 2500f); wsum += w; ysum += w * p.y; // smooth average height
                        }
                        dist[z, x] = Mathf.Sqrt(best);
                        roadY[z, x] = Mathf.Lerp(by, ysum / wsum, Mathf.InverseLerp(60f, 300f, Mathf.Sqrt(best)));
                    }
            }

            public void Sample(float wx, float wz, out float d, out float y)
            {
                float fx = Mathf.Clamp((wx - origin.x) / size * (N - 1), 0, N - 1.001f);
                float fz = Mathf.Clamp((wz - origin.z) / size * (N - 1), 0, N - 1.001f);
                int x0 = (int)fx, z0 = (int)fz; float tx = fx - x0, tz = fz - z0;
                d = Mathf.Lerp(Mathf.Lerp(dist[z0, x0], dist[z0, x0 + 1], tx), Mathf.Lerp(dist[z0 + 1, x0], dist[z0 + 1, x0 + 1], tx), tz);
                y = Mathf.Lerp(Mathf.Lerp(roadY[z0, x0], roadY[z0, x0 + 1], tx), Mathf.Lerp(roadY[z0 + 1, x0], roadY[z0 + 1, x0 + 1], tx), tz);
            }
        }

        static float Fbm(float x, float z, float freq, int octaves, float seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += Mathf.PerlinNoise(x * freq + seed, z * freq - seed * 0.7f) * amp;
                norm += amp; amp *= 0.5f; freq *= 2.03f;
            }
            return sum / norm;
        }

        static float Ridged(float x, float z, float freq, int octaves, float seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                float n = 1f - Mathf.Abs(Mathf.PerlinNoise(x * freq + seed, z * freq + seed * 1.3f) * 2f - 1f);
                sum += n * n * amp; norm += amp; amp *= 0.5f; freq *= 2.1f;
            }
            return sum / norm;
        }

        static float DistToPolyline(Vector2 p, List<Vector2> line)
        {
            float best = float.MaxValue;
            for (int i = 0; i < line.Count - 1; i++)
            {
                Vector2 a = line[i], ab = line[i + 1] - a;
                float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                best = Mathf.Min(best, (p - (a + ab * k)).sqrMagnitude);
            }
            return Mathf.Sqrt(best);
        }

        // ------------------------------------------------------------------ terrain

        class World
        {
            public Spec spec; public RoadIndex index; public CoarseField coarse; public float seed;
            public Vector2 lake; public float lakeR; public List<Vector2> river = new List<Vector2>();

            /// <summary>Ground height (world Y) at a world XZ point, before painting. Also returns road distance.</summary>
            public float Height(float x, float z, out float d)
            {
                float roadY, t, cd, cy;
                coarse.Sample(x, z, out cd, out cy);
                d = cd; roadY = cy; t = -1f;
                if (cd < 140f)
                {
                    // Near the road: precise distance, blended into the coarse field so there is no seam.
                    float pd = index.Query(new Vector3(x, 0, z), 160f, out float py, out float pt);
                    if (pd <= 160f)
                    {
                        float k = Mathf.InverseLerp(100f, 140f, cd);
                        d = Mathf.Lerp(pd, cd, k); roadY = Mathf.Lerp(py, cy, k); t = pt;
                    }
                }
                float far = 0f;

                float hw = SurfaceHalf;
                float rolling = (Fbm(x, z, 1f / 140f, 4, seed) - 0.45f) * (spec.snowy ? 26f : 9f);
                float mountainMask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(spec.snowy ? 70f : 230f, spec.snowy ? 650f : 760f, d + far));
                float ridges = Ridged(x, z, 1f / 520f, 5, seed + 17f);
                float mountains = mountainMask * ((spec.snowy ? 90f : 110f) + (spec.snowy ? 300f : 420f) * ridges);
                float natural = roadY + rolling * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(hw + 4f, hw + 70f, d)) + mountains;

                if (!spec.snowy)
                {
                    natural = Mathf.Max(natural, WaterY + 0.8f); // only the lake and river sit below the water line
                    Vector2 p = new Vector2(x, z);
                    float dl = Vector2.Distance(p, lake);
                    if (dl < lakeR + 40f)
                    {
                        float bed = WaterY - 6f * Mathf.Clamp01(1f - dl / lakeR) - 0.8f;
                        natural = Mathf.Lerp(bed, natural, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lakeR - 10f, lakeR + 40f, dl)));
                    }
                    if (river.Count > 1)
                    {
                        float dr = DistToPolyline(p, river);
                        if (dr < 30f)
                        {
                            float bed = WaterY - 2.4f;
                            natural = Mathf.Lerp(bed, natural, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(8f, 30f, dr)));
                        }
                    }
                }

                // Flatten under the road and blend the shoulders, except on the bridge where the river runs below.
                bool bridge = InRange(t, spec.bridgeRange);
                if (!bridge)
                {
                    float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(hw + 1.5f, hw + 24f, d));
                    natural = Mathf.Lerp(roadY - 0.25f, natural, blend);
                }
                return natural;
            }
        }

        static Terrain BuildTerrain(Transform root, Spec s, List<Sample> road, float scale, Vector3 offset, TrackKit kit, List<List<Vector3>> lanes = null)
        {
            var w = new World { spec = s, index = new RoadIndex(road, lanes), seed = s.seed * 13.37f };
            if (!s.snowy)
            {
                w.lake = new Vector2(ToWorld(s.lakeSketch, scale, offset).x, ToWorld(s.lakeSketch, scale, offset).z);
                w.lakeR = s.lakeRadiusSketch * scale;
                foreach (var r in s.riverSketch) { var p = ToWorld(r, scale, offset); w.river.Add(new Vector2(p.x, p.z)); }
            }

            Vector3 min = new Vector3(float.MaxValue, 0, float.MaxValue), max = new Vector3(float.MinValue, 0, float.MinValue);
            foreach (var r in road) { min = Vector3.Min(min, r.pos); max = Vector3.Max(max, r.pos); }
            float size = Mathf.Max(max.x - min.x, max.z - min.z) + Margin * 2f;
            Vector3 centre = (min + max) * 0.5f;
            Vector3 origin = new Vector3(centre.x - size * 0.5f, BaseY, centre.z - size * 0.5f);

            w.coarse = new CoarseField(road, origin, size);
            var data = new TerrainData();
            data.heightmapResolution = HeightRes;
            data.size = new Vector3(size, TerrainHeight, size);
            data.alphamapResolution = AlphaRes;
            data.baseMapResolution = 1024;

            // Save the asset before writing heights and paint: splat maps written before CreateAsset are lost.
            string path = ArtTrack + "TD_" + s.name.Replace(" ", "") + ".asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(data, path);

            var heights = new float[HeightRes, HeightRes];
            for (int zi = 0; zi < HeightRes; zi++)
            {
                if (zi % 64 == 0) EditorUtility.DisplayProgressBar("SHIFT//RUSH: " + s.name, "Shaping terrain", zi / (float)HeightRes);
                for (int xi = 0; xi < HeightRes; xi++)
                {
                    float x = origin.x + size * xi / (HeightRes - 1);
                    float z = origin.z + size * zi / (HeightRes - 1);
                    float d;
                    float h = w.Height(x, z, out d);
                    heights[zi, xi] = Mathf.Clamp01((h - BaseY) / TerrainHeight);
                }
            }
            data.SetHeights(0, 0, heights);

            // Paint.
            var layers = s.snowy
                ? new[] { kit.snow, kit.snowCliff, kit.pineDirt, kit.mud }
                : new[] { kit.pineGrass, kit.grassBright, kit.snowCliff, kit.riverRocks, kit.pineDirt, kit.snow };
            data.terrainLayers = layers;
            var alpha = new float[AlphaRes, AlphaRes, layers.Length];
            for (int zi = 0; zi < AlphaRes; zi++)
            {
                if (zi % 32 == 0) EditorUtility.DisplayProgressBar("SHIFT//RUSH: " + s.name, "Painting terrain", zi / (float)AlphaRes);
                for (int xi = 0; xi < AlphaRes; xi++)
                {
                    float nx = xi / (float)(AlphaRes - 1), nz = zi / (float)(AlphaRes - 1);
                    float x = origin.x + size * nx, z = origin.z + size * nz;
                    float h = data.GetInterpolatedHeight(nx, nz) + BaseY;
                    float steep = data.GetSteepness(nx, nz);
                    float roadY, t;
                    float d = w.index.Query(new Vector3(x, 0, z), 80f, out roadY, out t);
                    float patch = Fbm(x, z, 1f / 45f, 3, w.seed + 4f);
                    var wts = new float[layers.Length];
                    float shoulder = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SurfaceHalf + 0.5f, SurfaceHalf + 6f, d));
                    float cliff = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(28f, 42f, steep));
                    if (s.snowy)
                    {
                        wts[0] = 1f;
                        wts[3] = Mathf.Clamp01((patch - 0.62f) * 4f) * 0.7f;
                        wts[1] = cliff * 2f;
                        wts[2] = shoulder * 3f;
                    }
                    else
                    {
                        wts[0] = Mathf.Clamp01(patch - 0.55f) * 2f;   // pine grass in patches
                        wts[1] = 1.6f;                                 // bright meadow grass
                        wts[2] = cliff * 3f;
                        wts[3] = Mathf.Clamp01(1f - (h - WaterY) / 1.6f) * 4f;
                        wts[4] = shoulder * 4f;
                        wts[5] = Mathf.Clamp01((h - 70f) / 30f) * 6f;  // snow caps on the surrounding peaks
                    }
                    float sum = 0f; foreach (var v in wts) sum += v;
                    for (int l = 0; l < layers.Length; l++) alpha[zi, xi, l] = wts[l] / Mathf.Max(sum, 1e-4f);
                }
            }
            data.SetAlphamaps(0, 0, alpha);
            EditorUtility.ClearProgressBar();

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);

            var tgo = Terrain.CreateTerrainGameObject(data);
            tgo.name = "Terrain";
            tgo.transform.SetParent(root, false);
            tgo.transform.position = origin;
            var terrain = tgo.GetComponent<Terrain>();
            terrain.materialTemplate = TerrainMaterial();
            terrain.heightmapPixelError = 4f;
            terrain.basemapDistance = 600f;
            terrain.drawInstanced = true;
            return terrain;
        }

        static Material TerrainMaterial()
        {
            string path = ArtTrack + "M_Terrain.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            return m;
        }

        static void BuildWater(Transform root, Spec s, float scale, Vector3 offset, TrackKit kit)
        {
            if (s.snowy) return;
            var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Water";
            Object.DestroyImmediate(water.GetComponent<Collider>());
            water.transform.SetParent(root, false);
            var lake = ToWorld(s.lakeSketch, scale, offset);
            water.transform.position = new Vector3(lake.x + 200f, WaterY, lake.z);
            water.transform.localScale = new Vector3(110f, 1f, 70f);
            water.GetComponent<Renderer>().sharedMaterial = kit.water;
        }

        // ------------------------------------------------------------------ scenery

        static void BuildScenery(Transform root, Spec s, List<Sample> road, Terrain terrain, float scale, Vector3 offset, TrackKit kit, List<List<Vector3>> lanes = null)
        {
            var rng = new System.Random(s.seed);
            var index = new RoadIndex(road, lanes);
            var data = terrain.terrainData;
            Vector3 origin = terrain.transform.position;
            float size = data.size.x;
            var scenery = new GameObject("Scenery").transform;
            scenery.SetParent(root, false);

            System.Func<float> R = () => (float)rng.NextDouble();
            System.Func<float, float, float> H = (x, z) => terrain.SampleHeight(new Vector3(x, 0, z)) + origin.y;
            System.Func<float, float, float> Steep = (x, z) => data.GetSteepness((x - origin.x) / size, (z - origin.z) / size);

            Vector3 lake = s.snowy ? Vector3.zero : ToWorld(s.lakeSketch, scale, offset);
            float lakeR = s.lakeRadiusSketch * scale;
            var villagePts = new List<Vector3>();
            if (s.villageSketch != null) foreach (var v in s.villageSketch) villagePts.Add(ToWorld(v, scale, offset));

            System.Action<string, List<GameObject>, int, float, float, float, float, float> Scatter = (group, prefabs, tries, dMin, dMax, sMin, sMax, maxSteep) =>
            {
                if (prefabs.Count == 0) return;
                var parent = new GameObject(group).transform;
                parent.SetParent(scenery, false);
                Vector3 c = origin + new Vector3(size * 0.5f, 0, size * 0.5f);
                for (int i = 0; i < tries; i++)
                {
                    // Pick a road sample, then step out sideways: keeps density where the player can see it.
                    var smp = road[rng.Next(road.Count)];
                    Vector3 side = Vector3.Cross(Vector3.up, smp.dir).normalized * (R() < 0.5f ? -1f : 1f);
                    float dist = Mathf.Lerp(dMin, dMax, R() * R());
                    Vector3 p = smp.pos + side * dist + smp.dir * (R() - 0.5f) * 20f;
                    float ry, rt;
                    float d = index.Query(p, dMax + 20f, out ry, out rt);
                    if (d < dMin) continue;
                    if (!s.snowy && (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(lake.x, lake.z)) < lakeR + 8f)) continue;
                    float y = H(p.x, p.z);
                    if (!s.snowy && y < WaterY + 0.4f) continue;
                    if (Steep(p.x, p.z) > maxSteep) continue;
                    bool nearVillage = false;
                    foreach (var vp in villagePts) if (Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(vp.x, 0, vp.z)) < 30f) nearVillage = true;
                    if (nearVillage && group == "Trees") continue;
                    var prefab = prefabs[rng.Next(prefabs.Count)];
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    go.transform.position = new Vector3(p.x, y - 0.15f, p.z);
                    go.transform.rotation = Quaternion.Euler(0, R() * 360f, 0);
                    go.transform.localScale = Vector3.one * Mathf.Lerp(sMin, sMax, R());
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
                }
            };

            if (s.snowy)
            {
                Scatter("Trees", kit.pines, 1100, 22f, 230f, 0.8f, 1.5f, 34f);
                Scatter("Rocks", kit.rocks, 220, 22f, 140f, 0.5f, 1.4f, 60f);
                Scatter("Cliffs", kit.cliffs, 50, 38f, 100f, 1f, 1.6f, 90f);
            }
            else
            {
                Scatter("Trees", kit.pines, 2200, 21f, 260f, 0.8f, 1.6f, 32f);
                Scatter("Bushes", kit.bushes, 500, 15f, 60f, 0.8f, 1.5f, 35f);
                Scatter("Ground Cover", kit.groundCover, 600, 14.5f, 40f, 0.8f, 1.4f, 35f);
                Scatter("Rocks", kit.rocks, 200, 22f, 160f, 0.5f, 1.4f, 60f);
                Scatter("Cliffs", kit.cliffs, 30, 60f, 180f, 1f, 1.8f, 90f);
            }

            // Skyline: mountain range meshes in a ring well outside the circuit.
            if (false && kit.mountains.Count > 0) // the generated peaks make the skyline; these meshes floated
            {
                var ring = new GameObject("Skyline").transform;
                ring.SetParent(scenery, false);
                Vector3 c = origin + new Vector3(size * 0.5f, 0, size * 0.5f);
                int n = 10;
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2f / n + R() * 0.2f;
                    Vector3 p = c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (size * 0.62f);
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(kit.mountains[i % kit.mountains.Count], ring);
                    var b = Bounds(go);
                    float k = 900f / Mathf.Max(b.size.x, b.size.z, 1f);
                    go.transform.localScale = Vector3.one * k;
                    go.transform.position = new Vector3(p.x, H(p.x, p.z) - 30f, p.z);
                    go.transform.rotation = Quaternion.LookRotation(new Vector3(c.x - p.x, 0, c.z - p.z)) * Quaternion.Euler(0, R() * 30f - 15f, 0);
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                }
            }

            // Landmarks.
            if (s.snowy && kit.lookout != null)
            {
                Sample top = road[0];
                foreach (var r in road) if (r.pos.y > top.pos.y) top = r;
                Vector3 side = Vector3.Cross(Vector3.up, top.dir).normalized;
                Vector3 p = top.pos + side * 22f;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(kit.lookout, scenery);
                go.name = "Summit Lookout";
                go.transform.position = new Vector3(p.x, H(p.x, p.z), p.z);
                go.transform.rotation = Quaternion.LookRotation(-side);
            }
            if (!s.snowy)
            {
                var village = new GameObject("Village").transform;
                village.SetParent(scenery, false);
                foreach (var vp in villagePts)
                {
                    if (kit.cabin == null) break;
                    float ry, rt;
                    float d = index.Query(vp, 60f, out ry, out rt);
                    Vector3 p = vp;
                    if (d < 24f)
                    {
                        // Push the cabin off the road, away from the nearest road point.
                        Sample near = road[0]; float best = float.MaxValue;
                        foreach (var r in road) { float dd = (new Vector2(r.pos.x, r.pos.z) - new Vector2(vp.x, vp.z)).sqrMagnitude; if (dd < best) { best = dd; near = r; } }
                        Vector3 away = new Vector3(vp.x - near.pos.x, 0, vp.z - near.pos.z).normalized;
                        if (away == Vector3.zero) away = Vector3.Cross(Vector3.up, near.dir).normalized;
                        p = near.pos + away * 26f;
                    }
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(kit.cabin, village);
                    go.transform.position = new Vector3(p.x, H(p.x, p.z), p.z);
                    go.transform.rotation = Quaternion.Euler(0, R() * 360f, 0);
                }
                // Bridge piers where the river runs under the road.
                var concrete = AssetDatabase.LoadAssetAtPath<Material>(ArtTrack + "M_GalleryConcrete.mat");
                foreach (var smp in road)
                {
                    if (!InRange(smp.t, s.bridgeRange)) continue;
                    if (Mathf.RoundToInt(smp.dist) % 12 > 3) continue;
                    Vector3 side = Vector3.Cross(Vector3.up, smp.dir).normalized;
                    foreach (float sx in new[] { -SurfaceHalf + 2f, 0f, SurfaceHalf - 2f })
                    {
                        Vector3 top = smp.pos + side * sx;
                        float bed = H(top.x, top.z);
                        if (top.y - bed < 1f) continue;
                        var pier = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        pier.name = "Bridge Pier";
                        pier.transform.SetParent(scenery, false);
                        pier.transform.position = new Vector3(top.x, (top.y - 0.2f + bed - 1f) * 0.5f, top.z);
                        pier.transform.localScale = new Vector3(1.2f, top.y - 0.2f - (bed - 1f), 1.2f);
                        pier.GetComponent<Renderer>().sharedMaterial = concrete;
                    }
                }
                if (kit.jetty != null)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(kit.jetty, scenery);
                    go.name = "Lake Jetty";
                    Vector3 p = lake + new Vector3(-lakeR + 4f, 0, 0);
                    go.transform.position = new Vector3(p.x, WaterY + 0.2f, p.z);
                    go.transform.rotation = Quaternion.Euler(0, 90f, 0);
                }
            }
        }

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        // ------------------------------------------------------------------ start grid

        static void PlaceStartAndCar(Transform root, Spec s, List<Sample> road, TrackKit kit)
        {
            int startIndex = Mathf.Clamp(Mathf.RoundToInt(60f / 4f), 0, road.Count - 1);
            var start = road[startIndex];
            Quaternion rot = Quaternion.LookRotation(new Vector3(start.dir.x, 0, start.dir.z));

            var grid = new GameObject("Start Grid").transform;
            grid.SetParent(root, false);
            var gantry = (GameObject)PrefabUtility.InstantiatePrefab(kit.gantry, grid);
            gantry.transform.SetPositionAndRotation(start.pos, rot);
            var line = (GameObject)PrefabUtility.InstantiatePrefab(kit.startLine, grid);
            line.transform.SetPositionAndRotation(start.pos + Vector3.up * 0.04f - start.dir * 1.25f, rot);

            var carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Vehicles/Car_Zephyr.prefab");
            if (carPrefab != null)
            {
                var spawn = road[Mathf.Max(0, startIndex - 4)];
                var car = (GameObject)PrefabUtility.InstantiatePrefab(carPrefab);
                car.transform.SetPositionAndRotation(spawn.pos + Vector3.up * 0.6f, Quaternion.LookRotation(new Vector3(spawn.dir.x, 0, spawn.dir.z)));
            }
        }
    }
}
