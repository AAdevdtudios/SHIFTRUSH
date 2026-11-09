using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// Builds the Milestone 1 greybox test circuit: an oval road ribbon, ground, one normal ramp
    /// and one SHIFT ramp. Meshes are generated and saved as assets so the scene is reproducible.
    /// Menu: SHIFT RUSH/Build Greybox Test Track.
    /// </summary>
    public static class GreyboxTrackBuilder
    {
        const string Root = "Assets/_ShiftRush";
        const string MeshDir = Root + "/Art/Greybox";
        const string MatDir = Root + "/Art/Materials";
        public const string ScenePath = Root + "/Scenes/TestTrack.unity";

        // Oval shape: two straights joined by half circles.
        const float StraightLength = 220f;
        const float TurnRadius = 70f;
        const float RoadWidth = 18f;
        const int TurnSegments = 40;

        [MenuItem("SHIFT RUSH/Build Greybox Test Track")]
        public static void Build()
        {
            EnsureFolder(Root, "Art"); EnsureFolder(Root + "/Art", "Greybox"); EnsureFolder(Root + "/Art", "Materials");
            EnsureFolder(Root, "Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var defaultCam = GameObject.Find("Main Camera");
            if (defaultCam != null) Object.DestroyImmediate(defaultCam);

            var sun = GameObject.Find("Directional Light");
            if (sun != null) sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);

            var matGround = Mat("GB_Ground", new Color(0.36f, 0.42f, 0.38f), Color.black);
            var matRoad = Mat("GB_Road", new Color(0.17f, 0.17f, 0.19f), Color.black);
            var matLine = Mat("GB_Line", new Color(0.95f, 0.95f, 0.9f), Color.black);
            var matRamp = Mat("GB_Ramp", new Color(0.55f, 0.55f, 0.58f), Color.black);
            var matShift = Mat("GB_ShiftRamp", new Color(0.12f, 0.05f, 0.2f), new Color(1.0f, 0.15f, 0.8f) * 2.2f);

            var track = new GameObject("TestTrack");

            // Ground
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(track.transform);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(800f, 1f, 800f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = matGround;

            // Road ribbon + centre line
            var path = OvalPath();
            var road = MeshObject("Road", SaveMesh(Ribbon(path, RoadWidth, 0.03f, true), "GB_Road"), matRoad, track.transform, true);
            MeshObject("CentreLine", SaveMesh(Ribbon(path, 0.35f, 0.045f, true), "GB_CentreLine"), matLine, track.transform, false);
            MeshObject("EdgeLeft", SaveMesh(Ribbon(Offset(path, RoadWidth * 0.5f - 0.6f), 0.4f, 0.045f, true), "GB_EdgeL"), matLine, track.transform, false);
            MeshObject("EdgeRight", SaveMesh(Ribbon(Offset(path, -(RoadWidth * 0.5f - 0.6f)), 0.4f, 0.045f, true), "GB_EdgeR"), matLine, track.transform, false);

            // Ramps sit on the bottom straight (z = -TurnRadius), driven along +x.
            var rampMesh = SaveMesh(Wedge(10f, 2.6f, 16f), "GB_RampWedge");
            var normalRamp = MeshObject("Ramp_Normal", rampMesh, matRamp, track.transform, true);
            normalRamp.transform.position = new Vector3(-60f, 0.03f, -TurnRadius);
            normalRamp.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            var shiftMesh = SaveMesh(Wedge(12f, 3.4f, 18f), "GB_ShiftWedge");
            var shiftRamp = MeshObject("Ramp_SHIFT", shiftMesh, matShift, track.transform, true);
            shiftRamp.transform.position = new Vector3(40f, 0.03f, TurnRadius);
            shiftRamp.transform.rotation = Quaternion.Euler(0f, -90f, 0f);

            // Spawn point: start of the bottom straight, facing +x.
            var spawn = new GameObject("PlayerSpawn");
            spawn.transform.SetParent(track.transform);
            spawn.transform.position = new Vector3(-StraightLength * 0.5f + 10f, 0.6f, -TurnRadius);
            spawn.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[SHIFT//RUSH] Greybox test track built at " + ScenePath);
        }

        // ---------- geometry ----------

        static List<Vector3> OvalPath()
        {
            var pts = new List<Vector3>();
            float half = StraightLength * 0.5f;
            int straightSegs = 20;
            // bottom straight, -x -> +x at z = -R
            for (int i = 0; i < straightSegs; i++) pts.Add(new Vector3(Mathf.Lerp(-half, half, i / (float)straightSegs), 0f, -TurnRadius));
            // right turn around (half, 0)
            for (int i = 0; i < TurnSegments; i++)
            {
                float a = -90f + 180f * i / TurnSegments;
                pts.Add(new Vector3(half + Mathf.Cos(a * Mathf.Deg2Rad) * TurnRadius, 0f, Mathf.Sin(a * Mathf.Deg2Rad) * TurnRadius));
            }
            // top straight, +x -> -x at z = +R
            for (int i = 0; i < straightSegs; i++) pts.Add(new Vector3(Mathf.Lerp(half, -half, i / (float)straightSegs), 0f, TurnRadius));
            // left turn around (-half, 0)
            for (int i = 0; i < TurnSegments; i++)
            {
                float a = 90f + 180f * i / TurnSegments;
                pts.Add(new Vector3(-half + Mathf.Cos(a * Mathf.Deg2Rad) * TurnRadius, 0f, Mathf.Sin(a * Mathf.Deg2Rad) * TurnRadius));
            }
            return pts;
        }

        static Vector3 SideAt(List<Vector3> p, int i)
        {
            int n = p.Count;
            Vector3 fwd = (p[(i + 1) % n] - p[(i - 1 + n) % n]).normalized;
            return Vector3.Cross(Vector3.up, fwd).normalized;
        }

        static List<Vector3> Offset(List<Vector3> p, float d)
        {
            var o = new List<Vector3>(p.Count);
            for (int i = 0; i < p.Count; i++) o.Add(p[i] + SideAt(p, i) * d);
            return o;
        }

        static Mesh Ribbon(List<Vector3> p, float width, float y, bool closed)
        {
            int n = p.Count;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            float dist = 0f;
            for (int i = 0; i <= n; i++)
            {
                int k = i % n;
                if (i > 0) dist += Vector3.Distance(p[(i - 1) % n], p[k]);
                Vector3 s = SideAt(p, k) * width * 0.5f;
                v.Add(p[k] - s + Vector3.up * y); v.Add(p[k] + s + Vector3.up * y);
                uv.Add(new Vector2(0f, dist / width)); uv.Add(new Vector2(1f, dist / width));
            }
            for (int i = 0; i < n; i++)
            {
                int a = i * 2;
                t.Add(a); t.Add(a + 2); t.Add(a + 1);
                t.Add(a + 1); t.Add(a + 2); t.Add(a + 3);
            }
            var m = new Mesh { name = "Ribbon" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>Wedge ramp: rises along +z from 0 to height over length, centred on x.</summary>
        static Mesh Wedge(float width, float height, float length)
        {
            float w = width * 0.5f;
            Vector3 bl = new Vector3(-w, 0, 0), br = new Vector3(w, 0, 0);
            Vector3 fl = new Vector3(-w, 0, length), fr = new Vector3(w, 0, length);
            Vector3 tl = new Vector3(-w, height, length), tr = new Vector3(w, height, length);
            var v = new List<Vector3>(); var t = new List<int>();
            System.Action<Vector3, Vector3, Vector3, Vector3> quad = (a, b, c, d) =>
            {
                int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
            };
            System.Action<Vector3, Vector3, Vector3> tri = (a, b, c) =>
            {
                int i = v.Count; v.Add(a); v.Add(b); v.Add(c); t.Add(i); t.Add(i + 1); t.Add(i + 2);
            };
            quad(bl, tl, tr, br);   // slope
            quad(fl, fr, tr, tl);   // back face
            tri(bl, fl, tl);        // left side
            tri(br, tr, fr);        // right side
            var m = new Mesh { name = "Wedge" };
            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
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
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
