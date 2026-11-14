using System.Collections.Generic;
using System.IO;
using ShiftRush.Shift;
using UnityEditor;
using UnityEngine;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// SHIFT gameplay pieces, all generated (no paid art): the SHIFT ramp prefab, and the Ring Rush and Trap Run
    /// worlds (Resources prefabs the ShiftDirector spawns far from the track). SHIFT RUSH → SHIFT → Build SHIFT Kit.
    /// </summary>
    public static class ShiftKit
    {
        const string Dir = "Assets/_ShiftRush/Art/Shift/";
        const string Res = "Assets/_ShiftRush/Resources/";
        public const string RampPath = Dir + "P_ShiftRamp.prefab";
        static readonly Color Magenta = new Color32(255, 64, 217, 255);
        static readonly Color Cyan = new Color32(56, 225, 255, 255);

        const float Width = 30f, FinishZ = 300f, FloorStart = -30f, FloorEnd = 335f;

        [MenuItem("SHIFT RUSH/SHIFT/Build SHIFT Kit")]
        public static void Build()
        {
            Directory.CreateDirectory(Dir);
            // Textures first: importing a texture reloads materials changed earlier in the same pass.
            var grid = GridTexture(); var stripes = HazardTexture(); var chevrons = ChevronTexture();
            // Unlit so the neon grid reads the same under any track's sky and sun.
            var floor = UnlitMat("M_ArenaFloor", grid, Color.white, new Vector2(Width / 5f, (FloorEnd - FloorStart) / 5f));
            var wall = Mat("M_ArenaWall", null, new Color(0.08f, 0.08f, 0.12f), 0.6f, 0.2f, Color.black, Vector2.one);
            var neon = Mat("M_ArenaNeon", null, Magenta, 0.5f, 0f, Magenta * 3f, Vector2.one);
            var neonCyan = Mat("M_ArenaNeonCyan", null, Cyan, 0.5f, 0f, Cyan * 2f, Vector2.one);
            var white = Mat("M_ArenaWhite", null, Color.white, 0.5f, 0f, Color.white * 2.2f, Vector2.one);
            var hazard = UnlitMat("M_Hazard", stripes, Color.white, new Vector2(2f, 1f));
            var ring = Mat("M_Ring", null, Magenta, 0.6f, 0f, Magenta * 2.5f, Vector2.one);
            var rampMat = Mat("M_ShiftRamp", chevrons, new Color(0.12f, 0.06f, 0.18f), 0.5f, 0.1f, Magenta * 4f, new Vector2(1f, 2f));

            AssetDatabase.SaveAssets();
            BuildRamp(rampMat, neon);
            var kit = new ArenaParts { floor = floor, wall = wall, neon = neon, neonCyan = neonCyan, white = white, hazard = hazard, ring = ring };
            BuildRingRush(kit);
            BuildTrapRun(kit);
            AssetDatabase.SaveAssets();
            Debug.Log("[SHIFT//RUSH] SHIFT kit built: ramp, Ring Rush, Trap Run");
        }

        class ArenaParts { public Material floor, wall, neon, neonCyan, white, hazard, ring; }

        // ------------------------------------------------------------------ ramp

        const float RampWidth = 12f, RampHeight = 1.7f, RampLength = 12f;
        public static float RampLengthM { get { return RampLength; } }

        static void BuildRamp(Material rampMat, Material neon)
        {
            var root = new GameObject("P_ShiftRamp");
            var trig = root.AddComponent<BoxCollider>();
            trig.isTrigger = true;
            trig.center = new Vector3(0f, RampHeight + 1.2f, RampLength + 0.5f);
            trig.size = new Vector3(RampWidth + 2f, 3.5f, 3f);
            root.AddComponent<ShiftRamp>();

            var wedge = Child(root.transform, "Wedge", SaveMesh(Wedge(RampWidth, RampHeight, RampLength), "Mesh_ShiftRamp"), rampMat, true);
            // Neon edges up both sides of the slope.
            float s = Mathf.Atan2(RampHeight, RampLength) * Mathf.Rad2Deg, len = Mathf.Sqrt(RampHeight * RampHeight + RampLength * RampLength);
            for (int side = -1; side <= 1; side += 2)
            {
                var edge = Cube(root.transform, "Edge", neon, false);
                edge.transform.localPosition = new Vector3(side * (RampWidth * 0.5f + 0.12f), RampHeight * 0.5f + 0.1f, RampLength * 0.5f);
                edge.transform.localRotation = Quaternion.Euler(-s, 0f, 0f);
                edge.transform.localScale = new Vector3(0.25f, 0.2f, len);
            }
            // An arch at the foot so it reads from a distance.
            for (int side = -1; side <= 1; side += 2)
            {
                var post = Cube(root.transform, "Arch Post", neon, false);
                post.transform.localPosition = new Vector3(side * (RampWidth * 0.5f + 0.6f), 2.6f, -2f);
                post.transform.localScale = new Vector3(0.35f, 5.2f, 0.35f);
            }
            var bar = Cube(root.transform, "Arch Bar", neon, false);
            bar.transform.localPosition = new Vector3(0f, 5.2f, -2f);
            bar.transform.localScale = new Vector3(RampWidth + 1.55f, 0.35f, 0.35f);
            PrefabUtility.SaveAsPrefabAsset(root, RampPath);
            Object.DestroyImmediate(root);
        }

        // ------------------------------------------------------------------ arenas

        static GameObject ArenaShell(string name, ArenaParts k, out ShiftArena arena)
        {
            var root = new GameObject(name);
            arena = root.AddComponent<ShiftArena>();
            float len = FloorEnd - FloorStart, mid = (FloorEnd + FloorStart) * 0.5f;
            var floor = Cube(root.transform, "Floor", k.floor, true);
            floor.transform.localPosition = new Vector3(0f, -0.5f, mid);
            floor.transform.localScale = new Vector3(Width, 1f, len);
            for (int side = -1; side <= 1; side += 2)
            {
                var w = Cube(root.transform, "Wall", k.wall, true);
                w.transform.localPosition = new Vector3(side * (Width * 0.5f + 0.3f), 0.7f, mid);
                w.transform.localScale = new Vector3(0.6f, 1.4f, len);
                var strip = Cube(root.transform, "Wall Neon", k.neon, false);
                strip.transform.localPosition = new Vector3(side * (Width * 0.5f + 0.3f), 1.45f, mid);
                strip.transform.localScale = new Vector3(0.64f, 0.1f, len);
                for (float z = FloorStart + 10f; z < FloorEnd; z += 20f)
                {
                    var pylon = Cube(root.transform, "Pylon", k.neonCyan, false);
                    pylon.transform.localPosition = new Vector3(side * (Width * 0.5f + 2.5f), 3f, z);
                    pylon.transform.localScale = new Vector3(0.3f, 6f, 0.3f);
                }
            }
            foreach (var z in new[] { FloorStart, FloorEnd })
            {
                var end = Cube(root.transform, "End Wall", k.wall, true);
                end.transform.localPosition = new Vector3(0f, 2f, z);
                end.transform.localScale = new Vector3(Width + 1.2f, 4f, 0.6f);
            }
            // Finish arch and line.
            for (int side = -1; side <= 1; side += 2)
            {
                var post = Cube(root.transform, "Finish Post", k.white, false);
                post.transform.localPosition = new Vector3(side * (Width * 0.5f - 0.5f), 3.5f, FinishZ);
                post.transform.localScale = new Vector3(0.7f, 7f, 0.7f);
            }
            var bar = Cube(root.transform, "Finish Bar", k.white, false);
            bar.transform.localPosition = new Vector3(0f, 7f, FinishZ);
            bar.transform.localScale = new Vector3(Width - 0.3f, 0.7f, 0.7f);
            var line = Cube(root.transform, "Finish Line", k.white, false);
            line.transform.localPosition = new Vector3(0f, 0.01f, FinishZ);
            line.transform.localScale = new Vector3(Width, 0.02f, 1.2f);

            var start = new GameObject("Start").transform;
            start.SetParent(root.transform, false);
            start.localPosition = new Vector3(0f, 0.8f, 0f);
            arena.start = start;
            arena.finishZ = FinishZ;
            arena.timeLimit = 10f;
            return root;
        }

        static void BuildRingRush(ArenaParts k)
        {
            var root = ArenaShell("SR_Arena_RingRush", k, out var arena);
            arena.kind = ShiftArena.Kind.RingRush;
            arena.title = "RING RUSH";
            var torus = SaveMesh(Torus(3.3f, 0.32f, 48, 12), "Mesh_Ring");
            var rings = new List<Transform>();
            for (int i = 0; i < 10; i++)
            {
                var r = Child(root.transform, "Ring " + (i + 1), torus, k.ring, false);
                r.transform.localPosition = new Vector3(Mathf.Sin(i * 1.1f + 0.4f) * 7f, 3.0f, 28f + i * 27f);
                rings.Add(r.transform);
            }
            arena.rings = rings.ToArray();
            arena.ringRadius = 3.4f;
            arena.ringColor = Magenta;
            PrefabUtility.SaveAsPrefabAsset(root, Res + "SR_Arena_RingRush.prefab");
            Object.DestroyImmediate(root);
        }

        static void BuildTrapRun(ArenaParts k)
        {
            var root = ArenaShell("SR_Arena_TrapRun", k, out var arena);
            arena.kind = ShiftArena.Kind.TrapRun;
            arena.title = "TRAP RUN";
            arena.hitPenalty = 0.25f;
            var t = root.transform;
            Trap(t, k, "Slider", new Vector3(0, 1.2f, 45), new Vector3(6f, 2.4f, 3f), TrapMover.Motion.Slide, 9f, 0.4f, 0f);
            for (int i = 0; i < 5; i++)
                Trap(t, k, "Popper", new Vector3(-10 + i * 5, 1.5f, 95), new Vector3(3.2f, 3f, 1.6f), TrapMover.Motion.Pop, 3.3f, 0.6f, i * 0.2f);
            Trap(t, k, "Sweeper", new Vector3(0, 0.7f, 145), new Vector3(24f, 1.2f, 1.2f), TrapMover.Motion.Spin, 0f, 0.3f, 0f);
            Trap(t, k, "Hub", new Vector3(0, 1.5f, 145), new Vector3(2f, 3f, 2f), TrapMover.Motion.Spin, 0f, 0f, 0f);
            Trap(t, k, "Jaw L", new Vector3(-7.5f, 1.2f, 195), new Vector3(8f, 2.4f, 3f), TrapMover.Motion.Slide, 4.5f, 0.45f, 0f);
            Trap(t, k, "Jaw R", new Vector3(7.5f, 1.2f, 195), new Vector3(8f, 2.4f, 3f), TrapMover.Motion.Slide, 4.5f, 0.45f, 0.5f);
            for (int i = 0; i < 3; i++)
                Trap(t, k, "Spinner", new Vector3(-8 + i * 8, 0.7f, 240), new Vector3(7f, 1.1f, 1.1f), TrapMover.Motion.Spin, 0f, 0.5f, i * 0.33f);
            for (int i = 0; i < 5; i++)
                Trap(t, k, "Popper", new Vector3(-10 + i * 5, 1.5f, 278), new Vector3(3.2f, 3f, 1.6f), TrapMover.Motion.Pop, 3.3f, 0.7f, 0.8f - i * 0.2f);
            PrefabUtility.SaveAsPrefabAsset(root, Res + "SR_Arena_TrapRun.prefab");
            Object.DestroyImmediate(root);
        }

        static void Trap(Transform parent, ArenaParts k, string name, Vector3 pos, Vector3 size, TrapMover.Motion motion, float amp, float speed, float phase)
        {
            var go = Cube(parent, name, k.hazard, true);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.AddComponent<Rigidbody>().isKinematic = true;
            var m = go.AddComponent<TrapMover>();
            m.motion = motion; m.amplitude = amp; m.speed = speed; m.phase = phase;
        }

        // ------------------------------------------------------------------ helpers

        static GameObject Cube(Transform parent, string name, Material mat, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static GameObject Child(Transform parent, string name, Mesh mesh, Material mat, bool collider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        static Material Mat(string name, Texture2D tex, Color color, float smooth, float metal, Color emission, Vector2 tiling)
        {
            string path = Dir + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color);
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", tiling);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (emission.maxColorComponent > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
                m.SetTexture("_EmissionMap", tex);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else m.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material UnlitMat(string name, Texture2D tex, Color color, Vector2 tiling)
        {
            string path = Dir + name + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            m.SetColor("_BaseColor", color);
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", tiling);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Mesh SaveMesh(Mesh m, string name)
        {
            string path = Dir + name + ".asset";
            m.name = name;
            m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) { EditorUtility.CopySerialized(m, existing); EditorUtility.SetDirty(existing); return existing; }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>Wedge rising along +Z; the slope's UVs run 0..1 across and up so the chevrons point uphill.</summary>
        static Mesh Wedge(float width, float height, float length)
        {
            float w = width * 0.5f;
            Vector3 bl = new Vector3(-w, 0, 0), br = new Vector3(w, 0, 0), fl = new Vector3(-w, 0, length), fr = new Vector3(w, 0, length);
            Vector3 tl = new Vector3(-w, height, length), tr = new Vector3(w, height, length);
            var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
            System.Action<Vector3, Vector3, Vector3, Vector3, bool> quad = (a, b, c, d, slope) =>
            {
                int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                if (slope) { uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(1, 0)); }
                else { uv.Add(Vector2.zero); uv.Add(Vector2.zero); uv.Add(Vector2.zero); uv.Add(Vector2.zero); }
                t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
            };
            System.Action<Vector3, Vector3, Vector3> tri = (a, b, c) =>
            {
                int i = v.Count; v.Add(a); v.Add(b); v.Add(c); uv.Add(Vector2.zero); uv.Add(Vector2.zero); uv.Add(Vector2.zero);
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
            };
            quad(bl, tl, tr, br, true);
            quad(fl, fr, tr, tl, false);
            tri(bl, fl, tl);
            tri(br, tr, fr);
            var m = new Mesh();
            m.SetVertices(v); m.SetTriangles(t, 0); m.SetUVs(0, uv);
            return m;
        }

        /// <summary>Upright torus in the XY plane (drive through it along Z).</summary>
        static Mesh Torus(float major, float minor, int segs, int sides)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= segs; i++)
            {
                float a = i / (float)segs * Mathf.PI * 2f;
                var c = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                for (int j = 0; j <= sides; j++)
                {
                    float b = j / (float)sides * Mathf.PI * 2f;
                    v.Add(c * (major + Mathf.Cos(b) * minor) + Vector3.forward * Mathf.Sin(b) * minor);
                }
            }
            for (int i = 0; i < segs; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a = i * (sides + 1) + j, b = a + sides + 1;
                    t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            var m = new Mesh();
            m.SetVertices(v); m.SetTriangles(t, 0);
            return m;
        }

        static Texture2D GridTexture()
        {
            return SaveTex("T_ArenaGrid", 256, 256, (x, y) =>
            {
                float d = Mathf.Min(Mathf.Min(x, 255 - x), Mathf.Min(y, 255 - y));
                float line = Mathf.Clamp01(11f - d);
                float glow = Mathf.Clamp01(1f - d / 40f) * 0.35f;
                float v = Mathf.Max(line, glow);
                var dark = new Color(0.03f, 0.03f, 0.06f, 1f);
                return Color.Lerp(dark, new Color(1f, 0.3f, 0.86f, 1f), v);
            }, TextureWrapMode.Repeat);
        }

        static Texture2D HazardTexture()
        {
            return SaveTex("T_Hazard", 128, 128, (x, y) =>
            {
                bool stripe = Mathf.Repeat((x + y) / 32f, 1f) < 0.5f;
                return stripe ? new Color(1f, 0.25f, 0.85f, 1f) : new Color(0.06f, 0.06f, 0.09f, 1f);
            }, TextureWrapMode.Repeat);
        }

        static Texture2D ChevronTexture()
        {
            // Chevrons pointing up the slope (V), bright on dark.
            return SaveTex("T_RampChevron", 256, 256, (x, y) =>
            {
                float u = Mathf.Abs(x - 127.5f) / 128f;
                float vv = Mathf.Repeat(y / 128f + u * 0.6f, 1f);
                float c = vv > 0.15f && vv < 0.42f && u < 0.85f ? 1f : 0f;
                float edge = (x < 8 || x > 247) ? 1f : 0f;
                float v = Mathf.Max(c, edge);
                return new Color(v, v, v, 1f);
            }, TextureWrapMode.Repeat);
        }

        static Texture2D SaveTex(string name, int w, int h, System.Func<int, int, Color> f, TextureWrapMode wrap)
        {
            string path = Dir + name + ".png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = f(x, y);
            tex.SetPixels(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = wrap;
            imp.anisoLevel = 8;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
