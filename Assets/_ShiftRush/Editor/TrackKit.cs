using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// The pieces the track builder lays along the spline: generated road, guardrail, marker-post, gallery and
    /// start-gantry prefabs (our own meshes and textures, nothing paid), plus references to Synty Alpine scenery.
    /// </summary>
    public class TrackKit
    {
        const string Dir = "Assets/_ShiftRush/Art/Track/";
        const string Synty = "Assets/Synty/PolygonNatureBiomes/PNB_Alpine_Mountain/";

        public GameObject roadPiece, railPiece, post, galleryPiece, gantry, startLine, kerb;
        public Material water;
        public List<GameObject> pines = new List<GameObject>(), rocks = new List<GameObject>(), cliffs = new List<GameObject>(),
            snowMounds = new List<GameObject>(), mountains = new List<GameObject>(), bushes = new List<GameObject>(),
            groundCover = new List<GameObject>(), bgTrees = new List<GameObject>();
        public GameObject cabin, jetty, lookout;
        public TerrainLayer snow, snowCliff, pineDirt, pineGrass, grass, grassBright, moss, riverRocks, mud;

        public static TrackKit Load()
        {
            Directory.CreateDirectory(Dir);
            var k = new TrackKit();
            var asphalt = Mat("M_Asphalt", RoadTexture(), new Color(1, 1, 1), 0.28f, 0f);
            var steel = Mat("M_Guardrail", null, new Color32(170, 176, 184, 255), 0.55f, 0.85f);
            var white = Mat("M_PostWhite", null, new Color32(235, 235, 235, 255), 0.3f, 0f);
            var reflector = Mat("M_PostReflector", null, new Color(1f, 0.25f, 0.85f), 0.6f, 0f, new Color(1f, 0.25f, 0.85f) * 1.5f);
            var concrete = Mat("M_GalleryConcrete", null, new Color32(150, 150, 146, 255), 0.15f, 0f);
            var gantryMat = Mat("M_Gantry", null, new Color32(24, 24, 30, 255), 0.5f, 0.6f);
            var neon = Mat("M_GantryNeon", null, new Color(1f, 0.25f, 0.85f), 0.5f, 0f, new Color(1f, 0.25f, 0.85f) * 3f);
            var checker = Mat("M_StartLine", CheckerTexture(), Color.white, 0.3f, 0f);
            k.water = Mat("M_Water", null, new Color32(58, 112, 138, 255), 0.95f, 0f);

            k.roadPiece = Prefab("P_RoadPiece", RoadMesh(10f, TrackBuilder.SurfaceHalf * 2f), asphalt, true);
            var kerbMat = Mat("M_Kerb", KerbTexture(), Color.white, 0.35f, 0f);
            k.kerb = Prefab("P_Kerb", KerbMesh(10f, 1.2f), kerbMat, false);
            k.railPiece = Prefab("P_GuardrailPiece", RailMesh(8f), steel, true);
            // Frictionless like the invisible walls: a glancing hit slides you along the rail instead of stopping you.
            using (var edit = new PrefabUtility.EditPrefabContentsScope(AssetDatabase.GetAssetPath(k.railPiece)))
                edit.prefabContentsRoot.GetComponent<MeshCollider>().sharedMaterial = TrackBuilder.WallMaterial();
            k.post = PostPrefab(white, reflector);
            k.galleryPiece = Prefab("P_GalleryPiece", GalleryMesh(10f, TrackBuilder.SurfaceHalf * 2f + 1f, 7.5f), concrete, true);
            k.gantry = GantryPrefab(gantryMat, neon);
            k.startLine = Prefab("P_StartLine", Quad(TrackBuilder.RoadWidth, 2.5f), checker, false);

            string p = Synty + "Prefabs/";
            for (int i = 1; i <= 5; i++) Add(k.pines, p + "SM_Env_Pine_0" + i + ".prefab");
            for (int i = 1; i <= 9; i++) Add(k.rocks, p + "SM_Env_Rock_0" + i + ".prefab");
            for (int i = 1; i <= 7; i++) Add(k.cliffs, p + "SM_Env_Rock_Cliff_0" + i + ".prefab");
            for (int i = 1; i <= 4; i++) Add(k.snowMounds, p + "SM_Env_Snow_Mound_0" + i + ".prefab");
            for (int i = 1; i <= 4; i++) Add(k.mountains, p + "SM_Env_MountainRange_0" + i + ".prefab");
            for (int i = 1; i <= 2; i++) Add(k.bushes, p + "SM_Env_Bush_0" + i + ".prefab");
            for (int i = 1; i <= 3; i++) Add(k.groundCover, p + "SM_Env_GroundCover_0" + i + ".prefab");
            for (int i = 1; i <= 2; i++) Add(k.bgTrees, p + "SM_Env_Background_Trees_0" + i + ".prefab");
            k.cabin = AssetDatabase.LoadAssetAtPath<GameObject>(p + "Props/SM_Prop_Cabin_01.prefab");
            k.jetty = AssetDatabase.LoadAssetAtPath<GameObject>(p + "Props/SM_Prop_Jetty_01.prefab");
            k.lookout = AssetDatabase.LoadAssetAtPath<GameObject>(p + "Props/SM_Prop_Lookout_01.prefab");

            string t = Synty + "Terrain/";
            k.snow = AssetDatabase.LoadAssetAtPath<TerrainLayer>(t + "layer_snow_albedosnow_normalb05780438ad4031.terrainlayer");
            k.snowCliff = AssetDatabase.LoadAssetAtPath<TerrainLayer>(t + "SnowCliff.terrainlayer");
            k.pineDirt = AssetDatabase.LoadAssetAtPath<TerrainLayer>(t + "Pine_Dirt.terrainlayer");
            k.pineGrass = AssetDatabase.LoadAssetAtPath<TerrainLayer>(t + "Pine_Grass.terrainlayer");
            k.grass = AssetDatabase.LoadAssetAtPath<TerrainLayer>(t + "Grass_01.terrainlayer");
            k.moss = AssetDatabase.LoadAssetAtPath<TerrainLayer>(t + "Moss_Terrain_01.terrainlayer");
            k.grassBright = MeadowLayer(k.grass);
            k.riverRocks = AssetDatabase.LoadAssetAtPath<TerrainLayer>(t + "RiverRocks_01.terrainlayer");
            k.mud = AssetDatabase.LoadAssetAtPath<TerrainLayer>(t + "Mud_01.terrainlayer");
            return k;
        }

        /// <summary>Synty's grass texture tinted to a fresh meadow green (their layers all read autumnal).</summary>
        static TerrainLayer MeadowLayer(TerrainLayer source)
        {
            if (source == null) return null;
            string path = Dir + "TL_MeadowGrass.terrainlayer";
            var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (l == null) { l = new TerrainLayer(); AssetDatabase.CreateAsset(l, path); }
            l.diffuseTexture = TintedCopy(source.diffuseTexture, new Color(0.78f, 1.12f, 0.62f), "T_MeadowGrass");
            l.normalMapTexture = source.normalMapTexture;
            l.normalScale = source.normalScale;
            l.tileSize = source.tileSize;
            l.diffuseRemapMin = Vector4.zero;
            l.diffuseRemapMax = Vector4.one;
            l.smoothness = 0f;
            EditorUtility.SetDirty(l);
            return l;
        }

        /// <summary>Renders a texture through the GPU (works on non-readable imports), tints it and saves a PNG.</summary>
        static Texture2D TintedCopy(Texture2D src, Color tint, string name)
        {
            if (src == null) return null;
            string path = Dir + name + ".png";
            int w = Mathf.Min(src.width, 1024), h = Mathf.Min(src.height, 1024);
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, true);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            var px = tex.GetPixels();
            for (int i = 0; i < px.Length; i++) px[i] = new Color(px[i].r * tint.r, px[i].g * tint.g, px[i].b * tint.b);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void Add(List<GameObject> list, string path)
        {
            var g = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (g != null) list.Add(g);
        }

        // ------------------------------------------------------------------ materials and textures

        static Material Mat(string name, Texture2D tex, Color color, float smooth, float metal, Color emission = default)
        {
            string path = Dir + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (emission.maxColorComponent > 0f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Asphalt with white edge lines and a dashed centre line (5 m dash, 5 m gap per 10 m piece).</summary>
        static Texture2D RoadTexture()
        {
            string path = Dir + "T_Road.png";
            const int w = 256, h = 512;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, true);
            var rng = new System.Random(5);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w, v = y / (float)h;
                    float n = (float)rng.NextDouble();
                    float grain = 0.20f + n * 0.05f + Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.04f;
                    Color c = new Color(grain, grain, grain * 1.04f);
                    // Across the mesh: shoulder | asphalt | shoulder, in metres from the left edge.
                    float total = TrackBuilder.SurfaceHalf * 2f, m = u * total, sh = TrackBuilder.ShoulderWidth;
                    bool shoulder = m < sh || m > total - sh;
                    if (shoulder)
                    {
                        float g = 0.36f + n * 0.12f + Mathf.PerlinNoise(x * 0.3f, y * 0.3f) * 0.08f; // loose gravel
                        c = new Color(g * 1.05f, g, g * 0.9f);
                    }
                    bool edge = (m > sh + 0.25f && m < sh + 0.5f) || (m < total - sh - 0.25f && m > total - sh - 0.5f);
                    bool centre = Mathf.Abs(m - total * 0.5f) < 0.12f && v < 0.5f;
                    if (edge || centre) c = Color.Lerp(c, new Color(0.92f, 0.92f, 0.9f), 0.9f - n * 0.15f);
                    px[y * w + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapModeU = TextureWrapMode.Clamp; imp.wrapModeV = TextureWrapMode.Repeat;
            imp.anisoLevel = 8;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Red and white kerb blocks, 2 m each along the road.</summary>
        static Texture2D KerbTexture()
        {
            string path = Dir + "T_Kerb.png";
            const int w = 32, h = 128;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, true);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool red = (y / 32) % 2 == 0;
                    float shade = x < 3 || x > w - 4 ? 0.8f : 1f;
                    tex.SetPixel(x, y, (red ? new Color(0.85f, 0.08f, 0.12f) : new Color(0.95f, 0.95f, 0.95f)) * shade);
                }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapModeU = TextureWrapMode.Clamp; imp.wrapModeV = TextureWrapMode.Repeat;
            imp.filterMode = FilterMode.Point;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>A low kerb strip (raised a touch so it sits over the asphalt), 10 m per piece: 5 red/white blocks.</summary>
        static Mesh KerbMesh(float length, float width)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int r = 0; r <= 1; r++)
                for (int c = 0; c <= 2; c++)
                {
                    float u = c / 2f;
                    v.Add(new Vector3((u - 0.5f) * width, c == 1 ? 0.07f : 0.03f, r * length));
                    uv.Add(new Vector2(u, r * length / 4f));
                }
            tri.AddRange(new[] { 0, 3, 1, 1, 3, 4, 1, 4, 2, 2, 4, 5 });
            var m = new Mesh();
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            return SaveMesh(m, "Mesh_Kerb");
        }

        static Texture2D CheckerTexture()
        {
            string path = Dir + "T_StartLine.png";
            const int w = 192, h = 40;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool on = ((x / 8) + (y / 8)) % 2 == 0;
                    tex.SetPixel(x, y, on ? new Color(0.92f, 0.92f, 0.92f) : new Color(0.06f, 0.06f, 0.07f));
                }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.filterMode = FilterMode.Point;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ meshes (built along +Z, centred on X)

        static Mesh SaveMesh(Mesh m, string name)
        {
            string path = Dir + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            m.name = name;
            m.RecalculateNormals();
            m.RecalculateTangents(); // Spline Architect's subdivision reads tangents
            m.RecalculateBounds();
            if (existing != null) { EditorUtility.CopySerialized(m, existing); EditorUtility.SetDirty(existing); return existing; }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Mesh RoadMesh(float length, float width)
        {
            // A flat strip with 8 columns so the deformation can follow camber smoothly.
            const int cols = 12;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int r = 0; r <= 1; r++)
                for (int c = 0; c <= cols; c++)
                {
                    float u = c / (float)cols;
                    v.Add(new Vector3((u - 0.5f) * width, 0f, r * length));
                    uv.Add(new Vector2(u, r));
                }
            for (int c = 0; c < cols; c++)
            {
                int a = c, b = c + 1, d = c + cols + 1, e = c + cols + 2;
                tri.AddRange(new[] { a, d, b, b, d, e });
            }
            var m = new Mesh();
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            return SaveMesh(m, "Mesh_RoadPiece");
        }

        static void Box(List<Vector3> v, List<int> t, Vector3 min, Vector3 max)
        {
            int o = v.Count;
            Vector3[] c =
            {
                new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z),
            };
            int[][] faces = { new[] { 0, 3, 2, 1 }, new[] { 5, 6, 7, 4 }, new[] { 4, 7, 3, 0 }, new[] { 1, 2, 6, 5 }, new[] { 3, 7, 6, 2 }, new[] { 4, 0, 1, 5 } };
            foreach (var f in faces)
            {
                int b = v.Count;
                foreach (var i in f) v.Add(c[i]);
                t.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }
        }

        static Mesh FromBoxes(string name, System.Action<List<Vector3>, List<int>> build)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            build(v, t);
            var m = new Mesh();
            m.SetVertices(v); m.SetTriangles(t, 0);
            var uv = new List<Vector2>();
            foreach (var p in v) uv.Add(new Vector2(p.x + p.z, p.y));
            m.SetUVs(0, uv);
            return SaveMesh(m, name);
        }

        static Mesh RailMesh(float length)
        {
            return FromBoxes("Mesh_GuardrailPiece", (v, t) =>
            {
                Box(v, t, new Vector3(-0.08f, 0.45f, 0f), new Vector3(0.08f, 0.8f, length));      // rail
                for (float z = 0.5f; z < length; z += 2f)
                    Box(v, t, new Vector3(-0.06f, -0.3f, z - 0.06f), new Vector3(0.06f, 0.75f, z + 0.06f)); // posts
            });
        }

        static Mesh GalleryMesh(float length, float width, float height)
        {
            float hw = width * 0.5f;
            return FromBoxes("Mesh_GalleryPiece", (v, t) =>
            {
                Box(v, t, new Vector3(-hw - 0.8f, height, -0.02f), new Vector3(hw + 0.8f, height + 0.8f, length + 0.02f)); // roof slab
                Box(v, t, new Vector3(hw, -0.2f, -0.02f), new Vector3(hw + 0.8f, height, length + 0.02f));                  // mountain-side wall
                for (float z = 1f; z < length; z += 5f)
                    Box(v, t, new Vector3(-hw - 0.6f, -0.2f, z - 0.35f), new Vector3(-hw, height, z + 0.35f));             // valley-side pillars
            });
        }

        static Mesh Quad(float width, float length)
        {
            var m = new Mesh();
            m.SetVertices(new List<Vector3> { new Vector3(-width / 2, 0, 0), new Vector3(width / 2, 0, 0), new Vector3(-width / 2, 0, length), new Vector3(width / 2, 0, length) });
            m.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) });
            m.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
            return SaveMesh(m, "Mesh_StartLine");
        }

        static GameObject Prefab(string name, Mesh mesh, Material mat, bool collider)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, Dir + name + ".prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject PostPrefab(Material white, Material reflector)
        {
            var mesh = FromBoxes("Mesh_MarkerPost", (v, t) => Box(v, t, new Vector3(-0.06f, 0f, -0.06f), new Vector3(0.06f, 1.1f, 0.06f)));
            var band = FromBoxes("Mesh_MarkerPostBand", (v, t) => Box(v, t, new Vector3(-0.065f, 0.8f, -0.065f), new Vector3(0.065f, 0.95f, 0.065f)));
            var go = new GameObject("P_MarkerPost");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = white;
            var b = new GameObject("Reflector");
            b.transform.SetParent(go.transform, false);
            b.AddComponent<MeshFilter>().sharedMesh = band;
            b.AddComponent<MeshRenderer>().sharedMaterial = reflector;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, Dir + "P_MarkerPost.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject GantryPrefab(Material frame, Material neon)
        {
            float hw = TrackBuilder.SurfaceHalf + 1.5f;
            var mesh = FromBoxes("Mesh_Gantry", (v, t) =>
            {
                Box(v, t, new Vector3(-hw - 0.5f, 0f, -0.5f), new Vector3(-hw + 0.5f, 7.5f, 0.5f));
                Box(v, t, new Vector3(hw - 0.5f, 0f, -0.5f), new Vector3(hw + 0.5f, 7.5f, 0.5f));
                Box(v, t, new Vector3(-hw - 0.5f, 6.2f, -0.6f), new Vector3(hw + 0.5f, 7.8f, 0.6f));
            });
            var strip = FromBoxes("Mesh_GantryNeon", (v, t) =>
            {
                Box(v, t, new Vector3(-hw + 0.6f, 6.5f, -0.65f), new Vector3(hw - 0.6f, 6.75f, 0.65f));
                Box(v, t, new Vector3(-hw + 0.6f, 7.25f, -0.65f), new Vector3(hw - 0.6f, 7.5f, 0.65f));
            });
            var go = new GameObject("P_StartGantry");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = frame;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            var n = new GameObject("Neon");
            n.transform.SetParent(go.transform, false);
            n.AddComponent<MeshFilter>().sharedMesh = strip;
            n.AddComponent<MeshRenderer>().sharedMaterial = neon;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, Dir + "P_StartGantry.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }
    }
}
