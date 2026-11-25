using System.Collections.Generic;
using ShiftRush.Core;
using ShiftRush.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// Story mode's two menu screens, built from Heat's spare Chapters and Achievements panels:
    /// the case board (Detective Kowal's evidence board, one pinned photo per episode, the open file on the right)
    /// and the briefing ("Tonight on Channel 13": Slim's intro, the goals, the rival, the track, your car, Go Live).
    /// Called from Build Main Menu.
    /// </summary>
    public static partial class MenuBuilder
    {
        static readonly Color BoardBg = new Color32(16, 19, 24, 255);
        static readonly Color CasePaper = new Color32(242, 238, 227, 255);
        static readonly Color CaseInk = new Color32(20, 18, 24, 255);
        static readonly Color CaseRedC = new Color32(181, 18, 31, 255);
        static readonly Color StringRed = new Color32(232, 35, 58, 200);
        static readonly Color Sticky = new Color32(255, 232, 154, 255);
        static readonly Color GritOrange = new Color32(255, 138, 61, 255);

        // ---------------------------------------------------------------- case board

        static Animator BuildStoryBoard(Transform canvas, MenuRouter router)
        {
            StoryCatalogBuilder.Build();
            var panelT = canvas.Find("Main Content/Chapters Panel");
            if (panelT == null) { Debug.LogWarning("[Story] Heat's Chapters Panel is missing, no case board."); return null; }
            var panel = RepurposePanel(canvas, "Chapters Panel", MenuRouter.Story);
            StripHeatManagers(panel);
            var content = panel.Find("Content");
            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            var bg = Img(content, "Board", null, BoardBg); bg.raycastTarget = false;
            Stretch(bg.rectTransform);

            // Header: the case stamp and who's on it.
            var stampBg = Img(content, "Stamp", null, CaseRedC); stampBg.raycastTarget = false;
            Place(stampBg.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(66, -34), new Vector2(250, 56));
            stampBg.rectTransform.localRotation = Quaternion.Euler(0, 0, 3f);
            var stamp = Txt(stampBg.transform, "Label", "CASE 13-GP", bsBlack, 40, Color.white, TextAlignmentOptions.Center);
            Stretch(stamp.rectTransform);
            var who = Txt(content, "Subject", "CITY PD  //  DET. M. KOWAL  //  SUBJECT: THE ROOKIE", monoMedium, 17, new Color32(154, 157, 168, 255), TextAlignmentOptions.Left);
            Place(who.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(340, -50), new Vector2(800, 26));
            var act = Txt(content, "Act", "ACT 1", monoBold, 17, Paper, TextAlignmentOptions.Left);
            act.characterSpacing = 4;
            Place(act.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(340, -78), new Vector2(800, 26));

            var cat = StoryCatalog.Instance ?? AssetDatabase.LoadAssetAtPath<StoryCatalog>("Assets/_ShiftRush/Resources/SR_Story.asset");
            int count = cat != null ? Mathf.Min(12, cat.episodes.Count) : 12;

            // Chapter labels and the red string between the photos.
            var strings = Rect(content, "Red String");
            Stretch(strings);
            for (int ch = 0; ch < 3; ch++)
            {
                var l = Txt(content, "Chapter " + (ch + 1), "CHAPTER " + (ch + 1) + "  ·  " + (cat != null && ch < cat.chapters.Length ? cat.chapters[ch] : ""), bsBlack, 26,
                    new Color32(154, 157, 168, 255), TextAlignmentOptions.Left);
                l.characterSpacing = 4;
                Place(l.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -102 - ch * 286), new Vector2(700, 32));
            }
            var centres = new List<Vector2>();
            var cards = new List<CaseBoardPanel.Card>();
            float[] tilt = { -3f, 2f, -2f, 3f, 2f, -3f, 1f, -2f, -1f, 2f, -3f, 1.5f };
            for (int i = 0; i < count; i++)
            {
                int col = i % 4, row = i / 4;
                // The canvas is about 1830 x 1030 at 16:9: four 220-wide cards a row leave room for the file.
                Vector2 tl = new Vector2(60 + col * 270, -136 - row * 286);
                centres.Add(tl + new Vector2(110, -108));
                var root = Rect(content, "Episode " + (i + 1));
                Place(root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f), tl + new Vector2(110, -108), new Vector2(220, 216));
                root.localRotation = Quaternion.Euler(0, 0, tilt[i % tilt.Length]);
                var ring = Img(root, "Ring", null, Pink); ring.raycastTarget = false;
                Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(242, 238));
                var paper = Img(root, "Paper", null, CasePaper);
                Stretch(paper.rectTransform);
                var photo = Img(root, "Photo", null, new Color32(32, 36, 44, 255)); photo.raycastTarget = false; photo.preserveAspect = false;
                Place(photo.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(-24, 110));
                var pin = Img(root, "Pin", knob, CaseRedC); pin.raycastTarget = false;
                Place(pin.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -2), new Vector2(22, 22));
                var num = Txt(root, "Number", "EP 01", monoBold, 15, CaseInk, TextAlignmentOptions.Left);
                Place(num.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(12, -128), new Vector2(-24, 20));
                var status = Txt(root, "Status", "OPEN", monoBold, 15, CaseRedC, TextAlignmentOptions.Right);
                Place(status.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -128), new Vector2(-24, 20));
                var title = Txt(root, "Title", "FIRST DROP", bsBlack, 34, CaseInk, TextAlignmentOptions.Left);
                title.textWrappingMode = TextWrappingModes.NoWrap; title.enableAutoSizing = true; title.fontSizeMin = 20; title.fontSizeMax = 34;
                Place(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(12, -148), new Vector2(-24, 40));
                var kind = Txt(root, "Kind", "RACE", monoMedium, 13, new Color32(85, 82, 74, 255), TextAlignmentOptions.Left);
                kind.characterSpacing = 4;
                Place(kind.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(12, -190), new Vector2(-24, 20));
                var b = paper.gameObject.AddComponent<Button>(); Plain(b);
                cards.Add(new CaseBoardPanel.Card { root = root, paper = paper, photo = photo, ring = ring, pin = pin, number = num, title = title, status = status, kind = kind, button = b });
            }
            for (int i = 1; i < centres.Count; i++) StringLine(strings, centres[i - 1] + new Vector2(0, 104), centres[i] + new Vector2(0, 104), i % 4 == 0 ? 2f : 3f);

            // The open file down the right.
            var file = Img(content, "Open File", null, CasePaper); file.raycastTarget = false;
            Place(file.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -96), new Vector2(600, 790));
            var f = file.transform;
            var fileTag = Txt(f, "Tag", "OPEN FILE", monoBold, 15, CaseRedC, TextAlignmentOptions.Left);
            fileTag.characterSpacing = 4;
            Place(fileTag.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(30, -26), new Vector2(-60, 22));
            var fileTitle = Txt(f, "Title", "ON AIR", bsBlack, 78, CaseInk, TextAlignmentOptions.Left);
            fileTitle.textWrappingMode = TextWrappingModes.NoWrap; fileTitle.enableAutoSizing = true; fileTitle.fontSizeMin = 40; fileTitle.fontSizeMax = 78;
            Place(fileTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(30, -52), new Vector2(-60, 86));
            var syn = Txt(f, "Synopsis", "", barlowSemi, 23, CaseInk, TextAlignmentOptions.TopLeft);
            Place(syn.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(30, -146), new Vector2(-60, 96));
            var rule = Img(f, "Rule", null, CaseInk); rule.raycastTarget = false;
            Place(rule.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(30, -250), new Vector2(-60, 3));
            var goal = Txt(f, "Goal", "", barlowSemi, 23, CaseInk, TextAlignmentOptions.TopLeft);
            Place(goal.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(30, -264), new Vector2(-60, 62));
            var gloss = Txt(f, "Gloss", "", barlowSemi, 23, CaseInk, TextAlignmentOptions.TopLeft);
            Place(gloss.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(30, -330), new Vector2(-60, 30));
            var grit = Txt(f, "Grit", "", barlowSemi, 23, CaseInk, TextAlignmentOptions.TopLeft);
            Place(grit.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(30, -362), new Vector2(-60, 30));
            var pays = Txt(f, "Pays", "", barlowSemi, 23, CaseInk, TextAlignmentOptions.TopLeft);
            Place(pays.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(30, -394), new Vector2(-60, 30));

            var leanTitle = Txt(f, "Lean Title", "THE FEED LEANS", monoBold, 14, CaseInk, TextAlignmentOptions.Left);
            leanTitle.characterSpacing = 4;
            Place(leanTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(30, -446), new Vector2(-60, 20));
            var leanBg = Img(f, "Lean", null, GritOrange); leanBg.raycastTarget = false;
            Place(leanBg.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(30, -470), new Vector2(-60, 16));
            var leanFill = Img(leanBg.transform, "Gloss", null, Signal); leanFill.raycastTarget = false;
            var lf = leanFill.rectTransform; lf.anchorMin = Vector2.zero; lf.anchorMax = new Vector2(0.5f, 1f); lf.offsetMin = lf.offsetMax = Vector2.zero;
            var leanLabel = Txt(f, "Lean Label", "", monoBold, 14, CaseInk, TextAlignmentOptions.Left);
            Place(leanLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(30, -492), new Vector2(-60, 20));

            var strengthLabel = Txt(f, "Strength Title", "CASE STRENGTH", monoBold, 14, CaseInk, TextAlignmentOptions.Left);
            strengthLabel.characterSpacing = 4;
            Place(strengthLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(30, -530), new Vector2(-60, 20));
            var segs = new Image[6];
            for (int i = 0; i < segs.Length; i++)
            {
                segs[i] = Img(f, "Strength " + (i + 1), null, CaseRedC); segs[i].raycastTarget = false;
                Place(segs[i].rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(30 + i * 91, -556), new Vector2(85, 12));
            }

            var note = Img(f, "Kowal Note", null, Sticky); note.raycastTarget = false;
            Place(note.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -590), new Vector2(320, 96));
            note.rectTransform.localRotation = Quaternion.Euler(0, 0, -3f);
            var nt = Txt(note.transform, "Title", "KOWAL NOTE", monoBold, 13, new Color32(107, 78, 0, 255), TextAlignmentOptions.TopLeft);
            Place(nt.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(14, -10), new Vector2(-24, 18));
            var noteText = Txt(note.transform, "Text", "", barlowSemi, 21, CaseInk, TextAlignmentOptions.TopLeft);
            Place(noteText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(14, -30), new Vector2(-24, 60));

            var goShadow = Img(f, "Open Shadow", null, Signal); goShadow.raycastTarget = false;
            Place(goShadow.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(8, 16), new Vector2(-60, 70));
            var go = Img(f, "Open", null, Pink);
            Place(go.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(-60, 70));
            var goText = Txt(go.transform, "Label", "OPEN EPISODE", bsBlack, 40, Night, TextAlignmentOptions.Center);
            Stretch(goText.rectTransform);
            var goButton = go.gameObject.AddComponent<Button>(); Plain(goButton);

            // Scanner along the bottom.
            var bar = Img(content, "Scanner", null, new Color32(7, 8, 11, 255)); bar.raycastTarget = false;
            Place(bar.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 74));
            var barLine = Img(bar.transform, "Edge", null, Live); barLine.raycastTarget = false;
            Place(barLine.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 3));
            var scan = Txt(bar.transform, "Line", "", monoMedium, 18, new Color32(200, 203, 210, 255), TextAlignmentOptions.Left);
            Place(scan.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(66, 0), new Vector2(-560, 0));
            var hint = Txt(bar.transform, "Hint", "<color=#FF2E9A>ARROWS</color>  PICK     <color=#FF2E9A>ENTER</color>  OPEN     <color=#FF2E9A>ESC</color>  BACK", monoMedium, 15, new Color32(154, 157, 168, 255), TextAlignmentOptions.Right);
            Place(hint.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-60, 0), new Vector2(-1400, 0));

            var cb = panel.gameObject.AddComponent<CaseBoardPanel>();
            var so = new SerializedObject(cb);
            so.FindProperty("visibleGroup").objectReferenceValue = panel.GetComponent<CanvasGroup>();
            so.FindProperty("router").objectReferenceValue = router;
            var arr = so.FindProperty("cards");
            arr.arraySize = cards.Count;
            for (int i = 0; i < cards.Count; i++)
            {
                var p = arr.GetArrayElementAtIndex(i); var c = cards[i];
                p.FindPropertyRelative("root").objectReferenceValue = c.root;
                p.FindPropertyRelative("paper").objectReferenceValue = c.paper;
                p.FindPropertyRelative("photo").objectReferenceValue = c.photo;
                p.FindPropertyRelative("ring").objectReferenceValue = c.ring;
                p.FindPropertyRelative("pin").objectReferenceValue = c.pin;
                p.FindPropertyRelative("number").objectReferenceValue = c.number;
                p.FindPropertyRelative("title").objectReferenceValue = c.title;
                p.FindPropertyRelative("status").objectReferenceValue = c.status;
                p.FindPropertyRelative("kind").objectReferenceValue = c.kind;
                p.FindPropertyRelative("button").objectReferenceValue = c.button;
            }
            so.FindProperty("actLabel").objectReferenceValue = act;
            so.FindProperty("fileTag").objectReferenceValue = fileTag;
            so.FindProperty("fileTitle").objectReferenceValue = fileTitle;
            so.FindProperty("fileSynopsis").objectReferenceValue = syn;
            so.FindProperty("fileGoal").objectReferenceValue = goal;
            so.FindProperty("fileGloss").objectReferenceValue = gloss;
            so.FindProperty("fileGrit").objectReferenceValue = grit;
            so.FindProperty("filePays").objectReferenceValue = pays;
            so.FindProperty("kowalNote").objectReferenceValue = noteText;
            so.FindProperty("leanFill").objectReferenceValue = lf;
            so.FindProperty("leanLabel").objectReferenceValue = leanLabel;
            SetArray(so, "strength", segs);
            so.FindProperty("strengthLabel").objectReferenceValue = strengthLabel;
            so.FindProperty("openButton").objectReferenceValue = goButton;
            so.FindProperty("openLabel").objectReferenceValue = goText;
            so.FindProperty("openFill").objectReferenceValue = go;
            so.FindProperty("scanner").objectReferenceValue = scan;
            so.ApplyModifiedPropertiesWithoutUndo();

            CloneHotkeys(canvas.Find("Main Content/Home Panel"), content, router, "Back", router.Back);
            return panel.GetComponent<Animator>();
        }

        /// <summary>
        /// Heat's demo Chapter and Achievement managers live on these panels and throw on open once their demo
        /// content is gone (which leaves the panel stuck invisible), so they go.
        /// </summary>
        static void StripHeatManagers(Transform panel)
        {
            foreach (var mb in panel.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string n = mb.GetType().Name;
                if (n == "ChapterManager" || n == "AchievementManager" || n == "ChapterButton" || n == "AchievementItem") Object.DestroyImmediate(mb);
            }
        }

        /// <summary>A strand of red string between two pins, as a thin rotated bar.</summary>
        static void StringLine(RectTransform parent, Vector2 a, Vector2 b, float width)
        {
            var img = Img(parent, "String", null, StringRed); img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = a;
            Vector2 d = b - a;
            rt.sizeDelta = new Vector2(d.magnitude, width);
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        // ---------------------------------------------------------------- briefing

        static Animator BuildBriefing(Transform canvas, MenuRouter router)
        {
            if (canvas.Find("Main Content/Achievements") == null) { Debug.LogWarning("[Story] Heat's Achievements panel is missing, no briefing."); return null; }
            var panel = RepurposePanel(canvas, "Achievements", MenuRouter.Briefing);
            StripHeatManagers(panel);
            var content = panel.Find("Content");

            var bg = Img(content, "Background", null, Night); bg.raycastTarget = false;
            Stretch(bg.rectTransform);
            var slab = Img(content, "Slab", null, new Color32(20, 17, 31, 255)); slab.raycastTarget = false;
            Place(slab.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(160, 0), new Vector2(1080, 1500));
            slab.rectTransform.localRotation = Quaternion.Euler(0, 0, -10f);

            var photo = Img(content, "Flyover", null, new Color32(30, 25, 48, 255)); photo.raycastTarget = false;
            Place(photo.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-70, -132), new Vector2(820, 461));
            var photoLabel = Txt(content, "Flyover Label", "CAM 01  //  DRONE", monoMedium, 15, Dim, TextAlignmentOptions.Left);
            photoLabel.characterSpacing = 4;
            Place(photoLabel.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-70, -102), new Vector2(820, 24));

            var tagBg = Img(content, "Tonight", null, Paper); tagBg.raycastTarget = false;
            Place(tagBg.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(86, -60), new Vector2(250, 34));
            tagBg.rectTransform.localRotation = Quaternion.Euler(0, 0, 2f);
            var tonight = Txt(tagBg.transform, "Label", "TONIGHT ON CH 13", monoBold, 16, Night, TextAlignmentOptions.Center);
            Stretch(tonight.rectTransform);
            var tag = Txt(content, "Episode", "", monoMedium, 17, Mist, TextAlignmentOptions.Left);
            Place(tag.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(356, -64), new Vector2(700, 26));

            var echo = Txt(content, "Title Echo", "ON AIR", bsBlack, 200, Signal, TextAlignmentOptions.TopLeft);
            echo.textWrappingMode = TextWrappingModes.NoWrap; echo.enableAutoSizing = true; echo.fontSizeMin = 90; echo.fontSizeMax = 200;
            Place(echo.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(93, -117), new Vector2(880, 210));
            var title = Txt(content, "Title", "ON AIR", bsBlack, 200, Pink, TextAlignmentOptions.TopLeft);
            title.textWrappingMode = TextWrappingModes.NoWrap; title.enableAutoSizing = true; title.fontSizeMin = 90; title.fontSizeMax = 200;
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(86, -110), new Vector2(880, 210));

            var face = Img(content, "Slim", null, Pink); face.raycastTarget = false;
            Place(face.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(86, -346), new Vector2(76, 76));
            var faceIn = Img(face.transform, "Inner", null, new Color32(30, 25, 48, 255)); faceIn.raycastTarget = false;
            Place(faceIn.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-6, -6));
            var faceT = Txt(faceIn.transform, "Mark", "S", bsBlack, 44, Pink, TextAlignmentOptions.Center);
            Stretch(faceT.rectTransform);
            var slimWho = Txt(content, "Slim Name", "SLIM  ·  CHANNEL 13", monoBold, 15, new Color32(255, 111, 184, 255), TextAlignmentOptions.Left);
            slimWho.characterSpacing = 4;
            Place(slimWho.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(182, -344), new Vector2(700, 22));
            var slim = Txt(content, "Slim Line", "", barlowSemi, 30, Paper, TextAlignmentOptions.TopLeft);
            Place(slim.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(182, -370), new Vector2(720, 130));

            // The run: main goal and the two Feed bonuses.
            var runTitle = Txt(content, "Run Title", "THE RUN", monoBold, 15, Dim, TextAlignmentOptions.Left);
            runTitle.characterSpacing = 6;
            Place(runTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(86, -530), new Vector2(400, 22));
            TMP_Text goalText, goalPays, glossText, gritText;
            RunRow(content, "Main", "MAIN", Paper, -560, out goalText, out goalPays);
            RunRow(content, "Gloss", "GLOSS", Signal, -626, out glossText, out _);
            RunRow(content, "Grit", "GRIT", GritOrange, -692, out gritText, out _);
            var runNote = Txt(content, "Run Note", "BONUSES ARE YOURS TO CHASE. DRIFTS TILT THE FEED GLOSS, KNOCKS TILT IT GRIT.", monoMedium, 14, Dim, TextAlignmentOptions.Left);
            Place(runNote.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(86, -758), new Vector2(820, 22));

            // Cards: rival, track, car.
            GameObject rivalCard; TMP_Text rivalName, rivalLine, trackName, trackInfo, carName, carInfo;
            InfoCard(content, "Rival", "RIVAL TO BEAT", Gold, new Vector2(-70 - 2 * 280, -630), out rivalCard, out rivalName, out rivalLine);
            InfoCard(content, "Track", "TRACK", new Color32(140, 134, 163, 255), new Vector2(-70 - 280, -630), out _, out trackName, out trackInfo);
            InfoCard(content, "Car", "YOUR CAR", new Color32(140, 134, 163, 255), new Vector2(-70, -630), out _, out carName, out carInfo);

            // Buttons.
            var goShadow = Img(content, "Go Shadow", null, Signal); goShadow.raycastTarget = false;
            Place(goShadow.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-70 - 460 + 8, -812), new Vector2(370, 84));
            var go = Img(content, "Go Live", null, Pink);
            Place(go.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-70 - 460, -804), new Vector2(370, 84));
            var goText = Txt(go.transform, "Label", "GO LIVE", bsBlack, 52, Night, TextAlignmentOptions.Center);
            Stretch(goText.rectTransform);
            var goButton = go.gameObject.AddComponent<Button>(); Plain(goButton);
            var board = OutlineButton(content, "Case Board", "CASE BOARD", new Vector2(-70 - 220, -810), new Vector2(210, 72));
            var garage = OutlineButton(content, "Garage", "<color=#FF2E9A>G</color>  GARAGE", new Vector2(-70, -810), new Vector2(200, 72));

            // Chat along the bottom.
            var bar = Img(content, "Chat", null, new Color32(20, 17, 31, 255)); bar.raycastTarget = false;
            Place(bar.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 76));
            var edge = Img(bar.transform, "Edge", null, Pink); edge.raycastTarget = false;
            Place(edge.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 3));
            var chat = Txt(bar.transform, "Lines", "<b><color=#2EF2FF>CHAT</color></b>     <b><color=#2EF2FF>nitroqueen</color></b> <color=#BDB7D1>rookie on the big stage tonight</color>     <b><color=#FF8A3D>mudlord</color></b> <color=#BDB7D1>put someone in the wall</color>",
                barlowSemi, 22, Paper, TextAlignmentOptions.Left);
            Place(chat.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(66, 0), new Vector2(-560, 0));
            var hint = Txt(bar.transform, "Hint", "<color=#FF2E9A>ENTER</color>  GO LIVE     <color=#FF2E9A>G</color>  GARAGE     <color=#FF2E9A>ESC</color>  BACK", monoMedium, 15, Dim, TextAlignmentOptions.Right);
            Place(hint.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-60, 0), new Vector2(-1400, 0));

            var bp = panel.gameObject.AddComponent<BriefingPanel>();
            var so = new SerializedObject(bp);
            so.FindProperty("visibleGroup").objectReferenceValue = panel.GetComponent<CanvasGroup>();
            so.FindProperty("router").objectReferenceValue = router;
            so.FindProperty("tag").objectReferenceValue = tag;
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("titleEcho").objectReferenceValue = echo;
            so.FindProperty("slimLine").objectReferenceValue = slim;
            so.FindProperty("goalText").objectReferenceValue = goalText;
            so.FindProperty("goalPays").objectReferenceValue = goalPays;
            so.FindProperty("glossText").objectReferenceValue = glossText;
            so.FindProperty("gritText").objectReferenceValue = gritText;
            so.FindProperty("rivalCard").objectReferenceValue = rivalCard;
            so.FindProperty("rivalName").objectReferenceValue = rivalName;
            so.FindProperty("rivalLine").objectReferenceValue = rivalLine;
            so.FindProperty("trackName").objectReferenceValue = trackName;
            so.FindProperty("trackInfo").objectReferenceValue = trackInfo;
            so.FindProperty("carName").objectReferenceValue = carName;
            so.FindProperty("carInfo").objectReferenceValue = carInfo;
            so.FindProperty("photo").objectReferenceValue = photo;
            so.FindProperty("photoLabel").objectReferenceValue = photoLabel;
            so.FindProperty("goButton").objectReferenceValue = goButton;
            so.FindProperty("boardButton").objectReferenceValue = board;
            so.FindProperty("garageButton").objectReferenceValue = garage;
            so.ApplyModifiedPropertiesWithoutUndo();

            CloneHotkeys(canvas.Find("Main Content/Home Panel"), content, router, "Back", router.Back);
            return panel.GetComponent<Animator>();
        }

        static void RunRow(Transform content, string name, string chip, Color chipColour, float y, out TMP_Text text, out TMP_Text value)
        {
            var row = Img(content, name + " Row", null, new Color32(26, 22, 40, 255)); row.raycastTarget = false;
            Place(row.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(86, y), new Vector2(820, 58));
            var c = Img(row.transform, "Chip", null, chipColour); c.raycastTarget = false;
            Place(c.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(84, 28));
            var ct = Txt(c.transform, "Label", chip, monoBold, 14, Night, TextAlignmentOptions.Center);
            Stretch(ct.rectTransform);
            text = Txt(row.transform, "Text", "", barlowBold, 28, Paper, TextAlignmentOptions.Left);
            text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis;
            Place(text.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(118, 0), new Vector2(-260, 0));
            value = Txt(row.transform, "Value", "", monoBold, 22, Gold, TextAlignmentOptions.Right);
            Place(value.rectTransform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-18, 0), new Vector2(160, 0));
        }

        static void InfoCard(Transform content, string name, string heading, Color headColour, Vector2 topRight, out GameObject root, out TMP_Text big, out TMP_Text small)
        {
            var card = Img(content, name + " Card", null, Night); card.raycastTarget = false;
            Place(card.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), topRight, new Vector2(266, 140));
            Frame(card.transform, headColour, 2f);
            var h = Txt(card.transform, "Heading", heading, monoBold, 14, headColour, TextAlignmentOptions.TopLeft);
            h.characterSpacing = 4;
            Place(h.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(16, -14), new Vector2(-30, 20));
            big = Txt(card.transform, "Name", "", bsBlack, 40, Paper, TextAlignmentOptions.TopLeft);
            big.textWrappingMode = TextWrappingModes.NoWrap; big.enableAutoSizing = true; big.fontSizeMin = 24; big.fontSizeMax = 40;
            Place(big.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(16, -40), new Vector2(-30, 46));
            small = Txt(card.transform, "Info", "", barlowSemi, 19, Mist, TextAlignmentOptions.TopLeft);
            Place(small.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(16, -88), new Vector2(-30, 46));
            root = card.gameObject;
        }

        /// <summary>A thin border inside a rect: four bars.</summary>
        static void Frame(Transform parent, Color c, float w)
        {
            string[] n = { "Top", "Bottom", "Left", "Right" };
            for (int i = 0; i < 4; i++)
            {
                var bar = Img(parent, "Frame " + n[i], null, c); bar.raycastTarget = false;
                var rt = bar.rectTransform;
                if (i < 2) { rt.anchorMin = new Vector2(0, i == 0 ? 1 : 0); rt.anchorMax = new Vector2(1, i == 0 ? 1 : 0); rt.pivot = new Vector2(0.5f, i == 0 ? 1 : 0); rt.sizeDelta = new Vector2(0, w); }
                else { rt.anchorMin = new Vector2(i == 2 ? 0 : 1, 0); rt.anchorMax = new Vector2(i == 2 ? 0 : 1, 1); rt.pivot = new Vector2(i == 2 ? 0 : 1, 0.5f); rt.sizeDelta = new Vector2(w, 0); }
                rt.anchoredPosition = Vector2.zero;
            }
        }

        static Button OutlineButton(Transform content, string name, string label, Vector2 topRight, Vector2 size)
        {
            var img = Img(content, name, null, new Color(0, 0, 0, 0.01f));
            Place(img.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), topRight, size);
            Frame(img.transform, Paper, 2f);
            var t = Txt(img.transform, "Label", label, bsBold, 30, Paper, TextAlignmentOptions.Center);
            Stretch(t.rectTransform);
            var b = img.gameObject.AddComponent<Button>(); Plain(b);
            return b;
        }
    }
}
