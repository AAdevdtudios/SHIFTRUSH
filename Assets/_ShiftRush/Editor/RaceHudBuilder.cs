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
            LoadBroadcastFonts();
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
            if (bsBlack == null) LoadBroadcastFonts();
            if (outline == null) outline = SaveSprite("T_Outline", 64, 64, (x, y, w, h) => (x < 3 || y < 3 || x >= w - 3 || y >= h - 3) ? 1f : 0f, new Vector4(4, 4, 4, 4));
            var root = new GameObject("SR_RaceHUD", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300; // under the pause menu (400) and loading screen (500)
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var hud = root.AddComponent<RaceHUD>();
            var stream = root.AddComponent<StreamHud>();
            var src = root.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.outputAudioMixerGroup = MixerGroup("UI");
            var so = new SerializedObject(hud);
            var ss = new SerializedObject(stream);
            var dim = new Color(Night.r, Night.g, Night.b, 0.78f);

            // ================= race panels (hidden in free roam)
            var race = Rect(root.transform, "Race");
            Stretch(race);
            var raceGroup = race.gameObject.AddComponent<CanvasGroup>();
            raceGroup.blocksRaycasts = false;

            // Position, top left: a huge pink number with the cyan echo, the lap chip beside it.
            var posEcho = Txt(race, "Position Echo", "1/8", bsBlack, 150, Signal, TextAlignmentOptions.TopLeft);
            Place(posEcho.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(54, -26), new Vector2(420, 170));
            var posValue = Txt(race, "Position", "1/8", bsBlack, 150, Pink, TextAlignmentOptions.TopLeft);
            Place(posValue.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(46, -20), new Vector2(420, 170));
            posEcho.gameObject.AddComponent<ShiftRush.UI.TextEcho>().source = posValue;
            var lapBox = Img(race, "Lap", null, dim);
            Place(lapBox.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(46, -170), new Vector2(150, 38));
            var lapLabel = Txt(lapBox.transform, "Label", "LAP", monoBold, 15, Dim, TextAlignmentOptions.Left);
            lapLabel.characterSpacing = 8;
            Place(lapLabel.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(50, 0));
            var lapValue = Txt(lapBox.transform, "Value", "1/3", monoBold, 20, Paper, TextAlignmentOptions.Right);
            Place(lapValue.rectTransform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(90, 0));

            // Leaderboard (you and the cars around you), flat rows with a colour tag.
            var board = Rect(race, "Leaderboard");
            Place(board, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(46, -220), new Vector2(340, 6 * 36));
            var rowsProp = so.FindProperty("rows");
            rowsProp.arraySize = 6;
            for (int i = 0; i < 6; i++)
            {
                var row = Rect(board, "Row " + (i + 1));
                Place(row, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, -i * 36f), new Vector2(0, 32));
                var bg = Img(row, "Bg", null, dim); Stretch(bg.rectTransform);
                var hl = Img(row, "You", null, Pink); Stretch(hl.rectTransform);
                var rank = Txt(row, "Rank", "1", bsBlack, 24, Paper, TextAlignmentOptions.Center);
                Place(rank.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(6, 0), new Vector2(30, 0));
                var name = Txt(row, "Name", "YOU", barlowBold, 22, Paper, TextAlignmentOptions.Left);
                Place(name.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(44, 0), new Vector2(-150, 0));
                var gap = Txt(row, "Gap", "LEADER", monoMedium, 16, Mist, TextAlignmentOptions.Right);
                Place(gap.rectTransform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(110, 0));
                var el = rowsProp.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("root").objectReferenceValue = row.gameObject;
                el.FindPropertyRelative("highlight").objectReferenceValue = hl;
                el.FindPropertyRelative("rank").objectReferenceValue = rank;
                el.FindPropertyRelative("name").objectReferenceValue = name;
                el.FindPropertyRelative("gap").objectReferenceValue = gap;
            }
            // Times under the board, quiet mono lines.
            var timeVals = new TMP_Text[3];
            string[] timeNames = { "TIME", "LAP", "BEST" };
            for (int i = 0; i < 3; i++)
            {
                var l = Txt(race, timeNames[i] + " Label", timeNames[i], monoBold, 14, Dim, TextAlignmentOptions.Left);
                l.characterSpacing = 8;
                Place(l.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, -446 - i * 24), new Vector2(80, 22));
                var v = Txt(race, timeNames[i] + " Value", "--:--.---", monoBold, 17, i == 2 ? Pink : Paper, TextAlignmentOptions.Left);
                Place(v.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(130, -444 - i * 24), new Vector2(200, 24));
                timeVals[i] = v;
            }

            // Minimap, bottom left.
            var mapRoot = Rect(race, "Map");
            Place(mapRoot, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(30, 30), new Vector2(280, 280));
            var mapMask = mapRoot.gameObject.AddComponent<AMS.UI.SoftMask.UISoftMask>();
            mapMask.mask = softDisc; mapMask.fallOff = 1f;
            var map = Img(mapRoot, "Track", null, new Color(0.96f, 0.95f, 0.98f, 0.8f));
            Place(map.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(250, 250));
            var dot = Img(mapRoot, "You", mapArrow, Pink);
            Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.45f), Vector2.zero, new Vector2(30, 30));

            // ================= free roam tag
            var free = Rect(root.transform, "Free Roam");
            Place(free, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(600, 200));
            var fTag = Txt(free, "Label", "THE YARD", monoBold, 16, Pink, TextAlignmentOptions.Left);
            fTag.characterSpacing = 10;
            Place(fTag.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, -40), new Vector2(400, 24));
            var fv = Txt(free, "Value", "FREE ROAM", bsBlack, 84, Paper, TextAlignmentOptions.TopLeft);
            Place(fv.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(46, -60), new Vector2(560, 100));

            // Everything live sits on one layer that clears when the results come up.
            var liveLayer = Rect(root.transform, "Live");
            Stretch(liveLayer);
            var liveGroup = liveLayer.gameObject.AddComponent<CanvasGroup>();
            liveGroup.blocksRaycasts = false;

            // ================= stream corner, top right
            var corner = Rect(liveLayer, "Stream Corner");
            Place(corner, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -32), new Vector2(400, 420));
            var chips = Rect(corner, "Chips");
            Place(chips, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(400, 36));
            var ch = chips.gameObject.AddComponent<HorizontalLayoutGroup>();
            ch.childAlignment = TextAnchor.MiddleRight; ch.spacing = 10;
            ch.childControlWidth = ch.childControlHeight = true; ch.childForceExpandWidth = ch.childForceExpandHeight = false;
            var live = Chip(chips, "Live", Live, "LIVE", monoBold, 15, Color.white, true);
            var ldot = Img(live, "Dot", AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"), Color.white);
            ldot.transform.SetAsFirstSibling();
            var lle = ldot.gameObject.AddComponent<LayoutElement>(); lle.preferredWidth = lle.preferredHeight = 9;
            ldot.gameObject.AddComponent<ShiftRush.UI.LivePulse>();
            var viewers = Chip(chips, "Viewers", dim, "48.2K", monoMedium, 16, Paper, false);
            var hypeBox = Img(corner, "Hype Streak", null, dim);
            Place(hypeBox.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -46), new Vector2(360, 64));
            var hl2 = Txt(hypeBox.transform, "Label", "HYPE", monoBold, 15, Gold, TextAlignmentOptions.Left);
            hl2.characterSpacing = 8;
            Place(hl2.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -8), new Vector2(80, 20));
            var hs = Txt(hypeBox.transform, "Session", "+0", monoBold, 15, Gold, TextAlignmentOptions.Left);
            Place(hs.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, -8), new Vector2(120, 20));
            var streakText = Txt(hypeBox.transform, "Streak", "x1.0 STREAK", monoBold, 15, Paper, TextAlignmentOptions.Right);
            Place(streakText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -8), new Vector2(200, 20));
            var segs = new Image[8];
            for (int i = 0; i < 8; i++)
            {
                segs[i] = Img(hypeBox.transform, "Seg " + i, null, new Color32(42, 37, 64, 255));
                float w = (360f - 28f - 7 * 4f) / 8f;
                Place(segs[i].rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(14 + i * (w + 4f), 12), new Vector2(w, 12));
            }
            var chatList = Rect(corner, "Chat");
            Place(chatList, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -124), new Vector2(400, 160));
            var cv = chatList.gameObject.AddComponent<VerticalLayoutGroup>();
            cv.childAlignment = TextAnchor.UpperRight; cv.spacing = 6;
            cv.childControlWidth = cv.childControlHeight = true; cv.childForceExpandWidth = cv.childForceExpandHeight = false;

            // ================= fan dare card, left middle
            var dare = Rect(liveLayer, "Fan Dare");
            Place(dare, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(46, -92), new Vector2(380, 190));
            var dareGroup = dare.gameObject.AddComponent<CanvasGroup>();
            var dShadow = Img(dare, "Shadow", null, Gold);
            Place(dShadow.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(7, -7), Vector2.zero);
            var dBody = Img(dare, "Body", null, Night); Stretch(dBody.rectTransform);
            var dEdge = Img(dBody.transform, "Edge", outline, Gold);
            dEdge.type = Image.Type.Sliced; Stretch(dEdge.rectTransform);
            var dTagBg = Img(dBody.transform, "Tag", null, Gold);
            Place(dTagBg.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -14), new Vector2(110, 24));
            var dTag = Txt(dTagBg.transform, "Text", "FAN DARE", monoBold, 14, Night, TextAlignmentOptions.Center); Stretch(dTag.rectTransform);
            var dVotes = Txt(dBody.transform, "Votes", "2,140 VOTES", monoMedium, 14, Mist, TextAlignmentOptions.Right);
            Place(dVotes.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -16), new Vector2(200, 22));
            var dFrom = Txt(dBody.transform, "From", "", barlowSemi, 19, Mist, TextAlignmentOptions.Left);
            Place(dFrom.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(16, -46), new Vector2(-32, 24));
            var dTitle = Txt(dBody.transform, "Title", "HIT DRIFT TIER 3", bsBlack, 38, Paper, TextAlignmentOptions.TopLeft);
            Place(dTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(16, -70), new Vector2(-32, 48));
            var dReward = Txt(dBody.transform, "Reward", "+600 HYPE", monoBold, 18, Gold, TextAlignmentOptions.Left);
            Place(dReward.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(16, 48), new Vector2(200, 24));
            var dTimer = Txt(dBody.transform, "Timer", "0:20", monoBold, 18, Paper, TextAlignmentOptions.Right);
            Place(dTimer.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 48), new Vector2(120, 24));
            var dBar = Img(dBody.transform, "Bar", null, new Color32(42, 37, 64, 255));
            Place(dBar.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(-32, 6));
            var dFill = Img(dBar.transform, "Fill", null, Gold); Stretch(dFill.rectTransform);
            var dNote = Txt(dBody.transform, "Note", "DO IT TO TAKE IT. IGNORE IT, NOTHING LOST.", monoMedium, 12, Dim, TextAlignmentOptions.Left);
            Place(dNote.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(16, 8), new Vector2(-32, 18));

            // ================= centre: callout, countdown, messages
            var callout = Rect(liveLayer, "Callout");
            Place(callout, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(560, 120));
            var calloutGroup = callout.gameObject.AddComponent<CanvasGroup>();
            var cBody = Rect(callout, "Body"); Stretch(cBody);
            var cShadow = Img(cBody, "Shadow", null, Signal);
            Place(cShadow.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(7, -7), new Vector2(520, 66));
            var cSlab = Img(cBody, "Slab", null, Pink);
            Place(cSlab.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(520, 66));
            var cTitle = Txt(cSlab.transform, "Title", "CHAIN KNOCK x2", bsBlack, 50, Night, TextAlignmentOptions.Center); Stretch(cTitle.rectTransform);
            var cSubBg = Img(cBody, "Sub Bg", null, new Color(Night.r, Night.g, Night.b, 0.85f));
            Place(cSubBg.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(220, 32));
            var cSub = Txt(cSubBg.transform, "Sub", "+350 HYPE", monoBold, 20, Gold, TextAlignmentOptions.Center);
            Stretch(cSub.rectTransform);

            var ring = Img(root.transform, "Countdown Ring", gaugeRing, Color.white);
            ring.type = Image.Type.Filled; ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top; ring.fillClockwise = false;
            Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(300, 300));
            var count = Txt(root.transform, "Countdown", "3", bsBlack, 220, Paper, TextAlignmentOptions.Center);
            Place(count.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(600, 280));

            var msg = Rect(root.transform, "Message");
            Place(msg, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(620, 74));
            var msgGroup = msg.gameObject.AddComponent<CanvasGroup>();
            msgGroup.blocksRaycasts = false;
            var msgBg = Img(msg, "Bg", null, new Color(Night.r, Night.g, Night.b, 0.88f)); Stretch(msgBg.rectTransform);
            var msgEdge = Img(msg, "Edge", null, Pink);
            Place(msgEdge.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(6, 0));
            var msgText = Txt(msg, "Text", "FINAL LAP", bsBlack, 46, Paper, TextAlignmentOptions.Center);
            Stretch(msgText.rectTransform);

            // Drift meter, above the power-ups.
            var driftRoot = Rect(liveLayer, "Drift Meter");
            Place(driftRoot, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 236), new Vector2(440, 70));
            var driftGroup = driftRoot.gameObject.AddComponent<CanvasGroup>();
            var dmTitle = Txt(driftRoot, "Title", "DRIFT TIER 2", bsBlack, 32, Signal, TextAlignmentOptions.TopLeft);
            Place(dmTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), Vector2.zero, new Vector2(0, 36));
            var dmInfo = Txt(driftRoot, "Angle", "38°", monoBold, 16, Paper, TextAlignmentOptions.TopRight);
            Place(dmInfo.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -8), new Vector2(0, 24));
            var dmFills = new RectTransform[3];
            Color[] tierCol = { Paper, Signal, Pink };
            for (int i = 0; i < 3; i++)
            {
                float w = (440f - 12f) / 3f;
                var bg = Img(driftRoot, "Tier " + (i + 1), null, dim);
                Place(bg.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(i * (w + 6f), 16), new Vector2(w, 12));
                var f = Img(bg.transform, "Fill", null, tierCol[i]);
                Stretch(f.rectTransform);
                dmFills[i] = f.rectTransform;
                var lab = Txt(driftRoot, "Boost " + (i + 1), i == 2 ? "+38 KM/H" : (i == 0 ? "+12" : "+24"), monoBold, 12, Mist, TextAlignmentOptions.TopLeft);
                Place(lab.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(i * (w + 6f), -2), new Vector2(w, 16));
            }

            // Power-ups, bottom centre: the active one big, two in reserve.
            var pu = Rect(liveLayer, "Power-ups");
            Place(pu, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(330, 160));
            var bigShadow = Img(pu, "Active Shadow", null, Pink);
            Place(bigShadow.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(46, 30), new Vector2(118, 118));
            var big = Img(pu, "Active", null, Night);
            Place(big.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 36), new Vector2(118, 118));
            var bigEdge = Img(big.transform, "Edge", outline, Paper); bigEdge.type = Image.Type.Sliced; Stretch(bigEdge.rectTransform);
            var bigText = Txt(big.transform, "Text", "EMPTY", bsBold, 26, Dim, TextAlignmentOptions.Center); Stretch(bigText.rectTransform);
            for (int i = 0; i < 2; i++)
            {
                var s2 = Img(pu, "Reserve " + (i + 1), null, new Color(Night.r, Night.g, Night.b, 0.6f));
                Place(s2.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(172 + i * 80, 36), new Vector2(72, 72));
                var e2 = Img(s2.transform, "Edge", outline, new Color(1, 1, 1, 0.25f)); e2.type = Image.Type.Sliced; Stretch(e2.rectTransform);
                var t2 = Txt(s2.transform, "Text", "EMPTY", monoBold, 11, Dim, TextAlignmentOptions.Center); Stretch(t2.rectTransform);
            }
            var puNote = Txt(pu, "Note", "POWER-UPS ARRIVE SOON", monoMedium, 13, Dim, TextAlignmentOptions.Center);
            puNote.characterSpacing = 4;
            Place(puNote.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(0, 20));

            // ================= speed, nitro and move cooldowns, bottom right
            var br = Rect(liveLayer, "Dash");
            Place(br, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 30), new Vector2(460, 330));
            // Cooldown chips.
            string[] chipKeys = { "Q / E", "Q + E", "SPACE" }, chipNames = { "ROLL", "FWD ROLL", "DRIFT" };
            var cdFills = new RectTransform[3]; var cdImgs = new Image[3]; var cdStatus = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                var chip = Img(br, chipNames[i], null, dim);
                Place(chip.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-(2 - i) * 104f, 0), new Vector2(98, 62));
                var k = Txt(chip.transform, "Key", chipKeys[i], monoBold, 12, Mist, TextAlignmentOptions.TopLeft);
                Place(k.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(8, -6), new Vector2(-16, 16));
                var st = Txt(chip.transform, "Status", "READY", monoBold, 12, Gold, TextAlignmentOptions.TopRight);
                Place(st.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8, -6), new Vector2(-16, 16));
                var n2 = Txt(chip.transform, "Name", chipNames[i], barlowBold, 19, Paper, TextAlignmentOptions.TopLeft);
                Place(n2.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(8, -22), new Vector2(-16, 24));
                var bar = Img(chip.transform, "Bar", null, new Color32(42, 37, 64, 255));
                Place(bar.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(-16, 4));
                var f = Img(bar.transform, "Fill", null, Gold); Stretch(f.rectTransform);
                cdFills[i] = f.rectTransform; cdImgs[i] = f; cdStatus[i] = st;
            }
            // Gear and speed.
            var gearLabel = Txt(br, "Gear Label", "GEAR", monoBold, 15, Paper, TextAlignmentOptions.Right);
            gearLabel.characterSpacing = 8;
            Place(gearLabel.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-330, 150), new Vector2(90, 22));
            var gear = Txt(br, "Gear", "1", bsBlack, 64, Signal, TextAlignmentOptions.BottomRight);
            Place(gear.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-330, 82), new Vector2(90, 72));
            var speedShadow = Txt(br, "Speed Shadow", "000", bsBlack, 150, Night, TextAlignmentOptions.BottomRight);
            Place(speedShadow.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-64, 74), new Vector2(300, 160));
            var speed = Txt(br, "Speed", "000", bsBlack, 150, Paper, TextAlignmentOptions.BottomRight);
            Place(speed.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-70, 80), new Vector2(300, 160));
            speedShadow.gameObject.AddComponent<ShiftRush.UI.TextEcho>().source = speed;
            var unit = Txt(br, "Unit", "KM/H", monoBold, 16, Paper, TextAlignmentOptions.BottomLeft);
            Place(unit.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, 92), new Vector2(64, 24));
            // RPM: a flat bar that fills, flashing at the limiter.
            var rpmBg = Img(br, "RPM Bg", null, dim);
            Place(rpmBg.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, 62), new Vector2(440, 10));
            var rpm = Img(rpmBg.transform, "RPM", null, Pink);
            rpm.type = Image.Type.Filled; rpm.fillMethod = Image.FillMethod.Horizontal; rpm.fillOrigin = 0; rpm.fillAmount = 0.5f;
            rpm.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Stretch(rpm.rectTransform);
            // Nitro bar on F.
            var nos = Rect(br, "Nitro");
            Place(nos, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, 14), new Vector2(440, 30));
            var nosLabel = Txt(nos, "Label", "NITRO", monoBold, 14, Signal, TextAlignmentOptions.Left);
            nosLabel.characterSpacing = 8;
            Place(nosLabel.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(80, 0));
            var nosKey = Txt(nos, "Key", "SHIFT", monoBold, 14, Paper, TextAlignmentOptions.Right);
            Place(nosKey.rectTransform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), Vector2.zero, new Vector2(60, 0));
            var nosBg = Img(nos, "Bg", null, dim);
            Place(nosBg.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(8, 0), new Vector2(-160, -10));
            var nosFill = Img(nosBg.transform, "Fill", null, Signal);
            nosFill.rectTransform.anchorMin = Vector2.zero; nosFill.rectTransform.anchorMax = Vector2.one;
            nosFill.rectTransform.offsetMin = new Vector2(3, 3); nosFill.rectTransform.offsetMax = new Vector2(-3, -3);

            // ================= results: the stream pays out
            var fin = Rect(root.transform, "Results");
            Stretch(fin);
            var finGroup = fin.gameObject.AddComponent<CanvasGroup>();
            var finShade = Img(fin, "Shade", null, new Color(Night.r, Night.g, Night.b, 0.86f)); Stretch(finShade.rectTransform);
            var slab = Img(fin, "Slab", null, Pink);
            Place(slab.rectTransform, new Vector2(0, 0), new Vector2(0.44f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            var slabEdge = Img(fin, "Slab Edge", null, Signal);
            Place(slabEdge.rectTransform, new Vector2(0.44f, 0), new Vector2(0.44f, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(14, 0));
            var clip = Img(fin, "Clip Tag", null, Night);
            Place(clip.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, -70), new Vector2(300, 32));
            var clipText = Txt(clip.transform, "Text", "CLIP SAVED  //  CH 13", monoBold, 15, Paper, TextAlignmentOptions.Center); Stretch(clipText.rectTransform);
            var finPos = Txt(fin, "Position", "1ST", bsBlack, 320, Night, TextAlignmentOptions.TopLeft);
            Place(finPos.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(72, -120), new Vector2(760, 330));
            var finTitle = Txt(fin, "Title", "YOU OUTRAN THE LEAGUE", bsBlack, 50, Night, TextAlignmentOptions.TopLeft);
            Place(finTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, -450), new Vector2(760, 60));
            var tl = Txt(fin, "Total Label", "TOTAL", monoBold, 15, Night, TextAlignmentOptions.TopLeft);
            Place(tl.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(84, -530), new Vector2(200, 22));
            var finTotal = Txt(fin, "Total", "00:00.000", bsBlack, 46, Night, TextAlignmentOptions.TopLeft);
            Place(finTotal.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, -552), new Vector2(300, 60));
            var bl = Txt(fin, "Best Label", "BEST LAP EVER", monoBold, 15, Night, TextAlignmentOptions.TopLeft);
            Place(bl.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(384, -530), new Vector2(260, 22));
            var finBest = Txt(fin, "Best", "00:00.000", bsBlack, 46, Night, TextAlignmentOptions.TopLeft);
            Place(finBest.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(380, -552), new Vector2(300, 60));
            var finLaps = Txt(fin, "Laps", "", monoBold, 18, Night, TextAlignmentOptions.TopLeft);
            finLaps.lineSpacing = 12;
            Place(finLaps.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(84, -630), new Vector2(600, 260));

            var payTag = Txt(fin, "Pay Tag", "THE STREAM PAYS OUT", monoBold, 16, Mist, TextAlignmentOptions.TopLeft);
            payTag.characterSpacing = 8;
            Place(payTag.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 1), new Vector2(90, -90), new Vector2(760, 26));
            var payLines = Txt(fin, "Pay Lines", "", barlowBold, 30, Paper, TextAlignmentOptions.TopLeft);
            payLines.lineSpacing = 22;
            Place(payLines.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 1), new Vector2(90, -130), new Vector2(760, 420));
            var payLabel = Txt(fin, "Pay Label", "TOTAL HYPE", bsBlack, 48, Paper, TextAlignmentOptions.BottomLeft);
            Place(payLabel.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 1), new Vector2(90, -560), new Vector2(400, 70));
            var payTotal = Txt(fin, "Pay Total", "+0", bsBlack, 110, Gold, TextAlignmentOptions.BottomRight);
            Place(payTotal.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 1), new Vector2(390, -540), new Vector2(460, 120));
            var payBal = Txt(fin, "Pay Balance", "", monoBold, 16, Mist, TextAlignmentOptions.TopRight);
            Place(payBal.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 1), new Vector2(390, -668), new Vector2(460, 24));
            var finHint = Txt(fin, "Hint", "<color=#FF2E9A>ENTER</color>  RACE AGAIN      <color=#FF2E9A>M</color>  MAIN MENU", monoBold, 18, Paper, TextAlignmentOptions.Left);
            finHint.characterSpacing = 6;
            Place(finHint.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(90, 70), new Vector2(900, 30));

            // ================= wire up RaceHUD
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
            so.FindProperty("rpmArc").floatValue = 1f;
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
            so.FindProperty("accent").colorValue = Pink;
            so.FindProperty("steel").colorValue = Mist;
            so.ApplyModifiedPropertiesWithoutUndo();

            // ================= wire up StreamHud
            ss.FindProperty("viewersText").objectReferenceValue = viewers.GetComponentInChildren<TMP_Text>();
            ss.FindProperty("streakText").objectReferenceValue = streakText;
            SetArray(ss, "streakSegments", segs);
            ss.FindProperty("sessionHypeText").objectReferenceValue = hs;
            ss.FindProperty("chatParent").objectReferenceValue = chatList;
            ss.FindProperty("chatFont").objectReferenceValue = barlowSemi;
            ss.FindProperty("calloutGroup").objectReferenceValue = calloutGroup;
            ss.FindProperty("calloutBody").objectReferenceValue = cBody;
            ss.FindProperty("calloutTitle").objectReferenceValue = cTitle;
            ss.FindProperty("calloutSub").objectReferenceValue = cSub;
            ss.FindProperty("driftGroup").objectReferenceValue = driftGroup;
            ss.FindProperty("driftTitle").objectReferenceValue = dmTitle;
            ss.FindProperty("driftInfo").objectReferenceValue = dmInfo;
            SetArray(ss, "driftFills", dmFills);
            SetArray(ss, "cooldownFills", cdFills);
            SetArray(ss, "cooldownFillImages", cdImgs);
            SetArray(ss, "cooldownStatus", cdStatus);
            ss.FindProperty("dareGroup").objectReferenceValue = dareGroup;
            ss.FindProperty("dareFrom").objectReferenceValue = dFrom;
            ss.FindProperty("dareTitle").objectReferenceValue = dTitle;
            ss.FindProperty("dareReward").objectReferenceValue = dReward;
            ss.FindProperty("dareTimer").objectReferenceValue = dTimer;
            ss.FindProperty("dareVotes").objectReferenceValue = dVotes;
            ss.FindProperty("dareTimerFill").objectReferenceValue = dFill.rectTransform;
            ss.FindProperty("payoutLines").objectReferenceValue = payLines;
            ss.FindProperty("payoutHeadline").objectReferenceValue = finTitle;
            ss.FindProperty("liveLayer").objectReferenceValue = liveGroup;
            ss.FindProperty("payoutTotal").objectReferenceValue = payTotal;
            ss.FindProperty("payoutBalance").objectReferenceValue = payBal;
            ss.ApplyModifiedPropertiesWithoutUndo();

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
