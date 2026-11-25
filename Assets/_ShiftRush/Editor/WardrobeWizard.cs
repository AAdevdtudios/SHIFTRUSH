using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using ShiftRush.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// SHIFT RUSH → Driver → Wardrobe Wizard. Lists every loose piece in the character pack's Characters_Parts
    /// folders (the ones the body prefabs don't carry: real helmets, gloves, more hair, hats, tops, pants, shoes)
    /// and marks which are in the game. Pick one to see it on the driver, set how its shop icon looks (framing,
    /// angle, background, light), name and price it, then Add to Game. The piece is rigged to the body's skeleton
    /// (head pieces ride the head bone, clothes copy the skin weights of the body under them) and its icon is
    /// rendered from the real model, so it always matches what the player wears.
    /// </summary>
    public class WardrobeWizard : EditorWindow
    {
        const string PartsMale = DriverCatalogBuilder.Pack + "Prefabs/Modular_Male_Characters/Characters_Parts/";
        const string PartsFemale = DriverCatalogBuilder.Pack + "Prefabs/Modular_Female_Characters/Characters_Parts/";
        const string RigDir = DriverCatalogBuilder.ArtDir + "Rigged/";
        const string IconDir = DriverCatalogBuilder.ArtDir + "Icons/";
        const string PrefsKey = "GripPro.WardrobeWizard.Look";
        const int IconSize = 256;

        static readonly Color Gloss = new Color32(46, 242, 255, 255);
        static readonly Color Grit = new Color32(255, 138, 61, 255);
        static readonly Color Magenta = new Color32(255, 46, 160, 255);
        static readonly Color InGameGreen = new Color32(80, 230, 140, 255);
        static readonly Regex LooseName = new Regex(@"^([A-Z]+)_(\d+)(?:_(\d+))?(?:_(LEFT|RIGHT))?$");

        // Pack folder → slot. Folders not listed (heads, bare body, eyebrows, the built-in hands) aren't wearables.
        static readonly Dictionary<string, DriverSlot> Folders = new Dictionary<string, DriverSlot>
        {
            {"Hair", DriverSlot.Hair}, {"Hats", DriverSlot.Hat}, {"Beard", DriverSlot.Beard}, {"Glasses", DriverSlot.Glasses},
            {"Masks", DriverSlot.Mask}, {"Helmet", DriverSlot.Helmet}, {"Arms", DriverSlot.Gloves}, {"Torso", DriverSlot.Torso},
            {"Pants", DriverSlot.Pants}, {"Shoes", DriverSlot.Shoes}, {"Headphones", DriverSlot.Headphones},
        };

        // ------------------------------------------------------------------ data

        /// <summary>One colourway of a loose piece (gloves are a left and a right prefab).</summary>
        class Colourway
        {
            public int colour;
            public List<GameObject> prefabs = new List<GameObject>();
            public string id;
            public Sprite packIcon;
        }

        /// <summary>One loose piece with all its colourways.</summary>
        class Piece
        {
            public DriverBody body;
            public DriverSlot slot;
            public string code, number, style;
            public bool builtInTwin;   // the body prefab already has this style number (likely the same model)
            public List<Colourway> colours = new List<Colourway>();
            public string Label { get { return code + " " + number; } }
        }

        public enum Framing { Auto, Head, Torso, Legs, Feet, FullBody }
        public enum Backdrop { Channel13, DarkStudio, Solid, Transparent }

        /// <summary>The icon look, remembered between sessions so every new piece matches.</summary>
        [System.Serializable]
        class Look
        {
            public Framing framing = Framing.Auto;
            public float yaw = 25f, pitch = 8f, zoom = 1f;
            public Backdrop backdrop = Backdrop.Channel13;
            public Color solid = new Color32(26, 22, 40, 255);
            public float key = 1.3f, rim = 2.2f;
            public bool rimFollowsTag = true;
            public Color rimColour = Magenta;
            public bool onBody = true, relaxedArms = true, usePackIcon = false;
        }

        List<Piece> pieces = new List<Piece>();
        Look look = new Look();
        DriverBody body;
        int slotFilter;           // 0 = every slot, else slot + 1
        bool missingOnly;
        Piece picked;
        int pickedColour;
        string editName = "";
        DriverStyle editTag;
        int editPrice;
        Vector2 listScroll, sideScroll;

        Scene stage;
        GameObject stageBody, stagePiece;
        Camera cam;
        Light keyLight, rimLight;
        Texture2D preview;
        bool dirty = true;
        readonly Dictionary<string, Mesh> rigCache = new Dictionary<string, Mesh>();

        [MenuItem("SHIFT RUSH/Driver/Wardrobe Wizard")]
        static void Open()
        {
            var w = GetWindow<WardrobeWizard>("Wardrobe Wizard");
            w.minSize = new Vector2(820, 560);
        }

        void OnEnable()
        {
            string json = EditorPrefs.GetString(PrefsKey, "");
            if (!string.IsNullOrEmpty(json)) { try { JsonUtility.FromJsonOverwrite(json, look); } catch { } }
            Scan();
        }

        void OnDisable()
        {
            EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(look));
            CloseStage();
            foreach (var m in rigCache.Values) if (m != null && !AssetDatabase.Contains(m)) DestroyImmediate(m);
            rigCache.Clear();
            if (preview != null) DestroyImmediate(preview);
        }

        // ------------------------------------------------------------------ scanning the pack

        void Scan()
        {
            pieces.Clear();
            for (int b = 0; b < 2; b++)
            {
                string root = b == 0 ? PartsMale : PartsFemale;
                string letter = b == 0 ? "M" : "F";
                if (!Directory.Exists(root)) continue;
                var builtIn = BuiltInStyles((DriverBody)b);
                var index = new Dictionary<string, Piece>();
                foreach (var kv in Folders)
                {
                    string dir = root + kv.Key;
                    if (!Directory.Exists(dir)) continue;
                    foreach (var file in Directory.GetFiles(dir, "*.prefab"))
                    {
                        string name = Path.GetFileNameWithoutExtension(file);
                        var m = LooseName.Match(name);
                        if (!m.Success || !m.Groups[3].Success) continue; // prefixed (already in the body) or a bare base piece
                        string code = m.Groups[1].Value, number = m.Groups[2].Value;
                        if (kv.Value == DriverSlot.Gloves && number == "01") continue; // bare hands
                        int colour = int.Parse(m.Groups[3].Value);
                        string style = letter + "_LOOSE_" + code + "_" + number;
                        if (!index.TryGetValue(style, out var piece))
                        {
                            piece = new Piece { body = (DriverBody)b, slot = kv.Value, code = code, number = number, style = style, builtInTwin = builtIn.Contains(code + "_" + number) };
                            index[style] = piece; pieces.Add(piece);
                        }
                        var cw = piece.colours.Find(c => c.colour == colour);
                        if (cw == null)
                        {
                            cw = new Colourway { colour = colour, id = (letter + "_loose_" + code + "_" + number + "_" + colour).ToLowerInvariant() };
                            cw.packIcon = PackIcon((DriverBody)b, code + "_" + number + "_" + colour);
                            piece.colours.Add(cw);
                        }
                        var go = AssetDatabase.LoadAssetAtPath<GameObject>(file.Replace('\\', '/'));
                        if (go != null) cw.prefabs.Add(go);
                    }
                }
            }
            foreach (var p in pieces) p.colours.Sort((x, y) => x.colour.CompareTo(y.colour));
            pieces.Sort((x, y) => x.body != y.body ? x.body.CompareTo(y.body) : x.slot != y.slot ? x.slot.CompareTo(y.slot) : string.CompareOrdinal(x.number, y.number));
        }

        /// <summary>CODE_NUMBER of every style inside the body prefab.</summary>
        static HashSet<string> BuiltInStyles(DriverBody b)
        {
            var set = new HashSet<string>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DriverCatalogBuilder.BodyPaths[(int)b]);
            if (prefab == null) return set;
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                if (DriverLook.Parse(r.name, out string code, out string number, out _, out _)) set.Add(code + "_" + number);
            return set;
        }

        static Sprite PackIcon(DriverBody b, string key)
        {
            string dir = DriverCatalogBuilder.IconDirs[(int)b];
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(dir + key + ".png");
            if (s == null) s = AssetDatabase.LoadAssetAtPath<Sprite>(dir + key + "_LEFT.png");
            return s;
        }

        static DriverCatalog Catalog { get { return AssetDatabase.LoadAssetAtPath<DriverCatalog>(DriverCatalogBuilder.AssetPath); } }

        static bool InGame(DriverCatalog cat, Piece p)
        {
            if (cat == null) return false;
            foreach (var c in p.colours) if (cat.Find(c.id) != null) return true;
            return false;
        }

        // ------------------------------------------------------------------ GUI

        void OnGUI()
        {
            var cat = Catalog;
            if (cat == null)
            {
                EditorGUILayout.HelpBox("No driver catalog yet. Run SHIFT RUSH → Driver → Build Driver Catalog first.", MessageType.Warning);
                return;
            }
            Header(cat);
            EditorGUILayout.BeginHorizontal();
            PieceList(cat);
            EditorGUILayout.BeginVertical();
            sideScroll = EditorGUILayout.BeginScrollView(sideScroll);
            Side(cat);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            if (dirty && Event.current.type == EventType.Repaint) { dirty = false; EditorApplication.delayCall += RenderPreview; }
        }

        void Header(DriverCatalog cat)
        {
            var bar = EditorGUILayout.GetControlRect(false, 34);
            EditorGUI.DrawRect(bar, new Color32(20, 12, 34, 255));
            EditorGUI.DrawRect(new Rect(bar.x, bar.yMax - 2, bar.width, 2), Magenta);
            var title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15, normal = { textColor = Color.white } };
            GUI.Label(new Rect(bar.x + 10, bar.y + 7, 300, 22), "WARDROBE WIZARD  //  CH13", title);
            int total = 0, inGame = 0;
            foreach (var p in pieces) if (p.body == body) { total++; if (InGame(cat, p)) inGame++; }
            var sub = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, normal = { textColor = new Color(1, 1, 1, 0.7f) } };
            GUI.Label(new Rect(bar.xMax - 330, bar.y + 8, 320, 20), inGame + " of " + total + " loose pieces in the game", sub);

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            int nb = GUILayout.Toolbar((int)body, new[] { "DRIVER A (MALE)", "DRIVER B (FEMALE)" }, EditorStyles.toolbarButton, GUILayout.Width(300));
            if (nb != (int)body) { body = (DriverBody)nb; picked = null; CloseStage(); dirty = true; }
            GUILayout.Space(10);
            var names = new List<string> { "Every slot" };
            foreach (var s in Folders.Values) if (!names.Contains(s.ToString())) names.Add(s.ToString());
            int sf = EditorGUILayout.Popup(slotFilter, names.ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(120));
            slotFilter = sf;
            missingOnly = GUILayout.Toggle(missingOnly, "Missing only", EditorStyles.toolbarButton, GUILayout.Width(90));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Rescan pack", EditorStyles.toolbarButton, GUILayout.Width(90))) Scan();
            EditorGUILayout.EndHorizontal();
        }

        bool Shown(DriverCatalog cat, Piece p)
        {
            if (p.body != body) return false;
            if (slotFilter > 0)
            {
                var names = new List<string> { "" };
                foreach (var s in Folders.Values) if (!names.Contains(s.ToString())) names.Add(s.ToString());
                if (p.slot.ToString() != names[slotFilter]) return false;
            }
            return !missingOnly || !InGame(cat, p);
        }

        void PieceList(DriverCatalog cat)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            listScroll = EditorGUILayout.BeginScrollView(listScroll);
            DriverSlot? last = null;
            foreach (var p in pieces)
            {
                if (!Shown(cat, p)) continue;
                if (last != p.slot)
                {
                    last = p.slot;
                    GUILayout.Space(6);
                    GUILayout.Label(DriverCatalogBuilder.SlotWord(p.slot), EditorStyles.boldLabel);
                }
                var r = EditorGUILayout.GetControlRect(false, 30);
                bool on = picked == p;
                EditorGUI.DrawRect(r, on ? new Color32(60, 24, 70, 255) : new Color32(30, 26, 42, 255));
                if (on) EditorGUI.DrawRect(new Rect(r.x, r.y, 3, r.height), Magenta);
                var icon = p.colours.Count > 0 ? IconFor(cat, p.colours[0]) : null;
                if (icon != null) DrawSprite(new Rect(r.x + 6, r.y + 2, 26, 26), icon);
                var label = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.white } };
                GUI.Label(new Rect(r.x + 38, r.y + 6, 140, 18), p.Label + "  ·  " + p.colours.Count + " col", label);
                bool inGame = InGame(cat, p);
                string badge = inGame ? "IN GAME" : p.builtInTwin ? "TWIN BUILT IN" : "NOT IN GAME";
                Color bc = inGame ? InGameGreen : p.builtInTwin ? new Color(0.6f, 0.6f, 0.65f) : Grit;
                var bs = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleRight, normal = { textColor = bc } };
                GUI.Label(new Rect(r.xMax - 110, r.y + 7, 104, 16), badge, bs);
                if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition)) { Pick(cat, p); Event.current.Use(); }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void Pick(DriverCatalog cat, Piece p)
        {
            picked = p; pickedColour = 0;
            var existing = p.colours.Count > 0 ? cat.Find(p.colours[0].id) : null;
            editName = existing != null ? existing.displayName : DriverCatalogBuilder.SlotWord(p.slot) + " " + p.number;
            editTag = existing != null ? existing.tag : DriverStyle.Neutral;
            editPrice = existing != null ? existing.price : (DriverCatalogBuilder.Price.TryGetValue(p.slot, out int pr) ? pr : 300);
            dirty = true;
            Repaint();
        }

        void Side(DriverCatalog cat)
        {
            if (picked == null)
            {
                GUILayout.Space(20);
                EditorGUILayout.HelpBox("Pick a piece on the left to see it on the driver and set up its icon.\n\nTWIN BUILT IN means the body already carries a piece with the same number, so it is probably the same model: Add All Missing skips those.", MessageType.Info);
                BatchButtons(cat);
                return;
            }

            // Preview + colourways
            EditorGUILayout.BeginHorizontal();
            var pr = GUILayoutUtility.GetRect(300, 300, GUILayout.Width(300), GUILayout.Height(300));
            EditorGUI.DrawRect(pr, Color.black);
            if (preview != null) GUI.DrawTexture(pr, preview, ScaleMode.ScaleToFit, true);
            EditorGUILayout.BeginVertical();
            GUILayout.Label(picked.Label + "   (" + DriverCatalogBuilder.SlotWord(picked.slot) + ")", EditorStyles.boldLabel);
            GUILayout.Label("Colourways", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < picked.colours.Count; i++)
            {
                var cw = picked.colours[i];
                var r = GUILayoutUtility.GetRect(44, 44, GUILayout.Width(44), GUILayout.Height(44));
                EditorGUI.DrawRect(r, i == pickedColour ? Magenta : new Color32(40, 34, 56, 255));
                var ic = IconFor(cat, cw);
                if (ic != null) DrawSprite(new Rect(r.x + 3, r.y + 3, 38, 38), ic);
                else GUI.Label(r, cw.colour.ToString(), new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } });
                if (cat.Find(cw.id) != null) EditorGUI.DrawRect(new Rect(r.x, r.yMax - 3, r.width, 3), InGameGreen);
                if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition)) { pickedColour = i; dirty = true; Event.current.Use(); }
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.Label("Item", EditorStyles.miniBoldLabel);
            editName = EditorGUILayout.TextField("Name", editName).ToUpperInvariant();
            var nt = (DriverStyle)EditorGUILayout.EnumPopup("Fan lean", editTag);
            if (nt != editTag) { editTag = nt; dirty = true; }
            editPrice = Mathf.Max(0, EditorGUILayout.IntField("Price (Hype)", editPrice));
            if (picked.slot == DriverSlot.Torso || picked.slot == DriverSlot.Pants || picked.slot == DriverSlot.Shoes)
                EditorGUILayout.HelpBox("Clothes copy the skin weights of the body under them. Check the sleeves and hems with Relaxed arms on.", MessageType.None);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            // Icon look
            GUILayout.Space(8);
            GUILayout.Label("Icon look", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            look.onBody = GUILayout.Toolbar(look.onBody ? 0 : 1, new[] { "On the driver", "Piece alone" }) == 0;
            look.framing = (Framing)EditorGUILayout.EnumPopup("Framing", look.framing);
            look.yaw = EditorGUILayout.Slider("Turn", look.yaw, -180f, 180f);
            look.pitch = EditorGUILayout.Slider("Tilt", look.pitch, -30f, 60f);
            look.zoom = EditorGUILayout.Slider("Zoom", look.zoom, 0.5f, 2f);
            look.backdrop = (Backdrop)EditorGUILayout.EnumPopup("Background", look.backdrop);
            if (look.backdrop == Backdrop.Solid) look.solid = EditorGUILayout.ColorField("Colour", look.solid);
            look.key = EditorGUILayout.Slider("Key light", look.key, 0f, 3f);
            look.rim = EditorGUILayout.Slider("Rim light", look.rim, 0f, 5f);
            look.rimFollowsTag = EditorGUILayout.Toggle("Rim follows fan lean", look.rimFollowsTag);
            if (!look.rimFollowsTag) look.rimColour = EditorGUILayout.ColorField("Rim colour", look.rimColour);
            if (look.onBody) look.relaxedArms = EditorGUILayout.Toggle("Relaxed arms", look.relaxedArms);
            look.usePackIcon = EditorGUILayout.Toggle(new GUIContent("Use pack icon if there is one", "The pack ships flat icons for some pieces. Off renders every icon in the look above."), look.usePackIcon);
            if (EditorGUI.EndChangeCheck()) { dirty = true; EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(look)); }
            if (GUILayout.Button("Reset look")) { look = new Look(); dirty = true; }

            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Make Icon", GUILayout.Height(30)))
            {
                var path = SaveIcon(picked, picked.colours[pickedColour], cat);
                if (path != null) EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Sprite>(path));
            }
            bool inGame = InGame(cat, picked);
            var old = GUI.backgroundColor;
            GUI.backgroundColor = InGameGreen;
            if (GUILayout.Button(inGame ? "Update in Game" : "Add to Game", GUILayout.Height(30))) { AddPiece(cat, picked, editName, editTag, editPrice); dirty = true; }
            GUI.backgroundColor = old;
            using (new EditorGUI.DisabledScope(!inGame))
                if (GUILayout.Button("Remove from Game", GUILayout.Height(30))) RemovePiece(cat, picked);
            EditorGUILayout.EndHorizontal();
            BatchButtons(cat);
        }

        void BatchButtons(DriverCatalog cat)
        {
            GUILayout.Space(10);
            int missing = 0;
            foreach (var p in pieces) if (Shown(cat, p) && !InGame(cat, p) && !p.builtInTwin) missing++;
            using (new EditorGUI.DisabledScope(missing == 0))
                if (GUILayout.Button("Add All Missing (" + missing + " shown, current icon look)", GUILayout.Height(26)))
                {
                    if (EditorUtility.DisplayDialog("Add All Missing", "Rig, make icons for and add " + missing + " pieces to the game with the current icon look?", "Add them", "Cancel"))
                        AddAllMissing(cat);
                }
            int inGame = 0;
            foreach (var i in cat.items) if (i.mesh != null || i.prefab != null || !string.IsNullOrEmpty(i.part)) inGame++;
            if (GUILayout.Button("Re-render Every Icon in Game (" + inGame + ", current icon look)", GUILayout.Height(26)))
            {
                if (EditorUtility.DisplayDialog("Re-render Every Icon", "Render all " + inGame + " in-game icons on the driver with the current icon look? Built-in pieces stop using the pack's sprites.", "Render them", "Cancel"))
                    RenderAllIcons(cat);
            }
        }

        static void DrawSprite(Rect r, Sprite s)
        {
            var t = s.texture; var tr = s.textureRect;
            GUI.DrawTextureWithTexCoords(r, t, new Rect(tr.x / t.width, tr.y / t.height, tr.width / t.width, tr.height / t.height), true);
        }

        static Sprite IconFor(DriverCatalog cat, Colourway cw)
        {
            var item = cat.Find(cw.id);
            if (item != null && item.icon != null) return item.icon;
            var made = AssetDatabase.LoadAssetAtPath<Sprite>(IconDir + cw.id + ".png");
            return made != null ? made : cw.packIcon;
        }

        // ------------------------------------------------------------------ adding

        void AddAllMissing(DriverCatalog cat)
        {
            var todo = new List<Piece>();
            foreach (var p in pieces) if (Shown(cat, p) && !InGame(cat, p) && !p.builtInTwin) todo.Add(p);
            try
            {
                for (int i = 0; i < todo.Count; i++)
                {
                    var p = todo[i];
                    if (EditorUtility.DisplayCancelableProgressBar("Wardrobe Wizard", "Adding " + p.Label, (float)i / todo.Count)) break;
                    int price = DriverCatalogBuilder.Price.TryGetValue(p.slot, out int pr) ? pr : 300;
                    AddPiece(cat, p, DriverCatalogBuilder.SlotWord(p.slot) + " " + p.number, DriverStyle.Neutral, price, false);
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            AssetDatabase.SaveAssets();
            dirty = true;
        }

        /// <summary>Rigs every colourway, makes its icon and puts it in the catalog (replacing an earlier add).</summary>
        void AddPiece(DriverCatalog cat, Piece p, string displayName, DriverStyle tag, int price, bool save = true)
        {
            Directory.CreateDirectory(RigDir);
            var stylePicked = picked; int colourPicked = pickedColour;
            picked = p;
            foreach (var cw in p.colours)
            {
                var mesh = RiggedAsset(p, cw, out int clear);
                if (mesh == null) continue;
                pickedColour = p.colours.IndexOf(cw);
                string iconPath = look.usePackIcon && cw.packIcon != null ? null : SaveIcon(p, cw, cat);
                var icon = iconPath != null ? AssetDatabase.LoadAssetAtPath<Sprite>(iconPath) : cw.packIcon;
                var item = new DriverCatalog.Item
                {
                    id = cw.id, displayName = displayName, body = p.body, slot = p.slot, style = p.style, colour = cw.colour,
                    part = "", mesh = mesh, clearMask = clear, icon = icon, tag = tag, price = price,
                    swatch = iconPath != null ? lastSwatch : DriverCatalogBuilder.Swatch(icon),
                };
                int at = cat.items.FindIndex(x => x.id == item.id);
                if (at >= 0) cat.items[at] = item; else cat.items.Add(item);
            }
            picked = stylePicked; pickedColour = colourPicked;
            EditorUtility.SetDirty(cat);
            if (save) { AssetDatabase.SaveAssets(); Debug.Log("[Driver] Wardrobe Wizard added " + p.Label + " (" + p.colours.Count + " colourways) for " + p.body + "."); }
        }

        void RemovePiece(DriverCatalog cat, Piece p)
        {
            foreach (var cw in p.colours) cat.items.RemoveAll(x => x.id == cw.id);
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ rigging

        /// <summary>The rigged mesh saved as an asset (made, or remade, from the pack piece).</summary>
        Mesh RiggedAsset(Piece p, Colourway cw, out int clear)
        {
            var mesh = Rig(p, cw, out clear);
            if (mesh == null) return null;
            string path = RigDir + cw.id + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == mesh) return existing;
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = cw.id;
                rigCache[cw.id] = existing;
                DestroyImmediate(mesh);
                return existing;
            }
            mesh.name = cw.id;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>
        /// The piece's mesh, in the body's bind pose, with bone weights. Head pieces ride the head bone stiffly;
        /// everything else takes the blended weights of the nearest points on the body's own skin.
        /// </summary>
        Mesh Rig(Piece p, Colourway cw, out int clear)
        {
            clear = 0;
            if (rigCache.TryGetValue(cw.id, out var cached) && cached != null)
            {
                clear = ClearMask(cw);
                return cached;
            }
            var skin = BodySkin(p.body);
            if (skin == null || cw.prefabs.Count == 0) return null;

            // Every source mesh (gloves are two) into one, in the prefab's space (= the body's space).
            var combine = new List<CombineInstance>();
            var clearFlags = new List<bool>();
            foreach (var pf in cw.prefabs)
                foreach (var mf in pf.GetComponentsInChildren<MeshFilter>(true))
                {
                    var r = mf.GetComponent<MeshRenderer>();
                    var src = mf.sharedMesh;
                    if (src == null) continue;
                    var mats = r != null ? r.sharedMaterials : new Material[0];
                    for (int s = 0; s < src.subMeshCount; s++)
                    {
                        combine.Add(new CombineInstance { mesh = src, subMeshIndex = s, transform = pf.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix });
                        var m = s < mats.Length ? mats[s] : null;
                        clearFlags.Add(m != null && m.name.Contains("Transparent"));
                    }
                }
            if (combine.Count == 0) return null;
            // Keep a submesh per material kind (solid / see-through), merge the rest.
            var solidParts = new List<CombineInstance>(); var clearParts = new List<CombineInstance>();
            for (int i = 0; i < combine.Count; i++) (clearFlags[i] ? clearParts : solidParts).Add(combine[i]);
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            var groups = new List<CombineInstance>();
            var temps = new List<Mesh>();
            foreach (var part in new[] { solidParts, clearParts })
            {
                if (part.Count == 0) continue;
                var t = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                t.CombineMeshes(part.ToArray(), true, true);
                temps.Add(t);
                groups.Add(new CombineInstance { mesh = t, transform = Matrix4x4.identity });
                if (part == clearParts) clear |= 1 << (groups.Count - 1);
            }
            mesh.CombineMeshes(groups.ToArray(), false, true);
            foreach (var t in temps) DestroyImmediate(t);

            // The body's skin is posed by its own transform: bring the piece into the skin mesh's space.
            var toSkin = skin.transform.worldToLocalMatrix * skin.transform.root.localToWorldMatrix;
            var verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++) verts[i] = toSkin.MultiplyPoint3x4(verts[i]);
            mesh.vertices = verts;

            var weights = new BoneWeight[verts.Length];
            bool stiff = p.slot == DriverSlot.Hair || p.slot == DriverSlot.Hat || p.slot == DriverSlot.Beard || p.slot == DriverSlot.Glasses
                || p.slot == DriverSlot.Mask || p.slot == DriverSlot.Helmet || p.slot == DriverSlot.Headphones;
            int head = System.Array.FindIndex(skin.bones, b => b != null && b.name == "Head");
            if (stiff && head >= 0)
                for (int i = 0; i < weights.Length; i++) weights[i] = new BoneWeight { boneIndex0 = head, weight0 = 1f };
            else
                Transfer(skin.sharedMesh, verts, weights);
            mesh.boneWeights = weights;
            mesh.bindposes = skin.sharedMesh.bindposes;
            mesh.RecalculateBounds();
            mesh.name = cw.id;
            rigCache[cw.id] = mesh;
            return mesh;
        }

        static int ClearMask(Colourway cw)
        {
            bool solid = false, clear = false;
            foreach (var pf in cw.prefabs)
                foreach (var r in pf.GetComponentsInChildren<MeshRenderer>(true))
                    foreach (var m in r.sharedMaterials) { if (m != null && m.name.Contains("Transparent")) clear = true; else solid = true; }
            return clear ? (solid ? 2 : 1) : 0;
        }

        static readonly Dictionary<DriverBody, SkinnedMeshRenderer> skins = new Dictionary<DriverBody, SkinnedMeshRenderer>();

        static SkinnedMeshRenderer BodySkin(DriverBody b)
        {
            if (skins.TryGetValue(b, out var s) && s != null) return s;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DriverCatalogBuilder.BodyPaths[(int)b]);
            if (prefab == null) return null;
            foreach (var r in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (r.name.EndsWith("_FULL_BODY")) { skins[b] = r; return r; }
            return null;
        }

        /// <summary>Each vertex takes the inverse-distance blend of the 4 nearest skin vertices' bone weights.</summary>
        static void Transfer(Mesh skinMesh, Vector3[] verts, BoneWeight[] outWeights)
        {
            const float cell = 0.04f;
            const int K = 4;
            var sv = skinMesh.vertices;
            var sw = skinMesh.boneWeights;
            var grid = new Dictionary<long, List<int>>();
            for (int i = 0; i < sv.Length; i++)
            {
                long key = Key(sv[i], cell);
                if (!grid.TryGetValue(key, out var list)) { list = new List<int>(); grid[key] = list; }
                list.Add(i);
            }
            var bestI = new int[K]; var bestD = new float[K];
            var acc = new Dictionary<int, float>();
            for (int v = 0; v < verts.Length; v++)
            {
                for (int k = 0; k < K; k++) { bestI[k] = -1; bestD[k] = float.MaxValue; }
                var p = verts[v];
                int cx = Mathf.FloorToInt(p.x / cell), cy = Mathf.FloorToInt(p.y / cell), cz = Mathf.FloorToInt(p.z / cell);
                for (int ring = 0; ring < 40; ring++)
                {
                    for (int x = -ring; x <= ring; x++)
                        for (int y = -ring; y <= ring; y++)
                            for (int z = -ring; z <= ring; z++)
                            {
                                if (Mathf.Max(Mathf.Abs(x), Mathf.Max(Mathf.Abs(y), Mathf.Abs(z))) != ring) continue;
                                if (!grid.TryGetValue(Pack(cx + x, cy + y, cz + z), out var list)) continue;
                                foreach (int i in list)
                                {
                                    float d = (sv[i] - p).sqrMagnitude;
                                    if (d >= bestD[K - 1]) continue;
                                    int at = K - 1;
                                    while (at > 0 && bestD[at - 1] > d) { bestD[at] = bestD[at - 1]; bestI[at] = bestI[at - 1]; at--; }
                                    bestD[at] = d; bestI[at] = i;
                                }
                            }
                    // Anything outside this ring is at least ring * cell away.
                    if (bestI[K - 1] >= 0 && bestD[K - 1] <= (ring * cell) * (ring * cell)) break;
                }
                acc.Clear();
                float total = 0f;
                for (int k = 0; k < K; k++)
                {
                    if (bestI[k] < 0) continue;
                    float w = 1f / (Mathf.Sqrt(bestD[k]) + 0.002f);
                    var b = sw[bestI[k]];
                    Add(acc, b.boneIndex0, b.weight0 * w); Add(acc, b.boneIndex1, b.weight1 * w);
                    Add(acc, b.boneIndex2, b.weight2 * w); Add(acc, b.boneIndex3, b.weight3 * w);
                    total += w;
                }
                var top = new List<KeyValuePair<int, float>>(acc);
                top.Sort((a, c) => c.Value.CompareTo(a.Value));
                float sum = 0f;
                for (int k = 0; k < top.Count && k < 4; k++) sum += top[k].Value;
                var o = new BoneWeight();
                if (sum > 0f)
                {
                    if (top.Count > 0) { o.boneIndex0 = top[0].Key; o.weight0 = top[0].Value / sum; }
                    if (top.Count > 1) { o.boneIndex1 = top[1].Key; o.weight1 = top[1].Value / sum; }
                    if (top.Count > 2) { o.boneIndex2 = top[2].Key; o.weight2 = top[2].Value / sum; }
                    if (top.Count > 3) { o.boneIndex3 = top[3].Key; o.weight3 = top[3].Value / sum; }
                }
                outWeights[v] = o;
            }
        }

        static void Add(Dictionary<int, float> acc, int bone, float w)
        {
            if (w <= 0f) return;
            acc.TryGetValue(bone, out float cur);
            acc[bone] = cur + w;
        }

        static long Key(Vector3 p, float cell) { return Pack(Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell), Mathf.FloorToInt(p.z / cell)); }
        static long Pack(int x, int y, int z) { return ((long)(x + 100000) << 40) | ((long)(y + 100000) << 20) | (long)(z + 100000); }

        // ------------------------------------------------------------------ the stage

        void OpenStage()
        {
            if (stage.IsValid()) return;
            stage = EditorSceneManager.NewPreviewScene();
            var camGo = new GameObject("Wizard Camera");
            SceneManager.MoveGameObjectToScene(camGo, stage);
            cam = camGo.AddComponent<Camera>();
            cam.scene = stage;
            cam.enabled = false;
            cam.fieldOfView = 24f;
            cam.nearClipPlane = 0.02f; cam.farClipPlane = 50f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.allowHDR = false; cam.allowMSAA = true;
            keyLight = MakeLight("Key", stage);
            rimLight = MakeLight("Rim", stage);
        }

        static Light MakeLight(string name, Scene scene)
        {
            var g = new GameObject(name);
            SceneManager.MoveGameObjectToScene(g, scene);
            var l = g.AddComponent<Light>();
            l.type = LightType.Directional;
            l.shadows = LightShadows.None;
            return l;
        }

        void CloseStage()
        {
            if (stage.IsValid()) EditorSceneManager.ClosePreviewScene(stage);
            stage = default; stageBody = null; stagePiece = null; cam = null;
        }

        /// <summary>Dresses the stage: the body in the saved look with this piece on, or the piece alone.</summary>
        void Dress(Piece p, Colourway cw)
        {
            OpenStage();
            var cat = DriverCatalog.Instance;
            if (stagePiece != null) DestroyImmediate(stagePiece);
            if (stageBody != null && (stageBody.name != p.body.ToString() || !look.onBody)) { DestroyImmediate(stageBody); stageBody = null; }
            if (look.onBody)
            {
                if (!PoseBody(p.body)) return;
                var mesh = Rig(p, cw, out int clear);
                var temp = new DriverCatalog.Item { id = cw.id, body = p.body, slot = p.slot, style = p.style, part = "", mesh = mesh, clearMask = clear, tag = editTag };
                // Clothes slots need something on: the piece replaces what's saved for that slot.
                DriverLook.Apply(stageBody, p.body, p.slot, mesh != null ? temp : null);
                StripExtras(p.slot);
                if (p.slot == DriverSlot.Hat) HideHair();
            }
            else
            {
                stagePiece = new GameObject("Piece");
                SceneManager.MoveGameObjectToScene(stagePiece, stage);
                foreach (var pf in cw.prefabs)
                {
                    var g = (GameObject)Instantiate(pf, stagePiece.transform);
                    foreach (var r in g.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats = r.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++) mats[i] = mats[i] != null && mats[i].name.Contains("Transparent") && cat.seeThrough != null ? cat.seeThrough : cat.solid;
                        r.sharedMaterials = mats;
                    }
                }
            }
        }

        /// <summary>The body on the stage (made on demand) back in its rest pose, arms relaxed if the look says so.</summary>
        bool PoseBody(DriverBody b)
        {
            OpenStage();
            if (stagePiece != null) DestroyImmediate(stagePiece);
            if (stageBody != null && stageBody.name != b.ToString()) { DestroyImmediate(stageBody); stageBody = null; }
            if (stageBody == null)
            {
                var cat = DriverCatalog.Instance;
                var prefab = cat != null ? cat.Body(b) : null;
                if (prefab == null) return false;
                stageBody = (GameObject)PrefabUtility.InstantiatePrefab(prefab, stage);
                PrefabUtility.UnpackPrefabInstance(stageBody, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                stageBody.name = b.ToString();
                foreach (var mb in stageBody.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null) DestroyImmediate(mb);
                foreach (var a in stageBody.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            }
            ResetPose(b);
            if (look.relaxedArms) { Relax("Arm.L", "ForeArm.L"); Relax("Arm.R", "ForeArm.R"); }
            foreach (var smr in stageBody.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                // Small pack parts keep T-pose bounds: with the arms down a watch would be culled as off camera.
                smr.updateWhenOffscreen = true;
                // Pieces an earlier shot took off (a hat shot hides the hair) come back for this one.
                if (smr.transform.parent != null && smr.transform.parent.name == "Driver Loose Pieces") smr.gameObject.SetActive(true);
            }
            return true;
        }

        /// <summary>A hat's icon shows the hat, not the saved hair poking out under it.</summary>
        void HideHair()
        {
            foreach (var r in stageBody.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.gameObject.name;
                if (n.Contains("_loose_hair_") || (DriverLook.Parse(n, out string code, out _, out _, out _) && code == "HAIR")) r.gameObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------------ icons for every catalog piece

        static bool HeadSlot(DriverSlot s)
        {
            return s == DriverSlot.Hair || s == DriverSlot.Brows || s == DriverSlot.Beard || s == DriverSlot.Earrings || s == DriverSlot.Glasses
                || s == DriverSlot.Mask || s == DriverSlot.Hat || s == DriverSlot.Headphones || s == DriverSlot.Helmet;
        }

        /// <summary>Renders any catalog piece on the driver: head pieces as a head shot (face in view), hand pieces on one hand, clothes on their own bounds.</summary>
        Texture2D ShootItem(DriverCatalog.Item item, int size)
        {
            if (!PoseBody(item.body) || cam == null) return null;
            DriverLook.Apply(stageBody, item.body, item.slot, item);
            StripExtras(item.slot);
            if (item.slot == DriverSlot.Hat) HideHair();
            if (item.slot == DriverSlot.Watch) WristInView(item);
            var tagWas = editTag;
            editTag = item.tag;
            var tex = Capture(ItemFocus(item), size);
            editTag = tagWas;
            return tex;
        }

        Bounds ItemFocus(DriverCatalog.Item item)
        {
            Bounds b = new Bounds(); bool any = false;
            System.Action<Vector3> add = v => { if (!any) { b = new Bounds(v, Vector3.zero); any = true; } else b.Encapsulate(v); };
            var root = stageBody.transform;
            bool oneHand = item.slot == DriverSlot.Gloves || item.slot == DriverSlot.Watch;
            bool tiny = item.slot == DriverSlot.Brows || item.slot == DriverSlot.Earrings; // too small to frame alone
            foreach (var r in stageBody.GetComponentsInChildren<Renderer>())
            {
                string n = r.gameObject.name;
                bool head = n.Contains("_HEAD_");
                bool mine = item.mesh != null ? n == item.id
                    : DriverLook.Parse(n, out _, out _, out _, out string key) && key == item.part;
                if (item.prefab != null && HasParent(r.transform, "Driver Helmet")) mine = true; // a fitted helmet
                if (!(mine && !tiny) && !(head && HeadSlot(item.slot))) continue;
                var smr = r as SkinnedMeshRenderer;
                if (smr == null || smr.sharedMesh == null) { add(r.bounds.min); add(r.bounds.max); continue; }
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                var m = smr.transform.localToWorldMatrix;
                foreach (var v in baked.vertices)
                {
                    var wv = m.MultiplyPoint3x4(v);
                    if (oneHand && root.InverseTransformPoint(wv).x < 0f) continue;
                    add(wv);
                }
                DestroyImmediate(baked);
            }
            if (!any) b = new Bounds(root.position + root.up, Vector3.one);
            // A watch is a few centimetres: show the hand and wrist around it, not a close-up of the sleeve.
            if (item.slot == DriverSlot.Watch) b.size = Vector3.Max(b.size, new Vector3(0.26f, 0.3f, 0.26f));
            return b;
        }

        /// <summary>
        /// Icons show one piece: the saved look's other optional extras (hats, glasses, masks, cans, earrings, a
        /// watch, gloves, a beard, a helmet) come off, so a pair of glasses isn't shot under a cowboy hat.
        /// </summary>
        void StripExtras(DriverSlot keep)
        {
            var cat = DriverCatalog.Instance;
            foreach (var r in stageBody.GetComponentsInChildren<Renderer>(true))
            {
                var go = r.gameObject;
                DriverSlot slot;
                if (DriverLook.Parse(go.name, out string code, out _, out _, out _) && DriverLook.SlotOf(code, out slot)) { }
                else
                {
                    var loose = go.transform.parent != null && go.transform.parent.name == "Driver Loose Pieces" && cat != null ? cat.Find(go.name) : null;
                    if (loose != null) slot = loose.slot;
                    else if (go.transform.parent != null && go.transform.IsChildOf(stageBody.transform) && HasParent(go.transform, "Driver Helmet")) slot = DriverSlot.Helmet;
                    else continue;
                }
                if (slot != keep && DriverCatalog.Optional(slot)) go.SetActive(false);
            }
            // Saved gloves hide the bare hands: with the gloves off for this shot, the hands come back.
            if (keep != DriverSlot.Gloves)
                foreach (var r in stageBody.GetComponentsInChildren<Renderer>(true))
                    if (r.gameObject.name.Contains("_HAND_01_")) r.gameObject.SetActive(true);
        }

        /// <summary>A long sleeve hides the watch: shoot it with the first top that leaves the wrist bare.</summary>
        void WristInView(DriverCatalog.Item watch)
        {
            var cat = DriverCatalog.Instance;
            var root = stageBody.transform;
            Vector3 wrist = Vector3.zero; bool found = false;
            foreach (var r in stageBody.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (DriverLook.Parse(r.gameObject.name, out _, out _, out _, out string key) && key == watch.part)
                {
                    var c = BakedCentre(r);
                    if (root.InverseTransformPoint(c).x >= 0f) { wrist = c; found = true; }
                }
            if (!found || !SleeveOver(wrist)) return;
            foreach (var top in cat.items)
            {
                if (top.body != watch.body || top.slot != DriverSlot.Torso || top.mesh != null || string.IsNullOrEmpty(top.part)) continue;
                DriverLook.Apply(stageBody, watch.body, DriverSlot.Torso, top);
                StripExtras(DriverSlot.Watch);
                foreach (var r in stageBody.GetComponentsInChildren<Renderer>(true))
                    if (DriverLook.Parse(r.gameObject.name, out _, out _, out _, out string key) && key == watch.part) r.gameObject.SetActive(true);
                if (!SleeveOver(wrist)) return;
            }
        }

        /// <summary>Does the top being worn reach over this point (the watch on the wrist)?</summary>
        bool SleeveOver(Vector3 point)
        {
            var cat = DriverCatalog.Instance;
            foreach (var r in stageBody.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                string n = r.gameObject.name;
                bool torso = DriverLook.Parse(n, out string code, out _, out _, out _) ? code == "TORSO"
                    : r.transform.parent != null && r.transform.parent.name == "Driver Loose Pieces" && cat.Find(n) != null && cat.Find(n).slot == DriverSlot.Torso;
                if (!torso) continue;
                var baked = new Mesh();
                r.BakeMesh(baked, true);
                var m = r.transform.localToWorldMatrix;
                bool over = false;
                foreach (var v in baked.vertices) if ((m.MultiplyPoint3x4(v) - point).sqrMagnitude < 0.05f * 0.05f) { over = true; break; }
                DestroyImmediate(baked);
                if (over) return true;
            }
            return false;
        }

        static Vector3 BakedCentre(SkinnedMeshRenderer r)
        {
            var baked = new Mesh();
            r.BakeMesh(baked, true);
            var c = r.transform.localToWorldMatrix.MultiplyPoint3x4(baked.bounds.center);
            DestroyImmediate(baked);
            return c;
        }

        static bool HasParent(Transform t, string name)
        {
            for (var p = t.parent; p != null; p = p.parent) if (p.name == name) return true;
            return false;
        }

        /// <summary>Re-renders the icon of every piece in the game in the current look, so they all match.</summary>
        void RenderAllIcons(DriverCatalog cat, System.Predicate<DriverCatalog.Item> only = null)
        {
            Directory.CreateDirectory(IconDir);
            var made = new List<string>();
            try
            {
                for (int i = 0; i < cat.items.Count; i++)
                {
                    var item = cat.items[i];
                    if (string.IsNullOrEmpty(item.id) || (item.mesh == null && item.prefab == null && string.IsNullOrEmpty(item.part))) continue;
                    if (only != null && !only(item)) continue;
                    if (EditorUtility.DisplayCancelableProgressBar("Wardrobe Wizard", "Icon for " + item.displayName + " (" + item.id + ")", (float)i / cat.items.Count)) break;
                    var tex = ShootItem(item, IconSize);
                    if (tex == null) continue;
                    File.WriteAllBytes(IconDir + item.id + ".png", tex.EncodeToPNG());
                    DestroyImmediate(tex);
                    // Built-in pieces keep the pack sprite's swatch (the piece's own colour, no skin in it).
                    // Stock helmets keep their tint as the swatch.
                    if (item.mesh != null || (item.icon == null && item.prefab == null)) item.swatch = lastSwatch;
                    made.Add(item.id);
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            // One at a time: inside an asset-editing batch new files import as plain textures, not sprites.
            for (int i = 0; i < made.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Wardrobe Wizard", "Importing icons", (float)i / made.Count);
                string path = IconDir + made[i] + ".png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                if (imp != null && (imp.textureType != TextureImporterType.Sprite || imp.mipmapEnabled))
                {
                    imp.textureType = TextureImporterType.Sprite;
                    imp.spriteImportMode = SpriteImportMode.Single;
                    imp.alphaIsTransparency = true;
                    imp.mipmapEnabled = false;
                    imp.SaveAndReimport();
                }
            }
            EditorUtility.ClearProgressBar();
            foreach (var item in cat.items)
                if (made.Contains(item.id)) item.icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconDir + item.id + ".png");
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            dirty = true;
            Debug.Log("[Driver] Wardrobe Wizard re-rendered " + made.Count + " icons.");
        }

        void ResetPose(DriverBody b)
        {
            var prefab = DriverCatalog.Instance.Body(b);
            var rest = new Dictionary<string, Transform>();
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true)) rest[t.name] = t;
            foreach (var t in stageBody.GetComponentsInChildren<Transform>(true))
                if (t != stageBody.transform && rest.TryGetValue(t.name, out var r)) { t.localPosition = r.localPosition; t.localRotation = r.localRotation; }
        }

        /// <summary>Same arm swing as the showroom's DriverIdle, so icons show the pose players see.</summary>
        void Relax(string upper, string lower)
        {
            Transform arm = null, fore = null;
            foreach (var t in stageBody.GetComponentsInChildren<Transform>(true)) { if (t.name == upper) arm = t; else if (t.name == lower) fore = t; }
            if (arm == null || fore == null) return;
            var root = stageBody.transform;
            Vector3 dir = (fore.position - arm.position).normalized;
            float side = Mathf.Sign(Vector3.Dot(dir, root.right));
            Vector3 want = (-root.up * 0.95f + root.right * side * 0.3f).normalized;
            arm.rotation = Quaternion.FromToRotation(dir, want) * arm.rotation;
        }

        /// <summary>World bounds of what the camera should frame.</summary>
        Bounds Focus(Piece p, Colourway cw)
        {
            Bounds b = new Bounds();
            bool any = false;
            System.Action<Bounds> add = x => { if (!any) { b = x; any = true; } else b.Encapsulate(x); };
            var framing = look.framing;
            if (stageBody != null && framing != Framing.Auto)
            {
                var all = new Bounds(); bool a = false;
                foreach (var r in stageBody.GetComponentsInChildren<Renderer>()) { if (!a) { all = r.bounds; a = true; } else all.Encapsulate(r.bounds); }
                float h = all.size.y, y0 = all.min.y;
                Vector3 c = all.center;
                switch (framing)
                {
                    case Framing.Head: return new Bounds(new Vector3(c.x, y0 + h * 0.91f, c.z), new Vector3(0.36f, 0.36f, 0.36f));
                    case Framing.Torso: return new Bounds(new Vector3(c.x, y0 + h * 0.68f, c.z), new Vector3(0.7f, 0.62f, 0.4f));
                    case Framing.Legs: return new Bounds(new Vector3(c.x, y0 + h * 0.3f, c.z), new Vector3(0.5f, 0.8f, 0.4f));
                    case Framing.Feet: return new Bounds(new Vector3(c.x, y0 + h * 0.06f, c.z), new Vector3(0.4f, 0.24f, 0.4f));
                    default: return all;
                }
            }
            if (stageBody != null)
            {
                var holder = stageBody.GetComponentsInChildren<Transform>(true);
                foreach (var t in holder)
                {
                    if (t.name != cw.id) continue;
                    var smr = t.GetComponent<SkinnedMeshRenderer>();
                    if (smr == null) continue;
                    var baked = new Mesh();
                    smr.BakeMesh(baked, true);
                    var m = smr.transform.localToWorldMatrix;
                    // Gloves come as a pair: frame the left hand so the glove fills the icon.
                    var root = stageBody.transform;
                    foreach (var v in baked.vertices)
                    {
                        var wv = m.MultiplyPoint3x4(v);
                        if (p.slot == DriverSlot.Gloves && root.InverseTransformPoint(wv).x < 0f) continue;
                        add(new Bounds(wv, Vector3.zero));
                    }
                    DestroyImmediate(baked);
                }
            }
            if (!any && stagePiece != null) foreach (var r in stagePiece.GetComponentsInChildren<Renderer>()) add(r.bounds);
            if (!any) b = new Bounds(Vector3.up, Vector3.one);
            return b;
        }

        Color RimColour()
        {
            if (!look.rimFollowsTag) return look.rimColour;
            return editTag == DriverStyle.Gloss ? Gloss : editTag == DriverStyle.Grit ? Grit : Magenta;
        }

        /// <summary>Renders the picked piece in the current look (backdrop included) into a new texture.</summary>
        Texture2D Shoot(Piece p, Colourway cw, int size)
        {
            Dress(p, cw);
            if (cam == null) return null;
            return Capture(Focus(p, cw), size);
        }

        /// <summary>Frames the bounds in the current look, renders and lays the backdrop under it.</summary>
        Texture2D Capture(Bounds f, int size)
        {
            float radius = Mathf.Max(0.08f, f.extents.magnitude) * 1.08f;
            float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / look.zoom;
            // The bodies face +Z: the camera starts in front and orbits.
            var rot = Quaternion.Euler(look.pitch, 180f + look.yaw, 0f);
            cam.transform.position = f.center - rot * Vector3.forward * dist;
            cam.transform.rotation = rot;
            keyLight.transform.rotation = rot * Quaternion.Euler(25f, -35f, 0f);
            keyLight.intensity = look.key;
            keyLight.color = new Color(1f, 0.96f, 0.92f);
            rimLight.transform.rotation = Quaternion.LookRotation(-(rot * new Vector3(-0.5f, 0.45f, 1f)));
            rimLight.intensity = look.rim;
            rimLight.color = RimColour();

            var rt = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            var prevActive = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            RenderTexture.active = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);
            lastSwatch = PieceColour(tex);
            Composite(tex);
            return tex;
        }

        Color lastSwatch = Color.gray;

        /// <summary>The average colour of what was rendered (before the backdrop), for the Driver tab's swatch.</summary>
        static Color PieceColour(Texture2D tex)
        {
            var px = tex.GetPixels32();
            double r = 0, g = 0, b = 0; int n = 0;
            for (int i = 0; i < px.Length; i += 5)
            {
                if (px[i].a < 200) continue;
                r += px[i].r; g += px[i].g; b += px[i].b; n++;
            }
            return n == 0 ? Color.gray : new Color((float)(r / n / 255.0), (float)(g / n / 255.0), (float)(b / n / 255.0));
        }

        /// <summary>Lays the render over the chosen backdrop.</summary>
        void Composite(Texture2D tex)
        {
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels();
            Color top = new Color32(14, 8, 28, 255), bottom = new Color32(70, 14, 64, 255);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    var c = px[i];
                    if (look.backdrop == Backdrop.Transparent) continue;
                    Color bg;
                    float u = (float)x / w - 0.5f, v = (float)y / h - 0.5f;
                    if (look.backdrop == Backdrop.Channel13)
                    {
                        bg = Color.Lerp(bottom, top, (float)y / h);
                        float glow = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + (v + 0.1f) * (v + 0.1f)) * 2.1f);
                        bg += new Color(Magenta.r, Magenta.g, Magenta.b) * glow * glow * 0.35f;
                        if (y < h * 0.035f) bg = Color.Lerp(bg, Gloss, 0.85f); // the CH13 ticker strip
                        if ((y & 3) == 0) bg *= 0.94f;                         // scanlines
                    }
                    else if (look.backdrop == Backdrop.DarkStudio)
                    {
                        float r = Mathf.Clamp01(Mathf.Sqrt(u * u + v * v) * 1.6f);
                        bg = Color.Lerp(new Color(0.2f, 0.2f, 0.22f), new Color(0.04f, 0.04f, 0.05f), r);
                    }
                    else bg = look.solid;
                    bg.a = 1f;
                    px[i] = new Color(Mathf.Lerp(bg.r, c.r, c.a), Mathf.Lerp(bg.g, c.g, c.a), Mathf.Lerp(bg.b, c.b, c.a), 1f);
                }
            tex.SetPixels(px);
            tex.Apply();
        }

        void RenderPreview()
        {
            if (this == null || picked == null || picked.colours.Count == 0) return;
            pickedColour = Mathf.Clamp(pickedColour, 0, picked.colours.Count - 1);
            var tex = Shoot(picked, picked.colours[pickedColour], 512);
            if (preview != null) DestroyImmediate(preview);
            preview = tex;
            Repaint();
        }

        /// <summary>Saves the icon PNG and imports it as a sprite. Returns its asset path.</summary>
        string SaveIcon(Piece p, Colourway cw, DriverCatalog cat)
        {
            var tex = Shoot(p, cw, IconSize);
            if (tex == null) return null;
            Directory.CreateDirectory(IconDir);
            string path = IconDir + cw.id + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp != null && (imp.textureType != TextureImporterType.Sprite || imp.mipmapEnabled))
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }
            dirty = true;
            return path;
        }
    }
}
