using System.IO;
using ShiftRush.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// Art for the track select's right column: a studio picture of each car (front three-quarter, magenta rim
    /// light, transparent background) and each track's circuit as one clean line made from its race minimap.
    /// Both are made by "SHIFT RUSH/Build Main Menu", or on their own by "SHIFT RUSH/Build Track Select Art".
    /// </summary>
    public static partial class MenuBuilder
    {
        [MenuItem("SHIFT RUSH/Build Track Select Art")]
        static void BuildTrackSelectArtOnly()
        {
            var cat = AssetDatabase.LoadAssetAtPath<RaceCatalog>("Assets/_ShiftRush/Resources/RaceCatalog.asset");
            if (cat == null) return;
            MakeCarPictures(cat);
            MakeTrackMaps(cat);
            AssetDatabase.SaveAssets();
            Debug.Log("[SHIFT//RUSH] Track select art built.");
        }

        static void MakeCarPictures(RaceCatalog cat)
        {
            foreach (var car in cat.cars)
            {
                var source = car.showroomPrefab != null ? car.showroomPrefab : car.prefab;
                if (source == null) continue;
                var tex = RenderCar(source, 1024, 512);
                if (tex == null) continue;
                car.thumbnail = SaveSprite(tex, UIArt + "T_Car_" + car.id + ".png");
            }
            EditorUtility.SetDirty(cat);
        }

        /// <summary>The car alone in a preview scene: key light, cool fill and a magenta rim from behind.</summary>
        static Texture2D RenderCar(GameObject prefab, int w, int h)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var car = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                car.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                // Only the body and wheels: no particles, trails, lights or UI the drivable prefab carries.
                Bounds b = default; bool any = false;
                foreach (var r in car.GetComponentsInChildren<Renderer>(true))
                {
                    bool mesh = r is MeshRenderer || r is SkinnedMeshRenderer;
                    if (!mesh || !r.gameObject.activeInHierarchy) { r.enabled = false; continue; }
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
                foreach (var l in car.GetComponentsInChildren<Light>(true)) l.enabled = false;
                if (!any) return null;

                // Front three-quarter, a little above. Lights are set from the camera's side so the look holds
                // whatever the angle: key over the camera's shoulder, cool fill opposite, magenta kicker from behind.
                Vector3 dir = Quaternion.Euler(9f, 145f, 0f) * Vector3.forward * -1f; // car → camera
                void AddLight(Color c, float intensity, Vector3 towards)
                {
                    var go = new GameObject("Light");
                    SceneManager_Move(go, scene);
                    var l = go.AddComponent<Light>();
                    l.type = LightType.Directional; l.color = c; l.intensity = intensity;
                    go.transform.rotation = Quaternion.LookRotation(towards);
                }
                AddLight(new Color(1f, 0.97f, 0.92f), 1.6f, Quaternion.Euler(0f, -25f, 0f) * -dir + Vector3.down * 0.9f);
                AddLight(new Color(0.55f, 0.65f, 0.9f), 0.5f, Quaternion.Euler(0f, -70f, 0f) * -dir + Vector3.down * 0.3f);
                AddLight(new Color(1f, 0.25f, 0.85f), 0.8f, Quaternion.Euler(0f, 20f, 0f) * dir + Vector3.down * 0.5f);

                var camGo = new GameObject("Camera");
                SceneManager_Move(camGo, scene);
                var cam = camGo.AddComponent<Camera>();
                var data = camGo.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = false;
                cam.scene = scene;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0, 0, 0, 0);
                cam.fieldOfView = 22f;
                cam.nearClipPlane = 0.1f; cam.farClipPlane = 200f;
                float radius = b.extents.magnitude;
                float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.52f;
                cam.transform.position = b.center + dir * dist;
                cam.transform.LookAt(b.center + Vector3.down * b.extents.y * 0.15f);

                var rt = RenderTexture.GetTemporary(w * 2, h * 2, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;

                // Downsample 2x for clean edges.
                var big = new Texture2D(w * 2, h * 2, TextureFormat.RGBA32, false);
                RenderTexture.active = rt;
                big.ReadPixels(new Rect(0, 0, w * 2, h * 2), 0, 0); big.Apply();
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(rt);
                var src = big.GetPixels32();
                var dst = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int r = 0, g = 0, bl = 0, a = 0;
                        for (int dy = 0; dy < 2; dy++)
                            for (int dx = 0; dx < 2; dx++)
                            {
                                var p = src[(y * 2 + dy) * w * 2 + x * 2 + dx];
                                r += p.r * p.a; g += p.g * p.a; bl += p.b * p.a; a += p.a;
                            }
                        dst[y * w + x] = a == 0 ? new Color32(0, 0, 0, 0)
                            : new Color32((byte)(r / a), (byte)(g / a), (byte)(bl / a), (byte)(a / 4));
                    }
                Object.DestroyImmediate(big);
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.SetPixels32(dst); tex.Apply();
                return tex;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        static void SceneManager_Move(GameObject go, UnityEngine.SceneManagement.Scene scene)
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
        }

        /// <summary>
        /// Each track's circuit as one clean line, made from the race minimap (Art/Track/T_Map_*.png): halved, the line
        /// thickened so it reads small, and cropped to the circuit. White, so the menu tints it.
        /// </summary>
        static void MakeTrackMaps(RaceCatalog cat)
        {
            foreach (var t in cat.tracks)
            {
                string key = t.scene.StartsWith("Track_") ? t.scene.Substring(6) : t.scene;
                string src = "Assets/_ShiftRush/Art/Track/T_Map_" + key + ".png";
                if (!File.Exists(src)) continue;
                var tex = new Texture2D(2, 2);
                tex.LoadImage(File.ReadAllBytes(src));
                int w = tex.width / 2, h = tex.height / 2;
                var px = tex.GetPixels32();
                var half = new float[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int a = 0;
                        for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++) a = Mathf.Max(a, px[(y * 2 + dy) * tex.width + x * 2 + dx].a);
                        half[y * w + x] = a / 255f;
                    }
                Object.DestroyImmediate(tex);
                const int r = 3; // thicken
                var fat = new float[w * h];
                int x0 = w, y0 = h, x1 = 0, y1 = 0;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float m = 0f;
                        for (int dy = -r; dy <= r; dy++)
                            for (int dx = -r; dx <= r; dx++)
                            {
                                int sx = x + dx, sy = y + dy;
                                if (dx * dx + dy * dy > r * r + 1 || sx < 0 || sy < 0 || sx >= w || sy >= h) continue;
                                m = Mathf.Max(m, half[sy * w + sx]);
                            }
                        fat[y * w + x] = m;
                        if (m > 0.05f) { x0 = Mathf.Min(x0, x); y0 = Mathf.Min(y0, y); x1 = Mathf.Max(x1, x); y1 = Mathf.Max(y1, y); }
                    }
                if (x1 <= x0 || y1 <= y0) continue;
                const int pad = 6;
                x0 = Mathf.Max(0, x0 - pad); y0 = Mathf.Max(0, y0 - pad); x1 = Mathf.Min(w - 1, x1 + pad); y1 = Mathf.Min(h - 1, y1 + pad);
                int cw = x1 - x0 + 1, ch = y1 - y0 + 1;
                var cols = new Color32[cw * ch];
                for (int y = 0; y < ch; y++)
                    for (int x = 0; x < cw; x++)
                        cols[y * cw + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(fat[(y + y0) * w + x + x0]) * 255));
                var outTex = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
                outTex.SetPixels32(cols);
                t.map = SaveSprite(outTex, UIArt + "T_Circuit_" + key + ".png");
            }
            EditorUtility.SetDirty(cat);
        }

        static Sprite SaveSprite(Texture2D tex, string path)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
