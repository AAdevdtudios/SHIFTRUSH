using System.Collections.Generic;
using ShiftRush.Lab;
using ShiftRush.Vehicles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// Builds the MovesLab greybox for tuning Grip Pro's moves (dodge, spin, wall grip). Every piece has one job:
    ///  - Front straight: lane marks 4.5 m apart, the dodge ruler.
    ///  - Knock pad (inside the oval): five parked targets for the spin.
    ///  - Back straight, outer side: a pink quarter pipe that curves into a 14 m wall, the wall-ride test.
    ///  - Back straight, inner side: a flat pink wall to latch onto with a sideways dodge.
    ///  - Two cruisers lap the oval as moving targets.
    /// Menu: SHIFT RUSH/Grip Pro/Build MovesLab. Rebuilding overwrites the scene.
    /// </summary>
    public static class MovesLabBuilder
    {
        const string Root = "Assets/_ShiftRush";
        const string MeshDir = Root + "/Art/Greybox";
        const string MatDir = Root + "/Art/Materials";
        const string CarDir = Root + "/Prefabs/Vehicles/";
        public const string ScenePath = Root + "/Scenes/MovesLab.unity";

        const float Straight = 320f, Radius = 70f, RoadWidth = 24f;
        const float PipeRadius = 8f, PipeHeight = 14f, PipeHalfLength = 120f;
        const float LatchWallHalfLength = 80f, LatchWallHeight = 9f;

        [MenuItem("SHIFT RUSH/Grip Pro/Build MovesLab")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder(Root + "/Art", "Greybox");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var cam = GameObject.Find("Main Camera");
            if (cam != null) Object.DestroyImmediate(cam);
            var sun = GameObject.Find("Directional Light");
            if (sun != null) sun.transform.rotation = Quaternion.Euler(48f, -30f, 0f);

            var matGround = Mat("ML_Ground", new Color(0.33f, 0.4f, 0.36f), Color.black);
            var matRoad = Mat("ML_Road", new Color(0.16f, 0.16f, 0.19f), Color.black);
            var matLine = Mat("ML_Line", new Color(0.95f, 0.95f, 0.9f), Color.black);
            var matPad = Mat("ML_KnockPad", new Color(0.22f, 0.2f, 0.27f), Color.black);
            var matGrip = Mat("ML_Grip", new Color(0.55f, 0.12f, 0.42f), new Color(1f, 0.2f, 0.7f) * 0.35f);

            var lab = new GameObject("MovesLab");

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(lab.transform);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(900f, 1f, 900f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = matGround;

            // Oval road
            var path = Oval();
            MeshObject("Road", SaveMesh(Ribbon(path, RoadWidth, 0.03f), "ML_Road"), matRoad, lab.transform, true);
            MeshObject("EdgeLeft", SaveMesh(Ribbon(Offset(path, -(RoadWidth * 0.5f - 0.5f)), 0.35f, 0.045f), "ML_EdgeL"), matLine, lab.transform, false);
            MeshObject("EdgeRight", SaveMesh(Ribbon(Offset(path, RoadWidth * 0.5f - 0.5f), 0.35f, 0.045f), "ML_EdgeR"), matLine, lab.transform, false);

            // Dodge ruler: lane marks every 4.5 m across the front straight (z = -Radius, driven toward +x).
            var ruler = new GameObject("DodgeRuler");
            ruler.transform.SetParent(lab.transform);
            for (int k = -2; k <= 2; k++)
            {
                float z = -Radius + k * 4.5f;
                var mark = new List<Vector3> { new Vector3(-Straight * 0.5f, 0f, z), new Vector3(Straight * 0.5f, 0f, z) };
                MeshObject("Lane " + k, SaveMesh(Strip(mark, k == 0 ? 0.3f : 0.18f, 0.05f), "ML_Lane" + (k + 2)), matLine, ruler.transform, false);
            }

            // Knock pad with five parked targets, staggered, inside the oval near the front straight.
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = "KnockPad";
            pad.transform.SetParent(lab.transform);
            pad.transform.position = new Vector3(0f, -0.48f, -28f);
            pad.transform.localScale = new Vector3(180f, 1f, 26f);
            pad.GetComponent<MeshRenderer>().sharedMaterial = matPad;
            var spots = new List<Transform>();
            for (int i = 0; i < 5; i++)
            {
                var s = new GameObject("Target Spot " + (i + 1)).transform;
                s.SetParent(pad.transform.parent);
                s.position = new Vector3(-60f + i * 30f, 0.7f, -28f + (i % 2 == 0 ? -3.5f : 3.5f));
                s.rotation = Quaternion.Euler(0f, 90f, 0f);
                spots.Add(s);
            }

            // Back straight (z = +Radius, driven toward -x). Its right side is the outside of the oval.
            float outerEdge = Radius + RoadWidth * 0.5f;
            var pipe = MeshObject("GripPipe", SaveMesh(QuarterPipe(outerEdge), "ML_GripPipe"), matGrip, lab.transform, true);
            pipe.AddComponent<GripSurface>();

            var latch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            latch.name = "LatchWall";
            latch.transform.SetParent(lab.transform);
            latch.transform.position = new Vector3(0f, LatchWallHeight * 0.5f, Radius - RoadWidth * 0.5f - 1f);
            latch.transform.localScale = new Vector3(LatchWallHalfLength * 2f, LatchWallHeight, 1f);
            latch.GetComponent<MeshRenderer>().sharedMaterial = matGrip;
            latch.AddComponent<GripSurface>();

            // RCC Pro rig, as in TestTrack.
            new GameObject("RCCP_SceneManager").AddComponent<RCCP_SceneManager>();
            new GameObject("RCCP_SessionSetup").AddComponent<RCCPSessionSetup>();
            var camPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Realistic Car Controller Pro/Prefabs/Cameras/RCCP_Camera.prefab");
            if (camPrefab != null)
            {
                // Same chase camera as the race tracks: speed feel (FOV, speed lines, shake) and the landing jolt.
                var camGo = (GameObject)PrefabUtility.InstantiatePrefab(camPrefab);
                var feel = camGo.AddComponent<ShiftRush.CameraFX.SpeedFeel>();
                var so = new SerializedObject(feel);
                so.FindProperty("lineMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/M_SpeedLine.mat");
                so.ApplyModifiedPropertiesWithoutUndo();
                camGo.AddComponent<RCCPSpeedCamera>();
            }

            var zephyr = AssetDatabase.LoadAssetAtPath<GameObject>(CarDir + "Car_Zephyr.prefab");
            var car = (GameObject)PrefabUtility.InstantiatePrefab(zephyr);
            car.transform.position = new Vector3(-Straight * 0.5f + 20f, 0.7f, -Radius);
            car.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            if (car.GetComponent<GripMoves>() == null) car.AddComponent<GripMoves>(); // the Car Setup wizard normally has it on the prefab

            var labRunner = lab.AddComponent<MovesLab>();
            labRunner.parkedPrefabs = new[] { Car("Fireburst"), Car("Phobos"), Car("Predator"), Car("Tetsuo"), Car("Blackout") };
            labRunner.parkedSpots = spots.ToArray();
            labRunner.cruisers = new[]
            {
                new MovesLab.Cruiser { prefab = Car("Blackout"), lane = -5f, kmh = 70f },
                new MovesLab.Cruiser { prefab = Car("Tetsuo"), lane = 2f, kmh = 85f },
            };
            labRunner.cruisePath = path.ToArray();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[GRIP PRO] MovesLab built at " + ScenePath);
        }

        static GameObject Car(string name) { return AssetDatabase.LoadAssetAtPath<GameObject>(CarDir + "Car_" + name + ".prefab"); }

        // ---------- geometry ----------

        static List<Vector3> Oval()
        {
            var pts = new List<Vector3>();
            float half = Straight * 0.5f;
            const int straightSegs = 32, turnSegs = 40;
            for (int i = 0; i < straightSegs; i++) pts.Add(new Vector3(Mathf.Lerp(-half, half, i / (float)straightSegs), 0f, -Radius));
            for (int i = 0; i < turnSegs; i++)
            {
                float a = (-90f + 180f * i / turnSegs) * Mathf.Deg2Rad;
                pts.Add(new Vector3(half + Mathf.Cos(a) * Radius, 0f, Mathf.Sin(a) * Radius));
            }
            for (int i = 0; i < straightSegs; i++) pts.Add(new Vector3(Mathf.Lerp(half, -half, i / (float)straightSegs), 0f, Radius));
            for (int i = 0; i < turnSegs; i++)
            {
                float a = (90f + 180f * i / turnSegs) * Mathf.Deg2Rad;
                pts.Add(new Vector3(-half + Mathf.Cos(a) * Radius, 0f, Mathf.Sin(a) * Radius));
            }
            return pts;
        }

        /// <summary>Right-hand side of the path (Cross(up, forward)).</summary>
        static Vector3 SideAt(List<Vector3> p, int i, bool closed)
        {
            int n = p.Count;
            Vector3 a = closed ? p[(i - 1 + n) % n] : p[Mathf.Max(0, i - 1)];
            Vector3 b = closed ? p[(i + 1) % n] : p[Mathf.Min(n - 1, i + 1)];
            return Vector3.Cross(Vector3.up, (b - a).normalized).normalized;
        }

        static List<Vector3> Offset(List<Vector3> p, float d)
        {
            var o = new List<Vector3>(p.Count);
            for (int i = 0; i < p.Count; i++) o.Add(p[i] + SideAt(p, i, true) * d);
            return o;
        }

        static Mesh Ribbon(List<Vector3> p, float width, float y) { return Band(p, width, y, true); }
        static Mesh Strip(List<Vector3> p, float width, float y) { return Band(p, width, y, false); }

        static Mesh Band(List<Vector3> p, float width, float y, bool closed)
        {
            int n = p.Count, count = closed ? n + 1 : n;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            float dist = 0f;
            for (int i = 0; i < count; i++)
            {
                int k = i % n;
                if (i > 0) dist += Vector3.Distance(p[(i - 1) % n], p[k]);
                Vector3 s = SideAt(p, k, closed) * width * 0.5f;
                v.Add(p[k] - s + Vector3.up * y); v.Add(p[k] + s + Vector3.up * y);
                uv.Add(new Vector2(0f, dist / width)); uv.Add(new Vector2(1f, dist / width));
            }
            for (int i = 0; i < count - 1; i++)
            {
                int a = i * 2;
                t.Add(a); t.Add(a + 2); t.Add(a + 1);
                t.Add(a + 1); t.Add(a + 2); t.Add(a + 3);
            }
            var m = new Mesh { name = "Band" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds(); m.RecalculateTangents();
            return m;
        }

        /// <summary>
        /// Quarter pipe along x on the outside of the back straight: starts flush with the road edge at z0, curves
        /// up through a radius into a vertical wall. Faces point at the road.
        /// </summary>
        static Mesh QuarterPipe(float z0)
        {
            var profile = new List<Vector2>(); // (z, y)
            profile.Add(new Vector2(z0 - 0.5f, 0.02f));
            const int arcSegs = 16;
            for (int j = 0; j <= arcSegs; j++)
            {
                float a = (-90f + 90f * j / arcSegs) * Mathf.Deg2Rad;
                profile.Add(new Vector2(z0 + Mathf.Cos(a) * PipeRadius, PipeRadius + Mathf.Sin(a) * PipeRadius + 0.02f));
            }
            profile.Add(new Vector2(z0 + PipeRadius, PipeHeight));

            const int lengthSegs = 48;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            int cols = profile.Count;
            for (int i = 0; i <= lengthSegs; i++)
            {
                float x = Mathf.Lerp(-PipeHalfLength, PipeHalfLength, i / (float)lengthSegs);
                float run = 0f;
                for (int j = 0; j < cols; j++)
                {
                    if (j > 0) run += Vector2.Distance(profile[j - 1], profile[j]);
                    v.Add(new Vector3(x, profile[j].y, profile[j].x));
                    uv.Add(new Vector2(run / 4f, x / 4f));
                }
            }
            for (int i = 0; i < lengthSegs; i++)
                for (int j = 0; j < cols - 1; j++)
                {
                    int a = i * cols + j, b = a + 1, c = a + cols, d = c + 1;
                    // (profile step) x (+x) points into the pipe: up on the floor, toward the road on the wall.
                    t.Add(a); t.Add(b); t.Add(c);
                    t.Add(b); t.Add(d); t.Add(c);
                }
            var m = new Mesh { name = "QuarterPipe" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds(); m.RecalculateTangents();
            return m;
        }

        // ---------- helpers ----------

        static GameObject MeshObject(string name, Mesh mesh, Material mat, Transform parent, bool collide)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collide) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        static Mesh SaveMesh(Mesh m, string name)
        {
            string p = MeshDir + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(p);
            if (existing != null) { EditorUtility.CopySerialized(m, existing); return existing; }
            AssetDatabase.CreateAsset(m, p);
            return m;
        }

        static Material Mat(string name, Color baseColor, Color emission)
        {
            string p = MatDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, p);
            }
            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Smoothness", 0.15f);
            if (emission.maxColorComponent > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
