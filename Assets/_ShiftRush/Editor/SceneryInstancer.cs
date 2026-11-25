using System.Collections.Generic;
using System.IO;
using ShiftRush.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// SHIFT RUSH → Tracks → Instance Scenery: turns each circuit's Trees, Bushes and Ground Cover (thousands of
    /// LODGroup prefabs) into one <see cref="InstancedScenery"/> that draws them with GPU instancing, plus one
    /// trunk collider mesh per 128 m chunk. The originals are saved first as a prefab (Art/Track/Baked) and can be
    /// put back with Restore Scenery, e.g. before re-scattering a track. Runs on both circuits and saves them.
    /// </summary>
    public static class SceneryInstancer
    {
        static readonly string[] Scenes =
        {
            "Assets/_ShiftRush/Scenes/Track_GlacierValley.unity",
            "Assets/_ShiftRush/Scenes/Track_AlpinePass.unity",
            "Assets/_ShiftRush/Scenes/Track_StormbreakHighlands.unity",
        };
        static readonly string[] Groups = { "Trees", "Bushes", "Ground Cover" };
        const string BakedDir = "Assets/_ShiftRush/Art/Track/Baked/";
        const float ChunkSize = 128f;

        [MenuItem("SHIFT RUSH/Tracks/Instance Scenery")]
        static void InstanceAll() { RunOnTracks(Instance); }

        [MenuItem("SHIFT RUSH/Tracks/This Scene: Instance Scenery")]
        static void InstanceCurrent()
        {
            var scene = SceneManager.GetActiveScene();
            Instance(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Points every instanced plant at a saved, instancing-enabled copy of its material (Art/Track/Baked/Materials).
        /// Builds keep a shader's instancing variant only if a saved material uses it, so without these the plants
        /// would draw in the editor but not in a build. Run by Instance Scenery; also on its own for tracks already done.
        /// </summary>
        [MenuItem("SHIFT RUSH/Tracks/Save Instanced Materials")]
        static void SaveMaterialsAll() { RunOnTracks(s => { foreach (var c in Object.FindObjectsByType<InstancedScenery>(FindObjectsSortMode.None)) SaveMaterials(c); }); }

        static void SaveMaterials(InstancedScenery comp)
        {
            string dir = BakedDir + "Materials/";
            Directory.CreateDirectory(dir);
            foreach (var kind in comp.kinds)
                foreach (var level in kind.levels)
                    for (int m = 0; m < level.materials.Length; m++)
                    {
                        var src = level.materials[m];
                        if (src == null || src.enableInstancing) continue;
                        string path = dir + src.name + "_Instanced.mat";
                        var copy = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (copy == null)
                        {
                            copy = new Material(src) { name = src.name + "_Instanced", enableInstancing = true };
                            AssetDatabase.CreateAsset(copy, path);
                        }
                        level.materials[m] = copy;
                    }
            EditorUtility.SetDirty(comp);
        }

        [MenuItem("SHIFT RUSH/Tracks/Restore Scenery")]
        static void RestoreAll() { RunOnTracks(Restore); }

        [MenuItem("SHIFT RUSH/Tracks/This Scene: Restore Scenery")]
        static void RestoreCurrent()
        {
            var scene = SceneManager.GetActiveScene();
            Restore(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        static void RunOnTracks(System.Action<Scene> work)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string back = SceneManager.GetActiveScene().path;
            foreach (var path in Scenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                work(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(back)) EditorSceneManager.OpenScene(back, OpenSceneMode.Single);
        }

        static Transform FindScenery()
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == "Scenery") return t;
            return null;
        }

        static void Instance(Scene scene)
        {
            var scenery = FindScenery();
            if (scenery == null) { Debug.LogWarning("[SHIFT//RUSH] " + scene.name + ": no Scenery."); return; }
            var groups = new List<Transform>();
            foreach (var g in Groups) { var t = scenery.Find(g); if (t != null && t.childCount > 0) groups.Add(t); }
            if (groups.Count == 0) { Debug.LogWarning("[SHIFT//RUSH] " + scene.name + ": nothing to instance (already done?)."); return; }
            ClearInstanced(scenery);
            Directory.CreateDirectory(BakedDir);

            var comp = new GameObject("Instanced Scenery").AddComponent<InstancedScenery>();
            comp.transform.SetParent(scenery, false);
            comp.enabled = false; // filled first, then enabled (it reads its lists as it wakes)
            var kindOf = new Dictionary<Object, int>();
            var chunkOf = new Dictionary<(int, int, int), int>();
            var mats = new Dictionary<int, List<Matrix4x4>>();
            var scales = new Dictionary<int, List<float>>();
            var trunks = new Dictionary<(int, int), List<CapsuleCollider>>();
            int plants = 0;

            foreach (var group in groups)
                foreach (Transform plant in group)
                {
                    var lg = plant.GetComponent<LODGroup>();
                    var firstRenderer = plant.GetComponentInChildren<MeshRenderer>();
                    if (firstRenderer == null) continue;
                    Object key = PrefabUtility.GetCorrespondingObjectFromSource(plant.gameObject);
                    if (key == null) key = firstRenderer.GetComponent<MeshFilter>().sharedMesh;
                    int k;
                    if (!kindOf.TryGetValue(key, out k))
                    {
                        k = comp.kinds.Count;
                        kindOf[key] = k;
                        comp.kinds.Add(MakeKind(plant, lg, firstRenderer));
                    }
                    var lod0 = lg != null ? lg.GetLODs()[0].renderers[0] : firstRenderer;
                    var p = plant.position;
                    var cell = (k, Mathf.FloorToInt(p.x / ChunkSize), Mathf.FloorToInt(p.z / ChunkSize));
                    int c;
                    if (!chunkOf.TryGetValue(cell, out c))
                    {
                        c = comp.chunks.Count;
                        chunkOf[cell] = c;
                        comp.chunks.Add(new InstancedScenery.Chunk { kind = k, bounds = lod0.bounds });
                        mats[c] = new List<Matrix4x4>(); scales[c] = new List<float>();
                    }
                    var ch = comp.chunks[c];
                    var bounds = ch.bounds; bounds.Encapsulate(lod0.bounds); ch.bounds = bounds;
                    mats[c].Add(lod0.transform.localToWorldMatrix);
                    var s = plant.lossyScale;
                    scales[c].Add(Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)));
                    foreach (var cap in plant.GetComponentsInChildren<CapsuleCollider>())
                    {
                        var tc = (Mathf.FloorToInt(p.x / ChunkSize), Mathf.FloorToInt(p.z / ChunkSize));
                        if (!trunks.ContainsKey(tc)) trunks[tc] = new List<CapsuleCollider>();
                        trunks[tc].Add(cap);
                    }
                    plants++;
                }
            for (int c = 0; c < comp.chunks.Count; c++) { comp.chunks[c].matrices = mats[c].ToArray(); comp.chunks[c].scales = scales[c].ToArray(); }

            // Trunks: one static mesh collider per chunk, an eight-sided post per tree.
            string meshPath = BakedDir + scene.name + "_SceneryColliders.asset";
            var holder = new GameObject("Scenery Colliders");
            holder.transform.SetParent(scenery, false);
            holder.isStatic = true;
            Mesh container = null;
            foreach (var kv in trunks)
            {
                var mesh = TrunkMesh(kv.Value);
                mesh.name = "Trunks " + kv.Key.Item1 + "_" + kv.Key.Item2;
                if (container == null) { AssetDatabase.CreateAsset(mesh, meshPath); container = mesh; }
                else AssetDatabase.AddObjectToAsset(mesh, container);
                var go = new GameObject(mesh.name);
                go.transform.SetParent(holder.transform, false);
                go.isStatic = true;
                go.layer = kv.Value[0].gameObject.layer;
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }

            // Keep the originals, then take them out of the scene.
            var source = new GameObject("Scenery Source");
            foreach (var g in groups) g.SetParent(source.transform, true);
            PrefabUtility.SaveAsPrefabAsset(source, BakedDir + scene.name + "_ScenerySource.prefab");
            Object.DestroyImmediate(source);

            SaveMaterials(comp);
            comp.enabled = true;
            Debug.Log("[SHIFT//RUSH] " + scene.name + ": " + plants + " plants instanced in " + comp.chunks.Count + " chunks (" + comp.kinds.Count + " kinds), " + trunks.Count + " trunk collider chunks.");
        }

        static InstancedScenery.Kind MakeKind(Transform plant, LODGroup lg, MeshRenderer first)
        {
            var kind = new InstancedScenery.Kind { name = plant.name, layer = first.gameObject.layer };
            var levels = new List<InstancedScenery.Level>();
            if (lg != null)
            {
                kind.size = lg.size;
                foreach (var lod in lg.GetLODs())
                {
                    if (lod.renderers.Length == 0 || lod.renderers[0] == null) continue;
                    var r = lod.renderers[0];
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    levels.Add(new InstancedScenery.Level { mesh = mf.sharedMesh, materials = r.sharedMaterials, screenHeight = lod.screenRelativeTransitionHeight, shadows = r.shadowCastingMode });
                }
            }
            else
            {
                kind.size = first.bounds.size.magnitude / Mathf.Max(0.001f, plant.lossyScale.x);
                levels.Add(new InstancedScenery.Level { mesh = first.GetComponent<MeshFilter>().sharedMesh, materials = first.sharedMaterials, screenHeight = 0.005f, shadows = first.shadowCastingMode });
            }
            kind.levels = levels.ToArray();
            return kind;
        }

        static Mesh TrunkMesh(List<CapsuleCollider> caps)
        {
            const int sides = 8;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            foreach (var cap in caps)
            {
                var t = cap.transform;
                var s = t.lossyScale;
                float r = cap.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));
                float h = Mathf.Max(cap.height * Mathf.Abs(s.y), r * 2f);
                Vector3 centre = t.TransformPoint(cap.center);
                Vector3 up = t.up;
                Vector3 bottom = centre - up * h * 0.5f, top = centre + up * h * 0.5f;
                Vector3 a = Vector3.Cross(up, Vector3.forward).sqrMagnitude > 0.01f ? Vector3.Cross(up, Vector3.forward).normalized : Vector3.right;
                Vector3 b = Vector3.Cross(up, a);
                int start = verts.Count;
                for (int i = 0; i < sides; i++)
                {
                    float ang = i * Mathf.PI * 2f / sides;
                    Vector3 o = (a * Mathf.Cos(ang) + b * Mathf.Sin(ang)) * r;
                    verts.Add(bottom + o); verts.Add(top + o);
                }
                for (int i = 0; i < sides; i++)
                {
                    int i0 = start + i * 2, i1 = start + ((i + 1) % sides) * 2;
                    tris.Add(i0); tris.Add(i0 + 1); tris.Add(i1 + 1);
                    tris.Add(i0); tris.Add(i1 + 1); tris.Add(i1);
                }
            }
            var mesh = new Mesh { indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void ClearInstanced(Transform scenery)
        {
            var a = scenery.Find("Instanced Scenery"); if (a != null) Object.DestroyImmediate(a.gameObject);
            var b = scenery.Find("Scenery Colliders"); if (b != null) Object.DestroyImmediate(b.gameObject);
        }

        static void Restore(Scene scene)
        {
            var scenery = FindScenery();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BakedDir + scene.name + "_ScenerySource.prefab");
            if (scenery == null || prefab == null) { Debug.LogWarning("[SHIFT//RUSH] " + scene.name + ": nothing to restore."); return; }
            var src = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(src, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            var kids = new List<Transform>();
            foreach (Transform g in src.transform) kids.Add(g);
            foreach (var g in kids) g.SetParent(scenery, true);
            Object.DestroyImmediate(src);
            ClearInstanced(scenery);
            Debug.Log("[SHIFT//RUSH] " + scene.name + ": scenery restored as separate objects.");
        }
    }
}
