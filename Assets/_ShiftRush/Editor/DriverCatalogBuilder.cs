using System.Collections.Generic;
using System.IO;
using ShiftRush.Core;
using UnityEditor;
using UnityEngine;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// SHIFT RUSH → Driver → Build Driver Catalog. Reads the two modular bodies from the Customizable 3D City
    /// Characters pack and lists every part as a wearable item: a name, the icon the pack ships, a colour swatch
    /// taken from that icon, a GLOSS / GRIT / neutral tag and a Hype price. The first style in every slot is free.
    /// Also makes URP copies of the pack's materials (it ships built-in ones, which render pink in URP).
    /// Safe to rerun: hand edits to names, tags and prices in the asset are kept for parts that still exist.
    /// </summary>
    public static class DriverCatalogBuilder
    {
        internal const string Pack = "Assets/Customizable 3D City Characters Vol 1/";
        internal const string AssetPath = "Assets/_ShiftRush/Resources/SR_Driver.asset";
        internal const string ArtDir = "Assets/_ShiftRush/Art/Driver/";
        internal static readonly string[] BodyPaths =
        {
            Pack + "Prefabs/Modular_Male_Characters/M_C3CC1_ModularMale.prefab",
            Pack + "Prefabs/Modular_Female_Characters/F_C3CC1_ModularFemale.prefab",
        };
        internal static readonly string[] IconDirs = { Pack + "External/Sprites/Male_Sprites/", Pack + "External/Sprites/Female_Sprites/" };

        // Names for the pieces we can see in the pack's icons, by body letter + slot code + style number.
        static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            {"M_SHOES_01", "BARE FEET"}, {"F_SHOES_01", "BARE FEET"}, {"M_TORSO_05", "OPEN OVERSHIRT"}, {"M_TORSO_06", "ROLL NECK KNIT"}, {"M_TORSO_07", "PUFFER VEST"},
            {"M_TORSO_09", "TWO-TONE JACKET"}, {"M_TORSO_10", "TRACK JACKET"}, {"M_TORSO_13", "TANK TOP"}, {"M_TORSO_22", "ZIP JACKET"},
            {"F_TORSO_02", "SKATER DRESS"}, {"F_TORSO_03", "SHIFT DRESS"}, {"F_TORSO_07", "BIKER JACKET"}, {"F_TORSO_11", "TANK TOP"},
            {"F_TORSO_15", "BUTTON BLOUSE"}, {"F_TORSO_19", "ROLL NECK KNIT"}, {"F_TORSO_21", "LAYERED TEE"},
            {"M_PANTS_04", "CUT-OFF SHORTS"}, {"M_PANTS_12", "RIPPED JEANS"}, {"M_PANTS_13", "DARK JEANS"}, {"M_PANTS_15", "JOGGERS"}, {"M_PANTS_18", "CARGO JOGGERS"},
            {"M_HAT_01", "BOWLER"}, {"M_HAT_05", "CAP"}, {"M_HAT_07", "WIDE BRIM"}, {"M_HAT_08", "RANCH HAT"}, {"M_HAT_14", "FEDORA"},
            {"M_SHOES_02", "LOW TOPS"}, {"M_SHOES_03", "WORK BOOTS"}, {"M_SHOES_05", "SLIDES"}, {"M_SHOES_08", "HIGH TOPS"}, {"M_SHOES_10", "SLIP-ONS"}, {"M_SHOES_12", "COMBAT BOOTS"},
            {"M_MASK_01", "DOMINO MASK"}, {"F_MASK_01", "DOMINO MASK"},
            {"M_HEADPHONES_04", "STUDIO CANS"}, {"M_HEADPHONES_05", "HOOD CANS"},
        };

        // GLOSS = clean, sharp, bright. GRIT = worn, heavy, muddy. Everything else neutral.
        static readonly HashSet<string> Gloss = new HashSet<string>
        {
            "M_TORSO_10", "M_TORSO_22", "M_TORSO_09", "F_TORSO_15", "F_TORSO_03", "F_TORSO_21", "M_SHOES_02", "M_SHOES_10",
            "M_PANTS_13", "M_HAT_05", "M_HAT_14", "M_GLASSES_03", "F_GLASSES_01", "M_HEADPHONES_04", "F_HEADPHONES_01", "M_WATCHES_01", "F_WATCHES_01", "F_EARRING_01",
        };
        static readonly HashSet<string> Grit = new HashSet<string>
        {
            "M_TORSO_07", "M_TORSO_05", "F_TORSO_07", "M_PANTS_12", "M_PANTS_18", "M_SHOES_03", "M_SHOES_12", "M_HAT_08", "M_HAT_07",
            "M_MASK_01", "F_MASK_01", "M_BEARD_07", "M_HEADPHONES_05", "F_HEADPHONES_06", "M_WATCHES_02", "F_WATCHES_02",
        };

        internal static readonly Dictionary<DriverSlot, int> Price = new Dictionary<DriverSlot, int>
        {
            {DriverSlot.Hair, 200}, {DriverSlot.Brows, 0}, {DriverSlot.Beard, 150}, {DriverSlot.Earrings, 250}, {DriverSlot.Glasses, 300},
            {DriverSlot.Mask, 400}, {DriverSlot.Hat, 300}, {DriverSlot.Headphones, 450}, {DriverSlot.Torso, 600}, {DriverSlot.Pants, 400},
            {DriverSlot.Shoes, 350}, {DriverSlot.Watch, 500}, {DriverSlot.Helmet, 900}, {DriverSlot.Gloves, 350},
        };

        [MenuItem("SHIFT RUSH/Driver/Build Driver Catalog")]
        public static DriverCatalog Build() { return Build(false); }

        /// <summary>Same, but throws away hand edits and starts every name, tag and price fresh.</summary>
        [MenuItem("SHIFT RUSH/Driver/Rebuild Driver Catalog From Scratch")]
        static void BuildFresh() { Build(true); }

        public static DriverCatalog Build(bool keepNothing)
        {
            var cat = AssetDatabase.LoadAssetAtPath<DriverCatalog>(AssetPath);
            bool fresh = cat == null;
            if (fresh) cat = ScriptableObject.CreateInstance<DriverCatalog>();
            var old = new Dictionary<string, DriverCatalog.Item>();
            foreach (var i in cat.items) if (!string.IsNullOrEmpty(i.id)) old[i.id] = i;

            cat.bodies = new GameObject[2];
            var items = new List<DriverCatalog.Item>();
            for (int b = 0; b < 2; b++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BodyPaths[b]);
                if (prefab == null) { Debug.LogWarning("[Driver] Missing body prefab " + BodyPaths[b] + ". Import Customizable 3D City Characters Vol 1."); continue; }
                cat.bodies[b] = prefab;
                var firstStyle = new Dictionary<DriverSlot, string>();
                var seen = new HashSet<string>();
                var found = new List<DriverCatalog.Item>();
                foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
                {
                    if (t.GetComponent<Renderer>() == null) continue;
                    if (!DriverLook.Parse(t.name, out string code, out string number, out int colour, out string key)) continue;
                    if (!DriverLook.SlotOf(code, out DriverSlot slot) || !seen.Add(key)) continue;
                    // The driver is never naked: the pack's bare chest and underwear (the plain base top and pants,
                    // style 01 with no colourways) aren't wearable. Bare feet are fine.
                    if ((slot == DriverSlot.Torso || slot == DriverSlot.Pants) && number == "01" && colour == 0) continue;
                    string letter = b == 0 ? "M" : "F";
                    string styleKey = letter + "_" + code + "_" + number;
                    var item = new DriverCatalog.Item
                    {
                        id = key.ToLowerInvariant(),
                        body = (DriverBody)b,
                        slot = slot,
                        style = styleKey,
                        colour = colour,
                        part = key,
                        icon = Icon(IconDirs[b], key),
                        tag = Gloss.Contains(styleKey) ? DriverStyle.Gloss : Grit.Contains(styleKey) ? DriverStyle.Grit : DriverStyle.Neutral,
                    };
                    item.displayName = Names.TryGetValue(styleKey, out string n) ? n : SlotWord(slot) + " " + number;
                    item.swatch = Swatch(item.icon);
                    found.Add(item);
                }
                // Catalog order: by slot, then style, then colour. The first style in every slot is free (and worn by a
                // new driver), so the pack's bare base pieces (no icon: bare feet) go last in their
                // slot: a new driver starts dressed.
                found.Sort((x, y) =>
                {
                    if (x.slot != y.slot) return x.slot.CompareTo(y.slot);
                    if ((x.icon == null) != (y.icon == null)) return x.icon == null ? 1 : -1;
                    return x.style != y.style ? string.CompareOrdinal(x.style, y.style) : x.colour.CompareTo(y.colour);
                });
                foreach (var item in found)
                {
                    if (!firstStyle.ContainsKey(item.slot)) firstStyle[item.slot] = item.style;
                    // The first style is free, and so are the plain base pieces (no icon: bare feet).
                    item.price = firstStyle[item.slot] == item.style || item.icon == null ? 0 : Price[item.slot];
                    if (old.TryGetValue(item.id, out var prev) && !keepNothing)
                    {
                        // Keep hand edits made in the asset (a name that isn't the automatic one, tag and price).
                        string auto = SlotWord(item.slot) + " " + item.style.Substring(item.style.LastIndexOf('_') + 1);
                        if (prev.displayName != auto) item.displayName = prev.displayName;
                        item.tag = prev.tag; item.price = prev.price;
                    }
                    // An icon the Wardrobe Wizard rendered (on the driver, face in view) wins over the pack's sprite.
                    var rendered = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "Icons/" + item.id + ".png");
                    if (rendered != null)
                    {
                        if (item.icon == null && old.TryGetValue(item.id, out var had)) item.swatch = had.swatch;
                        item.icon = rendered;
                    }
                    items.Add(item);
                }
            }
            Materials(cat);
            Idles(cat);
            AddHelmets(items, old, keepNothing);
            // Loose pieces the Wardrobe Wizard rigged and added stay, whatever else gets rebuilt.
            foreach (var i in cat.items) if (i.mesh != null) items.Add(i);
            cat.items = items;

            if (fresh) { Directory.CreateDirectory(Path.GetDirectoryName(AssetPath)); AssetDatabase.CreateAsset(cat, AssetPath); }
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            Debug.Log("[Driver] Catalog: " + items.Count + " pieces across two bodies.");
            return cat;
        }

        // ---------------------------------------------------------------- helmets

        struct HelmetStyle
        {
            public string key, name; public DriverStyle tag; public int price;
            public Color visor, stripe; public float metal, smooth; public Color[] tints;
        }

        static readonly HelmetStyle[] HelmetStyles =
        {
            new HelmetStyle { key = "STOCK", name = "STOCK LID", tag = DriverStyle.Neutral, price = 0, visor = new Color(0.04f, 0.04f, 0.06f), stripe = new Color(0.5f, 0.5f, 0.55f), metal = 0f, smooth = 0.55f,
                tints = new[] { new Color(0.1f, 0.1f, 0.12f), new Color(0.92f, 0.92f, 0.94f), new Color(0.72f, 0.12f, 0.14f) } },
            new HelmetStyle { key = "CH13", name = "CH13 VISOR", tag = DriverStyle.Gloss, price = 800, visor = new Color(0.55f, 0.05f, 0.32f), stripe = new Color(0.18f, 0.95f, 1f), metal = 0.1f, smooth = 0.85f,
                tints = new[] { new Color(0.95f, 0.95f, 0.97f), new Color(0.06f, 0.05f, 0.08f), new Color(1f, 0.18f, 0.6f) } },
            new HelmetStyle { key = "CHROME", name = "CHROME DOME", tag = DriverStyle.Gloss, price = 1200, visor = new Color(0.02f, 0.03f, 0.05f), stripe = new Color(0.1f, 0.1f, 0.12f), metal = 1f, smooth = 0.92f,
                tints = new[] { new Color(0.85f, 0.86f, 0.9f), new Color(0.95f, 0.75f, 0.35f), new Color(0.55f, 0.6f, 0.7f) } },
            new HelmetStyle { key = "MUD", name = "MUD CAKED", tag = DriverStyle.Grit, price = 900, visor = new Color(0.08f, 0.07f, 0.05f), stripe = new Color(0.35f, 0.24f, 0.13f), metal = 0f, smooth = 0.15f,
                tints = new[] { new Color(0.42f, 0.33f, 0.2f), new Color(0.38f, 0.4f, 0.24f), new Color(0.55f, 0.28f, 0.16f) } },
        };

        /// <summary>The pack has no helmets, so we make low-poly ones (shell, visor, crest, chin) and fit them to the head.</summary>
        static void AddHelmets(List<DriverCatalog.Item> items, Dictionary<string, DriverCatalog.Item> old, bool keepNothing)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var st in HelmetStyles)
            {
                var prefab = HelmetPrefab(st, lit);
                for (int b = 0; b < 2; b++)
                    for (int c = 0; c < st.tints.Length; c++)
                    {
                        var item = new DriverCatalog.Item
                        {
                            id = (b == 0 ? "a" : "b") + "_helmet_" + st.key.ToLowerInvariant() + "_" + (c + 1),
                            displayName = st.name, body = (DriverBody)b, slot = DriverSlot.Helmet,
                            style = (b == 0 ? "M" : "F") + "_HELMET_" + st.key, colour = c + 1,
                            part = "", prefab = prefab, tint = st.tints[c], swatch = st.tints[c], tag = st.tag, price = st.price,
                        };
                        if (!keepNothing && old.TryGetValue(item.id, out var prev)) { item.displayName = prev.displayName; item.tag = prev.tag; item.price = prev.price; }
                        item.icon = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "Icons/" + item.id + ".png"); // rendered by the Wardrobe Wizard
                        items.Add(item);
                    }
            }
        }

        static GameObject HelmetPrefab(HelmetStyle st, Shader lit)
        {
            string path = ArtDir + "P_Helmet_" + st.key + ".prefab";
            var shell = HelmetMaterial(ArtDir + "M_Helmet_" + st.key + "_Shell.mat", lit, Color.white, st.metal, st.smooth);
            var visor = HelmetMaterial(ArtDir + "M_Helmet_" + st.key + "_Visor.mat", lit, st.visor, 0.2f, 0.96f);
            var stripe = HelmetMaterial(ArtDir + "M_Helmet_" + st.key + "_Stripe.mat", lit, st.stripe, 0f, 0.6f);
            var root = new GameObject("P_Helmet_" + st.key);
            Part(root, PrimitiveType.Sphere, "Shell", new Vector3(0f, 0.015f, -0.01f), new Vector3(0.37f, 0.37f, 0.4f), Vector3.zero, shell);
            Part(root, PrimitiveType.Cube, "Shell Chin", new Vector3(0f, -0.14f, 0.1f), new Vector3(0.24f, 0.08f, 0.14f), new Vector3(-12f, 0f, 0f), shell);
            Part(root, PrimitiveType.Sphere, "Visor", new Vector3(0f, -0.005f, 0.1f), new Vector3(0.3f, 0.13f, 0.22f), Vector3.zero, visor);
            Part(root, PrimitiveType.Cube, "Crest", new Vector3(0f, 0.19f, -0.02f), new Vector3(0.05f, 0.025f, 0.3f), new Vector3(8f, 0f, 0f), stripe);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static void Part(GameObject root, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Vector3 euler, Material m)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(root.transform, false);
            g.transform.localPosition = pos; g.transform.localScale = scale; g.transform.localEulerAngles = euler;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        static Material HelmetMaterial(string path, Shader lit, Color c, float metal, float smooth)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
            m.shader = lit;
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_Smoothness", smooth);
            EditorUtility.SetDirty(m);
            return m;
        }

        internal static string SlotWord(DriverSlot s)
        {
            switch (s)
            {
                case DriverSlot.Hair: return "CUT";
                case DriverSlot.Brows: return "BROWS";
                case DriverSlot.Beard: return "BEARD";
                case DriverSlot.Earrings: return "EARRINGS";
                case DriverSlot.Glasses: return "FRAMES";
                case DriverSlot.Mask: return "MASK";
                case DriverSlot.Hat: return "HAT";
                case DriverSlot.Headphones: return "CANS";
                case DriverSlot.Torso: return "TOP";
                case DriverSlot.Pants: return "PANTS";
                case DriverSlot.Shoes: return "SHOES";
                case DriverSlot.Helmet: return "HELMET";
                case DriverSlot.Gloves: return "GLOVES";
                default: return "WATCH";
            }
        }

        static Sprite Icon(string dir, string key)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(dir + key + ".png");
            if (s == null) s = AssetDatabase.LoadAssetAtPath<Sprite>(dir + key + "_LEFT.png");
            return s;
        }

        /// <summary>The average colour of the icon's opaque pixels, read from the file (the texture isn't readable).</summary>
        internal static Color Swatch(Sprite icon)
        {
            if (icon == null) return Color.gray;
            string path = AssetDatabase.GetAssetPath(icon.texture);
            if (!File.Exists(path)) return Color.gray;
            var tex = new Texture2D(2, 2);
            if (!tex.LoadImage(File.ReadAllBytes(path))) { Object.DestroyImmediate(tex); return Color.gray; }
            var px = tex.GetPixels32();
            double r = 0, g = 0, bl = 0; int n = 0;
            for (int i = 0; i < px.Length; i += 7)
            {
                if (px[i].a < 200) continue;
                r += px[i].r; g += px[i].g; bl += px[i].b; n++;
            }
            Object.DestroyImmediate(tex);
            return n == 0 ? Color.gray : new Color((float)(r / n / 255.0), (float)(g / n / 255.0), (float)(bl / n / 255.0));
        }

        const string KawaiiIdle = "Assets/KAWAII_ANIMATIOMS_CoolAction/Assets/Animations/Idle/";
        const string MocapIdle = "Assets/MoCapCentral/MC_Sample/Animations/Idle/";
        // First clip of each list is the resting idle, the rest play at random between it.
        static readonly string[] FemaleIdlePaths =
        {
            KawaiiIdle + "@CA_Idle05_LookBody.FBX", KawaiiIdle + "@CA_Idle01_Breathing.FBX",
            KawaiiIdle + "@CA_Idle02_LookAround.FBX", KawaiiIdle + "@CA_Idle09_CoolPose1.FBX",
        };
        static readonly string[] MaleIdlePaths =
        {
            MocapIdle + "MCU_am_Stand_Idle_03_LookAround.FBX", MocapIdle + "MCU_am_Stand_Idle_06_ScratchArm.FBX",
            MocapIdle + "MCU_am_Stand_Conv_Talk_05_Generic.FBX", MocapIdle + "MCU_am_Stand_HatTip_01.FBX",
            MocapIdle + "MCU_am_Stand_Wave_02_LHand.FBX", MocapIdle + "MCU_am_Stand_LookAt_05_PointRHand.FBX",
        };

        /// <summary>Showroom idles: Kawaii Cool Action for the female body, MoCap Central for the male. Lists you edit by hand are kept.</summary>
        internal static void Idles(DriverCatalog cat)
        {
            if (cat.maleIdles == null || cat.maleIdles.Length == 0) cat.maleIdles = LoadClips(MaleIdlePaths);
            if (cat.femaleIdles == null || cat.femaleIdles.Length == 0) cat.femaleIdles = LoadClips(FemaleIdlePaths);
        }

        static AnimationClip[] LoadClips(string[] paths)
        {
            var clips = new List<AnimationClip>();
            foreach (var p in paths)
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
                    if (o is AnimationClip c && !c.name.StartsWith("__preview")) { clips.Add(c); break; }
            return clips.ToArray();
        }

        /// <summary>URP Lit copies of the pack's atlas materials.</summary>
        static void Materials(DriverCatalog cat)
        {
            Directory.CreateDirectory(ArtDir);
            var srcSolid = AssetDatabase.LoadAssetAtPath<Material>(Pack + "Materials/Modular_Characters.mat");
            var srcClear = AssetDatabase.LoadAssetAtPath<Material>(Pack + "Materials/Modular_Characters_Transparent.mat");
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(Pack + "Textures/Texture_Modular_Characters.png");
            if (atlas == null && srcSolid != null) atlas = srcSolid.mainTexture as Texture2D;
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return;

            cat.solid = MakeMaterial(ArtDir + "M_Driver.mat", lit, atlas, false, srcSolid != null ? srcSolid.color : Color.white);
            cat.seeThrough = MakeMaterial(ArtDir + "M_Driver_Clear.mat", lit, atlas, true, srcClear != null ? srcClear.color : new Color(1f, 1f, 1f, 0.4f));
        }

        static Material MakeMaterial(string path, Shader lit, Texture2D atlas, bool clear, Color tint)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
            m.shader = lit;
            m.SetTexture("_BaseMap", atlas);
            m.SetColor("_BaseColor", clear ? new Color(tint.r, tint.g, tint.b, Mathf.Clamp(tint.a, 0.2f, 0.7f)) : Color.white);
            m.SetFloat("_Smoothness", clear ? 0.8f : 0.18f);
            m.SetFloat("_Metallic", 0f);
            if (clear)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else
            {
                m.SetFloat("_Surface", 0f);
                m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = -1;
            }
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
