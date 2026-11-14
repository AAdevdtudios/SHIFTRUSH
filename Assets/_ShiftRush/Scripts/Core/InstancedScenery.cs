using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShiftRush.Core
{
    /// <summary>
    /// Draws a track's trees, bushes and ground cover straight from saved position lists with GPU instancing,
    /// instead of one GameObject (plus one per detail level) per plant. A circuit loads thousands of objects
    /// lighter and draws the same plants in a handful of calls. Detail levels work like the plants' own
    /// LODGroups (same switch heights, per plant), and whole 64 m chunks outside the view are skipped.
    /// Made by SHIFT RUSH → Tracks → Instance Scenery; tree trunks keep their collisions as one mesh per chunk.
    /// </summary>
    [ExecuteAlways] // the plants show in the Scene view too
    public class InstancedScenery : MonoBehaviour
    {
        [System.Serializable]
        public class Level
        {
            public Mesh mesh;
            public Material[] materials;
            [Tooltip("Shown while the plant fills more than this share of the screen height (the LODGroup's own value).")]
            public float screenHeight;
            public ShadowCastingMode shadows = ShadowCastingMode.On;
        }

        [System.Serializable]
        public class Kind
        {
            public string name;
            [Tooltip("The LODGroup's size (local), for the switch distances.")]
            public float size = 1f;
            public Level[] levels;
            public int layer;
        }

        [System.Serializable]
        public class Chunk
        {
            public int kind;
            public Bounds bounds;
            public Matrix4x4[] matrices;
            public float[] scales; // largest axis scale per plant, for its switch distances
        }

        public List<Kind> kinds = new List<Kind>();
        public List<Chunk> chunks = new List<Chunk>();
        [Tooltip("Extra metres round each chunk when testing it against the view, so shadows from just off screen still fall.")]
        public float cullPadding = 25f;

        // Per kind, per level: this frame's matrices.
        List<Matrix4x4>[][] buckets;
        Material[][][] instanced; // instancing-enabled copies of the plant materials
        readonly Plane[] planes = new Plane[6];

        void OnEnable()
        {
            Prepare();
            RenderPipelineManager.beginCameraRendering += Draw;
        }

        void OnDisable() { RenderPipelineManager.beginCameraRendering -= Draw; }

        void Prepare()
        {
            buckets = new List<Matrix4x4>[kinds.Count][];
            instanced = new Material[kinds.Count][][];
            for (int k = 0; k < kinds.Count; k++)
            {
                var levels = kinds[k].levels;
                buckets[k] = new List<Matrix4x4>[levels.Length];
                instanced[k] = new Material[levels.Length][];
                for (int l = 0; l < levels.Length; l++)
                {
                    buckets[k][l] = new List<Matrix4x4>(256);
                    var src = levels[l].materials;
                    instanced[k][l] = new Material[src.Length];
                    for (int m = 0; m < src.Length; m++)
                    {
                        if (src[m] == null) continue;
                        if (src[m].enableInstancing) { instanced[k][l][m] = src[m]; continue; }
                        instanced[k][l][m] = new Material(src[m]) { name = src[m].name + " (Instanced)", enableInstancing = true };
                    }
                }
            }
        }

        // Each camera, as it starts: pick every plant's detail level for that camera and submit it.
        void Draw(ScriptableRenderContext context, Camera c)
        {
            if (buckets == null || (c.cameraType != CameraType.Game && c.cameraType != CameraType.SceneView)) return;
            GeometryUtility.CalculateFrustumPlanes(c, planes);
            Vector3 eye = c.transform.position;
            // Distance at which a plant of size 1 fills a given share of the screen height.
            float perHeight = QualitySettings.lodBias / (2f * Mathf.Tan(c.fieldOfView * 0.5f * Mathf.Deg2Rad));

            for (int k = 0; k < buckets.Length; k++) for (int l = 0; l < buckets[k].Length; l++) buckets[k][l].Clear();

            foreach (var ch in chunks)
            {
                var b = ch.bounds; b.Expand(cullPadding);
                if (!GeometryUtility.TestPlanesAABB(planes, b)) continue;
                var kind = kinds[ch.kind];
                var lv = kind.levels;
                var bk = buckets[ch.kind];
                for (int i = 0; i < ch.matrices.Length; i++)
                {
                    var m = ch.matrices[i];
                    float d = Vector3.Distance(eye, new Vector3(m.m03, m.m13, m.m23));
                    // Share of the screen height this plant fills, then the first level it qualifies for.
                    float h = kind.size * ch.scales[i] * perHeight / Mathf.Max(d, 0.01f);
                    for (int l = 0; l < lv.Length; l++)
                        if (h >= lv[l].screenHeight) { bk[l].Add(m); break; }
                    // Smaller than the last level's height: culled, like the LODGroup.
                }
            }

            for (int k = 0; k < kinds.Count; k++)
            {
                var kind = kinds[k];
                for (int l = 0; l < kind.levels.Length; l++)
                {
                    var list = buckets[k][l];
                    if (list.Count == 0) continue;
                    var level = kind.levels[l];
                    for (int s = 0; s < instanced[k][l].Length && s < level.mesh.subMeshCount; s++)
                    {
                        var mat = instanced[k][l][s];
                        if (mat == null) continue;
                        var rp = new RenderParams(mat)
                        {
                            shadowCastingMode = level.shadows,
                            receiveShadows = true,
                            layer = kind.layer,
                            worldBounds = new Bounds(eye, Vector3.one * 4000f),
                            lightProbeUsage = LightProbeUsage.Off,
                            camera = c,
                        };
                        for (int start = 0; start < list.Count; start += 500)
                            Graphics.RenderMeshInstanced(rp, level.mesh, s, list, Mathf.Min(500, list.Count - start), start);
                    }
                }
            }
        }

        void OnDestroy()
        {
            if (instanced == null) return;
            foreach (var k in instanced) foreach (var l in k) foreach (var m in l)
                if (m != null && m.name.EndsWith("(Instanced)")) DestroyImmediate(m);
        }
    }
}
