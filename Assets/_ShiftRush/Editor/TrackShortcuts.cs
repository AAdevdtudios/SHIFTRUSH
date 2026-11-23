using System.Collections.Generic;
using ShiftRush.Race;
using SplineArchitect;
using UnityEditor;
using UnityEngine;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// Small shortcuts: narrow dirt lanes that cut across the inside of a tight bend and rejoin the circuit.
    /// The builder looks for single bends where a gentle curve across the inside saves 30–90 m, stays clear of
    /// the rest of the circuit, the start, the gallery, the bridge and water, and isn't steep. It opens the
    /// guardrail at both mouths, flattens and paints the ground, keeps trees off it, lays the lane, and marks the
    /// way in with a neon gate. Each lane is a <see cref="ShortcutPath"/> the rivals and track limits know about.
    /// </summary>
    public static partial class TrackBuilder
    {
        /// <summary>Half the width of a shortcut lane (9 m wide: one car comfortably, two at a squeeze).</summary>
        public const float CutHalf = 4.5f;
        const int MaxCuts = 2;
        public static string lastCutStats = "";

        public class Cut
        {
            public List<Vector3> path;  // entry (on the road centre) → exit (on the road centre), every ~3 m
            public int from, to;        // road sample indices it leaves and rejoins at
            public float saving;        // metres shorter than the road
        }

        public static List<Cut> FindShortcuts(List<Sample> road, Spec s, float scale, Vector3 offset)
        {
            int n = road.Count;
            float step = (road[n - 1].dist + Vector3.Distance(road[n - 1].pos, road[0].pos)) / n;
            int startIndex = Mathf.Clamp(Mathf.RoundToInt(60f / 4f), 0, n - 1); // where PlaceStartAndCar puts the line

            // Places a shortcut can't touch: the start straight, the gallery and the bridge.
            var banned = new bool[n];
            for (int i = 0; i < n; i++)
            {
                int fromStart = Mathf.Abs(i - startIndex); fromStart = Mathf.Min(fromStart, n - fromStart);
                banned[i] = fromStart * step < 200f
                    || InRange(road[i].t, Pad(s.galleryRange, 0.03f)) || InRange(road[i].t, Pad(s.bridgeRange, 0.03f));
            }

            var index = new RoadIndex(road);
            Vector2 lake = Vector2.zero; float lakeR = 0f; var river = new List<Vector2>();
            if (!s.snowy && s.lakeRadiusSketch > 0f)
            {
                var l = ToWorld(s.lakeSketch, scale, offset); lake = new Vector2(l.x, l.z); lakeR = s.lakeRadiusSketch * scale;
                if (s.riverSketch != null) foreach (var r in s.riverSketch) { var p = ToWorld(r, scale, offset); river.Add(new Vector2(p.x, p.z)); }
            }

            var candidates = new List<Cut>();
            int rBan = 0, rTurn = 0, rLen = 0, rGrade = 0, rClear = 0;
            var scores = new List<float>();
            for (int i = 0; i < n; i += 2)
            {
                for (int k = 16; k <= 72; k += 2)
                {
                    int j = (i + k) % n;
                    bool bad = false;
                    float turn = 0f, absTurn = 0f;
                    for (int m = 0; m <= k && !bad; m++)
                    {
                        int a = (i + m) % n;
                        if (banned[a]) bad = true;
                        if (m < k) { float da = Vector3.SignedAngle(Flat(road[a].dir), Flat(road[(a + 1) % n].dir), Vector3.up); turn += da; absTurn += Mathf.Abs(da); }
                    }
                    if (bad) { rBan++; continue; }
                    // One bend, turning a good amount (not an S, not a kink).
                    if (Mathf.Abs(turn) < 65f || Mathf.Abs(turn) > 200f || absTurn - Mathf.Abs(turn) > 25f) { rTurn++; continue; }

                    var path = CutCurve(road[i].pos, Flat(road[i].dir), road[j].pos, Flat(road[j].dir), Mathf.Sign(turn));
                    float len = PathLength(path), arc = k * step, saving = arc - len;
                    if (len < 50f || len > 230f || saving < 25f || saving > 90f || arc / len < 1.12f) { rLen++; continue; }
                    if (Mathf.Abs(road[j].pos.y - road[i].pos.y) / len > 0.09f) { rGrade++; continue; }
                    if (!Clear(path, index, n, i, j, lake, lakeR, river)) { rClear++; continue; }

                    candidates.Add(new Cut { path = path, from = i, to = j, saving = saving });
                    scores.Add(Mathf.Min(saving, 70f) - Mathf.Abs(road[j].pos.y - road[i].pos.y) / len * 120f);
                }
            }

            // Best first, never overlapping, well apart.
            var chosen = new List<Cut>();
            while (chosen.Count < MaxCuts)
            {
                int best = -1;
                for (int c = 0; c < candidates.Count; c++)
                {
                    if (best >= 0 && scores[c] <= scores[best]) continue;
                    bool ok = true;
                    foreach (var o in chosen) if (CyclicOverlap(candidates[c].from - 40, candidates[c].to + 40, o.from, o.to, n)) ok = false;
                    if (ok) best = c;
                }
                if (best < 0) break;
                chosen.Add(candidates[best]);
                scores[best] = float.MinValue;
            }
            foreach (var c in chosen) Debug.Log("[SHIFT//RUSH] " + s.name + ": shortcut at " + (c.from * step).ToString("0") + " m, " + PathLength(c.path).ToString("0") + " m long, saves " + c.saving.ToString("0") + " m");
            if (chosen.Count == 0) Debug.Log("[SHIFT//RUSH] " + s.name + ": no bend suits a shortcut (rejected: banned " + rBan + ", turn " + rTurn + ", length " + rLen + ", grade " + rGrade + ", clearance " + rClear + ")");
            lastCutStats = "banned " + rBan + ", turn " + rTurn + ", length " + rLen + ", grade " + rGrade + ", clearance " + rClear + ", ok " + candidates.Count;
            return chosen;
        }

        static Vector2 Pad(Vector2 range, float pad) { return range.y > range.x ? new Vector2(range.x - pad, range.y + pad) : range; }

        static bool CyclicOverlap(int a0, int a1, int b0, int b1, int n)
        {
            for (int a = a0; a <= a1; a++)
            {
                int aa = ((a % n) + n) % n;
                int len = ((b1 - b0) % n + n) % n;
                int rel = ((aa - b0) % n + n) % n;
                if (rel <= len) return true;
            }
            return false;
        }

        /// <summary>Breaks off the road towards the far end of the bend and eases back in: a cubic curve, every ~3 m.
        /// Height eases from the entry's to the exit's.</summary>
        static List<Vector3> CutCurve(Vector3 p0, Vector3 d0, Vector3 p3, Vector3 d3, float inside)
        {
            // Mostly straight across the inside, easing in and out of the road's direction.
            Vector3 chord = Flat(p3 - p0);
            Vector3 t0 = (d0 * 0.5f + chord * 0.5f).normalized;
            Vector3 t3 = (d3 * 0.5f + chord * 0.5f).normalized;
            float h = Vector3.Distance(new Vector3(p0.x, 0, p0.z), new Vector3(p3.x, 0, p3.z)) * 0.33f;
            Vector3 p1 = p0 + t0 * h, p2 = p3 - t3 * h;
            var fine = new List<Vector3>();
            for (int i = 0; i <= 200; i++)
            {
                float u = i / 200f, w = 1f - u;
                var p = w * w * w * p0 + 3f * w * w * u * p1 + 3f * w * u * u * p2 + u * u * u * p3;
                p.y = Mathf.Lerp(p0.y, p3.y, Mathf.SmoothStep(0f, 1f, u));
                fine.Add(p);
            }
            var path = new List<Vector3> { fine[0] };
            float run = 0f;
            for (int i = 1; i < fine.Count; i++)
            {
                run += Vector3.Distance(fine[i - 1], fine[i]);
                if (run >= 3f || i == fine.Count - 1) { path.Add(fine[i]); run = 0f; }
            }
            return path;
        }

        static float PathLength(List<Vector3> path)
        {
            float l = 0f;
            for (int i = 0; i < path.Count - 1; i++) l += Vector3.Distance(path[i], path[i + 1]);
            return l;
        }

        /// <summary>The middle of the lane is well clear of the road (room for verges and walls), and the lane
        /// never comes near any other part of the circuit or the water.</summary>
        static bool Clear(List<Vector3> path, RoadIndex index, int n, int from, int to, Vector2 lake, float lakeR, List<Vector2> river)
        {
            int count = path.Count;
            for (int m = 1; m < count - 1; m++)
            {
                float u = m / (float)(count - 1);
                float roadY, t;
                float d = index.Query(path[m], 60f, out roadY, out t);
                if (u > 0.25f && u < 0.75f && d < SurfaceHalf + CutHalf + 4f) return false;
                if (d < 60f)
                {
                    int near = Mathf.RoundToInt(t * n) % n;
                    int len = ((to - from) % n + n) % n, rel = ((near - from + 6) % n + n) % n;
                    if (rel > len + 12) return false; // close to a different stretch of road
                }
                var p2 = new Vector2(path[m].x, path[m].z);
                if (lakeR > 0f && Vector2.Distance(p2, lake) < lakeR + 14f) return false;
                if (river.Count > 1 && DistToPolyline(p2, river) < 18f) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ rails with gaps

        /// <summary>Guardrails and marker posts, with gaps where a shortcut leaves or rejoins.</summary>
        static void AddRails(Spline spline, List<Sample> road, List<Cut> cuts, TrackKit kit)
        {
            float len = spline.Length;
            foreach (float side in new[] { 1f, -1f })
            {
                foreach (var run in OpenRuns(road, cuts, side, SurfaceHalf + 0.6f, len))
                    AddPopulation(spline, kit.railPiece, side * (SurfaceHalf + 0.6f), run.x, 0f, len - run.y, true, side > 0 ? "Guardrail L" : "Guardrail R");
                foreach (var run in OpenRuns(road, cuts, side, SurfaceHalf + 2f, len))
                    if (run.y - run.x > 30f)
                        AddPopulation(spline, kit.post, side * (SurfaceHalf + 2f), run.x, 25f, len - run.y, false, side > 0 ? "Posts L" : "Posts R");
            }
        }

        /// <summary>Stretches (spline metres) of one road edge that no shortcut crosses.</summary>
        static List<Vector2> OpenRuns(List<Sample> road, List<Cut> cuts, float side, float offset, float len)
        {
            int n = road.Count;
            float step = len / n;
            var blocked = new bool[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 p = road[i].pos + Vector3.Cross(Vector3.up, Flat(road[i].dir)) * side * offset;
                foreach (var c in cuts) if (DistToPath(p, c.path) < CutHalf + 3f) blocked[i] = true;
            }
            var runs = new List<Vector2>();
            int startRun = -1;
            for (int i = 0; i <= n; i++)
            {
                bool open = i < n && !blocked[i];
                if (open && startRun < 0) startRun = i;
                if (!open && startRun >= 0)
                {
                    float a = startRun == 0 ? 0f : startRun * step, b = i == n ? len : (i - 1) * step;
                    if (b - a > 4f) runs.Add(new Vector2(a, b));
                    startRun = -1;
                }
            }
            return runs;
        }

        public static float DistToPath(Vector3 p, IList<Vector3> path)
        {
            float best = float.MaxValue;
            var p2 = new Vector2(p.x, p.z);
            for (int i = 0; i < path.Count - 1; i++)
            {
                var a = new Vector2(path[i].x, path[i].z); var ab = new Vector2(path[i + 1].x, path[i + 1].z) - a;
                float k = Mathf.Clamp01(Vector2.Dot(p2 - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                best = Mathf.Min(best, (p2 - (a + ab * k)).sqrMagnitude);
            }
            return Mathf.Sqrt(best);
        }

        // ------------------------------------------------------------------ lanes

        static void BuildShortcuts(Transform root, List<Cut> cuts, List<Sample> road, Spec s, TrackKit kit)
        {
            if (cuts.Count == 0) return;
            var holder = new GameObject("Shortcuts").transform;
            holder.SetParent(root, false);
            var dirt = LaneMaterial(s.snowy ? kit.mud : kit.pineDirt);
            var neon = FindMaterial("M_ArenaNeon");
            var steel = AssetDatabase.LoadAssetAtPath<Material>(ArtTrack + "M_Guardrail.mat");
            var roadIdx = new RoadIndex(road);
            int count = 0;
            foreach (var c in cuts)
            {
                var go = new GameObject("Shortcut " + (++count));
                go.transform.SetParent(holder, false);
                var sp = go.AddComponent<ShortcutPath>();
                sp.points = c.path.ToArray();
                sp.halfWidth = CutHalf;

                var mesh = LaneMesh(c.path, "Mesh_Shortcut_" + s.name.Replace(" ", "") + "_" + count);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = dirt;
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);

                // Neon gate where the lane breaks away from the road: says "there's a way through here".
                for (int m = 1; m < c.path.Count - 1; m++)
                {
                    float ry, rt;
                    if (roadIdx.Query(c.path[m], 40f, out ry, out rt) < SurfaceHalf + CutHalf + 2f) continue;
                    var dir = Flat(c.path[m + 1] - c.path[m - 1]);
                    MakeGate(go.transform, c.path[m], dir, neon, steel);
                    break;
                }
            }
        }

        /// <summary>Flat lane strip (sunk just under the asphalt where it overlaps the road), dirt texture tiled
        /// by world metres, with a slight crown so it doesn't look painted on.</summary>
        static Mesh LaneMesh(List<Vector3> path, string name)
        {
            const int cols = 6;
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            float run = 0f;
            for (int i = 0; i < path.Count; i++)
            {
                if (i > 0) run += Vector3.Distance(path[i - 1], path[i]);
                var fwd = Flat(path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)]);
                var right = Vector3.Cross(Vector3.up, fwd);
                for (int c = 0; c <= cols; c++)
                {
                    float x = Mathf.Lerp(-CutHalf - 0.5f, CutHalf + 0.5f, c / (float)cols);
                    float crown = 0.06f * (1f - Mathf.Pow(x / (CutHalf + 0.5f), 2f));
                    verts.Add(path[i] + right * x + Vector3.up * (crown - 0.12f));
                    uvs.Add(new Vector2(x / 6f, run / 6f));
                }
            }
            for (int i = 0; i < path.Count - 1; i++)
                for (int c = 0; c < cols; c++)
                {
                    int a = i * (cols + 1) + c, b = a + cols + 1;
                    tris.Add(a); tris.Add(b); tris.Add(a + 1);
                    tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
                }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
            string path2 = ArtTrack + name + ".asset";
            AssetDatabase.DeleteAsset(path2);
            AssetDatabase.CreateAsset(mesh, path2);
            return mesh;
        }

        static Material LaneMaterial(TerrainLayer layer)
        {
            string path = ArtTrack + "M_ShortcutDirt" + (layer != null ? "_" + layer.name.Replace(" ", "") : "") + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            if (layer != null && layer.diffuseTexture != null)
            {
                mat.SetTexture("_BaseMap", layer.diffuseTexture);
                if (layer.normalMapTexture != null) { mat.SetTexture("_BumpMap", layer.normalMapTexture); mat.EnableKeyword("_NORMALMAP"); }
            }
            mat.SetColor("_BaseColor", new Color(0.82f, 0.78f, 0.74f));
            mat.SetFloat("_Smoothness", 0.08f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material FindMaterial(string name)
        {
            foreach (var g in AssetDatabase.FindAssets(name + " t:Material"))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                if (m != null && m.name == name) return m;
            }
            return null;
        }

        /// <summary>Two steel posts and a neon magenta bar over the lane, with chevrons pointing in.</summary>
        static void MakeGate(Transform parent, Vector3 at, Vector3 dir, Material neon, Material steel)
        {
            var gate = new GameObject("Shortcut Gate").transform;
            gate.SetParent(parent, false);
            gate.SetPositionAndRotation(at, Quaternion.LookRotation(dir, Vector3.up));
            float w = CutHalf + 1.2f;
            System.Action<string, Vector3, Vector3, Material, Quaternion> Box = (n, pos, size, mat, rot) =>
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = n;
                Object.DestroyImmediate(b.GetComponent<Collider>());
                b.transform.SetParent(gate, false);
                b.transform.localPosition = pos; b.transform.localScale = size; b.transform.localRotation = rot;
                b.GetComponent<Renderer>().sharedMaterial = mat;
            };
            Box("Post L", new Vector3(-w, 2.6f, 0), new Vector3(0.35f, 5.2f, 0.35f), steel, Quaternion.identity);
            Box("Post R", new Vector3(w, 2.6f, 0), new Vector3(0.35f, 5.2f, 0.35f), steel, Quaternion.identity);
            Box("Bar", new Vector3(0, 5.1f, 0), new Vector3(w * 2f + 0.4f, 0.32f, 0.32f), neon, Quaternion.identity);
            // Three "V" chevrons hanging under the bar: in here.
            for (int i = -1; i <= 1; i++)
            {
                float x = i * 2.2f;
                Box("Chevron", new Vector3(x - 0.3f, 4.35f, 0), new Vector3(0.18f, 1.0f, 0.12f), neon, Quaternion.Euler(0, 0, 32f));
                Box("Chevron", new Vector3(x + 0.3f, 4.35f, 0), new Vector3(0.18f, 1.0f, 0.12f), neon, Quaternion.Euler(0, 0, -32f));
            }
        }

        // ------------------------------------------------------------------ race line link

        /// <summary>After the race line exists: where each shortcut leaves and rejoins along it, and no
        /// checkpoint left on a stretch a shortcut skips (it moves to just past the exit).</summary>
        internal static void LinkShortcuts(RaceLine line)
        {
            var cuts = Object.FindObjectsByType<ShortcutPath>(FindObjectsSortMode.None);
            if (cuts.Length == 0 || line == null) return;
            foreach (var c in cuts)
            {
                c.entryDistance = line.distances[line.Nearest(c.points[0])];
                c.exitDistance = line.distances[line.Nearest(c.points[c.points.Length - 1])];
                EditorUtility.SetDirty(c);
            }
            var gates = new List<RaceLine.Gate>(line.gates);
            var used = new HashSet<int>();
            for (int g = 0; g < gates.Count; g++)
            {
                float d = line.distances[line.Nearest(gates[g].position)];
                foreach (var c in cuts)
                {
                    float span = Mathf.Repeat(c.exitDistance - c.entryDistance, line.length);
                    float rel = Mathf.Repeat(d - c.entryDistance + 10f, line.length);
                    if (g == 0 || rel > span + 20f) continue;
                    float nd = c.exitDistance + 15f;
                    int k = line.IndexAt(nd);
                    gates[g] = new RaceLine.Gate { position = line.PointAt(nd), forward = line.Direction(k), halfWidth = gates[g].halfWidth };
                }
            }
            // Two gates moved onto the same spot: keep one. Keep race order.
            var kept = new List<RaceLine.Gate>();
            foreach (var gate in gates)
            {
                int k = line.Nearest(gate.position) / 5;
                if (used.Add(k)) kept.Add(gate);
            }
            kept.Sort((a, b) =>
            {
                float da = a.Equals(gates[0]) ? -1f : line.distances[line.Nearest(a.position)];
                float db = b.Equals(gates[0]) ? -1f : line.distances[line.Nearest(b.position)];
                return da.CompareTo(db);
            });
            line.gates = kept.ToArray();
            EditorUtility.SetDirty(line);
        }
    }
}
