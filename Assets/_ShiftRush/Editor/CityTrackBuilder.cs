using System.Collections.Generic;
using ShiftRush.Race;
using SplineArchitect;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// SHIFT RUSH → Tracks → Build Downtown Neon: our own street circuit, about 4.4 km a lap, laid on an empty map
    /// out of Mega City's road tiles scaled up 1.6x (the pack's streets are too narrow for a full field of cars).
    /// The layout is a list of corners on a 24 m grid: Neon Boulevard (start, chopper drop), the Harbour Run along
    /// the water, the container hairpin, the overpass, the market chicane, the metro tunnel and the downtown esses
    /// round the park. Every other cell is a city block with a building picked for its district. Then knockable
    /// dressing and the Race Track Wizard's steps. Rerun to rebuild; tune the layout here, not in the scene.
    /// </summary>
    public static class CityTrackBuilder
    {
        const string SceneName = "Track_DowntownNeon";
        const string ScenePath = "Assets/_ShiftRush/Scenes/" + SceneName + ".unity";
        const string Template = "Assets/_ShiftRush/Scenes/TestArena.unity";
        const string Pack = "Assets/JC_LP_MegaCity/Prefabs/";
        const float S = 1.6f;            // scale on every city piece
        const float Tile = 15f;          // a 4-lane road tile, unscaled
        static float G { get { return Tile * S; } } // grid cell, metres

        // Corners of the circuit in grid cells (x east, y south), in driving order, starting on the boulevard.
        static readonly Vector2Int[] Corners =
        {
            new Vector2Int(24, 8), new Vector2Int(44, 8), new Vector2Int(44, 36), new Vector2Int(21, 36), new Vector2Int(21, 33),
            new Vector2Int(17, 33), new Vector2Int(17, 36), new Vector2Int(8, 36), new Vector2Int(8, 20), new Vector2Int(20, 20),
            new Vector2Int(20, 25), new Vector2Int(32, 25), new Vector2Int(32, 15), new Vector2Int(12, 15), new Vector2Int(12, 8),
        };

        // The overpass: along y = 36, ramps up from x 37 to 34, deck 33..30, down 29..26.
        const int OverY = 36, RampUpStart = 37, DeckStart = 33, DeckEnd = 30, RampDownEnd = 26;
        const float OverHeight = 7f;
        // The metro tunnel: along x = 8, from y 32 up to 24.
        const int TunnelX = 8, TunnelFrom = 24, TunnelTo = 32;
        // The harbour: water from x 46 east, the quay at x 45.
        const int QuayX = 45;
        // The park inside the esses.
        static readonly RectInt Park = new RectInt(21, 16, 11, 9);
        // A cross street passing under the overpass.
        const int CrossX = 31;
        // The block area: a ring of city well beyond the circuit, so the edge of the world is never in view.
        const int MinCell = -10, MaxCellY = 52;

        static Transform root;
        static System.Random rng;

        [MenuItem("SHIFT RUSH/Tracks/Build Downtown Neon")]
        public static string Build()
        {
            if (EditorApplication.isPlaying) return "Stop Play mode first";
            rng = new System.Random(2026);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(ScenePath);
            AssetDatabase.CopyAsset(Template, ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == "Plane" || go.GetComponent<RCCP_CarController>() != null) Object.DestroyImmediate(go);
            root = new GameObject("Downtown Neon").transform;

            var cells = Expand(Corners);
            var road = new HashSet<Vector2Int>(cells);
            // The road is a Spline Architect spline through the corners, each corner rounded off, rising over the
            // overpass. Asphalt and kerbs come from the same road kit as the other tracks.
            var spline = TrackBuilder.CityRoad(root, Rounded(Corners));
            var samples = TrackBuilder.SampleSpline(spline, 5f);
            routeSamples = new List<Vector3>(); foreach (var sm in samples) routeSamples.Add(sm.pos);
            CrossStreet();
            Overpass(cells);
            Tunnel();
            Harbour();
            Ground(cells);
            int buildings = Blocks(road);
            ParkBlock();
            StreetLights(cells);

            // The route for the wizard: every cell centre, at road height.
            var trackGo = new GameObject("Track");
            var info = trackGo.AddComponent<TrackInfo>();
            info.displayName = "Downtown Neon"; info.id = "downtown";
            info.description = "Our city at full width: the neon boulevard, the harbour, the overpass, the market chicane and the metro tunnel.";
            info.loadingTips = new[]
            {
                "The boulevard is the drop zone. Land straight and floor it.",
                "The container hairpin at the end of the harbour is the hardest stop on the lap.",
                "The chicane is narrow. That's where the cops box you in.",
                "The tunnel hides you from the cops for a few seconds.",
            };
            info.laps = 2; info.halfWidth = TrackBuilder.RoadWidth * 0.5f; info.checkpointSpacing = 140f;
            info.route = TrackInfo.RouteSource.RoadSpline; info.snapToGround = false;
            info.trackWalls = true; info.shiftRamps = false;
            new GameObject("Route").transform.SetParent(trackGo.transform, false);

            var dressing = new GameObject("Race Dressing").transform; dressing.SetParent(root, false);
            var pts = new List<Vector3>(); for (int i = 0; i < samples.Count; i += 2) pts.Add(samples[i].pos);
            int knocks = Dress(pts, TrackBuilder.RoadWidth * 0.5f, dressing);

            TrackBuilder.ApplyKroma("Kroma_Profile_27_GoldenHour");
            var wizard = EditorWindow.GetWindow<RaceTrackWizard>();
            typeof(RaceTrackWizard).GetMethod("RunAll", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(wizard, null);
            CityRigCheck(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            var line = Object.FindFirstObjectByType<RaceLine>();
            string report = "Downtown Neon: " + (line != null ? line.length.ToString("0") : "?") + " m lap, " + cells.Count + " road cells, " + buildings + " buildings, " + knocks + " knockables";
            Debug.Log("[CITY] " + report);
            return report;
        }

        // ------------------------------------------------------------------ layout

        static List<Vector2Int> Expand(Vector2Int[] corners)
        {
            var cells = new List<Vector2Int>();
            for (int i = 0; i < corners.Length; i++)
            {
                var a = corners[i]; var b = corners[(i + 1) % corners.Length];
                var step = new Vector2Int(System.Math.Sign(b.x - a.x), System.Math.Sign(b.y - a.y));
                for (var c = a; c != b; c += step) cells.Add(c);
            }
            return cells;
        }

        static Vector3 Centre(Vector2Int c) { return new Vector3(c.x * G, 0f, -c.y * G); }

        static List<Vector3> routeSamples;
        const float CornerRadius = 30f;

        /// <summary>
        /// The corners with each one rounded into an arc, points every ~12 m on the straights, heights from the
        /// overpass profile. The spline goes through these.
        /// </summary>
        static List<Vector3> Rounded(Vector2Int[] corners)
        {
            var pts = new List<Vector3>();
            int n = corners.Length;
            for (int i = 0; i < n; i++)
            {
                Vector3 prev = Centre(corners[(i - 1 + n) % n]), c = Centre(corners[i]), next = Centre(corners[(i + 1) % n]);
                Vector3 din = (c - prev).normalized, dout = (next - c).normalized;
                float r = Mathf.Min(CornerRadius, Vector3.Distance(prev, c) * 0.45f, Vector3.Distance(c, next) * 0.45f);
                Vector3 a = c - din * r, b = c + dout * r;
                // Arc from a to b (quadratic through the corner, close enough to a circle for a spline).
                for (int k = 0; k <= 4; k++)
                {
                    float t = k / 4f;
                    var q = Vector3.Lerp(Vector3.Lerp(a, c, t), Vector3.Lerp(c, b, t), t);
                    pts.Add(WithHeight(q));
                }
                // The straight to the next corner.
                float rNext = Mathf.Min(CornerRadius, Vector3.Distance(c, next) * 0.45f, Vector3.Distance(next, Centre(corners[(i + 2) % n])) * 0.45f);
                Vector3 s0 = b, s1 = next - dout * rNext;
                float len = Vector3.Distance(s0, s1);
                int steps = Mathf.Max(1, Mathf.RoundToInt(len / 12f));
                for (int k = 1; k < steps; k++) pts.Add(WithHeight(Vector3.Lerp(s0, s1, k / (float)steps)));
            }
            return pts;
        }

        static Vector3 WithHeight(Vector3 p)
        {
            // Over the overpass row the height follows the ramp profile smoothly, by x.
            if (Mathf.Abs(-p.z / G - OverY) < 0.6f)
            {
                float x = p.x / G;
                float h;
                if (x > RampUpStart + 0.5f || x < RampDownEnd - 0.5f) h = 0f;
                else if (x <= DeckStart && x >= DeckEnd) h = OverHeight;
                else if (x > DeckStart) h = OverHeight * Mathf.SmoothStep(0f, 1f, (RampUpStart + 0.5f - x) / (RampUpStart + 0.5f - DeckStart));
                else h = OverHeight * Mathf.SmoothStep(0f, 1f, (x - (RampDownEnd - 0.5f)) / (DeckEnd - (RampDownEnd - 0.5f)));
                p.y = h;
            }
            return p;
        }

        /// <summary>True if this box comes within the road plus pavement of the circuit.</summary>
        static bool OnRoad(Bounds b, float clearance)
        {
            if (routeSamples == null) return false;
            var e = b; e.Expand(new Vector3(clearance * 2f, 2000f, clearance * 2f));
            foreach (var p in routeSamples) if (e.Contains(p)) return true;
            return false;
        }

        static Bounds Footprint(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>The street under the overpass, as decoration (it isn't on the circuit).</summary>
        static void CrossStreet()
        {
            var parent = new GameObject("Cross Street").transform; parent.SetParent(root, false);
            for (int y = OverY - 6; y <= OverY + 6; y++)
                if (y != OverY) PlaceTile("Floor/SM_Floor_Road_14_L", parent, new Vector2Int(CrossX, y), 0, 0, 0, 0);
        }
        static Vector3 World(Vector2Int d) { return new Vector3(d.x, 0f, -d.y); }

        static float HeightAt(Vector2Int c)
        {
            if (c.y != OverY || c.x > RampUpStart || c.x < RampDownEnd) return 0f;
            if (c.x <= DeckStart && c.x >= DeckEnd) return OverHeight;
            if (c.x > DeckStart) return OverHeight * (RampUpStart + 1 - c.x) / (float)(RampUpStart + 1 - DeckStart);
            return OverHeight * (c.x - RampDownEnd + 1) / (float)(DeckEnd - RampDownEnd + 1);
        }

        // ------------------------------------------------------------------ road

        static void LayRoad(List<Vector2Int> cells)
        {
            var parent = new GameObject("Road").transform; parent.SetParent(root, false);
            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                var din = cells[i] - cells[(i - 1 + cells.Count) % cells.Count];
                var dout = cells[(i + 1) % cells.Count] - cells[i];
                if (din == dout)
                {
                    float yaw = Mathf.Atan2(World(din).x, World(din).z) * Mathf.Rad2Deg; // facing the way we drive
                    PlaceTile("Floor/SM_Floor_Road_14_L", parent, c, yaw, HeightAt(cells[(i - 1 + cells.Count) % cells.Count]), HeightAt(cells[(i + 1) % cells.Count]), HeightAt(c));
                }
                else
                {
                    // The corner piece joins its north and west edges at no rotation: turn it to join where the road
                    // comes in from and where it leaves to.
                    var e1 = World(-din); var e2 = World(dout);
                    float yaw = 0f;
                    for (int r = 0; r < 4; r++)
                    {
                        var q = Quaternion.Euler(0, r * 90f, 0);
                        var n = q * Vector3.forward; var w = q * Vector3.left;
                        if ((Same(n, e1) && Same(w, e2)) || (Same(n, e2) && Same(w, e1))) { yaw = r * 90f; break; }
                    }
                    PlaceTile("Floor/SM_Floor_Road_19", parent, c, yaw, 0, 0, HeightAt(c));
                }
            }
            // The cross street under the overpass, for the picture.
            for (int y = OverY - 6; y <= OverY + 6; y++)
            {
                if (y == OverY) { PlaceTile("Floor/SM_Floor_Road_16", parent, new Vector2Int(CrossX, y), 0, 0, 0, 0); continue; }
                PlaceTile("Floor/SM_Floor_Road_14_L", parent, new Vector2Int(CrossX, y), 0, 0, 0, 0);
            }
        }

        static bool Same(Vector3 a, Vector3 b) { return (a - b).sqrMagnitude < 0.01f; }

        /// <summary>A road tile centred on the cell, scaled, tilted to join its neighbours' heights when on a ramp.</summary>
        static GameObject PlaceTile(string prefab, Transform parent, Vector2Int c, float yaw, float hPrev, float hNext, float h)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + prefab + ".prefab");
            if (src == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
            var rot = Quaternion.Euler(0, yaw, 0);
            // On a ramp, tilt along the way we drive (yaw faces it) by the rise between the neighbouring cells.
            float slope = (hNext - hPrev) / (2f * G);
            if (Mathf.Abs(slope) > 0.0001f)
                rot = Quaternion.LookRotation((rot * Vector3.forward + Vector3.up * slope).normalized, Vector3.up);
            go.transform.localScale = Vector3.one * S;
            var centre = Centre(c) + Vector3.up * h;
            // Pieces pivot on one edge (their centre is 7.5 m behind it): put the centre on the cell's centre.
            go.transform.SetPositionAndRotation(centre - rot * new Vector3(0, 0, -7.5f * S), rot);
            return go;
        }

        static void Overpass(List<Vector2Int> cells)
        {
            var parent = new GameObject("Overpass").transform; parent.SetParent(root, false);
            var concrete = Mat("Concrete", new Color(0.55f, 0.55f, 0.58f));
            for (int x = RampDownEnd; x <= RampUpStart; x++)
            {
                var c = new Vector2Int(x, OverY);
                float h = HeightAt(c);
                if (h < 1.5f) continue;
                // Pillars both sides, and a parapet so it reads as a bridge.
                foreach (float side in new[] { -1f, 1f })
                {
                    var p = Centre(c) + Vector3.forward * side * (G * 0.5f - 1f);
                    Box(parent, "Pillar", p + Vector3.up * (h * 0.5f - 0.4f), new Vector3(2.2f, h - 0.8f, 2.2f), concrete);
                    Box(parent, "Parapet", p + Vector3.up * (h + 0.6f), new Vector3(G, 1.2f, 0.6f), concrete);
                }
                // A deck slab only where the road is flat: on the ramps a slab would poke up through the asphalt.
                if (x <= DeckStart && x >= DeckEnd) Box(parent, "Deck", Centre(c) + Vector3.up * (h - 1.1f), new Vector3(G, 1.6f, G - 1f), concrete);
            }
        }

        static void Tunnel()
        {
            var parent = new GameObject("Metro Tunnel").transform; parent.SetParent(root, false);
            var concrete = Mat("Tunnel Concrete", new Color(0.32f, 0.32f, 0.36f));
            var plaza = Mat("Plaza", new Color(0.6f, 0.58f, 0.55f));
            float h = 9f;
            for (int y = TunnelFrom; y <= TunnelTo; y++)
            {
                var c = Centre(new Vector2Int(TunnelX, y));
                Box(parent, "Roof", c + Vector3.up * (h + 0.6f), new Vector3(G + 6f, 1.2f, G), plaza);
                foreach (float side in new[] { -1f, 1f })
                    Box(parent, "Wall", c + Vector3.right * side * (G * 0.5f + 1.5f) + Vector3.up * h * 0.5f, new Vector3(3f, h, G), concrete);
                if (y % 2 == 0)
                {
                    var l = new GameObject("Tunnel Light"); l.transform.SetParent(parent, false);
                    l.transform.position = c + Vector3.up * (h - 1f);
                    var light = l.AddComponent<Light>(); light.type = LightType.Point; light.range = 26f; light.intensity = 3f;
                    light.color = new Color(1f, 0.72f, 0.45f);
                }
                // Life on the plaza above.
                Prop("Nature/SM_Nature_Tree_0" + (1 + rng.Next(8)), parent, c + Vector3.up * (h + 1.2f) + Vector3.right * 6f, rng.Next(360));
            }
        }

        static void Harbour()
        {
            var parent = new GameObject("Harbour").transform; parent.SetParent(root, false);
            var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Sea"; water.transform.SetParent(parent, false);
            var west = (QuayX + 0.5f) * G;
            water.transform.position = new Vector3(west + 600f, -1.6f, -22f * G);
            water.transform.localScale = new Vector3(120f, 1f, 120f);
            var wm = AssetDatabase.LoadAssetAtPath<Material>("Assets/JC_LP_MegaCity/Materials/M_MegaCity_Water.mat");
            if (wm != null) water.GetComponent<Renderer>().sharedMaterial = wm;
            Object.DestroyImmediate(water.GetComponent<Collider>());
            // The quay: containers stacked, cranes every few cells, a kerb so the drop to the water is visible.
            var concrete = Mat("Quay", new Color(0.45f, 0.45f, 0.47f));
            for (int y = 6; y <= 38; y++)
            {
                var c = Centre(new Vector2Int(QuayX, y));
                Box(parent, "Quay", c + Vector3.down * 0.8f, new Vector3(G, 1.6f, G), concrete);
                if (y % 6 == 0) Prop("Buildings/SM_Buildings_Port_04", parent, c + Vector3.right * 6f, 90f);
                else
                {
                    int stack = 1 + rng.Next(3);
                    for (int k = 0; k < stack; k++)
                        Prop("IndustrialProps/SM_IndustrialProps_ShippingContainer_0" + (1 + rng.Next(3)), parent, c + Vector3.up * (k * 2.6f * S) + Vector3.right * 4f, rng.Next(2) * 180f);
                }
            }
        }

        static void Ground(List<Vector2Int> cells)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = "City Ground"; g.transform.SetParent(root, false);
            float minX = MinCell * G - G, maxX = (QuayX + 0.5f) * G, minZ = -MaxCellY * G - G, maxZ = -MinCell * G + G;
            g.transform.position = new Vector3((minX + maxX) * 0.5f, -0.55f, (minZ + maxZ) * 0.5f);
            g.transform.localScale = new Vector3(maxX - minX, 1f, maxZ - minZ);
            g.GetComponent<Renderer>().sharedMaterial = Mat("Pavement", new Color(0.5f, 0.49f, 0.47f));
        }

        // ------------------------------------------------------------------ blocks

        static readonly string[] Downtown = { "Skyscraper_01", "Skyscraper_02", "Skyscraper_03", "Skyscraper_04", "Office_02", "Office_03", "Office_08", "Office_09", "Hotel_02", "Hotel_03", "Office_06" };
        static readonly string[] Harbourside = { "Warehouse_01", "Factory_04", "Factory_07", "Port_01", "Port_02", "Construction_01", "Car_Service" };
        static readonly string[] OldTown = { "House_01_V1", "House_02_V2", "House_03_V3", "House_04_V1", "House_05_V2", "House_09_V1", "House_11_V1", "House_14_V1", "Commercial_02", "Commercial_07", "Commercial_12", "Supermarket_01" };
        static readonly string[] Civic = { "Bank", "Church_01", "FireStation_01", "Office_01", "Office_11", "House_12_V1", "House_13_V1", "Commercial_14" };
        static readonly string[] Midtown = { "Office_04", "Office_05", "Office_07", "Office_10", "House_06_V1", "House_07_V2", "House_08_V1", "House_10_V1", "Commercial_15", "Commercial_20" };

        static int Blocks(HashSet<Vector2Int> road)
        {
            var parent = new GameObject("Blocks").transform; parent.SetParent(root, false);
            int n = 0;
            for (int y = MinCell; y <= MaxCellY; y++)
                for (int x = MinCell; x < QuayX; x++)
                {
                    var c = new Vector2Int(x, y);
                    if (road.Contains(c) || Park.Contains(c) || x == CrossX && Mathf.Abs(y - OverY) <= 6) continue;
                    if (x >= TunnelX - 1 && x <= TunnelX + 1 && y >= TunnelFrom && y <= TunnelTo) continue; // tunnel walls and plaza
                    bool front = false; Vector2Int face = Vector2Int.up;
                    foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                        if (road.Contains(c + d)) { front = true; face = d; break; }
                    if (!front && rng.NextDouble() > 0.8) continue;
                    string[] kit = y <= 12 ? Downtown : x >= 38 ? Harbourside : y >= 30 ? OldTown : x <= 12 ? Civic : Midtown;
                    string pick = kit[rng.Next(kit.Length)];
                    // Face the street (or a random side in the middle of a block).
                    float yaw = front ? Mathf.Atan2(face.x, -face.y) * Mathf.Rad2Deg : rng.Next(4) * 90f;
                    var b = Prop("Buildings/SM_Buildings_" + pick, parent, Centre(c), yaw);
                    if (b == null) continue;
                    // Never on the road or the pavement, never hanging off the edge of the map.
                    var fp = Footprint(b);
                    bool outside = fp.min.x < MinCell * G - G * 0.5f || fp.max.x > (QuayX - 0.5f) * G || fp.max.z > -MinCell * G + G * 0.5f || fp.min.z < -MaxCellY * G - G * 0.5f;
                    if (OnRoad(fp, TrackBuilder.RoadWidth * 0.5f + 4f) || outside) { Object.DestroyImmediate(b); continue; }
                    n++;
                }
            return n;
        }

        static void ParkBlock()
        {
            var parent = new GameObject("Park").transform; parent.SetParent(root, false);
            var grass = Mat("Park Grass", new Color(0.33f, 0.55f, 0.25f));
            var centre = new Vector3((Park.xMin + Park.xMax - 1) * 0.5f * G, 0.05f, -(Park.yMin + Park.yMax - 1) * 0.5f * G);
            Box(parent, "Lawn", centre, new Vector3(Park.width * G - 4f, 0.2f, Park.height * G - 4f), grass);
            for (int k = 0; k < 70; k++)
            {
                var p = centre + new Vector3(((float)rng.NextDouble() - 0.5f) * (Park.width * G - 16f), 0.1f, ((float)rng.NextDouble() - 0.5f) * (Park.height * G - 16f));
                Prop("Nature/SM_Nature_Tree_" + (1 + rng.Next(12)).ToString("00"), parent, p, rng.Next(360));
            }
            Prop("Buildings/SM_Buildings_Skatepark_01", parent, centre + new Vector3(30f, 0, 10f), 0f);
        }

        static void StreetLights(List<Vector2Int> cells)
        {
            var parent = new GameObject("Street Lights").transform; parent.SetParent(root, false);
            for (int i = 0; i < cells.Count; i += 2)
            {
                var c = cells[i];
                if (HeightAt(c) > 0.5f) continue;
                if (c.x == TunnelX && c.y >= TunnelFrom && c.y <= TunnelTo) continue;
                var d = cells[(i + 1) % cells.Count] - c;
                if (d == cells[i] - cells[(i - 1 + cells.Count) % cells.Count])
                {
                    var right = Vector3.Cross(Vector3.up, World(d));
                    float side = (i / 2) % 2 == 0 ? 1f : -1f;
                    // Lamp posts go down when hit (heavy, so they swing rather than fly).
                    Knockable("FloorProps/SM_FloorProps_Light_01", parent, Centre(c) + right * side * (G * 0.5f - 2f), Mathf.Atan2(-right.x * side, -right.z * side) * Mathf.Rad2Deg, 30f, S);
                }
            }
        }

        // ------------------------------------------------------------------ helpers

        static GameObject Prop(string prefab, Transform parent, Vector3 pos, float yaw)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + prefab + ".prefab");
            if (src == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            go.transform.localScale = Vector3.one * S;
            return go;
        }

        static void Box(Transform parent, string name, Vector3 pos, Vector3 size, Material m)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = name; b.transform.SetParent(parent, false);
            b.transform.position = pos; b.transform.localScale = size;
            b.GetComponent<Renderer>().sharedMaterial = m;
        }

        static Material Mat(string name, Color c)
        {
            string path = "Assets/_ShiftRush/Art/Track/M_City_" + name.Replace(" ", "") + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static float Ground(Vector3 p)
        {
            float best = float.MinValue;
            foreach (var hit in Physics.RaycastAll(p + Vector3.up * 30f, Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.point.y > best && hit.point.y < p.y + 12f) best = hit.point.y;
            return best > float.MinValue ? best : p.y;
        }

        static Vector3 Dir(List<Vector3> pts, int i)
        {
            var d = pts[(i + 1) % pts.Count] - pts[(i - 1 + pts.Count) % pts.Count]; d.y = 0;
            return d.sqrMagnitude > 0 ? d.normalized : Vector3.forward;
        }

        /// <summary>Cones and barrels on the outside of every corner, bins and boxes on the kerbs: all knockable.</summary>
        static int Dress(List<Vector3> pts, float half, Transform parent)
        {
            int count = 0;
            string[] cornerKit = { "FloorProps/SM_FloorProps_01", "IndustrialProps/SM_IndustrialProps_Barrel_01", "IndustrialProps/SM_IndustrialProps_Barrel_03" };
            string[] kerbKit = { "Props/SM_Props_Trash_01", "Props/SM_Props_Garbage_01", "Props/SM_Props_Box_01", "IndustrialProps/SM_IndustrialProps_Barrel_02" };
            int lastCorner = -100;
            for (int i = 0; i < pts.Count; i++)
            {
                var d0 = Dir(pts, (i - 2 + pts.Count) % pts.Count); var d1 = Dir(pts, (i + 2) % pts.Count);
                float turn = Vector3.SignedAngle(d0, d1, Vector3.up);
                var d = Dir(pts, i); var r = Vector3.Cross(Vector3.up, d);
                // One fan of cones per corner, at its apex.
                bool apex = Mathf.Abs(turn) > 50f && i - lastCorner > 6;
                if (apex) lastCorner = i;
                if (apex)
                {
                    float side = turn > 0 ? -1f : 1f;
                    for (int k = 0; k < 6; k++)
                    {
                        var p = pts[i] + r * side * (half - 0.5f) + d * (k - 2.5f) * 2.2f;
                        p.y = Ground(p);
                        if (Knockable(cornerKit[(k + i) % cornerKit.Length], parent, p, rng.Next(360))) count++;
                    }
                }
                else if (pts[i].y < 0.5f)
                {
                    // Kerb furniture on every straight cell, alternating sides: bins and boxes, and every few cells
                    // something bigger to plough through (bus stop, bench, vending machine, ad board, market stall
                    // in the old town).
                    float side = i % 2 == 0 ? 1f : -1f;
                    var p = pts[i] + r * side * (half - 0.3f) + d * ((float)rng.NextDouble() * 8f - 4f); p.y = Ground(p);
                    float faceRoad = Mathf.Atan2(-r.x * side, -r.z * side) * Mathf.Rad2Deg;
                    bool oldTown = -pts[i].z / G >= 30f;
                    if (i % 4 == 0)
                    {
                        string big = oldTown ? "Props/SM_Props_Market_0" + (1 + rng.Next(3))
                            : new[] { "FloorProps/SM_FloorProps_BusStation_01", "Props/SM_Props_Bench_01", "Props/SM_Props_VendingMachine_0" + (1 + rng.Next(3)), "Props/SM_Props_Ads_0" + (1 + rng.Next(8)) }[rng.Next(4)];
                        if (Knockable(big, parent, p, faceRoad, 25f, S)) count++;
                    }
                    else if (Knockable(kerbKit[rng.Next(kerbKit.Length)], parent, p, rng.Next(360))) count++;
                    if (i % 4 == 2)
                    {
                        var p2 = pts[i] - r * side * (half - 0.5f); p2.y = Ground(p2);
                        if (Knockable(kerbKit[rng.Next(kerbKit.Length)], parent, p2, rng.Next(360))) count++;
                    }
                }
                // A road sign on the inside of every corner.
                if (apex)
                {
                    float side = turn > 0 ? 1f : -1f;
                    var p = pts[i] + r * side * (half - 0.2f); p.y = Ground(p);
                    if (Knockable("FloorProps/SM_FloorProps_RoadSign_0" + (1 + rng.Next(9)), parent, p, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg + 180f, 12f, S)) count++;
                }
            }

            // Roadworks: three smash-through rows of barriers and cones across half the street, gap on the other side.
            foreach (float frac in new[] { 0.08f, 0.36f, 0.7f })
            {
                int i = Mathf.Clamp(Mathf.RoundToInt(pts.Count * frac), 1, pts.Count - 2);
                if (pts[i].y > 0.5f) i += 6;
                var d = Dir(pts, i); var r = Vector3.Cross(Vector3.up, d);
                float side = frac < 0.5f ? 1f : -1f;
                for (float x = 0.5f; x < half; x += 1.7f)
                {
                    var p = pts[i] + r * side * x; p.y = Ground(p);
                    if (Knockable(x < 1f || x > half - 2f ? "FloorProps/SM_FloorProps_01" : "IndustrialProps/SM_IndustrialProps_Construction_04", parent, p, Mathf.Atan2(r.x, r.z) * Mathf.Rad2Deg, 6f)) count++;
                }
            }
            return count;
        }

        static bool Knockable(string prefab, Transform parent, Vector3 pos, float yaw, float mass = 4f, float scale = 1.3f)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + prefab + ".prefab");
            if (src == null) return false;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            go.transform.localScale = Vector3.one * scale;
            if (go.GetComponent<Collider>() == null)
            {
                var box = go.AddComponent<BoxCollider>();
                var rs = go.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    var b = rs[0].bounds; foreach (var rr in rs) b.Encapsulate(rr.bounds);
                    box.center = go.transform.InverseTransformPoint(b.center);
                    var sz = go.transform.InverseTransformVector(b.size);
                    box.size = new Vector3(Mathf.Abs(sz.x), Mathf.Abs(sz.y), Mathf.Abs(sz.z));
                }
                foreach (var c in go.GetComponentsInChildren<Collider>()) if (c != box) Object.DestroyImmediate(c);
            }
            if (go.GetComponent<Rigidbody>() == null) go.AddComponent<Rigidbody>();
            var prop = go.AddComponent<BreakableProp>();
            var so = new SerializedObject(prop);
            so.FindProperty("mass").floatValue = mass;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        /// <summary>TestArena brings the race camera; if it's ever missing, copy Alpine's.</summary>
        static void CityRigCheck(UnityEngine.SceneManagement.Scene target)
        {
            foreach (var r in target.GetRootGameObjects()) if (r.name == "RCCP_Camera") return;
            var src = EditorSceneManager.OpenScene("Assets/_ShiftRush/Scenes/Track_AlpinePass.unity", OpenSceneMode.Additive);
            try
            {
                foreach (var r in src.GetRootGameObjects())
                    if (r.name == "RCCP_Camera" || r.name == "RCCP_Canvas")
                    {
                        var copy = Object.Instantiate(r); copy.name = r.name;
                        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(copy, target);
                    }
            }
            finally { EditorSceneManager.CloseScene(src, true); }
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(target);
        }
    }
}
