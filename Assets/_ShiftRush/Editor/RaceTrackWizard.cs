using System.Collections.Generic;
using System.IO;
using ShiftRush.Core;
using ShiftRush.Race;
using ShiftRush.Shift;
using ShiftRush.Vehicles;
using SplineArchitect;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// SHIFT RUSH → Tracks → Race Track Wizard. Turns the open scene into a race track, one step at a time, with a
    /// live checklist: the track's details, its route (the road spline, or points you place around the road), the
    /// start line, the race line with its checkpoints and minimap, the player's car on the grid, walls and SHIFT ramps
    /// (both optional), and finally the menu: a hero shot, the race catalog and the build. Every step can be rerun
    /// after you change the road, and "Run every step" does them all in order.
    /// </summary>
    public class RaceTrackWizard : EditorWindow
    {
        const string ScenesDir = "Assets/_ShiftRush/Scenes/";
        const string ArtDir = "Assets/_ShiftRush/Art/Track/";
        const float LineStep = 4f;

        static readonly Color Good = new Color(0.49f, 0.95f, 0.6f), Warn = new Color(1f, 0.77f, 0.24f), Bad = new Color(1f, 0.4f, 0.45f);
        Vector2 scroll;
        GUIStyle head, step, small;

        [MenuItem("SHIFT RUSH/Tracks/Race Track Wizard")]
        static void Open() { GetWindow<RaceTrackWizard>("Race Track Wizard").minSize = new Vector2(420, 560); }

        void OnInspectorUpdate() { Repaint(); } // keep the checklist live while you edit the scene

        // ------------------------------------------------------------------ state of the scene

        TrackInfo Info { get { return Object.FindFirstObjectByType<TrackInfo>(); } }
        Spline RoadSpline { get { return Object.FindFirstObjectByType<Spline>(); } }
        RaceLine Line { get { return Object.FindFirstObjectByType<RaceLine>(); } }
        Transform StartLine
        {
            get
            {
                var grid = GameObject.Find("Start Grid");
                if (grid == null) return null;
                var exact = grid.transform.Find("StartLine");
                if (exact != null) return exact;
                foreach (Transform c in grid.transform) if (c.name.Contains("StartLine")) return c;
                return null;
            }
        }
        bool HasCar
        {
            get { foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)) if (mb is IVehicle) return true; return false; }
        }
        int RoutePoints { get { var i = Info; return i != null && i.RouteRoot != null ? i.RouteRoot.childCount : 0; } }
        bool RouteReady
        {
            get
            {
                var i = Info;
                if (i == null) return false;
                return i.route == TrackInfo.RouteSource.RoadSpline ? RoadSpline != null : RoutePoints >= 4;
            }
        }
        string ScenePath { get { return EditorSceneManager.GetActiveScene().path; } }
        string SceneName { get { return Path.GetFileNameWithoutExtension(ScenePath); } }
        RaceCatalog Catalog { get { return AssetDatabase.LoadAssetAtPath<RaceCatalog>("Assets/_ShiftRush/Resources/RaceCatalog.asset"); } }
        bool InCatalog { get { var c = Catalog; return c != null && !string.IsNullOrEmpty(ScenePath) && c.tracks.Exists(t => t.scene == SceneName); } }
        bool InBuild { get { foreach (var s in EditorBuildSettings.scenes) if (s.path == ScenePath) return true; return false; } }

        // ------------------------------------------------------------------ window

        void Styles()
        {
            if (head != null) return;
            head = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 };
            step = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            small = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
        }

        void OnGUI()
        {
            Styles();
            if (EditorApplication.isPlaying) { EditorGUILayout.HelpBox("Stop Play mode to set up a track.", MessageType.Info); return; }
            scroll = EditorGUILayout.BeginScrollView(scroll);

            int left = 0;
            var info = Info;
            bool saved = !string.IsNullOrEmpty(ScenePath);
            var line = Line;

            EditorGUILayout.LabelField(saved ? SceneName : "Untitled scene", head);
            left += (saved ? 0 : 1) + (info ? 0 : 1) + (RouteReady ? 0 : 1) + (StartLine ? 0 : 1) + (line ? 0 : 1) + (HasCar ? 0 : 1) + (InCatalog && InBuild ? 0 : 1);
            var c = GUI.color; GUI.color = left == 0 ? Good : Warn;
            EditorGUILayout.LabelField(left == 0 ? "READY TO RACE" : left + (left == 1 ? " step left" : " steps left"), step);
            GUI.color = c;
            EditorGUILayout.Space(6);

            // 1. Scene
            if (Step("1  Scene", saved, saved ? ScenePath : "Save the scene first, so the track has a name and a home."))
            {
                if (!saved && GUILayout.Button("Save scene as a new track")) SaveSceneAs();
            }

            // 2. Track details
            if (Step("2  Track details", info != null, info != null ? info.displayName + "  ·  " + info.laps + " laps" : "Adds a Track object that keeps this track's settings."))
            {
                if (info == null) { if (GUILayout.Button("Start a track in this scene")) CreateInfo(); }
                else DrawInfo(info);
            }

            // 3. Route
            string routeNote = info == null ? "Needs step 2." :
                info.route == TrackInfo.RouteSource.RoadSpline ? (RoadSpline != null ? "Using the road spline (" + RoadSpline.name + ")." : "No road spline in this scene. Switch to route points.") :
                RoutePoints + " route points" + (RoutePoints < 4 ? "  (need at least 4)" : "") + ". Place them along the middle of the road, in driving order. The first one is the start.";
            if (Step("3  Route", RouteReady, routeNote) && info != null) DrawRoute(info);

            // 4. Start line
            if (Step("4  Start line", StartLine != null, StartLine != null ? "Start line placed. Move it to adjust, then rebuild the race line." : "The finish line, where laps are counted and the grid lines up behind."))
            {
                using (new EditorGUI.DisabledScope(!RouteReady))
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(StartLine != null ? "Move to the route start" : "Place at the route start")) PlaceStartLine();
                    if (StartLine != null && GUILayout.Button("Select")) Selection.activeTransform = StartLine;
                    EditorGUILayout.EndHorizontal();
                }
            }

            // 5. Race line
            string lineNote = line == null ? "The line cars follow: lap progress, positions, checkpoints and the minimap." :
                (line.length / 1000f).ToString("0.00") + " km  ·  " + (line.gates != null ? line.gates.Length : 0) + " checkpoints  ·  road under " + GroundCover(line) + "% of it";
            if (Step("5  Race line", line != null, lineNote))
            {
                using (new EditorGUI.DisabledScope(!RouteReady || info == null))
                    if (GUILayout.Button(line == null ? "Build race line" : "Rebuild race line")) BuildRaceLine();
            }

            // 6. Player car
            if (Step("6  Player car", HasCar, HasCar ? "A car is in the scene. On race day the garage's car takes its place on the grid." : "A car on the grid. The rivals join automatically when the race starts."))
            {
                using (new EditorGUI.DisabledScope(line == null))
                    if (GUILayout.Button(HasCar ? "Move the car to its grid slot" : "Place the player car on the grid")) PlaceCar();
            }

            // 7. Extras
            bool walls = GameObject.Find("Track Walls") != null;
            bool ramps = Object.FindObjectsByType<ShiftRamp>(FindObjectsSortMode.None).Length > 0;
            if (Step("7  Extras (optional)", true, "Walls keep cars on the circuit. SHIFT ramps open the challenge worlds."))
            {
                using (new EditorGUI.DisabledScope(line == null || info == null))
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Track walls", walls ? "On" : "Off");
                    if (GUILayout.Button(walls ? "Rebuild" : "Add", GUILayout.Width(80))) { TrackBuilder.AddTrackWalls(); MarkDirty(); }
                    if (walls && GUILayout.Button("Remove", GUILayout.Width(80))) { Object.DestroyImmediate(GameObject.Find("Track Walls")); MarkDirty(); }
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("SHIFT ramps", ramps ? "On" : "Off");
                    if (GUILayout.Button(ramps ? "Rebuild" : "Add", GUILayout.Width(80))) AddRamps();
                    if (ramps && GUILayout.Button("Remove", GUILayout.Width(80))) RemoveRamps();
                    EditorGUILayout.EndHorizontal();
                }
            }

            // 8. Menu
            string menuNote = !saved ? "Needs a saved scene." : (InCatalog ? "In the track select" : "Not in the track select yet") + "  ·  " + (InBuild ? "in the build" : "not in the build");
            if (Step("8  Track select and build", InCatalog && InBuild, menuNote))
            {
                using (new EditorGUI.DisabledScope(!saved || line == null || info == null))
                    if (GUILayout.Button(InCatalog ? "Update track select entry and hero shot" : "Add to track select and build")) Register();
            }

            EditorGUILayout.Space(10);
            using (new EditorGUI.DisabledScope(info == null || !RouteReady))
                if (GUILayout.Button("Run every step", GUILayout.Height(30))) RunAll();
            using (new EditorGUI.DisabledScope(left > 0))
                if (GUILayout.Button("Test race", GUILayout.Height(26))) { EditorSceneManager.SaveOpenScenes(); EditorApplication.isPlaying = true; }
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Changed the road? Rebuild the race line, then the walls and ramps, and update the track select.", small);
            EditorGUILayout.EndScrollView();
        }

        bool Step(string title, bool done, string note)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            var c = GUI.color; GUI.color = done ? Good : Bad;
            EditorGUILayout.LabelField(done ? "●" : "○", GUILayout.Width(16));
            GUI.color = c;
            EditorGUILayout.LabelField(title, step);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(note, small);
            bool open = true;
            EditorGUILayout.EndVertical();
            return open;
        }

        // ------------------------------------------------------------------ steps

        void SaveSceneAs()
        {
            string path = EditorUtility.SaveFilePanelInProject("Save track scene", "Track_NewTrack", "unity", "Where to save the track", ScenesDir);
            if (string.IsNullOrEmpty(path)) return;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), path);
        }

        void CreateInfo()
        {
            var go = new GameObject("Track");
            var info = go.AddComponent<TrackInfo>();
            string n = string.IsNullOrEmpty(SceneName) ? "New Track" : SceneName.Replace("Track_", "");
            info.displayName = System.Text.RegularExpressions.Regex.Replace(n, "([a-z])([A-Z])", "$1 $2");
            info.id = n.ToLowerInvariant().Replace(" ", "");
            info.route = RoadSpline != null ? TrackInfo.RouteSource.RoadSpline : TrackInfo.RouteSource.RoutePoints;
            new GameObject("Route").transform.SetParent(go.transform, false);
            Undo.RegisterCreatedObjectUndo(go, "Start track");
            Selection.activeGameObject = go;
            MarkDirty();
        }

        void DrawInfo(TrackInfo info)
        {
            var so = new SerializedObject(info);
            EditorGUILayout.PropertyField(so.FindProperty("displayName"), new GUIContent("Name"));
            EditorGUILayout.PropertyField(so.FindProperty("id"));
            EditorGUILayout.PropertyField(so.FindProperty("description"));
            EditorGUILayout.PropertyField(so.FindProperty("loadingTips"), true);
            EditorGUILayout.PropertyField(so.FindProperty("laps"));
            EditorGUILayout.PropertyField(so.FindProperty("halfWidth"), new GUIContent("Half width (m)"));
            EditorGUILayout.PropertyField(so.FindProperty("checkpointSpacing"), new GUIContent("Checkpoint every (m)"));
            if (so.ApplyModifiedProperties()) MarkDirty();
        }

        void DrawRoute(TrackInfo info)
        {
            var so = new SerializedObject(info);
            EditorGUILayout.PropertyField(so.FindProperty("route"), new GUIContent("Route from"));
            if (info.route == TrackInfo.RouteSource.RoutePoints)
            {
                EditorGUILayout.PropertyField(so.FindProperty("smoothing"));
                EditorGUILayout.PropertyField(so.FindProperty("snapToGround"));
            }
            if (so.ApplyModifiedProperties()) MarkDirty();
            if (info.route != TrackInfo.RouteSource.RoutePoints) return;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add point at view centre")) AddRoutePoint(info, false);
            using (new EditorGUI.DisabledScope(!(Selection.activeTransform != null && Selection.activeTransform.parent == info.RouteRoot)))
                if (GUILayout.Button("Insert after selected")) AddRoutePoint(info, true);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select route")) Selection.objects = RouteObjects(info);
            if (GUILayout.Button("Reverse direction")) ReverseRoute(info);
            if (GUILayout.Button("Clear")) { if (EditorUtility.DisplayDialog("Clear route", "Delete every route point?", "Delete", "Cancel")) ClearRoute(info); }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Look at the road in the Scene view and press Add: the point lands where the view's centre meets the ground. Drag points to adjust.", small);
        }

        Object[] RouteObjects(TrackInfo info)
        {
            var list = new List<Object>();
            foreach (Transform t in info.RouteRoot) list.Add(t.gameObject);
            return list.ToArray();
        }

        void AddRoutePoint(TrackInfo info, bool afterSelected)
        {
            var root = info.RouteRoot;
            if (root == null) { root = new GameObject("Route").transform; root.SetParent(info.transform, false); }
            Vector3 pos = info.transform.position;
            var sv = SceneView.lastActiveSceneView;
            if (sv != null)
            {
                var ray = new Ray(sv.camera.transform.position, sv.camera.transform.forward);
                if (Physics.Raycast(ray, out var hit, 5000f, ~0, QueryTriggerInteraction.Ignore)) pos = hit.point;
                else pos = sv.pivot;
            }
            var p = new GameObject("Point").transform;
            Undo.RegisterCreatedObjectUndo(p.gameObject, "Add route point");
            p.SetParent(root, true);
            p.position = pos;
            if (afterSelected && Selection.activeTransform != null && Selection.activeTransform.parent == root)
                p.SetSiblingIndex(Selection.activeTransform.GetSiblingIndex() + 1);
            Renumber(root);
            Selection.activeTransform = p;
            MarkDirty();
        }

        void ReverseRoute(TrackInfo info)
        {
            var root = info.RouteRoot;
            if (root == null || root.childCount < 2) return;
            var kids = new List<Transform>();
            foreach (Transform t in root) kids.Add(t);
            // Keep the first point (the start) first, reverse the rest.
            for (int i = 1; i < kids.Count; i++) kids[i].SetSiblingIndex(kids.Count - i);
            Renumber(root);
            MarkDirty();
        }

        void ClearRoute(TrackInfo info)
        {
            var root = info.RouteRoot;
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);
            MarkDirty();
        }

        static void Renumber(Transform root)
        {
            for (int i = 0; i < root.childCount; i++) root.GetChild(i).name = i == 0 ? "Point 01 (start)" : "Point " + (i + 1).ToString("00");
        }

        /// <summary>The route as evenly spaced samples (position + direction), in driving order, as a closed loop.</summary>
        List<TrackBuilder.Sample> SampleRoute(TrackInfo info)
        {
            Physics.SyncTransforms(); // colliders moved or made since the last physics step
            if (info.route == TrackInfo.RouteSource.RoadSpline && RoadSpline != null) return TrackBuilder.SampleSpline(RoadSpline, LineStep);

            var ctrl = new List<Vector3>();
            foreach (Transform t in info.RouteRoot) ctrl.Add(t.position);
            int m = ctrl.Count;
            var dense = new List<Vector3>();
            for (int i = 0; i < m; i++)
            {
                Vector3 p0 = ctrl[(i - 1 + m) % m], p1 = ctrl[i], p2 = ctrl[(i + 1) % m], p3 = ctrl[(i + 2) % m];
                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(p1, p2) / 1f));
                for (int s = 0; s < steps; s++)
                {
                    float t = s / (float)steps;
                    Vector3 curve = 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);
                    dense.Add(Vector3.Lerp(Vector3.Lerp(p1, p2, t), curve, info.smoothing));
                }
            }
            // Resample every LineStep metres along the loop.
            float total = 0f;
            for (int i = 0; i < dense.Count; i++) total += Vector3.Distance(dense[i], dense[(i + 1) % dense.Count]);
            int n = Mathf.Max(8, Mathf.CeilToInt(total / LineStep));
            var samples = new List<TrackBuilder.Sample>();
            int seg = 0; float segStart = 0f;
            for (int k = 0; k < n; k++)
            {
                float target = total * k / n;
                while (true)
                {
                    float len = Vector3.Distance(dense[seg], dense[(seg + 1) % dense.Count]);
                    if (segStart + len >= target || seg >= dense.Count - 1) break;
                    segStart += len; seg++;
                }
                var a = dense[seg]; var b = dense[(seg + 1) % dense.Count];
                float l = Mathf.Max(0.0001f, Vector3.Distance(a, b));
                var pos = Vector3.Lerp(a, b, Mathf.Clamp01((target - segStart) / l));
                if (info.snapToGround) pos = Ground(pos);
                samples.Add(new TrackBuilder.Sample { pos = pos, dist = target, t = k / (float)n });
            }
            for (int k = 0; k < samples.Count; k++)
            {
                var s = samples[k];
                var d = samples[(k + 1) % samples.Count].pos - samples[(k - 1 + samples.Count) % samples.Count].pos;
                s.dir = d.sqrMagnitude > 0f ? d.normalized : Vector3.forward;
                samples[k] = s;
            }
            return samples;
        }

        static Vector3 Ground(Vector3 p)
        {
            float best = float.MinValue;
            foreach (var hit in Physics.RaycastAll(p + Vector3.up * 30f, Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.rigidbody == null && hit.collider.GetComponentInParent<IVehicle>() == null && hit.point.y > best) best = hit.point.y;
            return best > float.MinValue ? new Vector3(p.x, best, p.z) : p;
        }

        void PlaceStartLine()
        {
            var info = Info;
            var road = SampleRoute(info);
            if (road.Count < 2) return;
            var grid = GameObject.Find("Start Grid");
            if (grid == null) { grid = new GameObject("Start Grid"); Undo.RegisterCreatedObjectUndo(grid, "Start grid"); }

            // "StartLine" faces the race direction (the race line and the grid read it), the chequer lies flat under it.
            var marker = StartLine;
            if (marker == null)
            {
                marker = new GameObject("StartLine").transform;
                marker.SetParent(grid.transform, false);
                Undo.RegisterCreatedObjectUndo(marker.gameObject, "Start line");
            }
            else Undo.RecordObject(marker, "Move start line");
            var fwd = road[0].dir; fwd.y = 0f;
            marker.position = road[0].pos;
            marker.rotation = Quaternion.LookRotation(fwd.sqrMagnitude > 0f ? fwd.normalized : Vector3.forward, Vector3.up);

            var chequer = marker.Find("Chequer");
            if (chequer == null)
            {
                chequer = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
                chequer.name = "Chequer";
                Object.DestroyImmediate(chequer.GetComponent<Collider>());
                chequer.SetParent(marker, false);
                chequer.GetComponent<Renderer>().sharedMaterial = CheckerMaterial();
            }
            chequer.localPosition = Vector3.up * 0.03f;
            chequer.localRotation = Quaternion.Euler(90f, 0f, 0f);
            chequer.localScale = new Vector3(info.halfWidth * 2f, 1.6f, 1f);
            Selection.activeTransform = marker;
            MarkDirty();
        }

        void BuildRaceLine()
        {
            var info = Info;
            var road = SampleRoute(info);
            int n = road.Count;
            if (n < 8) { Debug.LogWarning("[Track Wizard] The route is too short."); return; }
            foreach (var old in Object.FindObjectsByType<RaceLine>(FindObjectsSortMode.None)) Undo.DestroyObjectImmediate(old.gameObject);

            Vector3 startPos = road[0].pos, startFwd = road[0].dir;
            var marker = StartLine;
            if (marker != null) { startPos = marker.position; startFwd = marker.forward; }

            int startIndex = 0; float bestD = float.MaxValue;
            for (int i = 0; i < n; i++) { float d = (road[i].pos - startPos).sqrMagnitude; if (d < bestD) { bestD = d; startIndex = i; } }
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
            Undo.RegisterCreatedObjectUndo(go, "Race line");
            var line = go.AddComponent<RaceLine>();
            line.laps = info.laps;
            line.points = pts;
            line.distances = dist;
            line.length = length;
            line.halfWidth = info.halfWidth;

            int gateCount = Mathf.Max(4, Mathf.RoundToInt(length / Mathf.Max(30f, info.checkpointSpacing)));
            var gates = new List<RaceLine.Gate>();
            for (int g = 0; g < gateCount; g++)
            {
                float target = length * g / gateCount;
                int k = 0;
                while (k < n - 1 && dist[k + 1] <= target) k++;
                var fwd = g == 0 ? startFwd : line.Direction(k);
                fwd.y = 0f;
                gates.Add(new RaceLine.Gate { position = g == 0 ? startPos : pts[k], forward = fwd.normalized, halfWidth = info.halfWidth + 4f });
            }
            line.gates = gates.ToArray();
            TrackBuilder.LinkShortcuts(line);
            line.mapSprite = TrackBuilder.DrawMinimap(pts, info.displayName, out var bounds);
            line.mapBounds = bounds;
            MarkDirty();
            Debug.Log("[Track Wizard] Race line on " + info.displayName + ": " + length.ToString("0") + " m, " + gateCount + " checkpoints, " + info.laps + " laps.");
        }

        void PlaceCar()
        {
            var line = Line;
            var cat = Catalog;
            if (line == null || cat == null || cat.cars.Count == 0) return;
            var field = RivalField.Load();
            Vector3 pos; Quaternion rot;
            if (field != null) pos = RaceGrid.Slot(line, field, field.playerGridSlot, out rot, out _);
            else { int i = line.IndexAt(line.length - 30f); pos = line.points[i] + Vector3.up * 0.6f; rot = Quaternion.LookRotation(line.Direction(i), Vector3.up); }

            GameObject car = null;
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)) if (mb is IVehicle) { car = mb.gameObject; break; }
            if (car == null)
            {
                car = (GameObject)PrefabUtility.InstantiatePrefab(cat.cars[0].prefab);
                Undo.RegisterCreatedObjectUndo(car, "Place car");
            }
            else Undo.RecordObject(car.transform, "Move car");
            car.transform.SetPositionAndRotation(pos, rot);
            // RCC's session (input, chase camera, physics settings), as on every built track.
            if (Object.FindFirstObjectByType<RCCPSessionSetup>() == null)
            {
                var setup = new GameObject("RCCP Session Setup");
                setup.AddComponent<RCCPSessionSetup>();
                Undo.RegisterCreatedObjectUndo(setup, "RCCP session");
            }
            MarkDirty();
        }

        void AddRamps()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ShiftKit.RampPath) == null) ShiftKit.Build();
            TrackBuilder.AddShiftRamps(new TrackBuilder.Spec { name = Info.displayName, scene = ScenePath });
            MarkDirty();
        }

        void RemoveRamps()
        {
            var holder = GameObject.Find("SHIFT Ramps");
            if (holder != null) Undo.DestroyObjectImmediate(holder);
            foreach (var r in Object.FindObjectsByType<ShiftRamp>(FindObjectsSortMode.None)) Undo.DestroyObjectImmediate(r.gameObject);
            MarkDirty();
        }

        void Register()
        {
            var info = Info; var line = Line; var cat = Catalog;
            if (info == null || line == null || cat == null) return;
            EditorSceneManager.SaveOpenScenes();
            var hero = HeroShot(line, info);
            int i = cat.tracks.FindIndex(t => t.scene == SceneName || t.id == info.id);
            var entry = i >= 0 ? cat.tracks[i] : new RaceCatalog.Track();
            entry.id = info.id;
            entry.displayName = info.displayName;
            entry.scene = SceneName;
            entry.description = info.description;
            entry.lengthKm = Mathf.Round(line.length / 100f) / 10f;
            if (hero != null) entry.thumbnail = hero;
            entry.map = line.mapSprite;
            entry.loadingTips = info.loadingTips;
            if (i < 0) cat.tracks.Add(entry);
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[Track Wizard] " + info.displayName + " is in the track select and the build.");
        }

        /// <summary>A 16:9 shot down the track from a fifth of the way round, for the track select card.</summary>
        Sprite HeroShot(RaceLine line, TrackInfo info)
        {
            int i = line.IndexAt(line.length * 0.2f);
            int j = line.IndexAt(line.length * 0.2f + 40f);
            var camGo = new GameObject("Hero Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.transform.position = line.points[i] - line.Direction(i) * 14f + line.Right(i) * 6f + Vector3.up * 4f;
            cam.transform.LookAt(line.points[j] + Vector3.up * 1f);
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
            Directory.CreateDirectory(ArtDir);
            string path = ArtDir + "T_Hero_" + info.id + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        void RunAll()
        {
            var info = Info;
            if (StartLine == null) PlaceStartLine();
            BuildRaceLine();
            PlaceCar();
            if (info.trackWalls) TrackBuilder.AddTrackWalls();
            if (info.shiftRamps) AddRamps(); else RemoveRamps();
            if (!string.IsNullOrEmpty(ScenePath)) Register();
            MarkDirty();
        }

        // ------------------------------------------------------------------ helpers

        static int GroundCover(RaceLine line)
        {
            if (line.points == null || line.points.Length == 0) return 0;
            int hits = 0, tested = 0;
            for (int i = 0; i < line.points.Length; i += 10)
            {
                tested++;
                if (Physics.Raycast(line.points[i] + Vector3.up * 2f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore)) hits++;
            }
            return Mathf.RoundToInt(100f * hits / Mathf.Max(1, tested));
        }

        static Material CheckerMaterial()
        {
            const string matPath = "Assets/_ShiftRush/Art/Track/M_StartChecker.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m != null) return m;
            Directory.CreateDirectory(ArtDir);
            const string texPath = "Assets/_ShiftRush/Art/Track/T_StartChecker.png";
            var tex = new Texture2D(256, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 256; x++)
                    tex.SetPixel(x, y, ((x / 16) + (y / 16)) % 2 == 0 ? Color.white : new Color(0.06f, 0.06f, 0.08f));
            tex.Apply();
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
            imp.filterMode = FilterMode.Point;
            imp.SaveAndReimport();
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            m.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(m, matPath);
            return m;
        }

        static void MarkDirty() { EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene()); }
    }
}
