using ShiftRush.Shift;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftRush.EditorTools
{
    /// <summary>Builds Resources/SR_ShiftHUD.prefab: the SHIFT timing diamond, warp flash, challenge readout and nitro reward.</summary>
    public static partial class MenuBuilder
    {
        const string ShiftHudPath = "Assets/_ShiftRush/Resources/SR_ShiftHUD.prefab";

        [MenuItem("SHIFT RUSH/SHIFT/Build SHIFT HUD")]
        static void BuildShiftHudOnly()
        {
            LoadFonts();
            MakeArt();
            MakeShiftHud(LoadSounds());
            AssetDatabase.SaveAssets();
            Debug.Log("[SHIFT//RUSH] SHIFT HUD built: " + ShiftHudPath);
        }

        static void MakeShiftHud(Sounds sounds)
        {
            var root = new GameObject("SR_ShiftHUD", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 350; // over the race HUD, under pause (400)
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var hud = root.AddComponent<ShiftHUD>();
            var src = root.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.ignoreListenerPause = true;
            src.outputAudioMixerGroup = MixerGroup("UI");
            var so = new SerializedObject(hud);

            // ---------------- selector
            var sel = Layer(root.transform, "Selector");
            var selGroup = sel.gameObject.AddComponent<CanvasGroup>();
            var tint = Img(sel, "Tint", null, new Color(Ink.r, Ink.g, Ink.b, 0.45f));
            Stretch(tint.rectTransform);
            var band = Img(sel, "Streaks", streaks, new Color(Magenta.r, Magenta.g, Magenta.b, 0.22f));
            band.type = Image.Type.Tiled;
            Place(band.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 190), new Vector2(0, 330));
            var title = Txt(sel, "Title", "SHIFT<color=#FF40D9>//</color>", fontTitle, 150, Color.white, TextAlignmentOptions.Center);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 410), new Vector2(1000, 170));
            var sub = Txt(sel, "Prompt", "PRESS <color=#FF40D9>SHIFT</color> WHEN THE DIAMONDS <color=#3DFF8A>MEET</color>   //   2 OF 3 TO WARP", fontBold, 26, Color.white, TextAlignmentOptions.Center);
            sub.characterSpacing = 8;
            Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 316), new Vector2(1400, 40));

            // Timing diamond: a fixed white diamond outline; a magenta one closes in on it. Press as they meet.
            var green = new Color32(61, 255, 138, 255);
            var diamondFrame = SaveSprite("T_DiamondFrame", 256, 256, (x, y, w, h) =>
            {
                float d = (Mathf.Abs(x - w * 0.5f) + Mathf.Abs(y - h * 0.5f)) / (w * 0.5f); // 1 = the diamond's edge
                return Mathf.Clamp01((d - 0.84f) / 0.02f) * Mathf.Clamp01((0.98f - d) / 0.02f);
            }, Vector4.zero);
            var diamondFill = SaveSprite("T_DiamondFill", 256, 256, (x, y, w, h) =>
            {
                float d = (Mathf.Abs(x - w * 0.5f) + Mathf.Abs(y - h * 0.5f)) / (w * 0.5f);
                return Mathf.Clamp01((0.98f - d) / 0.03f);
            }, Vector4.zero);
            var diamondPos = new Vector2(0, 205);
            var sweet = Img(sel, "Sweet Spot", diamondFill, new Color(green.r / 255f, green.g / 255f, green.b / 255f, 0f));
            Place(sweet.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), diamondPos, new Vector2(180, 180));
            var target = Img(sel, "Target Diamond", diamondFrame, Color.white);
            Place(target.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), diamondPos, new Vector2(190, 190));
            var closer = Img(sel, "Closing Diamond", diamondFrame, Magenta);
            Place(closer.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), diamondPos, new Vector2(190, 190));
            var shiftKey = Txt(sel, "Key", "SHIFT", fontTitle, 34, Color.white, TextAlignmentOptions.Center);
            shiftKey.characterSpacing = 6;
            Place(shiftKey.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), diamondPos, new Vector2(170, 50));

            var pipsProp = so.FindProperty("pips");
            pipsProp.arraySize = 3;
            for (int k = 0; k < 3; k++)
            {
                var pip = Img(sel, "Try " + (k + 1), slant, new Color(1, 1, 1, 0.6f)); pip.type = Image.Type.Sliced;
                Place(pip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((k - 1) * 120, 70), new Vector2(96, 18));
                pipsProp.GetArrayElementAtIndex(k).objectReferenceValue = pip;
            }

            // ---------------- flash
            var flash = Img(root.transform, "Flash", null, new Color(1, 1, 1, 0));
            Stretch(flash.rectTransform);
            var flashTitle = Txt(root.transform, "Flash Title", "", fontTitle, 170, Ink, TextAlignmentOptions.Center);
            Place(flashTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600, 220));

            // ---------------- challenge readout (top centre, between the race HUD corners)
            var ch = Layer(root.transform, "Challenge");
            var chGroup = ch.gameObject.AddComponent<CanvasGroup>();
            var chBar = Img(ch, "Accent", slant, Magenta);
            Place(chBar.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -48), new Vector2(84, 10));
            var chTitle = Txt(ch, "Title", "RING RUSH", fontTitle, 44, Magenta, TextAlignmentOptions.Center);
            chTitle.characterSpacing = 6;
            Place(chTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -64), new Vector2(700, 56));
            var chCount = Txt(ch, "Count", "0/10", fontTitle, 104, Color.white, TextAlignmentOptions.Center);
            Place(chCount.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -116), new Vector2(700, 120));
            var chTime = Txt(ch, "Time", "10.0", fontTitle, 52, Color.white, TextAlignmentOptions.Center);
            Place(chTime.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -236), new Vector2(300, 64));
            var prog = new GameObject("Progress", typeof(RectTransform)).GetComponent<RectTransform>();
            prog.SetParent(ch, false);
            Place(prog, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -312), new Vector2(560, 10));
            var pBg = Img(prog, "Bg", slant, new Color(1, 1, 1, 0.15f)); pBg.type = Image.Type.Sliced; Stretch(pBg.rectTransform);
            var pFill = Img(prog, "Fill", slant, Color.white); pFill.type = Image.Type.Sliced; Stretch(pFill.rectTransform);
            pFill.rectTransform.anchorMax = new Vector2(0, 1);

            // ---------------- reward (lower centre)
            var rw = Layer(root.transform, "Reward");
            var rwGroup = rw.gameObject.AddComponent<CanvasGroup>();
            var rwBg = Img(rw, "Bg", slant, new Color(Panel.r, Panel.g, Panel.b, 0.85f));
            rwBg.type = Image.Type.Sliced;
            Place(rwBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -190), new Vector2(760, 170));
            var rwFrame = Img(rw, "Frame", slantFrame, Magenta);
            rwFrame.type = Image.Type.Sliced;
            Place(rwFrame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -190), new Vector2(760, 170));
            var rwHead = Txt(rw, "Headline", "+100% NITRO", fontTitle, 64, Color.white, TextAlignmentOptions.Center);
            Place(rwHead.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(740, 76));
            var rwDetail = Txt(rw, "Detail", "", fontBold, 20, Steel, TextAlignmentOptions.Center);
            rwDetail.characterSpacing = 8;
            Place(rwDetail.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -200), new Vector2(740, 30));
            var rwBar = new GameObject("Nitro", typeof(RectTransform)).GetComponent<RectTransform>();
            rwBar.SetParent(rw, false);
            Place(rwBar, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -236), new Vector2(520, 14));
            var rbBg = Img(rwBar, "Bg", slant, new Color(1, 1, 1, 0.15f)); rbBg.type = Image.Type.Sliced; Stretch(rbBg.rectTransform);
            var rbFill = Img(rwBar, "Fill", slant, Magenta); rbFill.type = Image.Type.Sliced; Stretch(rbFill.rectTransform);
            rbFill.rectTransform.anchorMax = new Vector2(0, 1);

            so.FindProperty("selectorGroup").objectReferenceValue = selGroup;
            so.FindProperty("targetDiamond").objectReferenceValue = target;
            so.FindProperty("closingDiamond").objectReferenceValue = closer;
            so.FindProperty("sweetFill").objectReferenceValue = sweet;
            so.FindProperty("green").colorValue = (Color)green;
            so.FindProperty("flash").objectReferenceValue = flash;
            so.FindProperty("flashTitle").objectReferenceValue = flashTitle;
            so.FindProperty("challengeGroup").objectReferenceValue = chGroup;
            so.FindProperty("challengeTitle").objectReferenceValue = chTitle;
            so.FindProperty("challengeCount").objectReferenceValue = chCount;
            so.FindProperty("challengeTime").objectReferenceValue = chTime;
            so.FindProperty("progressFill").objectReferenceValue = pFill.rectTransform;
            so.FindProperty("rewardGroup").objectReferenceValue = rwGroup;
            so.FindProperty("rewardHeadline").objectReferenceValue = rwHead;
            so.FindProperty("rewardDetail").objectReferenceValue = rwDetail;
            so.FindProperty("rewardFill").objectReferenceValue = rbFill.rectTransform;
            so.FindProperty("audioSource").objectReferenceValue = src;
            so.FindProperty("enterClip").objectReferenceValue = sounds.shift;
            so.FindProperty("pickClip").objectReferenceValue = sounds.confirm;
            so.FindProperty("pulseClip").objectReferenceValue = sounds.tick;
            so.FindProperty("rewardClip").objectReferenceValue = sounds.select;
            so.FindProperty("accent").colorValue = Magenta;
            so.ApplyModifiedPropertiesWithoutUndo();

            foreach (var g in root.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            PrefabUtility.SaveAsPrefabAsset(root, ShiftHudPath);
            Object.DestroyImmediate(root);
        }

        static RectTransform Layer(Transform parent, string name)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Stretch(rt);
            return rt;
        }
    }
}
