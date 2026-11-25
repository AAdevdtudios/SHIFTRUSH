using System.Collections.Generic;
using ShiftRush.Core;
using ShiftRush.Race;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// County Line: the big countryside circuit, about 6 km a lap so a full field has room. Farm roads along the
    /// valley floor, a bridge over the river, a climb past the lake onto the ridge, a winding descent through the
    /// woods and a fast run back past the village. Built with the same spline track builder as the other tracks.
    /// </summary>
    public static partial class TrackBuilder
    {
        const string CountryScene = Root + "Scenes/Track_CountyLine.unity";
        const string CountryId = "countyline";

        public static Spec CountyLine()
        {
            return new Spec
            {
                name = "County Line",
                scene = CountryScene,
                snowy = false,
                seed = 61,
                kroma = "Kroma_Profile_32_CozyFarming",
                targetLength = 6000f,
                lakeSketch = new Vector2(410f, 240f),
                lakeRadiusSketch = 44f,
                riverSketch = new[]
                {
                    new Vector2(405f, 285f), new Vector2(392f, 330f), new Vector2(378f, 375f),
                    new Vector2(368f, 420f), new Vector2(360f, 470f),
                },
                villageSketch = new[]
                {
                    new Vector2(200f, 378f), new Vector2(232f, 388f), new Vector2(262f, 380f),
                    new Vector2(216f, 362f), new Vector2(248f, 366f),
                },
                // Valley floor east (the long start straight), up the east side to the ridge, back west along the
                // top, then an S down the middle through the woods and home past the village.
                sketch = new[]
                {
                    new Vector3(80, 0, 420), new Vector3(170, 0, 428), new Vector3(260, 1, 425), new Vector3(350, 3, 415),
                    new Vector3(430, 6, 395), new Vector3(500, 11, 360), new Vector3(545, 20, 310), new Vector3(560, 28, 250),
                    new Vector3(548, 36, 190), new Vector3(515, 45, 140), new Vector3(470, 53, 105), new Vector3(410, 59, 85),
                    new Vector3(345, 62, 80), new Vector3(285, 59, 92), new Vector3(245, 53, 120), new Vector3(230, 46, 160),
                    new Vector3(240, 39, 200), new Vector3(270, 32, 232), new Vector3(300, 25, 265), new Vector3(305, 18, 305),
                    new Vector3(280, 13, 335), new Vector3(235, 8, 345), new Vector3(190, 6, 330), new Vector3(150, 4, 330),
                    new Vector3(115, 3, 350), new Vector3(88, 1, 385),
                },
            };
        }

        [MenuItem("SHIFT RUSH/Tracks/Build County Line")]
        public static void BuildCountyLine()
        {
            Build(CountyLine());
            var line = Object.FindFirstObjectByType<RaceLine>();
            var catalog = AssetDatabase.LoadAssetAtPath<RaceCatalog>(Root + "Resources/RaceCatalog.asset");
            if (catalog != null && line != null)
            {
                var track = new RaceCatalog.Track
                {
                    id = CountryId, displayName = "County Line", scene = "Track_CountyLine",
                    description = "Six kilometres of countryside: farm straights, the river bridge, the lake climb and a ridge-top run.",
                    lengthKm = Mathf.Round(line.length / 100f) / 10f, map = line.mapSprite,
                    loadingTips = new[]
                    {
                        "The valley straight is the longest in the game. Save nitro for it.",
                        "The ridge road narrows past the lake. Brake before the crest.",
                        "The woods descent tightens near the bottom.",
                    },
                };
                int i = catalog.tracks.FindIndex(t => t.id == CountryId || t.scene == track.scene);
                if (i >= 0) { track.thumbnail = catalog.tracks[i].thumbnail; catalog.tracks[i] = track; } else catalog.tracks.Add(track);
                EditorUtility.SetDirty(catalog);
            }
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(sc => sc.path == CountryScene)) scenes.Add(new EditorBuildSettingsScene(CountryScene, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AddCountryDestructibles();
            CaptureHero(CountryId, 0.12f);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Farm-road wreckage: crates, barrels, sacks and cones in clusters on both verges all the way round, breakable
        /// fence runs on the outside of the corners, and three smash-through walls of crates and sacks with gaps.
        /// </summary>
        static void AddCountryDestructibles()
        {
            var old = GameObject.Find("Track Destructibles");
            if (old != null) Object.DestroyImmediate(old);
            var line = Object.FindFirstObjectByType<RaceLine>();
            if (line == null || line.points == null || line.points.Length < 2) return;
            var root = new GameObject("Track Destructibles").transform;
            System.Func<string, GameObject> P = path => AssetDatabase.LoadAssetAtPath<GameObject>(path);
            const string Kroma = "Assets/FayLabs/Kroma/Scenes/Assets/Prefabs/";
            const string Gen = "Assets/Synty/PolygonGeneric/Prefabs/";
            var crate = P(Kroma + "Crate_Wooden.prefab"); var barrel = P(Kroma + "Barrel.prefab");
            var fence = P(Kroma + "Prop_WoodenFence_Single.prefab");
            var cone = P("Assets/ARCADE - Ultimate Vehicles Pack/Environment/Assets/Prefabs/Parking Zone/Cone.prefab");
            GameObject sacks = null, woodBarrel = null, genCrate = null;
            foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { Gen }))
            {
                var path = AssetDatabase.GUIDToAssetPath(g); var n = System.IO.Path.GetFileNameWithoutExtension(path);
                if (n == "SM_Gen_Prop_Sack_Stack_01") sacks = P(path);
                if (n == "SM_Gen_Prop_Barrel_Wood_01") woodBarrel = P(path);
                if (n == "SM_Gen_Prop_Crate_02") genCrate = P(path);
            }
            var kit = new List<GameObject>();
            foreach (var g in new[] { crate, barrel, cone, sacks, woodBarrel, genCrate }) if (g != null) kit.Add(g);
            if (kit.Count == 0) return;
            var rng = new System.Random(61);

            // Verge clusters every ~110 m, alternating sides.
            int clusters = Mathf.FloorToInt((line.length - 300f) / 110f);
            for (int c = 0; c < clusters; c++)
            {
                float d = 150f + c * 110f;
                int i = line.IndexAt(d);
                float side = c % 2 == 0 ? 1f : -1f;
                int items = 3 + rng.Next(3);
                for (int k = 0; k < items; k++)
                {
                    var prefab = kit[rng.Next(kit.Count)];
                    Vector3 at = line.PointAt(d + k * 3f) + line.Right(i) * side * (line.halfWidth - 1.5f + (float)rng.NextDouble() * 2f);
                    MakeBreakable(prefab, root, at, Quaternion.Euler(0f, rng.Next(360), 0f), prefab == cone ? 0.6f : 4f, 2f);
                }
            }

            // Fence runs on the outside of the tighter corners.
            if (fence != null)
                for (int i = 8; i < line.points.Length - 8; i += 4)
                {
                    float turn = Vector3.SignedAngle(line.Direction(i - 6), line.Direction(i + 6), Vector3.up);
                    if (Mathf.Abs(turn) < 35f) continue;
                    float side = turn > 0 ? -1f : 1f;
                    for (int k = -3; k <= 3; k++)
                    {
                        int j = Mathf.Clamp(i + k, 0, line.points.Length - 1);
                        Vector3 at = line.points[j] + line.Right(j) * side * (line.halfWidth + 0.5f);
                        MakeBreakable(fence, root, at, Quaternion.LookRotation(line.Direction(j)), 3f, 2f);
                    }
                    i += 20;
                }

            // Three smash-through walls with gaps.
            foreach (float frac in new[] { 0.18f, 0.52f, 0.83f })
            {
                float d = line.length * frac;
                int i = line.IndexAt(d);
                for (int k = -5; k <= 5; k++)
                {
                    if (k == -2 || k == 1 || k == 4) continue;
                    var prefab = k % 2 == 0 ? (genCrate ?? crate) : (sacks ?? barrel);
                    if (prefab == null) continue;
                    Vector3 at = line.PointAt(d) + line.Right(i) * k * 1.9f + Vector3.up * 0.1f;
                    MakeBreakable(prefab, root, at, Quaternion.LookRotation(line.Direction(i)), 5f, 2f);
                }
            }
        }

        /// <summary>A track select picture from a spot on the race line (fraction of the lap).</summary>
        public static void CaptureHero(string id, float along)
        {
            var line = Object.FindFirstObjectByType<RaceLine>();
            if (line == null || line.points == null || line.points.Length < 2) return;
            int index = Mathf.Clamp(Mathf.RoundToInt(line.points.Length * along), 0, line.points.Length - 1);
            Vector3 point = line.points[index], direction = line.Direction(index), right = line.Right(index);
            var go = new GameObject("Hero Camera");
            var cam = go.AddComponent<Camera>();
            go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            cam.transform.position = point - direction * 24f + right * 10f + Vector3.up * 6f;
            cam.transform.LookAt(point + direction * 40f + Vector3.up * 1.5f);
            cam.fieldOfView = 52f; cam.farClipPlane = 6000f;
            const int width = 1600, height = 900;
            var rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply();
            RenderTexture.active = null;
            Object.DestroyImmediate(go); rt.Release();
            string path = Root + "Art/UI/T_Hero_" + id + ".png";
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single; imp.mipmapEnabled = false;
            imp.SaveAndReimport();
            var catalog = AssetDatabase.LoadAssetAtPath<RaceCatalog>(Root + "Resources/RaceCatalog.asset");
            var track = catalog != null ? catalog.tracks.Find(t => t.id == id) : null;
            if (track != null) { track.thumbnail = AssetDatabase.LoadAssetAtPath<Sprite>(path); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); }
        }
    }
}
