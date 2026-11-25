using System.Collections.Generic;
using ShiftRush.Core;
using ShiftRush.Race;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShiftRush.EditorTools
{
    /// <summary>Builds the first large off-road circuit: a dirt loop through wet Scottish highlands.</summary>
    public static partial class TrackBuilder
    {
        const string HighlandsScene = Root + "Scenes/Track_StormbreakHighlands.unity";

        public static Spec StormbreakHighlands()
        {
            return new Spec
            {
                name = "Stormbreak Highlands",
                scene = HighlandsScene,
                snowy = false,
                seed = 47,
                kroma = "Kroma_Profile_33_LowPolyMorning",
                targetLength = 3400f,
                lakeSketch = new Vector2(275f, 210f),
                lakeRadiusSketch = 38f,
                riverSketch = new[]
                {
                    new Vector2(258f, 205f), new Vector2(300f, 228f), new Vector2(345f, 252f),
                    new Vector2(390f, 268f), new Vector2(445f, 278f),
                },
                villageSketch = new[]
                {
                    new Vector2(112f, 292f), new Vector2(132f, 307f), new Vector2(150f, 294f),
                    new Vector2(118f, 318f), new Vector2(160f, 315f),
                },
                // A broad, non-intersecting loop with a fast moorland south straight, river crossing,
                // ridge climb, forest switchbacks and a descending quarry-side return.
                sketch = new[]
                {
                    new Vector3(80, 0, 380), new Vector3(160, 0, 385), new Vector3(240, 2, 380),
                    new Vector3(315, 6, 365), new Vector3(375, 12, 330), new Vector3(420, 18, 280),
                    new Vector3(438, 26, 220), new Vector3(425, 34, 165), new Vector3(390, 38, 120),
                    new Vector3(335, 42, 92), new Vector3(275, 45, 80), new Vector3(225, 43, 95),
                    new Vector3(195, 38, 130), new Vector3(178, 32, 170), new Vector3(145, 25, 190),
                    new Vector3(105, 18, 180), new Vector3(72, 10, 145), new Vector3(50, 4, 105),
                    new Vector3(58, 0, 62), new Vector3(96, 4, 40), new Vector3(145, 10, 48),
                    new Vector3(195, 18, 72), new Vector3(230, 26, 110), new Vector3(242, 34, 150),
                    new Vector3(230, 40, 195), new Vector3(200, 44, 235), new Vector3(170, 38, 270),
                    new Vector3(125, 28, 300), new Vector3(78, 16, 330), new Vector3(58, 6, 355),
                },
            };
        }

        [MenuItem("SHIFT RUSH/Tracks/Build Stormbreak Highlands")]
        public static void BuildHighlands()
        {
            var spec = StormbreakHighlands();
            Build(spec);
            ClearHighlandsRoadTerrain();
            ApplyHighlandsSurface();
            AddHighlandsDestructibles();
            UpdateHighlandsCatalog();
            AddHighlandsToBuild();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("[SHIFT//RUSH] Stormbreak Highlands finished: dirt surface, destructibles, catalog and build entry.");
        }

        static void ApplyHighlandsSurface()
        {
            var kit = TrackKit.Load();
            var layer = kit.pineDirt != null ? kit.pineDirt : kit.mud;
            var path = ArtTrack + "M_StormbreakDirt.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            if (layer != null)
            {
                mat.SetTexture("_BaseMap", layer.diffuseTexture);
                if (layer.normalMapTexture != null)
                {
                    mat.SetTexture("_BumpMap", layer.normalMapTexture);
                    mat.EnableKeyword("_NORMALMAP");
                }
            }
            mat.SetColor("_BaseColor", new Color(0.72f, 0.64f, 0.52f));
            mat.SetFloat("_Smoothness", 0.04f);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);

            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (renderer.gameObject.name == "Road") renderer.sharedMaterial = mat;
        }

        static void AddHighlandsDestructibles()
        {
            var old = GameObject.Find("Track Destructibles");
            if (old != null) Object.DestroyImmediate(old);
            var line = Object.FindFirstObjectByType<RaceLine>();
            if (line == null || line.points == null || line.points.Length < 2) return;

            var root = new GameObject("Track Destructibles").transform;
            var crate = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FayLabs/Kroma/Scenes/Assets/Prefabs/Crate_Wooden.prefab");
            var barrel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FayLabs/Kroma/Scenes/Assets/Prefabs/Barrel.prefab");
            var cone = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ARCADE - Ultimate Vehicles Pack/Environment/Assets/Prefabs/Parking Zone/Cone.prefab");
            var choices = new[] { crate, barrel, cone };

            // Roadside clusters. They sit inside the invisible boundary and away from the racing line.
            for (int cluster = 0; cluster < 14; cluster++)
            {
                float d = 180f + cluster * (line.length - 360f) / 14f;
                int index = line.IndexAt(d);
                float side = cluster % 2 == 0 ? 1f : -1f;
                for (int item = 0; item < 3; item++)
                {
                    var prefab = choices[(cluster + item) % choices.Length];
                    if (prefab == null) continue;
                    Vector3 at = line.PointAt(d + item * 3.5f) + line.Right(index) * side * (8.5f + item * 1.2f);
                    MakeBreakable(prefab, root, at, Quaternion.LookRotation(line.Direction(index)) * Quaternion.Euler(0f, item * 17f, 0f), prefab == cone ? 0.6f : 4f, 2f);
                }
            }

            // Signature smash-through moment with gaps wide enough for the AI to choose a line.
            float roadblockDistance = line.length * 0.63f;
            int roadblockIndex = line.IndexAt(roadblockDistance);
            for (int i = -4; i <= 4; i++)
            {
                if (i == -1 || i == 2 || crate == null) continue;
                Vector3 at = line.PointAt(roadblockDistance) + line.Right(roadblockIndex) * i * 2.2f + Vector3.up * 0.15f;
                MakeBreakable(crate, root, at, Quaternion.LookRotation(line.Direction(roadblockIndex)), 5f, 2f);
            }
        }

        static void MakeBreakable(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation, float mass, float breakSpeed)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(position, rotation);
            if (go.GetComponentsInChildren<Collider>(true).Length == 0)
            {
                var bounds = RendererBounds(go);
                var box = go.AddComponent<BoxCollider>();
                box.center = go.transform.InverseTransformPoint(bounds.center);
                var scale = go.transform.lossyScale;
                box.size = new Vector3(bounds.size.x / Mathf.Max(0.01f, Mathf.Abs(scale.x)), bounds.size.y / Mathf.Max(0.01f, Mathf.Abs(scale.y)), bounds.size.z / Mathf.Max(0.01f, Mathf.Abs(scale.z)));
            }
            var body = go.GetComponent<Rigidbody>();
            if (body == null) body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            var prop = go.GetComponent<BreakableProp>();
            if (prop == null) prop = go.AddComponent<BreakableProp>();
            var so = new SerializedObject(prop);
            so.FindProperty("mass").floatValue = mass;
            so.FindProperty("breakSpeed").floatValue = breakSpeed;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("SHIFT RUSH/Tracks/Repair Stormbreak Road + Props")]
        public static void RepairHighlandsRoadAndProps()
        {
            ClearHighlandsRoadTerrain();
            TuneHighlandsProps();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("[SHIFT//RUSH] Stormbreak road clearance and lightweight props repaired.");
        }

        static void ClearHighlandsRoadTerrain()
        {
            var line = Object.FindFirstObjectByType<RaceLine>();
            var terrain = Object.FindFirstObjectByType<Terrain>();
            if (line == null || line.points == null || terrain == null) return;

            var data = terrain.terrainData;
            int resolution = data.heightmapResolution;
            var heights = data.GetHeights(0, 0, resolution, resolution);
            Vector3 origin = terrain.transform.position;
            float cellX = data.size.x / (resolution - 1f);
            float cellZ = data.size.z / (resolution - 1f);
            float radius = SurfaceHalf + 1.5f;
            int rx = Mathf.CeilToInt(radius / cellX), rz = Mathf.CeilToInt(radius / cellZ);

            foreach (var point in line.points)
            {
                int cx = Mathf.RoundToInt((point.x - origin.x) / data.size.x * (resolution - 1));
                int cz = Mathf.RoundToInt((point.z - origin.z) / data.size.z * (resolution - 1));
                float target = Mathf.Clamp01((point.y - 0.75f - origin.y) / data.size.y);
                for (int z = Mathf.Max(0, cz - rz); z <= Mathf.Min(resolution - 1, cz + rz); z++)
                    for (int x = Mathf.Max(0, cx - rx); x <= Mathf.Min(resolution - 1, cx + rx); x++)
                    {
                        float dx = (x - cx) * cellX, dz = (z - cz) * cellZ;
                        if (dx * dx + dz * dz <= radius * radius)
                            heights[z, x] = Mathf.Min(heights[z, x], target);
                    }
            }

            data.SetHeights(0, 0, heights);
            terrain.Flush();
            EditorUtility.SetDirty(data);
        }

        static void TuneHighlandsProps()
        {
            foreach (var prop in Object.FindObjectsByType<BreakableProp>(FindObjectsSortMode.None))
            {
                bool cone = prop.gameObject.name.Contains("Cone");
                bool roadblock = !cone && Vector3.Distance(prop.transform.position, Object.FindFirstObjectByType<RaceLine>().PointAt(Object.FindFirstObjectByType<RaceLine>().length * 0.63f)) < 25f;
                var so = new SerializedObject(prop);
                so.FindProperty("mass").floatValue = cone ? 0.6f : roadblock ? 5f : 4f;
                so.FindProperty("breakSpeed").floatValue = 2f;
                so.ApplyModifiedPropertiesWithoutUndo();
                var body = prop.GetComponent<Rigidbody>();
                body.isKinematic = true;
                EditorUtility.SetDirty(body);
            }
        }

        static Bounds RendererBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        static void UpdateHighlandsCatalog()
        {
            var line = Object.FindFirstObjectByType<RaceLine>();
            var catalog = AssetDatabase.LoadAssetAtPath<RaceCatalog>(Root + "Resources/RaceCatalog.asset");
            if (catalog == null || line == null) return;
            var track = new RaceCatalog.Track
            {
                id = "stormbreak",
                displayName = "Stormbreak Highlands",
                scene = "Track_StormbreakHighlands",
                description = "A flat-out dirt circuit across stormy moorland, river crossings and forest switchbacks.",
                lengthKm = Mathf.Round(line.length / 100f) / 10f,
                map = line.mapSprite,
                loadingTips = new[]
                {
                    "Loose dirt rewards smooth steering and early throttle.",
                    "Roadside crates, barrels and cones will give way at speed.",
                    "The ridge descent is faster than it looks.",
                    "SHIFT ramp: press Shift as the diamonds meet.",
                },
            };
            int index = catalog.tracks.FindIndex(t => t.id == track.id || t.scene == track.scene);
            if (index >= 0)
            {
                track.thumbnail = catalog.tracks[index].thumbnail;
                catalog.tracks[index] = track;
            }
            else catalog.tracks.Add(track);
            EditorUtility.SetDirty(catalog);
        }

        static void AddHighlandsToBuild()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(scene => scene.path == HighlandsScene)) scenes.Add(new EditorBuildSettingsScene(HighlandsScene, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        [MenuItem("SHIFT RUSH/Tracks/Capture Stormbreak Hero")]
        public static void CaptureHighlandsHero()
        {
            var line = Object.FindFirstObjectByType<RaceLine>();
            if (line == null || line.points == null || line.points.Length < 2) return;
            int index = Mathf.Clamp(Mathf.RoundToInt(line.points.Length * 0.19f), 0, line.points.Length - 1);
            Vector3 point = line.points[index], direction = line.Direction(index), right = line.Right(index);
            var cameraObject = new GameObject("Stormbreak Hero Camera");
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            camera.transform.position = point - direction * 22f + right * 10f + Vector3.up * 5f;
            camera.transform.LookAt(point + direction * 38f + Vector3.up * 1.5f);
            camera.fieldOfView = 52f;
            camera.farClipPlane = 5000f;
            const int width = 1600, height = 900;
            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = null;
            RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            Object.DestroyImmediate(cameraObject);
            target.Release();
            string path = Root + "Art/UI/T_Hero_StormbreakHighlands.png";
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            var catalog = AssetDatabase.LoadAssetAtPath<RaceCatalog>(Root + "Resources/RaceCatalog.asset");
            var track = catalog != null ? catalog.tracks.Find(t => t.id == "stormbreak") : null;
            if (track != null)
            {
                track.thumbnail = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
