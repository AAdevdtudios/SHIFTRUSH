using System.Collections.Generic;
using System.IO;
using ShiftRush.Core;
using SplineArchitect;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// Renders a hero shot of each built track (its own sky and lighting), saves it as a sprite and registers the
    /// tracks in the RaceCatalog with those shots as their art. The menu masks these into its slanted theme.
    /// SHIFT RUSH → Tracks → Capture Hero Shots + Update Catalog.
    /// </summary>
    public static class TrackHeroShots
    {
        const string UIArt = "Assets/_ShiftRush/Art/UI/";

        class Shot
        {
            public TrackBuilder.Spec spec; public float t; public Vector3 camOffset; public float lookAhead, fov; public string id, description; public string[] tips;
        }

        [MenuItem("SHIFT RUSH/Tracks/Capture Hero Shots + Update Catalog")]
        public static void CaptureAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var shots = new List<Shot>
            {
                new Shot { spec = TrackBuilder.AlpinePass(), id = "alpine", t = 0.415f, camOffset = new Vector3(-5.5f, 2.2f, -14f), lookAhead = 45f, fov = 48f,
                    description = "Snow, switchbacks and a misty summit. Climb the hairpins, then dive through the avalanche gallery.",
                    tips = new[] { "Brake before the hairpins, not in them.", "Hold Left Ctrl for nitro on the gallery straight.", "R puts you back on your wheels." } },
                new Shot { spec = TrackBuilder.GlacierValley(), id = "valley", t = 0.17f, camOffset = new Vector3(9f, 3.5f, -18f), lookAhead = 30f, fov = 52f,
                    description = "Flat-out sweepers around a glacier lake, over the river bridge and through the village.",
                    tips = new[] { "Keep it pinned through the lakeside sweepers.", "The bridge kink is flat out if you're brave.", "Hold Left Ctrl for nitro." } },
            };

            var tracks = new List<RaceCatalog.Track>();
            foreach (var s in shots)
            {
                var scene = EditorSceneManager.OpenScene(s.spec.scene, OpenSceneMode.Single);
                var spline = Object.FindFirstObjectByType<Spline>();
                if (spline == null) { Debug.LogWarning("No spline in " + s.spec.scene); continue; }
                spline.RebuildCache();
                Sprite hero = Render(spline, s, "T_Hero_" + s.spec.name.Replace(" ", ""));
                tracks.Add(new RaceCatalog.Track
                {
                    id = s.id, displayName = s.spec.name,
                    scene = Path.GetFileNameWithoutExtension(s.spec.scene),
                    description = s.description,
                    lengthKm = Mathf.Round(spline.Length / 100f) / 10f,
                    thumbnail = hero,
                    loadingTips = s.tips,
                });
            }

            var cat = AssetDatabase.LoadAssetAtPath<RaceCatalog>("Assets/_ShiftRush/Resources/RaceCatalog.asset");
            if (cat != null)
            {
                // Keep the test arena as a free-roam option after the real circuits; drop the old greybox oval.
                var arena = cat.tracks.Find(t => t.scene == "TestArena");
                cat.tracks.Clear();
                cat.tracks.AddRange(tracks);
                if (arena != null) cat.tracks.Add(arena);
                EditorUtility.SetDirty(cat);
                AssetDatabase.SaveAssets();
            }

            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene("Assets/_ShiftRush/Scenes/MainMenu.unity", true) };
            foreach (var s in shots) scenes.Add(new EditorBuildSettingsScene(s.spec.scene, true));
            scenes.Add(new EditorBuildSettingsScene("Assets/_ShiftRush/Scenes/TestArena.unity", true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[SHIFT//RUSH] Hero shots captured and catalog updated (" + tracks.Count + " circuits).");
        }

        static Sprite Render(Spline spline, Shot s, string file)
        {
            float ft = spline.TimeToFixedTime(s.t);
            Vector3 p = spline.GetPosition(ft), d = spline.GetDirection(ft).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, d).normalized;
            var go = new GameObject("Hero Camera");
            var cam = go.AddComponent<Camera>();
            go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            cam.transform.position = p + right * s.camOffset.x + Vector3.up * s.camOffset.y + d * s.camOffset.z;
            cam.transform.LookAt(p + d * s.lookAhead + Vector3.up * 1.5f);
            cam.fieldOfView = s.fov;
            cam.farClipPlane = 5000f;
            const int w = 1600, h = 900;
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = null;
            Object.DestroyImmediate(go);
            rt.Release();

            string path = UIArt + file + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
