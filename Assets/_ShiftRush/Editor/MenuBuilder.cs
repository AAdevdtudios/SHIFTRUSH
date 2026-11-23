using System.Collections.Generic;
using System.IO;
using Michsky.UI.Heat;
using ShiftRush.Core;
using ShiftRush.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// Generates the SHIFT//RUSH main menu from Heat UI's desktop demo scene: restyles Heat, swaps the demo
    /// content for the gear menu, garage, track select, real settings and credits, and builds the live showroom.
    /// SHIFT RUSH → Build Main Menu. Re-running regenerates MainMenu.unity from scratch (hand edits there are lost;
    /// change this script or the RaceCatalog instead).
    /// </summary>
    public static partial class MenuBuilder
    {
        const string ScenePath = "Assets/_ShiftRush/Scenes/MainMenu.unity";
        const string HeatScene = "Assets/Heat - Complete Modern UI/Scenes/Main Menu (Desktop).unity";
        const string HeatRoot = "Assets/Heat - Complete Modern UI/";
        const string UIArt = "Assets/_ShiftRush/Art/UI/";
        const string SfxRoot = "Assets/UI SFX Mega Pack/Assets/Audio/";

        // Palette
        static readonly Color Magenta = new Color32(255, 64, 217, 255);
        static readonly Color Ink = new Color32(10, 10, 15, 255);
        static readonly Color Panel = new Color32(12, 12, 18, 255);
        static readonly Color Steel = new Color32(154, 160, 180, 255);
        static readonly Color Cyan = new Color32(56, 225, 255, 255);

        static TMP_FontAsset fontTitle, fontBold, fontMedium, fontRegular, fontLight;
        static Sprite slant, slantFrame, streaks, chevron, fadeLeft, softBand;

        [MenuItem("SHIFT RUSH/Build Main Menu")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            LoadFonts();
            MakeArt();
            var sounds = LoadSounds();
            LoadBroadcastFonts(); // Heat's theme, the loading screen and every menu screen use the stream fonts
            StyleHeat(sounds);
            var catalog = MakeCatalog();
            PartsBuilder.Build(); // tyres, spoilers and car prices for the garage
            MakeCarPictures(catalog);
            MakeTrackMaps(catalog);
            var credits = MakeCreditsPreset();
            MakeLoadingScreen();
            MakePauseMenu(sounds);
            MakeRaceHud(sounds);
            MakeShiftHud(sounds);
            MakeCoach();
            MakeRushHud();

            AssetDatabase.SaveAssets(); // unsaved assets can be unloaded by the scene switch below
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(ScenePath);
            AssetDatabase.CopyAsset(HeatScene, ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var canvas = GameObject.Find("Canvas - Main Menu").transform;
            canvas.name = "Canvas - SHIFT RUSH";

            var showroom = BuildShowroom();
            var router = new GameObject("Menu Router").AddComponent<MenuRouter>();
            router.transform.SetParent(canvas, false);
            router.transform.SetSiblingIndex(1);
            var menuSounds = BuildMenuSounds(canvas, sounds);

            WireAudio(canvas);
            CleanBackground(canvas);
            RemoveDemoContent(canvas);
            var panels = canvas.Find("Main Content").GetComponent<PanelManager>();
            var home = BuildHomeBroadcast(canvas, router, showroom);
            var garage = BuildGarageBroadcast(canvas, router, showroom);
            var tracks = BuildTracksBroadcast(canvas, router, showroom);
            BuildSettings(canvas, router);
            BuildPanelList(panels, home, garage, tracks, canvas.Find("Main Content/Settings").GetComponent<Animator>());
            var creditsManager = BuildCredits(canvas, credits);
            var quit = BuildQuitWindow(canvas, router);
            BuildSplash(canvas);
            BuildHintBar(canvas);
            
            var so = new SerializedObject(router);
            so.FindProperty("panels").objectReferenceValue = panels;
            so.FindProperty("showroom").objectReferenceValue = showroom;
            so.FindProperty("credits").objectReferenceValue = creditsManager;
            so.FindProperty("quitWindow").objectReferenceValue = quit;
            so.ApplyModifiedPropertiesWithoutUndo();

            ShowcaseSetBuilder.Build(); // the night airstrip behind the car
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            SetBuildScenes();
            Debug.Log("[SHIFT//RUSH] Main menu built: " + ScenePath);
        }

        // ------------------------------------------------------------------ assets

        static void LoadFonts()
        {
            string f = HeatRoot + "Fonts/RobotoCondensed-";
            fontTitle = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(f + "BoldItalic SDF.asset");
            fontBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(f + "Bold SDF.asset");
            fontMedium = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(f + "Medium SDF.asset");
            fontRegular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(f + "Regular SDF.asset");
            fontLight = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(f + "Light SDF.asset");
        }

        /// <summary>Slanted (italic) shapes drawn in code so the look is ours and needs no paid art.</summary>
        static void MakeArt()
        {
            Directory.CreateDirectory(UIArt);
            slant = SaveSprite("T_Slant", 160, 64, (x, y, w, h) => Parallelogram(x, y, w, h, 22f, 0f), new Vector4(26, 0, 26, 0));
            slantFrame = SaveSprite("T_SlantFrame", 160, 96, (x, y, w, h) => Parallelogram(x, y, w, h, 22f, 3f), new Vector4(28, 6, 28, 6));
            chevron = SaveSprite("T_Chevron", 64, 96, Chevron, Vector4.zero);
            fadeLeft = SaveSprite("T_FadeLeft", 256, 4, (x, y, w, h) => Mathf.SmoothStep(1f, 0f, x / w), Vector4.zero);
            // Mask for the track hero art: a wide slanted band whose edges melt away.
            softBand = SaveSprite("T_SoftBand", 512, 256, (x, y, w, h) =>
            {
                float lean = 150f, soft = 70f, t = y / h;
                float left = lean * t, right = w - lean + lean * t;
                float d = Mathf.Min(x - left, right - x);
                float v = Mathf.Min(y, h - y);
                return Mathf.SmoothStep(0f, 1f, d / soft) * Mathf.SmoothStep(0f, 1f, v / (soft * 0.6f));
            }, Vector4.zero);
            var rng = new System.Random(7);
            var lines = new List<Vector4>();
            for (int i = 0; i < 70; i++)
                lines.Add(new Vector4(rng.Next(0, 1024), rng.Next(2, 254), rng.Next(40, 380), (float)rng.NextDouble() * 0.8f + 0.2f));
            streaks = SaveSprite("T_Streaks", 1024, 256, (x, y, w, h) =>
            {
                float a = 0f;
                foreach (var l in lines)
                {
                    if (Mathf.Abs(y - l.y) > 1.2f) continue;
                    float dx = Mathf.Repeat(x - l.x, w);
                    if (dx < l.z) a = Mathf.Max(a, l.w * (1f - dx / l.z) * (1f - Mathf.Abs(y - l.y) / 1.2f));
                }
                return a;
            }, Vector4.zero, TextureWrapMode.Repeat);
        }

        static float Parallelogram(float x, float y, float w, float h, float lean, float outline)
        {
            float t = y / h;
            float left = lean * t, right = w - lean + lean * t;
            float d = Mathf.Min(x - left, right - x, y, h - 1 - y);
            float inside = Mathf.Clamp01(d + 0.5f);
            if (outline <= 0f) return inside;
            float inner = Mathf.Clamp01(d - outline + 0.5f);
            return inside - inner;
        }

        static float Chevron(float x, float y, float w, float h)
        {
            // A right-pointing chevron, 9 px thick.
            float cy = h * 0.5f;
            float target = 12f + (w - 24f) * (1f - Mathf.Abs(y - cy) / (h * 0.42f));
            if (Mathf.Abs(y - cy) > h * 0.42f) return 0f;
            return Mathf.Clamp01(5f - Mathf.Abs(x - target) + 0.5f);
        }

        static Sprite SaveSprite(string name, int w, int h, System.Func<float, float, float, float, float> alpha, Vector4 border, TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            string path = UIArt + name + ".png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(x + 0.5f, y + 0.5f, w, h)) * 255));
            tex.SetPixels32(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.wrapMode = wrap;
            imp.spriteBorder = border;
            var settings = new TextureImporterSettings();
            imp.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            imp.SetTextureSettings(settings);
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        class Sounds { public AudioClip shift, scrollSoft, hover, select, back, confirm, tick; }

        static Sounds LoadSounds()
        {
            System.Func<string, AudioClip> c = p => AssetDatabase.LoadAssetAtPath<AudioClip>(SfxRoot + p);
            return new Sounds
            {
                shift = c("Repair/repair_77.wav"),
                scrollSoft = c("Buttons/button_press_452.wav"),
                hover = c("Buttons/button_press_741.wav"),
                select = c("Buttons/button_press_453.wav"),
                back = c("Cancel/cancel_200.wav"),
                confirm = c("Ok/ok_104.wav"),
                tick = c("Sliders/slide_15.wav"),
            };
        }

        static void StyleHeat(Sounds s)
        {
            var ui = AssetDatabase.LoadAssetAtPath<UIManager>(HeatRoot + "Resources/Heat UI Manager.asset");
            Undo.RecordObject(ui, "Style Heat");
            // The pirate stream theme: signal pink on night, Big Shoulders for headings, Barlow for the rest.
            ui.accentColor = Pink;
            ui.accentColorInvert = Night;
            ui.primaryColor = Paper;
            ui.secondaryColor = Night;
            ui.negativeColor = Live;
            ui.backgroundColor = new Color32(16, 14, 25, 255);
            ui.fontBold = bsBold;
            ui.fontSemiBold = barlowBold;
            ui.fontMedium = barlowSemi;
            ui.fontRegular = barlowMedium;
            ui.fontLight = barlowMedium;
            ui.hoverSound = s.hover;
            ui.clickSound = s.select;
            ui.enableSplashScreen = true;
            ui.showSplashScreenOnce = true;
            ui.pakType = UIManager.PressAnyKeyTextType.Default; // "Default" is the one that reads pakText
            ui.pakText = "Press {Any Key} To Go Live";
            EditorUtility.SetDirty(ui);
            AssetDatabase.SaveAssetIfDirty(ui);
        }

        static RaceCatalog MakeCatalog()
        {
            const string path = "Assets/_ShiftRush/Resources/RaceCatalog.asset";
            var cat = AssetDatabase.LoadAssetAtPath<RaceCatalog>(path);
            if (cat != null) return cat; // keep the user's edits
            cat = ScriptableObject.CreateInstance<RaceCatalog>();
            cat.cars.Add(new RaceCatalog.Car
            {
                id = "zephyr",
                displayName = "Zephyr",
                tagline = "Light, twitchy, loves a jump.",
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_ShiftRush/Prefabs/Vehicles/Car_Zephyr.prefab"),
                topSpeed = 7, acceleration = 6, handling = 7, boost = 5,
                accent = Magenta,
            });
            // Circuits are added by SHIFT RUSH > Tracks > Capture Hero Shots. TestArena is a dev scene, not on the list.
            AssetDatabase.CreateAsset(cat, path);
            return cat;
        }

        static CreditsPreset MakeCreditsPreset()
        {
            const string path = "Assets/_ShiftRush/Resources/SR_Credits.asset";
            var preset = AssetDatabase.LoadAssetAtPath<CreditsPreset>(path);
            if (preset == null)
            {
                preset = ScriptableObject.CreateInstance<CreditsPreset>();
                AssetDatabase.CreateAsset(preset, path);
            }
            preset.credits.Clear();
            preset.mentions.Clear();
            System.Action<string, string[]> add = (h, items) =>
                preset.credits.Add(new CreditsPreset.CreditsSection { headerTitle = h, headerTitleKey = "", items = new List<string>(items) });
            add("Razorbill Studio", new[] { "Design, code and level design" });
            add("Concept & Inspiration", new[] { "Jennifer Ugwoke" });
            add("Made for", new[] { "GitHub Game Off 2026", "Theme: SHIFT" });
            add("Built with", new[] { "Unity 6 (URP)" });
            add("Third-party packs", new[]
            {
                "Realistic Car Controller Pro (BoneCracker Games)",
                "ARCADE: Ultimate Vehicles Pack",
                "Spline Architect",
                "POLYGON Alpine Mountain (Synty)",
                "KROMA: Atmosphere Toolkit (FayLabs)",
                "MicroVerse (Jason Booth)",
                "Heat: Complete Modern UI (Michsky)",
                "UI SFX Mega Pack",
            });
            add("Music", new[]
            {
                "\"Chosen One\" by OctoSound (Pixabay)",
                "\"Background Music\" by Andrii Poradovskyi (Pixabay)",
                "\"Trap Dynamic Brass Thunder Power\" by alexzavesa (Pixabay)",
                "\"Dance Funk\" by roybushband (Pixabay)",
                "\"Bricks\" by Soundroll (Uppbeat, license HT0NEZRASPO4VVV7)",
                "\"Dodgin' Bullets\" by Bosnow (Uppbeat, license H0XZQHXDFK9YUU62)",
                "\"Furious\" by FASS (Uppbeat, license GQHZBDC576X1ZPK5)",
            });
            add("Controller graphics", new[] { "Zacksly (CC BY 3.0)" });
            preset.mentions.Add(new CreditsPreset.MentionSection
            {
                ID = "thanks",
                description = "Thanks for playing GRIP PRO: CHANNEL 13.",
                descriptionKey = "",
            });
            EditorUtility.SetDirty(preset);
            return preset;
        }

        // ------------------------------------------------------------------ loading screen

        static void MakeLoadingScreen()
        {
            // Channel 13 cutting to the next event: night ground, the track photo in a melted band, a big title
            // with the cyan echo, "connecting" chip, a flat pink bar and a tip from the crew.
            const string path = "Assets/_ShiftRush/Resources/SR_LoadingScreen.prefab";
            var root = new GameObject("SR_LoadingScreen", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            var group = root.AddComponent<CanvasGroup>();
            var loader = root.AddComponent<SceneLoader>();

            var bg = Img(root.transform, "Background", null, Night);
            Stretch(bg.rectTransform);

            var heroBand = new GameObject("Hero Band", typeof(RectTransform)).GetComponent<RectTransform>();
            heroBand.SetParent(root.transform, false);
            Place(heroBand, new Vector2(0.25f, 0.2f), new Vector2(1.08f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var loadMask = heroBand.gameObject.AddComponent<AMS.UI.SoftMask.UISoftMask>();
            loadMask.mask = softBand;
            loadMask.fallOff = 0.6f;
            var loadHero = Img(heroBand, "Hero", null, new Color(0.78f, 0.76f, 0.88f, 0.8f));
            loadHero.raycastTarget = false;
            Stretch(loadHero.rectTransform);
            var lfit = loadHero.gameObject.AddComponent<AspectRatioFitter>();
            lfit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; lfit.aspectRatio = 16f / 9f;
            loadHero.enabled = false;

            // Chips, top left: LIVE and the channel cutting over.
            var chips = new GameObject("Chips", typeof(RectTransform)).GetComponent<RectTransform>();
            chips.SetParent(root.transform, false);
            Place(chips, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(66, -54), new Vector2(900, 40));
            var hl = chips.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleLeft; hl.spacing = 12;
            hl.childControlWidth = hl.childControlHeight = true; hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            var live = Chip(chips, "Live", Live, "LIVE", monoBold, 17, Color.white, true);
            var dot = Img(live, "Dot", AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"), Color.white);
            dot.raycastTarget = false; dot.transform.SetAsFirstSibling();
            var dle = dot.gameObject.AddComponent<LayoutElement>(); dle.preferredWidth = dle.preferredHeight = 11;
            dot.gameObject.AddComponent<LivePulse>();
            Chip(chips, "Cut", new Color(Night.r, Night.g, Night.b, 0.85f), "CH 13  //  CUTTING TO THE NEXT EVENT", monoMedium, 17, Mist, false);

            // The two logo stripes slide while loading (SceneLoader moves this group).
            var stripes = new GameObject("Stripes", typeof(RectTransform)).GetComponent<RectTransform>();
            stripes.SetParent(root.transform, false);
            Place(stripes, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(66, 172), new Vector2(300, 24));
            var sp = Img(stripes, "Pink", null, Pink); sp.raycastTarget = false;
            Place(sp.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(288, 6));
            var sc = Img(stripes, "Cyan", null, Signal); sc.raycastTarget = false;
            Place(sc.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -13), new Vector2(216, 6));

            var echo = Txt(root.transform, "Title Echo", "TRACK", bsBlack, 160, Signal, TextAlignmentOptions.Left);
            Place(echo.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0.5f), new Vector2(84, 52), new Vector2(-220, 190));
            var title = Txt(root.transform, "Title", "TRACK", bsBlack, 160, Paper, TextAlignmentOptions.Left);
            Place(title.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0.5f), new Vector2(66, 60), new Vector2(-220, 190));
            echo.gameObject.AddComponent<TextEcho>().source = title; // the echo follows whatever the title says
            var sub = Txt(root.transform, "Subtitle", "ZEPHYR", monoBold, 22, Pink, TextAlignmentOptions.Left);
            sub.characterSpacing = 8;
            Place(sub.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0.5f), new Vector2(70, -54), new Vector2(-220, 40));

            var track = Img(root.transform, "Bar Track", null, new Color32(42, 37, 64, 255));
            Place(track.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(66, 110), new Vector2(-132, 10));
            var fill = Img(track.transform, "Fill", null, Pink);
            Stretch(fill.rectTransform);

            var pct = Txt(root.transform, "Percent", "00%", bsBlack, 56, Paper, TextAlignmentOptions.Right);
            Place(pct.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-66, 130), new Vector2(300, 64));
            var tipTag = Txt(root.transform, "Tip Tag", "FROM THE CREW", monoBold, 15, Dim, TextAlignmentOptions.Left);
            tipTag.characterSpacing = 8;
            Place(tipTag.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(66, 196), new Vector2(600, 24));
            var tip = Txt(root.transform, "Tip", "", barlowSemi, 26, Mist, TextAlignmentOptions.Left);
            Place(tip.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(66, 140), new Vector2(-560, 56));

            var so = new SerializedObject(loader);
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("subtitle").objectReferenceValue = sub;
            so.FindProperty("tip").objectReferenceValue = tip;
            so.FindProperty("percent").objectReferenceValue = pct;
            so.FindProperty("fill").objectReferenceValue = fill;
            so.FindProperty("streaks").objectReferenceValue = null; // the stripes stay still: calm, like the logo
            so.FindProperty("hero").objectReferenceValue = loadHero;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        // ------------------------------------------------------------------ showroom

        static Showroom BuildShowroom()
        {
            var cam = Camera.main;
            var root = new GameObject("Showroom");
            var turntable = new GameObject("Turntable").transform;
            turntable.SetParent(root.transform, false);

            var floorMat = Mat("M_ShowroomFloor", new Color32(34, 34, 44, 255), 0.88f, 0.1f, Color.black);
            var plinthMat = Mat("M_ShowroomPlinth", new Color32(52, 52, 66, 255), 0.75f, 0.5f, Color.black);
            var ringMat = Mat("M_ShowroomRing", Magenta, 0.5f, 0f, Magenta * 2.2f);

            var floor = Prim(PrimitiveType.Cylinder, "Floor", root.transform, new Vector3(0, -0.06f, 0), new Vector3(70, 0.05f, 70), floorMat);
            var plinth = Prim(PrimitiveType.Cylinder, "Plinth", turntable, new Vector3(0, -0.04f, 0), new Vector3(6.4f, 0.04f, 6.4f), plinthMat);
            var ring = Prim(PrimitiveType.Cylinder, "Glow Ring", root.transform, new Vector3(0, -0.055f, 0), new Vector3(7.3f, 0.02f, 7.3f), ringMat);

            // Lights: a soft key from above, magenta and cyan rims behind.
            AddLight(root.transform, "Key", LightType.Spot, new Vector3(0, 7, 1.5f), new Vector3(80, 200, 0), Color.white, 320f, 20f, 62f);
            AddLight(root.transform, "Rim Magenta", LightType.Point, new Vector3(-4.5f, 1.6f, 3.5f), Vector3.zero, Magenta, 40f, 12f, 0f);
            AddLight(root.transform, "Rim Cyan", LightType.Point, new Vector3(4.5f, 1.4f, 3.2f), Vector3.zero, Cyan, 26f, 12f, 0f);
            foreach (var l in Object.FindObjectsByType<UnityEngine.Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) l.intensity = 0.15f;

            // Background streaks rushing past, far behind the car.
            var lines = new GameObject("Rush Lines");
            lines.transform.SetParent(root.transform, false);
            lines.transform.localPosition = new Vector3(0, 2.4f, -18f);
            lines.transform.localRotation = Quaternion.Euler(0, 205f, 0);
            var ps = lines.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.startLifetime = 1.4f; main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(Magenta.r, Magenta.g, Magenta.b, 0.5f), new Color(1, 1, 1, 0.35f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 300;
            main.useUnscaledTime = true;
            var em = ps.emission; em.rateOverTime = 70f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(1, 7, 2); shape.position = new Vector3(-26, 0, 0);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(34f, 46f); vel.y = new ParticleSystem.MinMaxCurve(0f, 0f); vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var r = lines.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.12f; r.lengthScale = 2f;
            r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_ShiftRush/Art/Materials/M_SpeedLine.mat");
            r.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();

            // Camera rig: pivot at the car, camera behind and slightly above, long lens for a product shot.
            var pivot = new GameObject("Camera Pivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.rotation = Quaternion.Euler(4f, 205f, 0f);
            cam.transform.SetParent(pivot, false);
            cam.transform.localPosition = new Vector3(0, 1.1f, -13f);
            cam.transform.localRotation = Quaternion.identity;
            cam.orthographic = false; // Heat's demo camera is orthographic
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(6, 6, 9, 255);
            var data = cam.GetComponent<UniversalAdditionalCameraData>() ?? cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color32(6, 6, 9, 255);
            RenderSettings.fogDensity = 0.045f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color32(18, 18, 26, 255);
            RenderSettings.skybox = null;

            // Glow and grade.
            var vol = new GameObject("Showroom Look").AddComponent<Volume>();
            vol.transform.SetParent(root.transform, false);
            vol.isGlobal = true;
            const string vpPath = "Assets/_ShiftRush/Art/Materials/VP_Showroom.asset";
            AssetDatabase.DeleteAsset(vpPath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, vpPath);
            var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(1.4f); bloom.threshold.Override(0.9f); bloom.scatter.Override(0.65f);
            var vig = profile.Add<Vignette>(true); vig.intensity.Override(0.38f); vig.smoothness.Override(0.5f);
            var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            var ca = profile.Add<ColorAdjustments>(true); ca.contrast.Override(12f); ca.postExposure.Override(0.2f);
            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            vol.sharedProfile = profile;

            var showroom = root.AddComponent<Showroom>();
            var so = new SerializedObject(showroom);
            so.FindProperty("turntable").objectReferenceValue = turntable;
            so.FindProperty("cam").objectReferenceValue = cam;
            so.FindProperty("orbitPivot").objectReferenceValue = pivot;
            so.FindProperty("homeOffset").vector3Value = new Vector3(-2.7f, 0.2f, 0f);
            so.FindProperty("garageOffset").vector3Value = new Vector3(0f, -1.0f, 0.6f);
            var hide = so.FindProperty("hideWhenPosed");
            hide.arraySize = 3;
            hide.GetArrayElementAtIndex(0).objectReferenceValue = floor;
            hide.GetArrayElementAtIndex(1).objectReferenceValue = plinth;
            hide.GetArrayElementAtIndex(2).objectReferenceValue = ring;
            so.ApplyModifiedPropertiesWithoutUndo();
            return showroom;
        }

        static Material Mat(string name, Color color, float smooth, float metal, Color emission)
        {
            string path = "Assets/_ShiftRush/Art/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (emission.maxColorComponent > 0f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static void AddLight(Transform parent, string name, LightType type, Vector3 pos, Vector3 euler, Color color, float intensity, float range, float angle)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            var l = go.AddComponent<UnityEngine.Light>();
            l.type = type; l.color = color; l.intensity = intensity; l.range = range;
            if (type == LightType.Spot) { l.spotAngle = angle; l.shadows = LightShadows.Soft; }
        }

        // ------------------------------------------------------------------ scene plumbing

        static MenuSounds BuildMenuSounds(Transform canvas, Sounds s)
        {
            var go = new GameObject("Menu Sounds");
            go.transform.SetParent(canvas, false);
            go.transform.SetSiblingIndex(2);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.outputAudioMixerGroup = MixerGroup("UI");
            var ms = go.AddComponent<MenuSounds>();
            var so = new SerializedObject(ms);
            so.FindProperty("shift").objectReferenceValue = s.shift;
            so.FindProperty("scrollSoft").objectReferenceValue = s.scrollSoft;
            so.FindProperty("hover").objectReferenceValue = s.hover;
            so.FindProperty("select").objectReferenceValue = s.select;
            so.FindProperty("back").objectReferenceValue = s.back;
            so.FindProperty("confirm").objectReferenceValue = s.confirm;
            so.FindProperty("tick").objectReferenceValue = s.tick;
            so.ApplyModifiedPropertiesWithoutUndo();
            return ms;
        }

        static AudioMixerGroup MixerGroup(string name)
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/_ShiftRush/Resources/SR_Mixer.mixer");
            var g = mixer != null ? mixer.FindMatchingGroups(name) : null;
            return g != null && g.Length > 0 ? g[0] : null;
        }

        static void WireAudio(Transform canvas)
        {
            var am = canvas.GetComponentInChildren<UIManagerAudio>(true);
            var so = new SerializedObject(am);
            so.FindProperty("audioMixer").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/_ShiftRush/Resources/SR_Mixer.mixer");
            so.ApplyModifiedPropertiesWithoutUndo();
            am.GetComponent<AudioSource>().outputAudioMixerGroup = MixerGroup("UI");
            if (am.GetComponent<HeatHoverFade>() == null) am.gameObject.AddComponent<HeatHoverFade>();
        }

        static void CleanBackground(Transform canvas)
        {
            var bg = canvas.Find("Background");
            bg.Find("Image Parent").gameObject.SetActive(false);
            // Keep Heat's filter and shadows, which darken the left side so the menu reads over the car.
            bg.Find("Filter").gameObject.SetActive(false);
            var fade = Img(bg, "Left Fade", fadeLeft, new Color(Ink.r, Ink.g, Ink.b, 0.55f));
            fade.raycastTarget = false;
            Place(fade.rectTransform, new Vector2(0, 0), new Vector2(0.4f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
        }

        static void RemoveDemoContent(Transform canvas)
        {
            var main = canvas.Find("Main Content");
            foreach (var n in new[] { "Chapters Panel", "Achievements", "Top Panel", "Bottom Panel" })
            {
                var t = main.Find(n);
                if (t) Object.DestroyImmediate(t.gameObject);
            }
            var modals = canvas.Find("Modal Windows");
            foreach (var n in new[] { "New Game", "Load Game", "Purchase Window" })
            {
                var t = modals.Find(n);
                if (t) Object.DestroyImmediate(t.gameObject);
            }
            // Separator objects in Heat's demo hierarchy.
            for (int i = main.childCount - 1; i >= 0; i--)
                if (main.GetChild(i).name.StartsWith("---")) Object.DestroyImmediate(main.GetChild(i).gameObject);
        }

        // ------------------------------------------------------------------ home

        static Animator BuildHome(Transform canvas, MenuRouter router, Showroom showroom)
        {
            var panel = canvas.Find("Main Content/Home Panel");
            var content = panel.Find("Content");
            Object.DestroyImmediate(content.Find("Box Container").gameObject);

            // Title block, top left.
            var title = Txt(content, "Title", "SHIFT<color=#FF40D9>//</color>RUSH", fontTitle, 150, Color.white, TextAlignmentOptions.Left);
            title.characterSpacing = -2;
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(120, -110), new Vector2(1100, 170));
            var stripe = Img(content, "Title Stripe", slant, Magenta);
            stripe.type = Image.Type.Sliced;
            Place(stripe.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(126, -96), new Vector2(150, 14));
            var studio = Txt(content, "Studio", "RAZORBILL STUDIO  //  GAME OFF 2026", fontBold, 22, Steel, TextAlignmentOptions.Left);
            studio.characterSpacing = 14;
            Place(studio.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(128, -282), new Vector2(900, 40));

            var band = Img(content, "Title Streaks", streaks, new Color(1, 1, 1, 0.08f));
            band.type = Image.Type.Tiled;
            band.raycastTarget = false;
            Place(band.rectTransform, new Vector2(0, 1), new Vector2(0.6f, 1), new Vector2(0, 1), new Vector2(0, -100), new Vector2(0, 190));
            band.transform.SetAsFirstSibling();

            // Gear list.
            var list = new GameObject("Gears", typeof(RectTransform)).GetComponent<RectTransform>();
            list.SetParent(content, false);
            Place(list, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 1), new Vector2(120, 40), new Vector2(760, 420));

            var selector = Img(list, "Selector", slant, new Color(Magenta.r, Magenta.g, Magenta.b, 0.92f));
            selector.type = Image.Type.Sliced;
            selector.raycastTarget = false;
            Place(selector.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(-24, -48), new Vector2(560, 84));

            var gearMenu = list.gameObject.AddComponent<GearMenu>();
            var gears = new List<(string key, string label, string caption, UnityAction call, bool web)>
            {
                ("1", "RACE", "Pick a car, pick a track", router.OpenGarage, false),
                ("2", "SETTINGS", "Controls, sound, graphics", router.OpenSettings, false),
                ("3", "CREDITS", "Who made this", router.OpenCredits, false),
                ("R", "QUIT", "Back to the desktop", router.AskQuit, true),
            };
            var gso = new SerializedObject(gearMenu);
            var arr = gso.FindProperty("gears");
            arr.arraySize = gears.Count;
            for (int i = 0; i < gears.Count; i++)
            {
                var g = gears[i];
                var row = new GameObject("Gear " + g.key, typeof(RectTransform)).GetComponent<RectTransform>();
                row.SetParent(list, false);
                Place(row, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(0, -48 - i * 100), new Vector2(700, 84));
                var hit = Img(row, "Hit", null, new Color(0, 0, 0, 0));
                Stretch(hit.rectTransform);

                var keyBox = Img(row, "Key Box", slantFrame, new Color(1, 1, 1, 0.35f));
                keyBox.type = Image.Type.Sliced;
                keyBox.raycastTarget = false;
                Place(keyBox.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(84, 62));
                var key = Txt(row, "Key", g.key, fontTitle, 40, Color.white, TextAlignmentOptions.Center);
                Place(key.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(84, 62));
                var label = Txt(row, "Label", g.label, fontTitle, 72, Color.white, TextAlignmentOptions.Left);
                Place(label.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(112, 2), new Vector2(420, 84));
                var cap = Txt(row, "Caption", g.caption, fontMedium, 22, Color.white, TextAlignmentOptions.Left);
                cap.characterSpacing = 6;
                Place(cap.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(540, -4), new Vector2(420, 40));
                cap.alpha = 0f;

                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("key").stringValue = g.key;
                e.FindPropertyRelative("label").stringValue = g.label;
                e.FindPropertyRelative("caption").stringValue = g.caption;
                e.FindPropertyRelative("row").objectReferenceValue = row;
                e.FindPropertyRelative("keyText").objectReferenceValue = key;
                e.FindPropertyRelative("labelText").objectReferenceValue = label;
                e.FindPropertyRelative("captionText").objectReferenceValue = cap;
                e.FindPropertyRelative("hideOnWeb").boolValue = g.web;
            }
            gso.FindProperty("selector").objectReferenceValue = selector.rectTransform;
            gso.FindProperty("visibleGroup").objectReferenceValue = panel.GetComponent<CanvasGroup>();
            gso.FindProperty("showroom").objectReferenceValue = showroom;
            gso.ApplyModifiedPropertiesWithoutUndo();
            for (int i = 0; i < gears.Count; i++) UnityEventTools.AddVoidPersistentListener(gearMenu_Event(gearMenu, i), gears[i].call);

            SetupHotkeys(panel, router, "Quit", router.AskQuit);
            return panel.GetComponent<Animator>();
        }

        static UnityEvent gearMenu_Event(GearMenu menu, int i)
        {
            var field = typeof(GearMenu).GetField("gears", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var list = (List<GearMenu.Gear>)field.GetValue(menu);
            return list[i].onEngage;
        }

        /// <summary>Keeps only Heat's Esc hotkey in a panel, relabelled and pointed at our router.</summary>
        static void SetupHotkeys(Transform panel, MenuRouter router, string label, UnityAction call)
        {
            var hot = panel.Find("Content/Hotkeys");
            if (hot == null) return;
            for (int i = hot.childCount - 1; i >= 0; i--)
            {
                var child = hot.GetChild(i);
                var hk = child.GetComponent<HotkeyEvent>();
                if (hk == null || hk.keyID != "Exit") { Object.DestroyImmediate(child.gameObject); continue; }
                ConfigureHotkey(hk, label, call);
            }
        }

        static void ConfigureHotkey(HotkeyEvent hk, string label, UnityAction call)
        {
            var lo = hk.GetComponent<LocalizedObject>();
            if (lo) Object.DestroyImmediate(lo);
            hk.useLocalization = false;
            hk.hotkeyLabel = label;
            for (int i = hk.onHotkeyPress.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(hk.onHotkeyPress, i);
            UnityEventTools.AddVoidPersistentListener(hk.onHotkeyPress, call);
            foreach (var t in hk.GetComponentsInChildren<TMP_Text>(true)) if (t.name == "Label") t.text = label;
            EditorUtility.SetDirty(hk);
        }

        static Transform CloneHotkeys(Transform fromPanel, Transform toContent, MenuRouter router, string label, UnityAction call)
        {
            var src = fromPanel.Find("Content/Hotkeys");
            var copy = Object.Instantiate(src.gameObject, toContent).transform;
            copy.name = "Hotkeys";
            foreach (var hk in copy.GetComponentsInChildren<HotkeyEvent>(true)) ConfigureHotkey(hk, label, call);
            return copy;
        }

        // ------------------------------------------------------------------ garage

        static Animator BuildGarage(Transform canvas, MenuRouter router, Showroom showroom)
        {
            var panel = RepurposePanel(canvas, "Shop", MenuRouter.Garage);
            var content = panel.Find("Content");
            Header(content, "GARAGE", "STEP 1 / 2   CHOOSE YOUR CAR");

            var nameText = Txt(content, "Car Name", "ZEPHYR", fontTitle, 128, Color.white, TextAlignmentOptions.Left);
            Place(nameText.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(120, 210), new Vector2(900, 150));
            var tag = Txt(content, "Tagline", "", fontMedium, 28, Steel, TextAlignmentOptions.Left);
            Place(tag.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(126, 168), new Vector2(900, 44));
            var counter = Txt(content, "Counter", "01 / 01", fontBold, 26, Steel, TextAlignmentOptions.Left);
            counter.characterSpacing = 10;
            Place(counter.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(128, 360), new Vector2(300, 40));

            // Stats, bottom right.
            var statsRoot = new GameObject("Stats", typeof(RectTransform)).GetComponent<RectTransform>();
            statsRoot.SetParent(content, false);
            Place(statsRoot, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-120, 236), new Vector2(620, 230));
            string[] statNames = { "TOP SPEED", "ACCELERATION", "HANDLING", "NITRO" };
            var bars = new StatBar[4];
            for (int s = 0; s < 4; s++)
            {
                var row = new GameObject(statNames[s], typeof(RectTransform)).GetComponent<RectTransform>();
                row.SetParent(statsRoot, false);
                Place(row, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, -s * 56), new Vector2(0, 48));
                var l = Txt(row, "Label", statNames[s], fontBold, 22, Steel, TextAlignmentOptions.Left);
                l.characterSpacing = 8;
                Place(l.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(220, 0));
                var segs = new Image[10];
                for (int k = 0; k < 10; k++)
                {
                    segs[k] = Img(row, "Seg " + k, slant, new Color(1, 1, 1, 0.12f));
                    segs[k].type = Image.Type.Sliced;
                    segs[k].pixelsPerUnitMultiplier = 4f;
                    Place(segs[k].rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(230 + k * 38, 0), new Vector2(34, 26));
                }
                bars[s] = row.gameObject.AddComponent<StatBar>();
                var bso = new SerializedObject(bars[s]);
                var sp = bso.FindProperty("segments");
                sp.arraySize = 10;
                for (int k = 0; k < 10; k++) sp.GetArrayElementAtIndex(k).objectReferenceValue = segs[k];
                bso.ApplyModifiedPropertiesWithoutUndo();
            }

            // Arrows either side of the car, and a select button.
            var prev = ArrowButton(content, "Prev", true);
            var next = ArrowButton(content, "Next", false);
            var confirm = SlantButton(content, "Select", "SELECT", new Vector2(1, 0), new Vector2(-120, 150), new Vector2(330, 64));

            var csp = panel.gameObject.AddComponent<CarSelectPanel>();
            var so = new SerializedObject(csp);
            so.FindProperty("visibleGroup").objectReferenceValue = panel.GetComponent<CanvasGroup>();
            so.FindProperty("showroom").objectReferenceValue = showroom;
            so.FindProperty("carName").objectReferenceValue = nameText;
            so.FindProperty("tagline").objectReferenceValue = tag;
            so.FindProperty("counter").objectReferenceValue = counter;
            var st = so.FindProperty("stats");
            st.arraySize = 4;
            for (int s = 0; s < 4; s++) st.GetArrayElementAtIndex(s).objectReferenceValue = bars[s];
            so.FindProperty("previousButton").objectReferenceValue = prev;
            so.FindProperty("nextButton").objectReferenceValue = next;
            so.FindProperty("confirmButton").objectReferenceValue = confirm;
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddVoidPersistentListener(csp.onConfirm, router.OpenTracks);

            CloneHotkeys(canvas.Find("Main Content/Home Panel"), content, router, "Back", router.Back);
            return panel.GetComponent<Animator>();
        }

        // ------------------------------------------------------------------ tracks

        static Animator BuildTracks(Transform canvas, MenuRouter router, Showroom showroom)
        {
            var panel = RepurposePanel(canvas, "Extras", MenuRouter.Tracks);
            var content = panel.Find("Content");
            Header(content, "TRACKS", "STEP 2 / 2   CHOOSE A TRACK");

            // Selected track's hero shot, soft-masked into a wide slanted band. It lives on its own canvas placed in
            // the showroom camera's space, far behind the car, so the live car sits in front of the track photo.
            var showCam = (Camera)new SerializedObject(showroom).FindProperty("cam").objectReferenceValue;
            var backdropGo = new GameObject("Canvas - Track Backdrop", typeof(RectTransform));
            var backdropCanvas = backdropGo.AddComponent<Canvas>();
            backdropCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            backdropCanvas.worldCamera = showCam;
            backdropCanvas.planeDistance = 18f; // just behind the car (~13 m), so the car draws in front but little floor does
            var backdropScaler = backdropGo.AddComponent<CanvasScaler>();
            backdropScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            backdropScaler.referenceResolution = new Vector2(1920, 1080);
            backdropScaler.matchWidthOrHeight = 0.5f;
            var backdropGroup = backdropGo.AddComponent<CanvasGroup>();
            backdropGroup.alpha = 0f; backdropGroup.interactable = false; backdropGroup.blocksRaycasts = false;
            var band = new GameObject("Hero Band", typeof(RectTransform)).GetComponent<RectTransform>();
            band.SetParent(backdropGo.transform, false);
            Place(band, new Vector2(-0.02f, 0.1f), new Vector2(1.02f, 0.96f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var bandMask = band.gameObject.AddComponent<AMS.UI.SoftMask.UISoftMask>();
            bandMask.mask = softBand;
            bandMask.fallOff = 0.6f;
            var heroImg = Img(band, "Hero", null, new Color(0.82f, 0.82f, 0.86f, 1f)); // under bloom threshold, keeps text readable
            heroImg.raycastTarget = false;
            Stretch(heroImg.rectTransform);
            var heroFit = heroImg.gameObject.AddComponent<AspectRatioFitter>();
            heroFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; heroFit.aspectRatio = 16f / 9f;
            var heroStreaks = Img(band, "Streaks", streaks, new Color(Magenta.r, Magenta.g, Magenta.b, 0.18f));
            heroStreaks.type = Image.Type.Tiled; heroStreaks.raycastTarget = false;
            Stretch(heroStreaks.rectTransform);

            var title = Txt(content, "Track Title", "TEST ARENA", fontTitle, 96, Color.white, TextAlignmentOptions.Left);
            Place(title.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(120, 120), new Vector2(1000, 120));
            var desc = Txt(content, "Track Description", "", fontRegular, 28, Steel, TextAlignmentOptions.TopLeft);
            Place(desc.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 1), new Vector2(126, 50), new Vector2(760, 100));
            var details = Txt(content, "Track Details", "", fontBold, 26, Magenta, TextAlignmentOptions.Left);
            details.characterSpacing = 10;
            Place(details.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(128, 200), new Vector2(400, 40));

            // Right column: the car you picked in the garage (a picture, not the live model), then the circuit.
            var carBox = SidePanel(content, "Your Car", "YOUR CAR", -110f, 220f);
            var carPic = Img(carBox, "Car Picture", null, Color.white);
            carPic.raycastTarget = false; carPic.preserveAspect = true;
            Place(carPic.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(-40, -44));
            var carName = Txt(carBox, "Car Name", "ZEPHYR", fontTitle, 34, Color.white, TextAlignmentOptions.BottomRight);
            Place(carName.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-34, 12), new Vector2(-60, 44));
            var mapBox = SidePanel(content, "Circuit", "CIRCUIT", -360f, 280f);
            var mapImg = Img(mapBox, "Circuit Line", null, new Color(0.66f, 0.68f, 0.74f, 0.75f)); // grey line
            mapImg.raycastTarget = false; mapImg.preserveAspect = true;
            Place(mapImg.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -12), new Vector2(-110, -84));

            // Difficulty, under the circuit: arrows round the level's name, three pips, and what it means.
            var diffBox = SidePanel(content, "Difficulty", "DIFFICULTY", -660f, 170f);
            var diffValue = Txt(diffBox, "Value", "RUSH", fontTitle, 46, Magenta, TextAlignmentOptions.Center);
            Place(diffValue.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -62), new Vector2(300, 56));
            Button DiffArrow(string name, string glyph, float x)
            {
                var a = Txt(diffBox, name, glyph, fontTitle, 44, Color.white, TextAlignmentOptions.Center);
                a.raycastTarget = true;
                Place(a.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(x, -62), new Vector2(60, 56));
                var b = a.gameObject.AddComponent<Button>();
                b.targetGraphic = a;
                var cb = b.colors; cb.normalColor = Color.white; cb.highlightedColor = Magenta; cb.disabledColor = new Color(1, 1, 1, 0.15f); b.colors = cb;
                return b;
            }
            var diffDown = DiffArrow("Down", "\u2039", -170f);
            var diffUp = DiffArrow("Up", "\u203A", 170f);
            var diffPips = new Image[3];
            for (int k = 0; k < 3; k++)
            {
                diffPips[k] = Img(diffBox, "Pip " + (k + 1), slant, new Color(1, 1, 1, 0.15f));
                diffPips[k].type = Image.Type.Sliced; diffPips[k].pixelsPerUnitMultiplier = 4f; diffPips[k].raycastTarget = false;
                Place(diffPips[k].rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2((k - 1) * 62, -100), new Vector2(54, 8));
            }
            var diffBlurb = Txt(diffBox, "Blurb", "", fontBold, 17, Steel, TextAlignmentOptions.Center);
            diffBlurb.enableAutoSizing = true; diffBlurb.fontSizeMin = 13; diffBlurb.fontSizeMax = 17;
            Place(diffBlurb.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(-70, 44));

            var row = new GameObject("Cards", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            Place(row, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(120, 190), new Vector2(-240, 240));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 28; layout.childControlWidth = false; layout.childControlHeight = false;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.LowerLeft;

            // Card template.
            var card = new GameObject("Card Template", typeof(RectTransform)).GetComponent<RectTransform>();
            card.SetParent(row, false);
            card.sizeDelta = new Vector2(400, 220);
            var hit = Img(card, "Hit", null, new Color(0, 0, 0, 0));
            Stretch(hit.rectTransform);
            var body = new GameObject("Body", typeof(RectTransform)).GetComponent<RectTransform>();
            body.SetParent(card, false);
            Stretch(body);
            var fillBg = Img(body, "Fill", slant, new Color(Panel.r, Panel.g, Panel.b, 0.85f));
            fillBg.type = Image.Type.Sliced; fillBg.raycastTarget = false;
            Stretch(fillBg.rectTransform);
            var cardStreaks = Img(body, "Streaks", streaks, new Color(1, 1, 1, 0.07f));
            cardStreaks.type = Image.Type.Tiled; cardStreaks.raycastTarget = false;
            Stretch(cardStreaks.rectTransform);
            cardStreaks.rectTransform.offsetMin = new Vector2(30, 8); cardStreaks.rectTransform.offsetMax = new Vector2(-30, -8);
            // Track photo, soft-masked to the card's slant.
            var photoMask = new GameObject("Photo Mask", typeof(RectTransform)).GetComponent<RectTransform>();
            photoMask.SetParent(body, false);
            Stretch(photoMask);
            photoMask.offsetMin = new Vector2(4, 4); photoMask.offsetMax = new Vector2(-4, -4);
            var cardMask = photoMask.gameObject.AddComponent<AMS.UI.SoftMask.UISoftMask>();
            cardMask.mask = slant;
            cardMask.fallOff = 0.25f;
            var cmso = new SerializedObject(cardMask);
            var uvMode = cmso.FindProperty("m_MaskUV"); if (uvMode != null) { uvMode.enumValueIndex = 1; cmso.ApplyModifiedPropertiesWithoutUndo(); }
            var thumb = Img(photoMask, "Thumbnail", null, new Color(0.7f, 0.7f, 0.75f, 0.9f)); // dimmed so the name reads
            thumb.raycastTarget = false;
            Stretch(thumb.rectTransform);
            var frame = Img(body, "Frame", slantFrame, new Color(1, 1, 1, 0.15f));
            frame.type = Image.Type.Sliced; frame.raycastTarget = false;
            Stretch(frame.rectTransform);
            var num = Txt(body, "Number", "01", fontTitle, 64, new Color(1, 1, 1, 0.2f), TextAlignmentOptions.TopRight);
            Place(num.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-34, -10), new Vector2(160, 80));
            var tname = Txt(body, "Name", "TRACK", fontTitle, 40, Color.white, TextAlignmentOptions.BottomLeft);
            Place(tname.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(36, 56), new Vector2(-60, 56));
            var tlen = Txt(body, "Length", "0.0 KM", fontBold, 22, Steel, TextAlignmentOptions.BottomLeft);
            tlen.characterSpacing = 8;
            Place(tlen.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(30, 22), new Vector2(-60, 34));
            var tc = card.gameObject.AddComponent<TrackCard>();
            var cso = new SerializedObject(tc);
            cso.FindProperty("number").objectReferenceValue = num;
            cso.FindProperty("trackName").objectReferenceValue = tname;
            cso.FindProperty("length").objectReferenceValue = tlen;
            cso.FindProperty("thumbnail").objectReferenceValue = thumb;
            cso.FindProperty("frame").objectReferenceValue = frame;
            cso.FindProperty("body").objectReferenceValue = body;
            cso.FindProperty("activeFrame").colorValue = Magenta;
            cso.ApplyModifiedPropertiesWithoutUndo();
            card.gameObject.SetActive(false);

            var tsp = panel.gameObject.AddComponent<TrackSelectPanel>();
            var so = new SerializedObject(tsp);
            so.FindProperty("visibleGroup").objectReferenceValue = panel.GetComponent<CanvasGroup>();
            so.FindProperty("cardParent").objectReferenceValue = row;
            so.FindProperty("cardTemplate").objectReferenceValue = tc;
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("description").objectReferenceValue = desc;
            so.FindProperty("details").objectReferenceValue = details;
            so.FindProperty("hero").objectReferenceValue = heroImg;
            so.FindProperty("backdrop").objectReferenceValue = backdropGroup;
            so.FindProperty("carPicture").objectReferenceValue = carPic;
            so.FindProperty("carName").objectReferenceValue = carName;
            so.FindProperty("map").objectReferenceValue = mapImg;
            so.FindProperty("difficultyValue").objectReferenceValue = diffValue;
            so.FindProperty("difficultyBlurb").objectReferenceValue = diffBlurb;
            so.FindProperty("difficultyDown").objectReferenceValue = diffDown;
            so.FindProperty("difficultyUp").objectReferenceValue = diffUp;
            var pipsProp = so.FindProperty("difficultyPips");
            pipsProp.arraySize = diffPips.Length;
            for (int k = 0; k < diffPips.Length; k++) pipsProp.GetArrayElementAtIndex(k).objectReferenceValue = diffPips[k];
            so.ApplyModifiedPropertiesWithoutUndo();

            CloneHotkeys(canvas.Find("Main Content/Home Panel"), content, router, "Back", router.Back);
            return panel.GetComponent<Animator>();
        }

        /// <summary>A slanted panel down the right of the track select, with a small magenta tag in its corner.</summary>
        static RectTransform SidePanel(Transform content, string name, string tag, float top, float height)
        {
            var box = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            box.SetParent(content, false);
            Place(box, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, top), new Vector2(480, height));
            var bg = Img(box, "Bg", slant, new Color(Panel.r, Panel.g, Panel.b, 0.72f));
            bg.type = Image.Type.Sliced; bg.raycastTarget = false;
            Stretch(bg.rectTransform);
            var lines = Img(box, "Streaks", streaks, new Color(1, 1, 1, 0.05f));
            lines.type = Image.Type.Tiled; lines.raycastTarget = false;
            Stretch(lines.rectTransform);
            lines.rectTransform.offsetMin = new Vector2(30, 8); lines.rectTransform.offsetMax = new Vector2(-30, -8);
            var frame = Img(box, "Frame", slantFrame, new Color(1, 1, 1, 0.15f));
            frame.type = Image.Type.Sliced; frame.raycastTarget = false;
            Stretch(frame.rectTransform);
            var stripe = Img(box, "Tag Stripe", slant, Magenta);
            stripe.type = Image.Type.Sliced; stripe.raycastTarget = false; stripe.pixelsPerUnitMultiplier = 4f;
            Place(stripe.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -20), new Vector2(46, 8));
            var label = Txt(box, "Tag", tag, fontBold, 18, Magenta, TextAlignmentOptions.Left);
            label.characterSpacing = 10; label.raycastTarget = false;
            Place(label.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(98, -12), new Vector2(300, 24));
            return box;
        }

        /// <summary>Takes a Heat demo panel (for its show/hide animation), empties it and renames it.</summary>
        static Transform RepurposePanel(Transform canvas, string heatName, string newName)
        {
            var panel = canvas.Find("Main Content/" + heatName);
            panel.name = newName;
            var content = panel.Find("Content");
            for (int i = content.childCount - 1; i >= 0; i--) Object.DestroyImmediate(content.GetChild(i).gameObject);
            foreach (var lo in panel.GetComponents<LocalizedObject>()) Object.DestroyImmediate(lo);
            return panel;
        }

        static void Header(Transform content, string title, string step)
        {
            var t = Txt(content, "Header", title, fontTitle, 96, Color.white, TextAlignmentOptions.Left);
            Place(t.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(120, -96), new Vector2(900, 120));
            var stripe = Img(content, "Header Stripe", slant, Magenta);
            stripe.type = Image.Type.Sliced;
            Place(stripe.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(126, -84), new Vector2(110, 12));
            var s = Txt(content, "Step", step, fontBold, 22, Steel, TextAlignmentOptions.Left);
            s.characterSpacing = 12;
            Place(s.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(128, -214), new Vector2(900, 40));
        }

        static Button ArrowButton(Transform parent, string name, bool left)
        {
            var img = Img(parent, name, chevron, new Color(1, 1, 1, 0.6f));
            Place(img.rectTransform, new Vector2(left ? 0.3f : 0.7f, 0.55f), new Vector2(left ? 0.3f : 0.7f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56, 84));
            if (left) img.rectTransform.localScale = new Vector3(-1, 1, 1);
            var b = img.gameObject.AddComponent<Button>();
            var cb = b.colors; cb.highlightedColor = Magenta; cb.pressedColor = Color.white; cb.disabledColor = new Color(1, 1, 1, 0.15f); b.colors = cb;
            return b;
        }

        static Button SlantButton(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var img = Img(parent, name, slant, Magenta);
            img.type = Image.Type.Sliced;
            Place(img.rectTransform, anchor, anchor, anchor, pos, size);
            var t = Txt(img.transform, "Label", label, fontTitle, 30, Ink, TextAlignmentOptions.Center);
            t.characterSpacing = 6;
            Stretch(t.rectTransform);
            var b = img.gameObject.AddComponent<Button>();
            var cb = b.colors; cb.highlightedColor = new Color(1, 0.75f, 0.95f); cb.pressedColor = Color.white; b.colors = cb;
            return b;
        }

        // ------------------------------------------------------------------ settings

        static void BuildSettings(Transform canvas, MenuRouter router)
        {
            var settings = canvas.Find("Main Content/Settings");
            foreach (var lo in settings.GetComponents<LocalizedObject>()) Object.DestroyImmediate(lo);
            var pc = settings.Find("Content/Panel Content");
            var lists = pc.Find("Panels");

            // Category tabs.
            RenameTab(pc.Find("Categories/General"), "GAMEPLAY");

            // Gameplay (Heat's "General" list).
            var gen = Deep(lists.Find("General"), "List");
            var items = FindRows(gen);
            ConfigureSelector(items["Language"], "Speed Effects", GameSettings.SpeedFxKey, GameSettings.SpeedFxLabels, 2,
                "How hard the camera pulls back, widens and shakes at speed, with streaks and lens fringing.");
            ConfigureSwitch(items["Enable Hints"], "Camera Shake", GameSettings.CameraShakeKey, true,
                "A light rumble on the chase camera at high speed and on nitro.");
            ConfigureSwitch(items["Enable Subtitles"], "Reduced Motion", GameSettings.ReducedMotionKey, false,
                "Removes speed streaks, lens distortion and shake, and halves the camera pull-back. Kinder if fast motion makes you queasy.");
            ConfigureSelector(items["Subtitle Scale"], "Speed Units", GameSettings.UnitsKey, new[] { "KM/H", "MPH" }, 0,
                "How speed is shown on the dashboard.");
            SetRowTitle(items["UI Scale"], "Menu Scale");
            SetDescription(items["UI Scale"], "Menu Scale", "Makes the menus bigger or smaller.");
            var mini = Object.Instantiate(items["Subtitle Scale"].gameObject, items["Subtitle Scale"].parent).transform;
            ConfigureSelector(mini, "Minimap", GameSettings.MinimapKey, GameSettings.MinimapLabels, 0,
                "Local shows a faded patch of track around your car that turns with you. Full shows the whole circuit.");
            var auto = Object.Instantiate(items["Enable Hints"].gameObject, items["Enable Hints"].parent).transform;
            ConfigureSwitch(auto, "Auto Accelerate", GameSettings.AutoAccelerateKey, false,
                "The car keeps the throttle down for you. You steer, brake and fire nitro. Braking still slows you and backs you up.");
            var difficulty = Object.Instantiate(items["Subtitle Scale"].gameObject, items["Subtitle Scale"].parent).transform;
            ConfigureSelector(difficulty, "Difficulty", Difficulty.Key, Difficulty.Labels, Difficulty.Default,
                "CRUISE: gentler rivals, a wider SHIFT window and more nitro, start 3rd. RUSH: the race as meant. OVERDRIVE: faster rivals who never wait, a tight SHIFT window, a SHIFT lands you P2-P4, start last. Also on the track select screen.");
            var tutorial = Object.Instantiate(items["Enable Hints"].gameObject, items["Enable Hints"].parent).transform;
            ConfigureSwitch(tutorial, "Tutorial", GameSettings.TutorialKey, true,
                "Coach hints in your first races: driving, SHIFT ramps, the timing, the worlds and nitro. For your first race only: they turn themselves off after it. Switch back on to see them again.");
            items["Speed Effects"] = items["Language"];
            // Order: auto accelerate, tutorial, difficulty, speed effects, shake, reduced motion, units, minimap, menu scale.
            auto.SetSiblingIndex(0);
            tutorial.SetSiblingIndex(1);
            difficulty.SetSiblingIndex(2);
            items["Language"].SetSiblingIndex(3);
            items["Enable Hints"].SetSiblingIndex(4);
            items["Enable Subtitles"].SetSiblingIndex(5);
            items["Subtitle Scale"].SetSiblingIndex(6);
            mini.SetSiblingIndex(7);
            items["UI Scale"].SetSiblingIndex(8);
            HideSectionHeaders(gen);

            // Controls.
            var ctl = Deep(lists.Find("Controls"), "List");
            var crows = FindRows(ctl);
            foreach (var n in new[] { "Camera Sensitivity", "Sprint Mode", "Reverse Look" }) if (crows.ContainsKey(n)) Object.DestroyImmediate(crows[n].gameObject);
            HideSectionHeaders(ctl);
            var bindingRows = new List<Transform>();
            foreach (var kv in crows) if (kv.Key.StartsWith("Settings Item (Binding)") && kv.Value != null) bindingRows.Add(kv.Value);
            bindingRows.Sort((a, b) => a.GetSiblingIndex().CompareTo(b.GetSiblingIndex()));
            var binds = new (string label, string action, string comp, string part, string desc)[]
            {
                ("Accelerate", "Throttle", "wasd", "positive", "Throttle."),
                ("Brake / Reverse", "Brake", "wasd", "positive", "Brake, then reverse when stopped."),
                ("Steer Left", "Steering", "wasd", "negative", ""),
                ("Steer Right", "Steering", "wasd", "positive", ""),
                ("Handbrake", "Handbrake", "keyboard", "positive", "Pull it to start a drift."),
                ("Nitro", "NOS", "keyboard", "positive", "Hold for a burst of speed while the tank lasts."),
                ("Change Camera", "Change Camera", "", "", "Cycles the camera views."),
                ("Look Back", "Look Back", "", "", "Hold to look behind you."),
            };
            BindingRow resetOwner = null;
            for (int i = 0; i < bindingRows.Count; i++)
            {
                var row = bindingRows[i];
                if (i >= binds.Length) { Object.DestroyImmediate(row.gameObject); continue; }
                var b = binds[i];
                row.name = b.label;
                SetRowTitle(row, b.label);
                SetDescription(row, b.label, (b.desc + " Click, then press the new key. Esc cancels.").Trim());
                var primary = row.Find("Binding 1").GetComponent<ButtonManager>();
                var secondary = row.Find("Binding 2");
                if (secondary) secondary.gameObject.SetActive(false);
                primary.useLocalization = false;
                var br = row.gameObject.AddComponent<BindingRow>();
                br.Configure(b.action, b.comp, b.part, primary); // the click is hooked up at runtime (BindingRow.Awake)
                resetOwner = resetOwner ?? br;
            }
            if (bindingRows.Count > 0 && resetOwner != null)
            {
                var reset = Object.Instantiate(bindingRows[0].gameObject, bindingRows[0].parent).transform;
                reset.SetSiblingIndex(bindingRows[bindingRows.Count - 1].GetSiblingIndex() + 1);
                reset.name = "Reset Controls";
                SetRowTitle(reset, "Reset Controls");
                SetDescription(reset, "Reset Controls", "Puts every key back to the SHIFT//RUSH defaults.");
                var btn = reset.Find("Binding 1").GetComponent<ButtonManager>();
                btn.buttonText = "RESET";
                reset.GetComponent<BindingRow>().ConfigureReset(btn); // BindingRow hooks up the click at runtime
            }

            // Audio.
            var aud = FindRows(Deep(lists.Find("Audio"), "List"));
            SetDescription(aud["Master Volume"], "Master Volume", "Everything.");
            SetDescription(aud["Music Volume"], "Music Volume", "Menu and race music.");
            SetRowTitle(aud["SFX Volume"], "Car & Effects");
            SetDescription(aud["SFX Volume"], "Car & Effects", "Engines, tyres, crashes and nitro.");
            SetRowTitle(aud["UI Volume"], "Menu Sounds");
            SetDescription(aud["UI Volume"], "Menu Sounds", "Clicks, shifts and menu effects.");
            // Music: SHUFFLE ALL, OFF or one song on repeat. The songs are read from Resources/Audio/Music when the
            // menu opens (MusicChoiceDropdown), so dropping a file in that folder is all it takes.
            // A drop-down list (cloned from the Visuals → Resolution row) so any number of songs fits.
            var resRow = FindRows(Deep(lists.Find("Visuals"), "List"))["Resolution"];
            var music = Object.Instantiate(resRow.gameObject, aud["Music Volume"].parent).transform;
            music.SetSiblingIndex(aud["Music Volume"].GetSiblingIndex() + 1);
            music.name = "Music";
            SetRowTitle(music, "Music");
            SetDescription(music, "Music", "Shuffle every song, pick one to play on repeat, or turn the music off. Songs come from the Music folder.");
            var musicDd = music.GetComponentInChildren<Michsky.UI.Heat.Dropdown>(true);
            foreach (var lo in musicDd.GetComponentsInChildren<LocalizedObject>(true)) Object.DestroyImmediate(lo);
            musicDd.items.Clear();
            foreach (var label in new[] { "SHUFFLE ALL", "OFF" }) musicDd.items.Add(new Michsky.UI.Heat.Dropdown.Item { itemName = label, localizationKey = "" });
            musicDd.saveSelected = false;
            musicDd.enableScrollbar = true;
            musicDd.panelSize = 300f;
            for (int k = musicDd.onValueChanged.GetPersistentEventCount() - 1; k >= 0; k--) UnityEventTools.RemovePersistentListener(musicDd.onValueChanged, k);
            var ddRt = (RectTransform)musicDd.transform;
            ddRt.sizeDelta = new Vector2(420f, ddRt.sizeDelta.y);
            if (musicDd.headerText != null) { musicDd.headerText.enableAutoSizing = false; musicDd.headerText.overflowMode = TextOverflowModes.Ellipsis; }
            musicDd.gameObject.AddComponent<MusicChoiceDropdown>();

            // Menu scroll sound: a selector cloned from the gameplay Speed Units row.
            var scroll = Object.Instantiate(items["Subtitle Scale"].gameObject, aud["UI Volume"].parent).transform;
            scroll.SetSiblingIndex(aud["UI Volume"].GetSiblingIndex() + 1);
            ConfigureSelector(scroll, "Menu Scroll Sound", GameSettings.ScrollSoundKey, GameSettings.ScrollSoundLabels, 0,
                "The sound when you move between menu items: a soft tick, a mechanical clunk, or nothing.");
            HideSectionHeaders(Deep(lists.Find("Audio"), "List"));

            // Visuals: add a quality level selector based on the Texture Quality row.
            var vis = Deep(lists.Find("Visuals"), "List");
            var vrows = FindRows(vis);
            if (vrows.ContainsKey("Texture Quality"))
            {
                var q = Object.Instantiate(vrows["Texture Quality"].gameObject, vrows["Texture Quality"].parent).transform;
                q.name = "Graphics Quality";
                q.SetSiblingIndex(0);
                var names = QualitySettings.names;
                var labels = new string[names.Length];
                for (int i = 0; i < names.Length; i++) labels[i] = names[i] == "Mobile" ? "LOW" : names[i] == "PC" ? "HIGH" : names[i].ToUpperInvariant();
                ConfigureSelector(q, "Graphics Quality", QualityBoot.Key, labels, names.Length - 1, "Overall render quality: shadows, lighting and effects. Lower it if the game stutters.");
                var hs = q.GetComponentInChildren<HorizontalSelector>(true);
                var qb = settings.gameObject.AddComponent<QualityBoot>();
                UnityEventTools.AddPersistentListener(hs.onValueChanged, qb.SetLevel);
            }
            // Toon Look: a switch (cloned from Gameplay's) right under Graphics Quality.
            var toon = Object.Instantiate(items["Enable Hints"].gameObject, vis).transform;
            ConfigureSwitch(toon, "Toon Look", GameSettings.ToonLookKey, true,
                "Cel-shaded cars with a coloured rim, and ink outlines round everything. Off for the plain look, or if the game stutters.");
            toon.SetSiblingIndex(1);
            HideSectionHeaders(vis);
            // Applies window mode, frame rate, VSync, textures and filtering as they change (see DisplaySettings).
            if (vis.GetComponent<DisplaySettingsBinder>() == null) vis.gameObject.AddComponent<DisplaySettingsBinder>();

            // Header, and our own default for the description panel (Heat's shows its logo).
            var headEcho = Txt(settings.Find("Content"), "Header Echo", "SETTINGS", bsBlack, 84, Signal, TextAlignmentOptions.Left);
            Place(headEcho.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(66, -46), new Vector2(800, 100));
            var head = Txt(settings.Find("Content"), "Header", "SETTINGS", bsBlack, 84, Paper, TextAlignmentOptions.Left);
            Place(head.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -40), new Vector2(800, 100));
            var sTag = Txt(settings.Find("Content"), "Channel Tag", "CH 13  //  STUDIO DESK", monoMedium, 16, Dim, TextAlignmentOptions.Right);
            sTag.characterSpacing = 6;
            Place(sTag.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-60, -60), new Vector2(600, 26));
            var shot = LoadShowroomShot();
            var sdm = settings.GetComponentInChildren<SettingsDescriptionManager>(true);
            if (sdm != null)
            {
                var dso = new SerializedObject(sdm);
                dso.FindProperty("cover").objectReferenceValue = shot;
                dso.FindProperty("title").stringValue = "SETTINGS";
                dso.FindProperty("description").stringValue = "Point at a setting to see what it does. Everything saves as you change it, so the stream never drops.";
                dso.FindProperty("titleKey").stringValue = "";
                dso.FindProperty("descriptionKey").stringValue = "";
                dso.FindProperty("useLocalization").boolValue = false;
                dso.ApplyModifiedPropertiesWithoutUndo();
                var ci = dso.FindProperty("coverImage").objectReferenceValue as Image;
                if (ci != null) ci.sprite = shot;
            }
            foreach (var d in settings.GetComponentsInChildren<SettingsDescription>(true))
            {
                var o = new SerializedObject(d);
                o.FindProperty("cover").objectReferenceValue = shot;
                o.ApplyModifiedPropertiesWithoutUndo();
            }

            // Hotkeys: keep category switching, Esc goes back.
            var hot = settings.Find("Content/Hotkeys");
            for (int i = hot.childCount - 1; i >= 0; i--)
            {
                var hk = hot.GetChild(i).GetComponent<HotkeyEvent>();
                if (hk == null) continue;
                if (hk.name.Contains("Page")) { Object.DestroyImmediate(hk.gameObject); continue; }
                if (hk.keyID == "Exit") ConfigureHotkey(hk, "Back", router.Back);
            }
        }

        static Transform Deep(Transform root, string name)
        {
            if (root == null) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        /// <summary>A render of the showroom car (saved from Play mode), used as the settings cover.</summary>
        static Sprite LoadShowroomShot()
        {
            string path = UIArt + "T_ShowroomShot.png";
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return null;
            if (imp.textureType != TextureImporterType.Sprite)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Dictionary<string, Transform> FindRows(Transform list)
        {
            var d = new Dictionary<string, Transform>();
            foreach (var se in list.GetComponentsInChildren<SettingsElement>(true))
                if (!d.ContainsKey(se.name)) d[se.name] = se.transform;
            return d;
        }

        static void HideSectionHeaders(Transform list)
        {
            // Heat groups rows under small accent headers ("UI & HUD", "Accessibility"); ours speak for themselves.
            var content = list.GetComponentInChildren<VerticalLayoutGroup>(true);
            if (content == null) return;
            foreach (Transform child in content.transform)
                if (child.GetComponent<SettingsElement>() == null) child.gameObject.SetActive(false);
        }

        static void RenameTab(Transform tab, string label)
        {
            var pb = tab.GetComponent<PanelButton>();
            foreach (var lo in tab.GetComponents<LocalizedObject>()) Object.DestroyImmediate(lo);
            if (pb != null)
            {
                var so = new SerializedObject(pb);
                var p = so.FindProperty("buttonText");
                if (p != null) { p.stringValue = label; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
            foreach (var t in tab.GetComponentsInChildren<TMP_Text>(true)) t.text = label;
        }

        static void SetRowTitle(Transform row, string title)
        {
            var t = row.Find("Text");
            if (t == null) return;
            foreach (var lo in t.GetComponents<LocalizedObject>()) Object.DestroyImmediate(lo);
            t.GetComponent<TMP_Text>().text = title;
        }

        static void SetDescription(Transform row, string title, string text)
        {
            var d = row.GetComponent<SettingsDescription>();
            if (d == null) return;
            var so = new SerializedObject(d);
            so.FindProperty("title").stringValue = title;
            so.FindProperty("description").stringValue = text;
            so.FindProperty("titleKey").stringValue = "";
            so.FindProperty("descriptionKey").stringValue = "";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureSelector(Transform row, string title, string saveKey, string[] options, int defaultIndex, string desc)
        {
            row.name = title;
            SetRowTitle(row, title);
            SetDescription(row, title, desc);
            var hs = row.GetComponentInChildren<HorizontalSelector>(true);
            foreach (var lo in hs.GetComponents<LocalizedObject>()) Object.DestroyImmediate(lo);
            hs.useLocalization = false;
            hs.items.Clear();
            foreach (var o in options) hs.items.Add(new HorizontalSelector.Item { itemTitle = o, localizationKey = "" });
            hs.saveSelected = true;
            hs.saveKey = saveKey;
            hs.defaultIndex = defaultIndex;
            hs.loopSelection = false;
            for (int k = hs.onValueChanged.GetPersistentEventCount() - 1; k >= 0; k--) UnityEventTools.RemovePersistentListener(hs.onValueChanged, k);
            // Indicator dots: one per option.
            if (hs.indicatorParent != null)
            {
                var dots = hs.indicatorParent;
                while (dots.childCount > options.Length && dots.childCount > 1) Object.DestroyImmediate(dots.GetChild(dots.childCount - 1).gameObject);
                while (dots.childCount < options.Length) Object.Instantiate(dots.GetChild(0).gameObject, dots);
            }
            EditorUtility.SetDirty(hs);
        }

        static void ConfigureSwitch(Transform row, string title, string saveKey, bool defaultOn, string desc)
        {
            row.name = title;
            SetRowTitle(row, title);
            SetDescription(row, title, desc);
            var sw = row.GetComponentInChildren<SwitchManager>(true);
            sw.saveValue = true;
            sw.saveKey = saveKey;
            var so = new SerializedObject(sw);
            var on = so.FindProperty("isOn");
            if (on != null) { on.boolValue = defaultOn; so.ApplyModifiedPropertiesWithoutUndo(); }
            EditorUtility.SetDirty(sw);
        }

        // ------------------------------------------------------------------ panel list, credits, quit, splash

        static void BuildPanelList(PanelManager pm, Animator home, Animator garage, Animator tracks, Animator settings)
        {
            pm.panels.Clear();
            System.Action<string, Animator> add = (n, a) =>
            {
                var hot = a.transform.Find("Content/Hotkeys");
                pm.panels.Add(new PanelManager.PanelItem { panelName = n, panelObject = a, hotkeyParent = hot });
            };
            add(MenuRouter.Home, home);
            add(MenuRouter.Garage, garage);
            add(MenuRouter.Tracks, tracks);
            add(MenuRouter.Settings, settings);
            pm.currentPanelIndex = 0;
            EditorUtility.SetDirty(pm);
        }

        static CreditsManager BuildCredits(Transform canvas, CreditsPreset preset)
        {
            preset = AssetDatabase.LoadAssetAtPath<CreditsPreset>("Assets/_ShiftRush/Resources/SR_Credits.asset");
            var cm = canvas.GetComponentInChildren<CreditsManager>(true);
            var so = new SerializedObject(cm);
            so.FindProperty("creditsPreset").objectReferenceValue = preset;
            so.ApplyModifiedPropertiesWithoutUndo();
            // Heat's logo at the top of the credits roll becomes our wordmark.
            foreach (var logo in cm.GetComponentsInChildren<UIManagerLogo>(true))
            {
                var img = logo.GetComponent<Image>();
                if (img) img.enabled = false;
                logo.enabled = false;
                var mark = Txt(logo.transform, "SHIFT RUSH Wordmark", "SHIFT<color=#FF40D9>//</color>RUSH", fontTitle, 120, Color.white, TextAlignmentOptions.Center);
                Stretch(mark.rectTransform);
                mark.rectTransform.offsetMin = new Vector2(-400, 0);
                mark.rectTransform.offsetMax = new Vector2(400, 0);
            }
            var solid = cm.transform.Find("Solid");
            if (solid) solid.GetComponent<Image>().color = new Color(Ink.r, Ink.g, Ink.b, 0.94f);
            return cm;
        }

        static ModalWindowManager BuildQuitWindow(Transform canvas, MenuRouter router)
        {
            var exit = canvas.Find("Modal Windows/Exit").GetComponent<ModalWindowManager>();
            foreach (var lo in exit.GetComponents<LocalizedObject>()) Object.DestroyImmediate(lo);
            exit.useLocalization = false;
            exit.titleText = "LEAVE THE GARAGE?";
            exit.descriptionText = "Your settings are saved. Come back for another run.";
            EditorUtility.SetDirty(exit);
            return exit;
        }

        static void BuildSplash(Transform canvas)
        {
            var splash = canvas.Find("Splash Screen");
            var holder = splash.Find("Content/Logo Holder");
            foreach (Transform c in holder) c.gameObject.SetActive(false);
            // GRIP PRO in the stream look: pink with a cyan echo, leaning forward.
            var echo = Txt(holder, "Title Echo", "GRIP PRO", bsBlack, 190, Signal, TextAlignmentOptions.Center);
            echo.fontStyle = FontStyles.Italic;
            Stretch(echo.rectTransform);
            echo.rectTransform.anchoredPosition = new Vector2(14, -8);
            var t = Txt(holder, "Grip Pro Title", "GRIP PRO", bsBlack, 190, Pink, TextAlignmentOptions.Center);
            t.fontStyle = FontStyles.Italic;
            Stretch(t.rectTransform);
            var le = holder.GetComponent<LayoutElement>() ?? holder.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 200; le.preferredWidth = 1200;
        }

        static void BuildHintBar(Transform canvas)
        {
            var v = Txt(canvas.Find("Main Content"), "Build Tag", "JAM BUILD  " + Application.version, fontBold, 18, new Color(1, 1, 1, 0.25f), TextAlignmentOptions.Right);
            v.characterSpacing = 8;
            Place(v.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-504 - 40, 22), new Vector2(500, 30)); // bottom right of the shot, clear of The Feed
        }

        static void Hint(Transform content, string text)
        {
            var t = Txt(content, "Hint Bar", text, fontBold, 20, Steel, TextAlignmentOptions.Left);
            t.characterSpacing = 8;
            Place(t.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(120, 40), new Vector2(1200, 40));
        }

        static void SetBuildScenes()
        {
            var list = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true),
            };
            var names = new List<string> { "Track_AlpinePass", "Track_GlacierValley", "MovesLab" };
            // Every track in the catalog too (including ones made with the Race Track Wizard).
            var cat = AssetDatabase.LoadAssetAtPath<RaceCatalog>("Assets/_ShiftRush/Resources/RaceCatalog.asset");
            if (cat != null) foreach (var tr in cat.tracks) if (!string.IsNullOrEmpty(tr.scene) && !names.Contains(tr.scene)) names.Add(tr.scene);
            foreach (var t in names)
            {
                string p = null;
                foreach (var guid in AssetDatabase.FindAssets(t + " t:Scene"))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (System.IO.Path.GetFileNameWithoutExtension(path) == t) { p = path; break; }
                }
                if (p != null && !list.Exists(x => x.path == p)) list.Add(new EditorBuildSettingsScene(p, true));
            }
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ------------------------------------------------------------------ small UI helpers

        static Image Img(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            return img;
        }

        static TextMeshProUGUI Txt(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text;
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = align == TextAlignmentOptions.TopLeft ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        static void Place(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = min; rt.anchorMax = max; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
