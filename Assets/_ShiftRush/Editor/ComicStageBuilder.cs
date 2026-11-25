using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// SHIFT RUSH → Comic → Build Comic Renderer: makes the comic material, a Comic_Renderer (a copy of the PC renderer
    /// with the toon outline off and the "Comic Panel" full screen pass on, just before post, so bloom and tonemapping still play over the inked frame)
    /// and adds it to both pipeline assets' renderer lists, so only cameras that pick it get the comic look.
    /// Safe to run again. Prints the renderer's index for <see cref="Comic.ComicPanelCamera"/>.
    /// </summary>
    public static class ComicStageBuilder
    {
        const string MatPath = "Assets/_ShiftRush/Art/Materials/M_ComicPanel.mat";
        const string SourceRenderer = "Assets/Settings/PC_Renderer.asset";
        const string ComicRenderer = "Assets/Settings/Comic_Renderer.asset";
        static readonly string[] Pipelines = { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" };
        const string FeatureName = "Comic Panel";

        [MenuItem("SHIFT RUSH/Comic/Build Comic Renderer")]
        public static int Build()
        {
            var shader = Shader.Find("ShiftRush/Comic Panel");
            if (shader == null) { Debug.LogError("[COMIC] Comic Panel shader missing or not compiled."); return -1; }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, MatPath); }
            mat.shader = shader;

            if (AssetDatabase.LoadAssetAtPath<UniversalRendererData>(ComicRenderer) == null)
                AssetDatabase.CopyAsset(SourceRenderer, ComicRenderer);
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(ComicRenderer);
            if (data == null) { Debug.LogError("[COMIC] Could not make " + ComicRenderer); return -1; }

            FullScreenPassRendererFeature comic = null;
            foreach (var f in data.rendererFeatures)
            {
                if (f == null) continue;
                if (f is FullScreenPassRendererFeature fs && f.name == FeatureName) comic = fs;
                else if (f.name == "Toon Outline") { f.SetActive(false); EditorUtility.SetDirty(f); } // the comic pass draws its own ink
            }
            if (comic == null)
            {
                comic = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                comic.name = FeatureName;
                AssetDatabase.AddObjectToAsset(comic, data);
                data.rendererFeatures.Add(comic);
                AssetDatabase.SaveAssets();
                string guid; long id;
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(comic, out guid, out id))
                {
                    var so = new SerializedObject(data);
                    var map = so.FindProperty("m_RendererFeatureMap");
                    map.arraySize++;
                    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            comic.passMaterial = mat;
            comic.passIndex = 0;
            comic.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing; // after post renders black into a capture target under Render Graph
            comic.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
            comic.fetchColorBuffer = true;
            comic.SetActive(true);
            EditorUtility.SetDirty(comic);
            EditorUtility.SetDirty(data);

            int index = -1;
            foreach (var p in Pipelines)
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(p);
                if (asset == null) continue;
                var so = new SerializedObject(asset);
                var list = so.FindProperty("m_RendererDataList");
                int at = -1;
                for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == data) at = i;
                if (at < 0)
                {
                    at = list.arraySize;
                    list.arraySize++;
                    list.GetArrayElementAtIndex(at).objectReferenceValue = data;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(asset);
                }
                if (index >= 0 && index != at) Debug.LogWarning("[COMIC] Comic renderer sits at a different index in " + p + " (" + at + ")");
                index = at;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[COMIC] Comic renderer ready at index " + index);
            return index;
        }

        const string IntroArt = "Assets/_ShiftRush/Art/Comic/Intro/";
        const string IntroData = "Assets/_ShiftRush/Resources/SR_ComicIntro.asset";

        /// <summary>
        /// SHIFT RUSH → Comic → Capture Intro Panels: captures every panel camera in the open scene whose name starts with
        /// "intro_", in name order, into Art/Comic/Intro, and lists them in Resources/SR_ComicIntro for the intro player.
        /// </summary>
        [MenuItem("SHIFT RUSH/Comic/Capture Intro Panels")]
        public static int CaptureIntro()
        {
            var cams = new System.Collections.Generic.List<Comic.ComicPanelCamera>();
            foreach (var c in Object.FindObjectsByType<Comic.ComicPanelCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.panelName.StartsWith("intro_")) cams.Add(c);
            cams.Sort((a, b) => string.CompareOrdinal(a.panelName, b.panelName));
            System.IO.Directory.CreateDirectory(IntroArt);

            var data = AssetDatabase.LoadAssetAtPath<Comic.ComicIntroData>(IntroData);
            if (data == null) { data = ScriptableObject.CreateInstance<Comic.ComicIntroData>(); AssetDatabase.CreateAsset(data, IntroData); }
            data.mono = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(Fonts + "JetBrainsMono-Bold SDF.asset");
            data.pages.Clear();

            // Cameras named intro_05a, intro_05b... are slices of one page: captured on their own, then set side by side.
            var pages = new System.Collections.Generic.List<string>();
            var slices = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();
            foreach (var c in cams)
            {
                bool wasOn = c.gameObject.activeSelf;
                c.gameObject.SetActive(true);
                string shot = c.Capture();
                c.gameObject.SetActive(wasOn);
                string page = c.panelName.Length > 8 ? c.panelName.Substring(0, 8) : c.panelName;
                if (page == c.panelName) { pages.Add(page); continue; }
                if (!slices.ContainsKey(page)) { slices[page] = new System.Collections.Generic.List<string>(); pages.Add(page); }
                slices[page].Add(shot);
            }
            string docs = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../docs/comic"));
            foreach (var kv in slices) JoinSlices(kv.Value, System.IO.Path.Combine(docs, kv.Key + ".png"));

            foreach (var name in pages)
            {
                string src = System.IO.Path.Combine(docs, name + ".png");
                // The game's copy is a high quality JPG: a fraction of the PNG's size in the repo, same look once imported.
                string dst = IntroArt + name + ".jpg";
                var tex = new Texture2D(2, 2);
                tex.LoadImage(System.IO.File.ReadAllBytes(src));
                System.IO.File.WriteAllBytes(dst, tex.EncodeToJPG(92));
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
                var imp = (TextureImporter)AssetImporter.GetAtPath(dst);
                imp.textureType = TextureImporterType.Default;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.mipmapEnabled = false;
                imp.maxTextureSize = 2048;
                imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.SaveAndReimport();
                data.pages.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(dst));
            }
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log("[COMIC] Intro: " + data.pages.Count + " panels captured");
            return data.pages.Count;
        }

        /// <summary>Lays slice PNGs side by side on paper, with a gutter between them, at their own height.</summary>
        static void JoinSlices(System.Collections.Generic.List<string> files, string outPath)
        {
            const int gutter = 21;
            var texs = new System.Collections.Generic.List<Texture2D>();
            foreach (var f in files) { var t = new Texture2D(2, 2); t.LoadImage(System.IO.File.ReadAllBytes(f)); texs.Add(t); }
            int h = 0, w = gutter * (texs.Count - 1);
            foreach (var t in texs) { w += t.width; h = Mathf.Max(h, t.height); }
            var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var paper = new Color32(250, 247, 240, 255);
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = paper;
            outTex.SetPixels32(px);
            int x = 0;
            foreach (var t in texs) { outTex.SetPixels(x, h - t.height, t.width, t.height, t.GetPixels()); x += t.width + gutter; Object.DestroyImmediate(t); }
            outTex.Apply();
            System.IO.File.WriteAllBytes(outPath, outTex.EncodeToPNG());
            Object.DestroyImmediate(outTex);
        }

        [MenuItem("SHIFT RUSH/Comic/Replay Intro On Next Play")]
        public static void ReplayIntro()
        {
            PlayerPrefs.DeleteKey(Comic.ComicIntro.SeenKey);
            PlayerPrefs.Save();
            Debug.Log("[COMIC] The intro plays next time the main menu starts.");
        }

        const string LetteringRenderer ="Assets/Settings/Comic_Lettering_Renderer.asset";
        const string Fonts = "Assets/_ShiftRush/Art/Fonts/";
        const int UiLayer = 5;

        /// <summary>
        /// A copy of the PC renderer with every full screen pass off (no toon outline, no comic pass, no AO), for the
        /// lettering's overlay camera, so balloons and captions draw clean over the inked panel.
        /// </summary>
        public static int BuildLetteringRenderer()
        {
            if (AssetDatabase.LoadAssetAtPath<UniversalRendererData>(LetteringRenderer) == null)
                AssetDatabase.CopyAsset(SourceRenderer, LetteringRenderer);
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(LetteringRenderer);
            if (data == null) return -1;
            foreach (var f in data.rendererFeatures) if (f != null) { f.SetActive(false); EditorUtility.SetDirty(f); }
            EditorUtility.SetDirty(data);
            int index = -1;
            foreach (var p in Pipelines)
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(p);
                if (asset == null) continue;
                var so = new SerializedObject(asset);
                var list = so.FindProperty("m_RendererDataList");
                int at = -1;
                for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == data) at = i;
                if (at < 0)
                {
                    at = list.arraySize;
                    list.arraySize++;
                    list.GetArrayElementAtIndex(at).objectReferenceValue = data;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(asset);
                }
                index = at;
            }
            AssetDatabase.SaveAssets();
            return index;
        }

        /// <summary>
        /// SHIFT RUSH → Comic → Add Lettering (select the panel camera): stacks an orthographic overlay camera on it
        /// that draws only the UI layer through the clean renderer, with a world space canvas the size of the panel and a
        /// <see cref="Comic.ComicLettering"/> to fill it. The panel camera stops drawing the UI layer.
        /// </summary>
        [MenuItem("SHIFT RUSH/Comic/Add Lettering")]
        public static void AddLetteringToSelection()
        {
            var cam = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<Camera>() : null;
            if (cam == null) cam = Object.FindFirstObjectByType<Comic.ComicPanelCamera>()?.GetComponent<Camera>();
            if (cam == null) { Debug.LogError("[COMIC] Select the panel camera first."); return; }
            AddLettering(cam);
        }

        public static Comic.ComicLettering AddLettering(Camera panel)
        {
            var existing = panel.GetComponentInChildren<Comic.ComicLettering>();
            if (existing != null) return existing;
            int index = BuildLetteringRenderer();

            var camGo = new GameObject("Lettering Camera");
            Undo.RegisterCreatedObjectUndo(camGo, "Add Lettering");
            camGo.transform.SetParent(panel.transform, false);
            camGo.layer = UiLayer;
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 5f;
            cam.cullingMask = 1 << UiLayer;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderType = CameraRenderType.Overlay;
            data.renderPostProcessing = false;
            data.SetRenderer(index);

            var baseData = panel.GetComponent<UniversalAdditionalCameraData>();
            if (baseData == null) baseData = panel.gameObject.AddComponent<UniversalAdditionalCameraData>();
            if (!baseData.cameraStack.Contains(cam)) baseData.cameraStack.Add(cam);
            panel.cullingMask &= ~(1 << UiLayer);

            var canvasGo = new GameObject("Lettering Canvas", typeof(RectTransform));
            canvasGo.layer = UiLayer;
            canvasGo.transform.SetParent(camGo.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;

            var letters = camGo.AddComponent<Comic.ComicLettering>();
            letters.panelCamera = panel;
            letters.letteringCamera = cam;
            letters.canvas = canvas;
            letters.titleFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(Fonts + "BigShouldersDisplay-Bold SDF.asset");
            letters.bodyFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(Fonts + "JetBrainsMono-Bold SDF.asset");
            letters.sfxFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(Fonts + "BigShouldersDisplay-Black SDF.asset");
            EditorUtility.SetDirty(panel);
            Debug.Log("[COMIC] Lettering added to " + panel.name + " (renderer " + index + ")");
            return letters;
        }
    }
}
