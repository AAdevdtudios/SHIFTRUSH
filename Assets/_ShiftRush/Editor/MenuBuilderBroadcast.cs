using System.Collections.Generic;
using ShiftRush.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// The home screen in the pirate stream style (menu D): GRIP PRO top left, the live tag, viewers and Hype
    /// top right of the shot, a row of stream panels along the bottom, and The Feed chat column down the right.
    /// Called from Build Main Menu.
    /// </summary>
    public static partial class MenuBuilder
    {
        const string FontDir = "Assets/_ShiftRush/Art/Fonts/";
        static readonly Color Pink = new Color32(255, 46, 154, 255);
        static readonly Color Signal = new Color32(46, 242, 255, 255);
        static readonly Color Gold = new Color32(255, 197, 61, 255);
        static readonly Color Live = new Color32(232, 35, 58, 255);
        static readonly Color Night = new Color32(11, 10, 18, 255);
        static readonly Color Paper = new Color32(244, 241, 250, 255);
        static readonly Color Mist = new Color32(189, 183, 209, 255);
        static readonly Color Dim = new Color32(140, 134, 163, 255);

        static TMP_FontAsset bsBlack, bsBold, barlowBold, barlowSemi, barlowMedium, monoBold, monoMedium;

        static void LoadBroadcastFonts()
        {
            bsBlack = FontAsset("BigShouldersDisplay-Black");
            bsBold = FontAsset("BigShouldersDisplay-Bold");
            barlowBold = FontAsset("BarlowCondensed-Bold");
            barlowSemi = FontAsset("BarlowCondensed-SemiBold");
            barlowMedium = FontAsset("BarlowCondensed-Medium");
            monoBold = FontAsset("JetBrainsMono-Bold");
            monoMedium = FontAsset("JetBrainsMono-Medium");
            bsBlack.italicStyle = 26; // GRIP PRO leans forward
            EditorUtility.SetDirty(bsBlack);
        }

        static TMP_FontAsset FontAsset(string file)
        {
            string path = FontDir + file + " SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontDir + file + ".ttf");
            var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fa.name = file + " SDF";
            AssetDatabase.CreateAsset(fa, path);
            fa.material.name = file + " SDF Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            foreach (var tex in fa.atlasTextures) { tex.name = file + " SDF Atlas"; AssetDatabase.AddObjectToAsset(tex, fa); }
            AssetDatabase.SaveAssets();
            return fa;
        }

        static Animator BuildHomeBroadcast(Transform canvas, MenuRouter router, Showroom showroom)
        {
            var panel = canvas.Find("Main Content/Home Panel");
            var content = panel.Find("Content");
            Object.DestroyImmediate(content.Find("Box Container").gameObject);

            var feed = BuildFeed(content);
            BuildLogo(content);
            BuildLiveChips(content, feed);
            BuildMenuRow(content, router, showroom, feed, panel.GetComponent<CanvasGroup>());

            SetupHotkeys(panel, router, "Quit", router.AskQuit);
            return panel.GetComponent<Animator>();
        }

        // ------------------------------------------------------------------ logo

        static void BuildLogo(Transform content)
        {
            var logo = Rect(content, "Logo");
            Place(logo, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -50), new Vector2(900, 200));

            var s1 = Img(logo, "Stripe Pink", null, Pink); s1.raycastTarget = false;
            Place(s1.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(4, 0), new Vector2(288, 6));
            var s2 = Img(logo, "Stripe Cyan", null, Signal); s2.raycastTarget = false;
            Place(s2.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(34, -13), new Vector2(216, 6));

            var shadow = Txt(logo, "Title Echo", "GRIP PRO", bsBlack, 150, Signal, TextAlignmentOptions.TopLeft);
            shadow.fontStyle = FontStyles.Italic;
            shadow.characterSpacing = 4;
            shadow.textWrappingMode = TextWrappingModes.NoWrap;
            Place(shadow.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -40), new Vector2(900, 170));
            var title = Txt(logo, "Title", "GRIP PRO", bsBlack, 150, Pink, TextAlignmentOptions.TopLeft);
            title.fontStyle = FontStyles.Italic;
            title.characterSpacing = 4;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -32), new Vector2(900, 170));
        }

        // ------------------------------------------------------------------ live / viewers / hype

        static void BuildLiveChips(Transform content, TheFeed feed)
        {
            var row = Rect(content, "Live Chips");
            Place(row, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-504 - 56, -50), new Vector2(700, 40));
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleRight;
            h.spacing = 12;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;

            var live = Chip(row, "Live", Live, "LIVE", monoBold, 17, Color.white, true);
            var dot = Img(live, "Dot", AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"), Color.white);
            dot.raycastTarget = false;
            dot.transform.SetAsFirstSibling();
            var le = dot.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = le.preferredHeight = 11; le.minHeight = 11;
            dot.gameObject.AddComponent<LivePulse>();

            var viewers = Chip(row, "Viewers", new Color(Night.r, Night.g, Night.b, 0.8f), "48.2K WATCHING", monoMedium, 18, Paper, false);
            var hype = Chip(row, "Hype", new Color(Night.r, Night.g, Night.b, 0.8f), "0 HYPE", monoBold, 18, Gold, false);

            var so = new SerializedObject(feed);
            so.FindProperty("viewersText").objectReferenceValue = viewers.GetComponentInChildren<TMP_Text>();
            so.FindProperty("hypeText").objectReferenceValue = hype.GetComponentInChildren<TMP_Text>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static RectTransform Chip(RectTransform parent, string name, Color bg, string text, TMP_FontAsset font, float size, Color colour, bool bold)
        {
            var img = Img(parent, name, null, bg);
            img.raycastTarget = false;
            var h = img.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(13, 13, 7, 7);
            h.spacing = 9;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            var t = Txt(img.transform, "Text", text, font, size, colour, TextAlignmentOptions.Center);
            t.characterSpacing = bold ? 12 : 2;
            return img.rectTransform;
        }

        // ------------------------------------------------------------------ menu row

        static void BuildMenuRow(Transform content, MenuRouter router, Showroom showroom, TheFeed feed, CanvasGroup visible)
        {
            var nav = Rect(content, "Broadcast Menu");
            Place(nav, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(58, 58), new Vector2(1300, 260));
            var menu = nav.gameObject.AddComponent<BroadcastMenu>();

            var hint = Txt(nav, "Hint", "<color=#FF2E9A>A D</color>  MOVE     <color=#FF2E9A>W S</color>  SETTINGS     <color=#FF2E9A>ENTER</color>  GO", monoMedium, 15, Dim, TextAlignmentOptions.Left);
            hint.characterSpacing = 6;
            Place(hint.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 186), new Vector2(900, 26));

            var entries = new List<(string label, string caption, float w, float h, bool small, bool locked, string lockedMsg, UnityAction call, bool web)>
            {
                ("STORY", "ACT 1 · COMING SOON", 276, 128, false, true, "Act 1: The League is still being filmed. Story mode drops soon.", null, false),
                ("QUICK RACE", "LAST TRACK", 228, 112, false, false, null, router.QuickRace, false),
                ("GARAGE", "YOUR CARS", 190, 112, false, false, null, router.OpenGarage, false),
                ("FREE ROAM", "NO RULES", 200, 112, false, false, null, router.FreeRoam, false),
                ("SETTINGS", null, 168, 40, true, false, null, router.OpenSettings, false),
                ("CREDITS", null, 168, 40, true, false, null, router.OpenCredits, false),
                ("QUIT", null, 168, 40, true, false, null, router.AskQuit, true),
            };

            var so = new SerializedObject(menu);
            var arr = so.FindProperty("items");
            arr.arraySize = entries.Count;
            float x = 0f;
            int smallIndex = 0;
            float stackX = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var bg = Img(nav, e.label.Replace(" ", "") + " Panel", null, new Color(Night.r, Night.g, Night.b, 0.9f));
                var rt = bg.rectTransform;
                TMP_Text label, caption = null;
                if (!e.small)
                {
                    Place(rt, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(x, 0), new Vector2(e.w, e.h));
                    x += e.w + 10f;
                    stackX = x;
                    caption = Txt(rt, "Caption", e.caption, monoBold, 14, e.label == "GARAGE" ? Gold : Dim, TextAlignmentOptions.TopLeft);
                    caption.characterSpacing = 8;
                    caption.textWrappingMode = TextWrappingModes.NoWrap;
                    Place(caption.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(22, -16), new Vector2(-30, 22));
                    label = Txt(rt, "Label", e.label, i == 0 ? bsBlack : bsBold, i == 0 ? 66 : 48, Paper, TextAlignmentOptions.BottomLeft);
                    label.textWrappingMode = TextWrappingModes.NoWrap;
                    Place(label.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0), new Vector2(20, 12), new Vector2(-30, -44));
                }
                else
                {
                    Place(rt, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(stackX, (2 - smallIndex) * (e.h + 7f)), new Vector2(e.w, e.h));
                    smallIndex++;
                    label = Txt(rt, "Label", e.label, bsBold, 28, e.label == "QUIT" ? Dim : Mist, TextAlignmentOptions.Left);
                    Place(label.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(-20, 0));
                }

                var p = arr.GetArrayElementAtIndex(i);
                p.FindPropertyRelative("root").objectReferenceValue = rt;
                p.FindPropertyRelative("background").objectReferenceValue = bg;
                p.FindPropertyRelative("label").objectReferenceValue = label;
                p.FindPropertyRelative("caption").objectReferenceValue = caption;
                p.FindPropertyRelative("small").boolValue = e.small;
                p.FindPropertyRelative("locked").boolValue = e.locked;
                p.FindPropertyRelative("lockedMessage").stringValue = e.lockedMsg ?? "";
                p.FindPropertyRelative("hideOnWeb").boolValue = e.web;
            }
            so.FindProperty("visibleGroup").objectReferenceValue = visible;
            so.FindProperty("showroom").objectReferenceValue = showroom;
            so.FindProperty("feed").objectReferenceValue = feed;
            so.ApplyModifiedPropertiesWithoutUndo();

            var field = typeof(BroadcastMenu).GetField("items", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var list = (List<BroadcastMenu.Item>)field.GetValue(menu);
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].call != null) UnityEventTools.AddVoidPersistentListener(list[i].onEngage, entries[i].call);
            EditorUtility.SetDirty(menu);
        }


        // ------------------------------------------------------------------ garage

        /// <summary>
        /// The crew's garage in the stream style: your cars down the left, the car on the turntable in the middle
        /// with the tuning slots teased above it, name, stats and drift character on the right, and the button
        /// on to track select.
        /// </summary>
        static Animator BuildGarageBroadcast(Transform canvas, MenuRouter router, Showroom showroom)
        {
            var panel = RepurposePanel(canvas, "Shop", MenuRouter.Garage);
            var content = panel.Find("Content");
            var cat = AssetDatabase.LoadAssetAtPath<ShiftRush.Core.RaceCatalog>("Assets/_ShiftRush/Resources/RaceCatalog.asset");
            int count = cat != null ? cat.cars.Count : 0;

            // Header, top left: title with the cyan echo.
            var echo = Txt(content, "Title Echo", "THE CREW'S GARAGE", bsBlack, 84, Signal, TextAlignmentOptions.Left);
            Place(echo.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(66, -46), new Vector2(1100, 100));
            var title = Txt(content, "Title", "THE CREW'S GARAGE", bsBlack, 84, Paper, TextAlignmentOptions.Left);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -40), new Vector2(1100, 100));

            // Top right: live tag, camera label, Hype.
            var chips = Rect(content, "Live Chips");
            Place(chips, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-60, -54), new Vector2(900, 40));
            var h = chips.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleRight; h.spacing = 12;
            h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = h.childForceExpandHeight = false;
            var live = Chip(chips, "Live", Live, "LIVE", monoBold, 17, Color.white, true);
            var dot = Img(live, "Dot", AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"), Color.white);
            dot.raycastTarget = false; dot.transform.SetAsFirstSibling();
            var le = dot.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = le.preferredHeight = 11;
            dot.gameObject.AddComponent<LivePulse>();
            Chip(chips, "Camera", new Color(Night.r, Night.g, Night.b, 0.8f), "CAM 04 // WORKSHOP", monoMedium, 17, Mist, false);
            var hype = Chip(chips, "Hype", new Color(Night.r, Night.g, Night.b, 0.8f), "0 HYPE", monoBold, 18, Gold, false);
            hype.gameObject.AddComponent<HypeLabel>();

            // Your cars, down the left.
            var listTitle = Txt(content, "List Title", "YOUR CARS  " + count + " / " + count, monoBold, 16, Dim, TextAlignmentOptions.Left);
            listTitle.characterSpacing = 8;
            Place(listTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(66, -178), new Vector2(400, 24));
            var backs = new Image[count]; var names = new TMP_Text[count]; var subs = new TMP_Text[count]; var buttons = new Button[count];
            for (int i = 0; i < count; i++)
            {
                var car = cat.cars[i];
                var bg = Img(content, "Car " + (i + 1), null, new Color32(26, 22, 40, 255));
                Place(bg.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(66, -212 - i * 92), new Vector2(330, 82));
                var accent = Img(bg.transform, "Accent", null, car.accent); accent.raycastTarget = false;
                Place(accent.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(6, 0));
                var n = Txt(bg.transform, "Name", car.displayName.ToUpperInvariant(), bsBlack, 40, Paper, TextAlignmentOptions.TopLeft);
                n.textWrappingMode = TextWrappingModes.NoWrap;
                Place(n.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(22, -6), new Vector2(-30, 46));
                var drift = car.prefab != null && car.prefab.GetComponent<ShiftRush.Vehicles.DriftMoves>() != null
                    ? car.prefab.GetComponent<ShiftRush.Vehicles.DriftMoves>().Character : ShiftRush.Vehicles.CarTypes.Drift(car.type);
                var sub = Txt(bg.transform, "Type", ShiftRush.Vehicles.CarTypes.Name(car.type) + "  ·  " + drift.ToString().ToUpperInvariant(), barlowBold, 19, Dim, TextAlignmentOptions.TopLeft);
                sub.textWrappingMode = TextWrappingModes.NoWrap;
                Place(sub.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(22, -50), new Vector2(-30, 26));
                var b = bg.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
                backs[i] = bg; names[i] = n; subs[i] = sub; buttons[i] = b;
            }

            // Tuning slots, teased over the car until parts arrive.
            var slots = Rect(content, "Tuning Slots");
            Place(slots, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-40, -176), new Vector2(680, 74));
            var sh = slots.gameObject.AddComponent<HorizontalLayoutGroup>();
            sh.spacing = 10; sh.childControlWidth = sh.childControlHeight = true; sh.childForceExpandWidth = true; sh.childForceExpandHeight = true;
            foreach (var slot in new[] { "TYRES", "SPOILER", "HOOD INLET", "ROOF" })
            {
                var sbg = Img(slots, slot, null, new Color(Night.r, Night.g, Night.b, 0.78f)); sbg.raycastTarget = false;
                var st = Txt(sbg.transform, "Slot", slot, monoBold, 14, Dim, TextAlignmentOptions.TopLeft);
                st.characterSpacing = 8;
                Place(st.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(14, -10), new Vector2(-20, 20));
                var sv = Txt(sbg.transform, "Value", "SOON", bsBold, 30, new Color(Paper.r, Paper.g, Paper.b, 0.45f), TextAlignmentOptions.TopLeft);
                Place(sv.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(14, -32), new Vector2(-20, 36));
            }
            var tuneTag = Txt(content, "Tuning Tag", "TUNING OPENS SOON  ·  EARN HYPE ON THE STREAM", monoMedium, 14, Dim, TextAlignmentOptions.Center);
            tuneTag.characterSpacing = 6;
            Place(tuneTag.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-40, -258), new Vector2(680, 22));

            // Right column: name, line, stats, drift character, go button.
            const float RX = -60f, RW = 470f;
            var nameText = Txt(content, "Car Name", "ZEPHYR", bsBlack, 104, Paper, TextAlignmentOptions.Right);
            Place(nameText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(RX, -166), new Vector2(700, 110));
            var tag = Txt(content, "Tagline", "", barlowSemi, 22, Mist, TextAlignmentOptions.TopRight);
            Place(tag.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(RX, -276), new Vector2(RW, 64));
            var counter = Txt(content, "Counter", "01 / 06", monoBold, 16, Dim, TextAlignmentOptions.Right);
            counter.characterSpacing = 8;
            Place(counter.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(RX, -150), new Vector2(300, 22));

            string[] statNames = { "TOP SPEED", "ACCELERATION", "HANDLING", "NITRO" };
            var bars = new StatBar[4];
            for (int s2 = 0; s2 < 4; s2++)
            {
                var row = Rect(content, statNames[s2]);
                Place(row, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(RX, -364 - s2 * 54), new Vector2(RW, 46));
                var l = Txt(row, "Label", statNames[s2], barlowBold, 22, Paper, TextAlignmentOptions.TopLeft);
                l.textWrappingMode = TextWrappingModes.NoWrap;
                Place(l.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), Vector2.zero, new Vector2(0, 26));
                var segs = new Image[10];
                for (int k = 0; k < 10; k++)
                {
                    segs[k] = Img(row, "Seg " + k, null, new Color(1, 1, 1, 0.12f));
                    segs[k].raycastTarget = false;
                    Place(segs[k].rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(k * (RW / 10f), 0), new Vector2(RW / 10f - 4f, 11));
                }
                bars[s2] = row.gameObject.AddComponent<StatBar>();
                var bso = new SerializedObject(bars[s2]);
                var sp = bso.FindProperty("segments");
                sp.arraySize = 10;
                for (int k = 0; k < 10; k++) sp.GetArrayElementAtIndex(k).objectReferenceValue = segs[k];
                bso.FindProperty("empty").colorValue = new Color32(42, 37, 64, 255);
                bso.ApplyModifiedPropertiesWithoutUndo();
            }

            var driftBox = Img(content, "Drift Character", null, new Color32(20, 17, 31, 235)); driftBox.raycastTarget = false;
            Place(driftBox.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(RX, -592), new Vector2(RW, 150));
            var dTitle = Txt(driftBox.transform, "Title", "DRIFT CHARACTER", monoBold, 15, Dim, TextAlignmentOptions.TopLeft);
            dTitle.characterSpacing = 8;
            Place(dTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(16, -14), new Vector2(-30, 22));
            var pills = new Image[3]; var pillText = new TMP_Text[3];
            string[] pn = { "DRIFTY", "BALANCED", "GRIPPY" };
            for (int i = 0; i < 3; i++)
            {
                var pb = Img(driftBox.transform, pn[i], null, new Color32(26, 22, 40, 255)); pb.raycastTarget = false;
                float pw = (RW - 32f - 12f) / 3f;
                Place(pb.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(16 + i * (pw + 6f), -46), new Vector2(pw, 42));
                var pt = Txt(pb.transform, "Label", pn[i], barlowBold, 22, Dim, TextAlignmentOptions.Center);
                Stretch(pt.rectTransform);
                pills[i] = pb; pillText[i] = pt;
            }
            var note = Txt(driftBox.transform, "Note", "", barlowMedium, 20, Mist, TextAlignmentOptions.TopLeft);
            Place(note.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(16, -100), new Vector2(-30, 44));

            // Go button: pink slab with the cyan offset.
            var goShadow = Img(content, "Go Shadow", null, Signal); goShadow.raycastTarget = false;
            Place(goShadow.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(RX + 8, 142), new Vector2(RW, 74));
            var go = Img(content, "Go", null, Pink);
            Place(go.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(RX, 150), new Vector2(RW, 74));
            var goText = Txt(go.transform, "Label", "PICK A TRACK", bsBlack, 46, Night, TextAlignmentOptions.Center);
            Stretch(goText.rectTransform);
            var goButton = go.gameObject.AddComponent<Button>();
            goButton.transition = Selectable.Transition.None;
            var gnav = goButton.navigation; gnav.mode = Navigation.Mode.None; goButton.navigation = gnav;

            var hint = Txt(content, "Hint", "<color=#FF2E9A>W S</color>  CHANGE CAR     <color=#FF2E9A>ENTER</color>  PICK A TRACK", monoMedium, 15, Dim, TextAlignmentOptions.Left);
            hint.characterSpacing = 6;
            Place(hint.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(66, 40), new Vector2(900, 26));

            var csp = panel.gameObject.AddComponent<CarSelectPanel>();
            var so = new SerializedObject(csp);
            so.FindProperty("visibleGroup").objectReferenceValue = panel.GetComponent<CanvasGroup>();
            so.FindProperty("showroom").objectReferenceValue = showroom;
            so.FindProperty("carName").objectReferenceValue = nameText;
            so.FindProperty("tagline").objectReferenceValue = tag;
            so.FindProperty("counter").objectReferenceValue = counter;
            var st2 = so.FindProperty("stats"); st2.arraySize = 4;
            for (int i = 0; i < 4; i++) st2.GetArrayElementAtIndex(i).objectReferenceValue = bars[i];
            so.FindProperty("confirmButton").objectReferenceValue = goButton;
            SetArray(so, "listBacks", backs); SetArray(so, "listNames", names); SetArray(so, "listSubs", subs); SetArray(so, "listButtons", buttons);
            SetArray(so, "driftPills", pills); SetArray(so, "driftLabels", pillText);
            so.FindProperty("driftNote").objectReferenceValue = note;
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddVoidPersistentListener(csp.onConfirm, router.OpenTracks);

            CloneHotkeys(canvas.Find("Main Content/Home Panel"), content, router, "Back", router.Back);
            return panel.GetComponent<Animator>();
        }

        static void SetArray(SerializedObject so, string field, Object[] values)
        {
            var p = so.FindProperty(field);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }


        // ------------------------------------------------------------------ track select

        static Sprite outline;

        /// <summary>
        /// Tonight's events: the picked track's photo behind the live car, its name and story line on the left,
        /// your car, the circuit and difficulty down the right, and the track cards along the bottom.
        /// Same wiring as before (TrackSelectPanel), in the stream look.
        /// </summary>
        static Animator BuildTracksBroadcast(Transform canvas, MenuRouter router, Showroom showroom)
        {
            outline = SaveSprite("T_Outline", 64, 64, (x, y, w, h) => (x < 3 || y < 3 || x >= w - 3 || y >= h - 3) ? 1f : 0f, new Vector4(4, 4, 4, 4));
            var panel = RepurposePanel(canvas, "Extras", MenuRouter.Tracks);
            var content = panel.Find("Content");

            // Header with echo, and the live chips.
            var echo = Txt(content, "Title Echo", "TONIGHT'S EVENTS", bsBlack, 84, Signal, TextAlignmentOptions.Left);
            Place(echo.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(66, -46), new Vector2(1100, 100));
            var head = Txt(content, "Title", "TONIGHT'S EVENTS", bsBlack, 84, Paper, TextAlignmentOptions.Left);
            Place(head.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -40), new Vector2(1100, 100));
            var chips = Rect(content, "Live Chips");
            Place(chips, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-60, -54), new Vector2(900, 40));
            var h = chips.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleRight; h.spacing = 12;
            h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = h.childForceExpandHeight = false;
            var live = Chip(chips, "Live", Live, "LIVE", monoBold, 17, Color.white, true);
            var dot = Img(live, "Dot", AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"), Color.white);
            dot.raycastTarget = false; dot.transform.SetAsFirstSibling();
            var le = dot.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = le.preferredHeight = 11;
            dot.gameObject.AddComponent<LivePulse>();
            Chip(chips, "Camera", new Color(Night.r, Night.g, Night.b, 0.8f), "CAM 07 // ROUTE PLANNER", monoMedium, 17, Mist, false);
            Chip(chips, "Hype", new Color(Night.r, Night.g, Night.b, 0.8f), "0 HYPE", monoBold, 18, Gold, false).gameObject.AddComponent<HypeLabel>();

            // The track photo, on its own canvas far behind the live car, melted into a wide band.
            var showCam = (Camera)new SerializedObject(showroom).FindProperty("cam").objectReferenceValue;
            var backdropGo = new GameObject("Canvas - Track Backdrop", typeof(RectTransform));
            var backdropCanvas = backdropGo.AddComponent<Canvas>();
            backdropCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            backdropCanvas.worldCamera = showCam;
            backdropCanvas.planeDistance = 18f;
            var bs = backdropGo.AddComponent<CanvasScaler>();
            bs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; bs.referenceResolution = new Vector2(1920, 1080); bs.matchWidthOrHeight = 0.5f;
            var backdropGroup = backdropGo.AddComponent<CanvasGroup>();
            backdropGroup.alpha = 0f; backdropGroup.interactable = false; backdropGroup.blocksRaycasts = false;
            var band = Rect(backdropGo.transform, "Hero Band");
            Place(band, new Vector2(-0.02f, 0.1f), new Vector2(1.02f, 0.96f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var bandMask = band.gameObject.AddComponent<AMS.UI.SoftMask.UISoftMask>();
            bandMask.mask = softBand; bandMask.fallOff = 0.6f;
            var heroImg = Img(band, "Hero", null, new Color(0.78f, 0.78f, 0.86f, 1f));
            heroImg.raycastTarget = false;
            Stretch(heroImg.rectTransform);
            var heroFit = heroImg.gameObject.AddComponent<AspectRatioFitter>();
            heroFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; heroFit.aspectRatio = 16f / 9f;
            var scan = Img(band, "Signal Tint", null, new Color(0.18f, 0.08f, 0.3f, 0.28f)); // night grade over the photo
            scan.raycastTarget = false; Stretch(scan.rectTransform);

            // Left: track name, story line, details.
            var tag = Txt(content, "Event Tag", "EVENT", monoBold, 16, Pink, TextAlignmentOptions.Left);
            tag.characterSpacing = 10;
            Place(tag.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(66, 268), new Vector2(600, 26));
            var title = Txt(content, "Track Title", "TEST ARENA", bsBlack, 112, Paper, TextAlignmentOptions.Left);
            Place(title.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(62, 196), new Vector2(1100, 120));
            var details = Txt(content, "Track Details", "", monoBold, 18, Pink, TextAlignmentOptions.Left);
            details.characterSpacing = 8;
            Place(details.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(66, 122), new Vector2(700, 30));
            var desc = Txt(content, "Track Description", "", barlowMedium, 26, Mist, TextAlignmentOptions.TopLeft);
            Place(desc.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 1), new Vector2(66, 96), new Vector2(720, 110));

            // Right column.
            var carBox = StreamBox(content, "Your Car", "YOUR CAR", -150f, 200f);
            var carPic = Img(carBox, "Car Picture", null, Color.white);
            carPic.raycastTarget = false; carPic.preserveAspect = true;
            Place(carPic.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -8), new Vector2(-40, -54));
            var carName = Txt(carBox, "Car Name", "ZEPHYR", bsBlack, 36, Paper, TextAlignmentOptions.BottomRight);
            Place(carName.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 10), new Vector2(-30, 44));
            var mapBox = StreamBox(content, "Circuit", "CIRCUIT", -366f, 250f);
            var mapImg = Img(mapBox, "Circuit Line", null, new Color(0.74f, 0.72f, 0.82f, 0.85f));
            mapImg.raycastTarget = false; mapImg.preserveAspect = true;
            Place(mapImg.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -14), new Vector2(-80, -70));
            var diffBox = StreamBox(content, "Difficulty", "DIFFICULTY", -632f, 150f);
            var diffValue = Txt(diffBox, "Value", "RUSH", bsBlack, 48, Pink, TextAlignmentOptions.Center);
            Place(diffValue.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -64), new Vector2(300, 56));
            Button DiffArrow(string name, string glyph, float x)
            {
                var a = Txt(diffBox, name, glyph, bsBlack, 48, Paper, TextAlignmentOptions.Center);
                a.raycastTarget = true;
                Place(a.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(x, -64), new Vector2(60, 56));
                var b = a.gameObject.AddComponent<Button>();
                b.targetGraphic = a;
                var cb = b.colors; cb.normalColor = Color.white; cb.highlightedColor = Pink; cb.disabledColor = new Color(1, 1, 1, 0.15f); b.colors = cb;
                return b;
            }
            var diffDown = DiffArrow("Down", "<", -170f);
            var diffUp = DiffArrow("Up", ">", 170f);
            var diffPips = new Image[3];
            for (int k = 0; k < 3; k++)
            {
                diffPips[k] = Img(diffBox, "Pip " + (k + 1), null, new Color(1, 1, 1, 0.15f));
                diffPips[k].raycastTarget = false;
                Place(diffPips[k].rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2((k - 1) * 66, -100), new Vector2(58, 6));
            }
            var diffBlurb = Txt(diffBox, "Blurb", "", barlowSemi, 18, Mist, TextAlignmentOptions.Center);
            diffBlurb.enableAutoSizing = true; diffBlurb.fontSizeMin = 13; diffBlurb.fontSizeMax = 18;
            Place(diffBlurb.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(-40, 30));

            // Track cards along the bottom.
            var row = Rect(content, "Cards");
            Place(row, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(66, 86), new Vector2(-640, 230));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 18; layout.childControlWidth = false; layout.childControlHeight = false;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.LowerLeft;

            var card = Rect(row, "Card Template");
            card.sizeDelta = new Vector2(380, 210);
            var hit = Img(card, "Hit", null, new Color(0, 0, 0, 0));
            Stretch(hit.rectTransform);
            var body = Rect(card, "Body");
            Stretch(body);
            var fillBg = Img(body, "Fill", null, new Color(Night.r, Night.g, Night.b, 0.9f)); fillBg.raycastTarget = false;
            Stretch(fillBg.rectTransform);
            var photo = Rect(body, "Photo");
            Stretch(photo); photo.offsetMin = new Vector2(4, 4); photo.offsetMax = new Vector2(-4, -4);
            photo.gameObject.AddComponent<RectMask2D>();
            var thumb = Img(photo, "Thumbnail", null, new Color(0.62f, 0.6f, 0.7f, 0.9f));
            thumb.raycastTarget = false; Stretch(thumb.rectTransform);
            var shade = Img(photo, "Shade", fadeLeft, new Color(Night.r, Night.g, Night.b, 0.85f)); shade.raycastTarget = false;
            shade.rectTransform.anchorMin = Vector2.zero; shade.rectTransform.anchorMax = new Vector2(1, 1); shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
            var frame = Img(body, "Frame", outline, new Color(1, 1, 1, 0.15f));
            frame.type = Image.Type.Sliced; frame.raycastTarget = false;
            Stretch(frame.rectTransform);
            var num = Txt(body, "Number", "01", monoBold, 18, Dim, TextAlignmentOptions.TopRight);
            Place(num.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -12), new Vector2(120, 26));
            var tname = Txt(body, "Name", "TRACK", bsBlack, 44, Paper, TextAlignmentOptions.BottomLeft);
            Place(tname.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(18, 44), new Vector2(-30, 56));
            var tlen = Txt(body, "Length", "0.0 KM", monoBold, 16, Mist, TextAlignmentOptions.BottomLeft);
            tlen.characterSpacing = 6;
            Place(tlen.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(20, 18), new Vector2(-30, 26));
            var tc = card.gameObject.AddComponent<TrackCard>();
            var cso = new SerializedObject(tc);
            cso.FindProperty("number").objectReferenceValue = num;
            cso.FindProperty("trackName").objectReferenceValue = tname;
            cso.FindProperty("length").objectReferenceValue = tlen;
            cso.FindProperty("thumbnail").objectReferenceValue = thumb;
            cso.FindProperty("frame").objectReferenceValue = frame;
            cso.FindProperty("body").objectReferenceValue = body;
            cso.FindProperty("activeFrame").colorValue = Pink;
            cso.FindProperty("idleFrame").colorValue = new Color(1, 1, 1, 0.12f);
            cso.ApplyModifiedPropertiesWithoutUndo();
            card.gameObject.SetActive(false);

            var hint = Txt(content, "Hint", "<color=#FF2E9A>A D</color>  CHANGE TRACK     <color=#FF2E9A>W S</color>  DIFFICULTY     <color=#FF2E9A>ENTER</color>  GO LIVE", monoMedium, 15, Dim, TextAlignmentOptions.Left);
            hint.characterSpacing = 6;
            Place(hint.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(66, 40), new Vector2(1200, 26));

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
            so.FindProperty("pipOff").colorValue = new Color32(42, 37, 64, 255);
            var lc = so.FindProperty("levelColours");
            lc.arraySize = 3;
            lc.GetArrayElementAtIndex(0).colorValue = Signal;
            lc.GetArrayElementAtIndex(1).colorValue = Pink;
            lc.GetArrayElementAtIndex(2).colorValue = Gold;
            SetArray(so, "difficultyPips", diffPips);
            so.ApplyModifiedPropertiesWithoutUndo();

            CloneHotkeys(canvas.Find("Main Content/Home Panel"), content, router, "Back", router.Back);
            return panel.GetComponent<Animator>();
        }

        /// <summary>A flat dark box down the right with a small mono tag.</summary>
        static RectTransform StreamBox(Transform content, string name, string tag, float top, float height)
        {
            var bg = Img(content, name, null, new Color32(20, 17, 31, 225));
            bg.raycastTarget = false;
            var box = bg.rectTransform;
            Place(box, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-60, top), new Vector2(470, height));
            var edge = Img(box, "Edge", null, Pink); edge.raycastTarget = false;
            Place(edge.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(40, 4));
            var label = Txt(box, "Tag", tag, monoBold, 15, Dim, TextAlignmentOptions.Left);
            label.characterSpacing = 8;
            Place(label.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(16, -14), new Vector2(-30, 22));
            return box;
        }

        // ------------------------------------------------------------------ The Feed

        static TheFeed BuildFeed(Transform content)
        {
            const float W = 504f;
            var col = Img(content, "The Feed", null, new Color32(16, 14, 25, 255));
            col.raycastTarget = true; // the chat isn't see-through to clicks
            Place(col.rectTransform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), Vector2.zero, new Vector2(W, 0));
            var edge = Img(col.transform, "Edge", null, Pink); edge.raycastTarget = false;
            Place(edge.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(3, 0));

            var head = Txt(col.transform, "Header", "THE FEED", bsBlack, 38, Paper, TextAlignmentOptions.Left);
            Place(head.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(26, -18), new Vector2(-50, 46));
            var ch = Txt(col.transform, "Channel", "CH 13", monoMedium, 16, Dim, TextAlignmentOptions.Right);
            Place(ch.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -28), new Vector2(-50, 26));
            var rule = Img(col.transform, "Rule", null, new Color32(42, 37, 64, 255)); rule.raycastTarget = false;
            Place(rule.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -78), new Vector2(0, 1));

            // Pinned story card.
            var pin = Img(col.transform, "Pinned Story", null, Paper); pin.raycastTarget = false;
            Place(pin.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -96), new Vector2(-38, 176));
            var pinTag = Txt(pin.transform, "Tag", "PINNED  ·  STORY", monoBold, 14, new Color32(196, 21, 122, 255), TextAlignmentOptions.TopLeft);
            pinTag.characterSpacing = 8;
            Place(pinTag.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(17, -14), new Vector2(-34, 20));
            var pinHead = Txt(pin.transform, "Headline", "THE CREW NEEDS A DRIVER FOR THE TOLL ROAD", bsBlack, 34, Night, TextAlignmentOptions.TopLeft);
            pinHead.lineSpacing = -18;
            Place(pinHead.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(17, -38), new Vector2(-34, 80));
            var pinBody = Txt(pin.transform, "Body", "Slim says the Predator's keys go to whoever wins it.", barlowMedium, 21, Night, TextAlignmentOptions.TopLeft);
            Place(pinBody.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(17, -124), new Vector2(-34, 44));

            // News card.
            var newsEdge = Img(col.transform, "News", null, Live); newsEdge.raycastTarget = false;
            Place(newsEdge.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -284), new Vector2(-38, 92));
            var news = Img(newsEdge.transform, "Fill", null, new Color32(42, 21, 32, 255)); news.raycastTarget = false;
            Place(news.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-4, -4));
            var newsTag = Txt(news.transform, "Tag", "NEWS", monoBold, 14, new Color32(255, 107, 122, 255), TextAlignmentOptions.TopLeft);
            newsTag.characterSpacing = 8;
            Place(newsTag.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(16, -12), new Vector2(-30, 20));
            var newsText = Txt(news.transform, "Text", "Police raid the docks event. Crew escapes by plane.", barlowSemi, 23, Paper, TextAlignmentOptions.TopLeft);
            Place(newsText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(16, -34), new Vector2(-30, 50));

            // Live chat, newest at the bottom, older lines scroll off the top.
            var msgs = Rect(col.transform, "Messages");
            msgs.anchorMin = new Vector2(0, 0); msgs.anchorMax = new Vector2(1, 1);
            msgs.offsetMin = new Vector2(24, 26); msgs.offsetMax = new Vector2(-22, -392);
            msgs.gameObject.AddComponent<RectMask2D>();
            var v = msgs.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.LowerLeft;
            v.spacing = 13;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;

            var feed = col.gameObject.AddComponent<TheFeed>();
            var so = new SerializedObject(feed);
            so.FindProperty("messages").objectReferenceValue = msgs;
            so.FindProperty("bodyFont").objectReferenceValue = barlowSemi;
            so.FindProperty("monoFont").objectReferenceValue = monoBold;
            so.ApplyModifiedPropertiesWithoutUndo();
            return feed;
        }

        static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }
    }
}
