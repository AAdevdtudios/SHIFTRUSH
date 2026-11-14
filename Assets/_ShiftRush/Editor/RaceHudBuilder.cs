using ShiftRush.Race;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// Builds Resources/SR_RaceHUD.prefab in the menu's style: position + leaderboard top left, lap + times top
    /// right, countdown and messages in the centre, speedometer bottom right, minimap bottom left, results screen.
    /// </summary>
    public static partial class MenuBuilder
    {
        const string HudPath = "Assets/_ShiftRush/Resources/SR_RaceHUD.prefab";
        static Sprite gaugeRing, gaugeTicks, disc, mapArrow, softDisc;

        [MenuItem("SHIFT RUSH/Build Race HUD")]
        static void BuildRaceHudOnly()
        {
            LoadFonts();
            MakeArt();
            MakeRaceHud(LoadSounds());
            AssetDatabase.SaveAssets();
            Debug.Log("[SHIFT//RUSH] Race HUD built: " + HudPath);
        }

        static void MakeHudArt()
        {
            gaugeRing = SaveSprite("T_GaugeRing", 256, 256, (x, y, w, h) =>
            {
                float r = Vector2.Distance(new Vector2(x, y), new Vector2(w, h) * 0.5f);
                return Mathf.Clamp01(Mathf.Min(r - 104f, 120f - r) + 0.5f);
            }, Vector4.zero);
            // 270° of ticks with the gap at the bottom; every third tick is long.
            gaugeTicks = SaveSprite("T_GaugeTicks", 256, 256, (x, y, w, h) =>
            {
                var d = new Vector2(x - w * 0.5f, y - h * 0.5f);
                float r = d.magnitude;
                float ang = Mathf.Atan2(d.x, -d.y) * Mathf.Rad2Deg; // 0 = straight down, +ve towards the left
                if (ang < 0f) ang += 360f;
                if (ang < 45f || ang > 315f) return 0f;
                float step = 270f / 24f, k = (ang - 45f) / step, near = Mathf.Abs(k - Mathf.Round(k)) * step;
                bool major = Mathf.RoundToInt(k) % 3 == 0;
                float inner = major ? 84f : 92f;
                float width = (near * Mathf.Deg2Rad) * r;
                if (r < inner || r > 100f) return 0f;
                return Mathf.Clamp01((major ? 1.6f : 1.0f) - width + 0.5f);
            }, Vector4.zero);
            disc = SaveSprite("T_Disc", 256, 256, (x, y, w, h) =>
            {
                float r = Vector2.Distance(new Vector2(x, y), new Vector2(w, h) * 0.5f);
                return Mathf.Clamp01(126f - r + 0.5f);
            }, Vector4.zero);
            // Minimap mask: solid in the middle, melting to nothing at the edge.
            softDisc = SaveSprite("T_SoftDisc", 256, 256, (x, y, w, h) =>
            {
                float r = Vector2.Distance(new Vector2(x, y), new Vector2(w, h) * 0.5f) / (w * 0.5f);
                return 1f - Mathf.SmoothStep(0.35f, 0.98f, r);
            }, Vector4.zero);
            mapArrow = SaveSprite("T_MapArrow", 64, 64, (x, y, w, h) =>
            {
                // Arrow pointing up (+y), slightly notched at the back.
                float u = (x - w * 0.5f) / (w * 0.5f), v = y / h;
                float half = (1f - v) * 0.9f;
                if (v < 0.08f || v > 0.95f) return 0f;
                float notch = v < 0.32f ? Mathf.Abs(u) * 0.9f : 0f;
                return Mathf.Clamp01((half - Mathf.Abs(u)) * 24f) * (v < 0.32f && v < 0.08f + notch ? 0f : 1f);
            }, Vector4.zero);
        }

        static void MakeRaceHud(Sounds sounds)
        {
            MakeHudArt();
            var root = new GameObject("SR_RaceHUD", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300; // under the pause menu (400) and loading screen (500)
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var hud = root.AddComponent<RaceHUD>();
            var src = root.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.outputAudioMixerGroup = MixerGroup("UI");
            var so = new SerializedObject(hud);

            // ---------------- race panels (hidden in free roam)
            var race = new GameObject("Race", typeof(RectTransform)).GetComponent<RectTransform>();
            race.SetParent(root.transform, false);
            Stretch(race);
            var raceGroup = race.gameObject.AddComponent<CanvasGroup>();
            raceGroup.blocksRaycasts = false;

            // Position, top left.
            var tl = Corner(race, "Position", new Vector2(0, 1));
            var tlBar = Img(tl, "Accent", slant, Magenta);
            Place(tlBar.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -46), new Vector2(84, 10));
            var posLabel = Label(tl, "Label", "POSITION", new Vector2(0, 1), new Vector2(64, -64), TextAlignmentOptions.Left);
            var posValue = Txt(tl, "Value", "1/1", fontTitle, 112, Color.white, TextAlignmentOptions.TopLeft);
            Place(posValue.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -84), new Vector2(420, 120));

            // Leaderboard under it (6 slots: you + 5 opponents).
            var board = new GameObject("Leaderboard", typeof(RectTransform)).GetComponent<RectTransform>();
            board.SetParent(tl, false);
            Place(board, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(52, -214), new Vector2(380, 6 * 40));
            var rowsProp = so.FindProperty("rows");
            rowsProp.arraySize = 6;
            for (int i = 0; i < 6; i++)
            {
                var row = new GameObject("Row " + (i + 1), typeof(RectTransform)).GetComponent<RectTransform>();
                row.SetParent(board, false);
                Place(row, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(i * -4f, -i * 40f), new Vector2(0, 34));
                var bg = Img(row, "Bg", slant, new Color(Panel.r, Panel.g, Panel.b, 0.72f));
                bg.type = Image.Type.Sliced; Stretch(bg.rectTransform);
                var hl = Img(row, "You", slant, new Color(Magenta.r, Magenta.g, Magenta.b, 0.9f));
                hl.type = Image.Type.Sliced; Stretch(hl.rectTransform);
                hl.rectTransform.offsetMax = new Vector2(-(380 - 46), 0);
                var rank = Txt(row, "Rank", "1", fontTitle, 24, Color.white, TextAlignmentOptions.Center);
                Place(rank.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(34, 0));
                var name = Txt(row, "Name", "YOU", fontBold, 22, Color.white, TextAlignmentOptions.Left);
                name.characterSpacing = 4;
                Place(name.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(60, 0), new Vector2(-170, 0));
                var gap = Txt(row, "Gap", "LEADER", fontMedium, 20, Steel, TextAlignmentOptions.Right);
                Place(gap.rectTransform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(120, 0));
                var el = rowsProp.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("root").objectReferenceValue = row.gameObject;
                el.FindPropertyRelative("highlight").objectReferenceValue = hl;
                el.FindPropertyRelative("rank").objectReferenceValue = rank;
                el.FindPropertyRelative("name").objectReferenceValue = name;
                el.FindPropertyRelative("gap").objectReferenceValue = gap;
            }

            // Lap + times, top right.
            var tr = Corner(race, "Lap", new Vector2(1, 1));
            var trBar = Img(tr, "Accent", slant, Magenta);
            Place(trBar.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-60, -46), new Vector2(84, 10));
            Label(tr, "Label", "LAP", new Vector2(1, 1), new Vector2(-64, -64), TextAlignmentOptions.Right);
            var lapValue = Txt(tr, "Value", "1/3", fontTitle, 112, Color.white, TextAlignmentOptions.TopRight);
            Place(lapValue.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-60, -84), new Vector2(420, 120));
            var times = new GameObject("Times", typeof(RectTransform)).GetComponent<RectTransform>();
            times.SetParent(tr, false);
            Place(times, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-52, -214), new Vector2(380, 3 * 40));
            TMP_Text[] timeVals = new TMP_Text[3];
            string[] timeNames = { "TIME", "LAP", "BEST" };
            for (int i = 0; i < 3; i++)
            {
                var row = new GameObject(timeNames[i], typeof(RectTransform)).GetComponent<RectTransform>();
                row.SetParent(times, false);
                Place(row, new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(i * -4f, -i * 40f), new Vector2(0, 34));
                var bg = Img(row, "Bg", slant, new Color(Panel.r, Panel.g, Panel.b, 0.72f));
                bg.type = Image.Type.Sliced; Stretch(bg.rectTransform);
                var l = Txt(row, "Label", timeNames[i], fontBold, 20, Steel, TextAlignmentOptions.Left);
                l.characterSpacing = 8;
                Place(l.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(120, 0));
                var v = Txt(row, "Value", "--:--.---", fontTitle, 26, i == 2 ? Magenta : Color.white, TextAlignmentOptions.Right);
                Place(v.rectTransform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-26, 0), new Vector2(220, 0));
                timeVals[i] = v;
            }

            // Minimap, bottom left.
            var bl = Corner(race, "Minimap", new Vector2(0, 0));
            var mapRoot = new GameObject("Map", typeof(RectTransform)).GetComponent<RectTransform>();
            mapRoot.SetParent(bl, false);
            Place(mapRoot, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(40, 40), new Vector2(320, 320));
            // No frame: just the track line, faded out towards the edge by a soft round mask.
            var mapMask = mapRoot.gameObject.AddComponent<AMS.UI.SoftMask.UISoftMask>();
            mapMask.mask = softDisc;
            mapMask.fallOff = 1f;
            var map = Img(mapRoot, "Track", null, new Color(1, 1, 1, 0.75f));
            Place(map.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(280, 280));
            map.raycastTarget = false;
            var dot = Img(mapRoot, "You", mapArrow, Magenta);
            Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.45f), Vector2.zero, new Vector2(30, 30));

            // ---------------- free roam tag
            var free = new GameObject("Free Roam", typeof(RectTransform)).GetComponent<RectTransform>();
            free.SetParent(root.transform, false);
            Place(free, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(500, 160));
            var fBar = Img(free, "Accent", slant, Magenta);
            Place(fBar.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -46), new Vector2(84, 10));
            Label(free, "Label", "TEST ARENA", new Vector2(0, 1), new Vector2(64, -64), TextAlignmentOptions.Left);
            var fv = Txt(free, "Value", "FREE ROAM", fontTitle, 56, Color.white, TextAlignmentOptions.TopLeft);
            Place(fv.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(58, -88), new Vector2(460, 70));

            // ---------------- centre: countdown + messages
            var ring = Img(root.transform, "Countdown Ring", gaugeRing, Color.white);
            ring.type = Image.Type.Filled; ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top; ring.fillClockwise = false;
            Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(300, 300));
            var count = Txt(root.transform, "Countdown", "3", fontTitle, 190, Color.white, TextAlignmentOptions.Center);
            Place(count.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(600, 260));

            var msg = new GameObject("Message", typeof(RectTransform)).GetComponent<RectTransform>();
            msg.SetParent(root.transform, false);
            Place(msg, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 300), new Vector2(760, 80));
            var msgGroup = msg.gameObject.AddComponent<CanvasGroup>();
            msgGroup.blocksRaycasts = false;
            var msgBg = Img(msg, "Bg", slant, new Color(Panel.r, Panel.g, Panel.b, 0.85f));
            msgBg.type = Image.Type.Sliced; Stretch(msgBg.rectTransform);
            var msgFrame = Img(msg, "Frame", slantFrame, Magenta);
            msgFrame.type = Image.Type.Sliced; Stretch(msgFrame.rectTransform);
            var msgText = Txt(msg, "Text", "FINAL LAP", fontTitle, 46, Color.white, TextAlignmentOptions.Center);
            msgText.characterSpacing = 8;
            Stretch(msgText.rectTransform);

            // ---------------- speedometer, bottom right
            var br = Corner(root.transform, "Speedometer", new Vector2(1, 0));
            var gauge = new GameObject("Gauge", typeof(RectTransform)).GetComponent<RectTransform>();
            gauge.SetParent(br, false);
            Place(gauge, Vector2.one * 0, Vector2.one * 0, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340, 340));
            gauge.anchorMin = gauge.anchorMax = new Vector2(1, 0);
            gauge.anchoredPosition = new Vector2(-60 - 170, 50 + 170);
            var gBg = Img(gauge, "Disc", disc, new Color(Panel.r, Panel.g, Panel.b, 0.55f));
            Stretch(gBg.rectTransform);
            var track = Img(gauge, "Arc", gaugeRing, new Color(1, 1, 1, 0.1f));
            track.type = Image.Type.Filled; track.fillMethod = Image.FillMethod.Radial360;
            track.fillOrigin = (int)Image.Origin360.Bottom; track.fillClockwise = true; track.fillAmount = 0.75f;
            Stretch(track.rectTransform); track.rectTransform.localEulerAngles = new Vector3(0, 0, -45f);
            var rpm = Img(gauge, "RPM", gaugeRing, Magenta);
            rpm.type = Image.Type.Filled; rpm.fillMethod = Image.FillMethod.Radial360;
            rpm.fillOrigin = (int)Image.Origin360.Bottom; rpm.fillClockwise = true; rpm.fillAmount = 0.4f;
            Stretch(rpm.rectTransform); rpm.rectTransform.localEulerAngles = new Vector3(0, 0, -45f);
            var ticks = Img(gauge, "Ticks", gaugeTicks, new Color(1, 1, 1, 0.55f));
            Stretch(ticks.rectTransform);
            var speed = Txt(gauge, "Speed", "000", fontTitle, 104, Color.white, TextAlignmentOptions.Center);
            Place(speed.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 18), new Vector2(260, 110));
            var unit = Txt(gauge, "Unit", "KM/H", fontBold, 20, Steel, TextAlignmentOptions.Center);
            unit.characterSpacing = 10;
            Place(unit.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(200, 30));
            var gearBox = Img(gauge, "Gear", slant, Magenta);
            gearBox.type = Image.Type.Sliced;
            Place(gearBox.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -92), new Vector2(84, 46));
            var gear = Txt(gearBox.rectTransform, "Value", "1", fontTitle, 36, Color.white, TextAlignmentOptions.Center);
            Stretch(gear.rectTransform);

            var nos = new GameObject("Nitro", typeof(RectTransform)).GetComponent<RectTransform>();
            nos.SetParent(br, false);
            Place(nos, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-90, 34), new Vector2(280, 14));
            var nosLabel = Txt(nos, "Label", "NOS", fontBold, 18, Steel, TextAlignmentOptions.Right);
            nosLabel.characterSpacing = 8;
            Place(nosLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(80, 24));
            var nosBg = Img(nos, "Bg", slant, new Color(1, 1, 1, 0.12f));
            nosBg.type = Image.Type.Sliced; Stretch(nosBg.rectTransform);
            var nosFill = Img(nos, "Fill", slant, Magenta);
            nosFill.type = Image.Type.Sliced;
            nosFill.rectTransform.anchorMin = Vector2.zero; nosFill.rectTransform.anchorMax = Vector2.one;
            nosFill.rectTransform.offsetMin = nosFill.rectTransform.offsetMax = Vector2.zero;

            // ---------------- results
            var fin = new GameObject("Results", typeof(RectTransform)).GetComponent<RectTransform>();
            fin.SetParent(root.transform, false);
            Stretch(fin);
            var finGroup = fin.gameObject.AddComponent<CanvasGroup>();
            var finShade = Img(fin, "Shade", null, new Color(Ink.r, Ink.g, Ink.b, 0.45f));
            Stretch(finShade.rectTransform);
            var finDim = Img(fin, "Dim", fadeLeft, new Color(Ink.r, Ink.g, Ink.b, 0.95f));
            Stretch(finDim.rectTransform);
            finDim.rectTransform.anchorMax = new Vector2(0.7f, 1f);
            var finBar = Img(fin, "Accent", slant, Magenta);
            Place(finBar.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(120, 300), new Vector2(110, 12));
            var finTitle = Txt(fin, "Title", "RACE COMPLETE", fontTitle, 96, Color.white, TextAlignmentOptions.Left);
            Place(finTitle.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(116, 230), new Vector2(1000, 110));
            Label(fin, "Position Label", "FINISHED", new Vector2(0, 0.5f), new Vector2(124, 160), TextAlignmentOptions.Left);
            var finPos = Txt(fin, "Position", "1ST", fontTitle, 150, Magenta, TextAlignmentOptions.TopLeft);
            Place(finPos.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 1), new Vector2(116, 150), new Vector2(600, 170));
            Label(fin, "Total Label", "TOTAL TIME", new Vector2(0, 0.5f), new Vector2(124, -36), TextAlignmentOptions.Left);
            var finTotal = Txt(fin, "Total", "00:00.000", fontTitle, 56, Color.white, TextAlignmentOptions.TopLeft);
            Place(finTotal.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 1), new Vector2(120, -50), new Vector2(400, 70));
            Label(fin, "Best Label", "BEST LAP EVER", new Vector2(0, 0.5f), new Vector2(504, -36), TextAlignmentOptions.Left);
            var finBest = Txt(fin, "Best", "00:00.000", fontTitle, 56, Magenta, TextAlignmentOptions.TopLeft);
            Place(finBest.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 1), new Vector2(500, -50), new Vector2(400, 70));
            var finLaps = Txt(fin, "Laps", "", fontBold, 28, Color.white, TextAlignmentOptions.TopLeft);
            finLaps.lineSpacing = 18;
            Place(finLaps.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 1), new Vector2(124, -150), new Vector2(600, 180));
            var finHint = Txt(fin, "Hint", "<color=#FF40D9>ENTER</color>  RACE AGAIN      <color=#FF40D9>M</color>  MAIN MENU", fontBold, 22, Color.white, TextAlignmentOptions.Left);
            finHint.characterSpacing = 6;
            Place(finHint.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(124, 70), new Vector2(900, 40));

            // ---------------- wire up
            so.FindProperty("raceGroup").objectReferenceValue = raceGroup;
            so.FindProperty("freeRoamTag").objectReferenceValue = free.gameObject;
            so.FindProperty("positionValue").objectReferenceValue = posValue;
            so.FindProperty("lapValue").objectReferenceValue = lapValue;
            so.FindProperty("timeValue").objectReferenceValue = timeVals[0];
            so.FindProperty("lapTimeValue").objectReferenceValue = timeVals[1];
            so.FindProperty("bestValue").objectReferenceValue = timeVals[2];
            so.FindProperty("countdownText").objectReferenceValue = count;
            so.FindProperty("countdownRing").objectReferenceValue = ring;
            so.FindProperty("messageGroup").objectReferenceValue = msgGroup;
            so.FindProperty("messageText").objectReferenceValue = msgText;
            so.FindProperty("speedValue").objectReferenceValue = speed;
            so.FindProperty("unitText").objectReferenceValue = unit;
            so.FindProperty("gearText").objectReferenceValue = gear;
            so.FindProperty("rpmFill").objectReferenceValue = rpm;
            so.FindProperty("nitroFill").objectReferenceValue = nosFill.rectTransform;
            so.FindProperty("nitroFillImage").objectReferenceValue = nosFill;
            so.FindProperty("nitroGroup").objectReferenceValue = nos.gameObject;
            so.FindProperty("mapImage").objectReferenceValue = map;
            so.FindProperty("mapView").objectReferenceValue = mapRoot;
            so.FindProperty("mapFade").objectReferenceValue = mapMask;
            so.FindProperty("mapDot").objectReferenceValue = dot.rectTransform;
            so.FindProperty("rivalDot").objectReferenceValue = disc;
            so.FindProperty("finishGroup").objectReferenceValue = finGroup;
            so.FindProperty("finishPosition").objectReferenceValue = finPos;
            so.FindProperty("finishTotal").objectReferenceValue = finTotal;
            so.FindProperty("finishBest").objectReferenceValue = finBest;
            so.FindProperty("finishLaps").objectReferenceValue = finLaps;
            so.FindProperty("audioSource").objectReferenceValue = src;
            so.FindProperty("beep").objectReferenceValue = sounds.select;
            so.FindProperty("go").objectReferenceValue = sounds.confirm;
            so.FindProperty("lap").objectReferenceValue = sounds.shift;
            so.FindProperty("accent").colorValue = Magenta;
            so.FindProperty("steel").colorValue = Steel;
            so.ApplyModifiedPropertiesWithoutUndo();

            foreach (var g in root.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            PrefabUtility.SaveAsPrefabAsset(root, HudPath);
            Object.DestroyImmediate(root);
        }

        static RectTransform Corner(Transform parent, string name, Vector2 anchor)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Stretch(rt);
            return rt;
        }

        static TextMeshProUGUI Label(Transform parent, string name, string text, Vector2 anchor, Vector2 pos, TextAlignmentOptions align)
        {
            var t = Txt(parent, name, text, fontBold, 22, Steel, align);
            t.characterSpacing = 10;
            var pivot = new Vector2(align == TextAlignmentOptions.Right ? 1 : 0, 1);
            Place(t.rectTransform, anchor, anchor, pivot, pos, new Vector2(400, 30));
            return t;
        }
    }
}
