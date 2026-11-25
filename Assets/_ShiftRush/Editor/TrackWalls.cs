using System.Collections.Generic;
using ShiftRush.Race;
using UnityEditor;
using UnityEngine;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// Invisible walls that follow the race line just outside the guardrails (and both sides of every shortcut
    /// lane), so a car always stays on the circuit. They're frictionless: hit one and you slide along it instead
    /// of stopping dead. Openings are left exactly where a shortcut meets the road.
    /// </summary>
    public static partial class TrackBuilder
    {
        const float WallOffset = SurfaceHalf + 1.4f, WallDown = 3f, WallUp = 4.5f;

        [MenuItem("SHIFT RUSH/Tracks/This Scene: Add Track Walls")]
        static void AddWallsToThisScene()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode first."); return; }
            AddTrackWalls();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }

        public static void AddTrackWalls()
        {
            var old = GameObject.Find("Track Walls");
            if (old != null) Object.DestroyImmediate(old);
            var line = Object.FindFirstObjectByType<RaceLine>();
            if (line == null) { Debug.LogWarning("Track walls need a Race Line."); return; }
            var cuts = Object.FindObjectsByType<ShortcutPath>(FindObjectsSortMode.None);

            var root = new GameObject("Track Walls");
            int propLayer = LayerMask.NameToLayer("RCCP_Prop");
            if (propLayer >= 0) root.layer = propLayer; // the chase camera ignores this layer (see RaceManager)
            var mat = WallMaterial();
            var mesh = new Mesh { name = "Mesh_TrackWalls_" + UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            var verts = new List<Vector3>(); var tris = new List<int>();

            // Main circuit, both edges.
            var pts = line.points; int n = pts.Length;
            foreach (float side in new[] { 1f, -1f })
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    Vector3 a = pts[i] + line.Right(i) * side * WallOffset, b = pts[j] + line.Right(j) * side * WallOffset;
                    Vector3 mid = (a + b) * 0.5f;
                    bool open = false;
                    foreach (var c in cuts) if (c.DistanceTo(mid) < c.halfWidth + 1f) open = true;
                    if (!open) Panel(verts, tris, a, b);
                }

            // Shortcut lanes, both edges, except where they're still on the road.
            foreach (var c in cuts)
            {
                var p = c.points;
                for (int i = 0; i < p.Length - 1; i++)
                {
                    var r0 = Vector3.Cross(Vector3.up, Flat(p[Mathf.Min(i + 1, p.Length - 1)] - p[Mathf.Max(i - 1, 0)]));
                    var r1 = Vector3.Cross(Vector3.up, Flat(p[Mathf.Min(i + 2, p.Length - 1)] - p[i]));
                    foreach (float side in new[] { 1f, -1f })
                    {
                        Vector3 a = p[i] + r0 * side * (c.halfWidth + 1.2f), b = p[i + 1] + r1 * side * (c.halfWidth + 1.2f);
                        Vector3 mid = (a + b) * 0.5f;
                        int k = line.Nearest(mid);
                        if (Mathf.Abs(line.Lateral(mid, k)) < WallOffset + 0.5f) continue;
                        Panel(verts, tris, a, b);
                    }
                }
            }

            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds(); mesh.RecalculateNormals();
            string path = ArtTrack + mesh.name + ".asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            var col = root.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
            col.sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic);
            Debug.Log("[SHIFT//RUSH] Track walls: " + tris.Count / 12 + " panels");
        }

        /// <summary>A vertical panel from a to b, faces both ways so nothing passes from either side.</summary>
        static void Panel(List<Vector3> v, List<int> t, Vector3 a, Vector3 b)
        {
            int s = v.Count;
            v.Add(a + Vector3.down * WallDown); v.Add(b + Vector3.down * WallDown);
            v.Add(a + Vector3.up * WallUp); v.Add(b + Vector3.up * WallUp);
            t.Add(s); t.Add(s + 2); t.Add(s + 1); t.Add(s + 1); t.Add(s + 2); t.Add(s + 3);
            t.Add(s); t.Add(s + 1); t.Add(s + 2); t.Add(s + 1); t.Add(s + 3); t.Add(s + 2);
        }

        public static PhysicsMaterial WallMaterial()
        {
            string path = ArtTrack + "PM_TrackWall.asset";
            var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (m != null) return m;
            m = new PhysicsMaterial("PM_TrackWall")
            {
                dynamicFriction = 0f, staticFriction = 0f, bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum,
            };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
