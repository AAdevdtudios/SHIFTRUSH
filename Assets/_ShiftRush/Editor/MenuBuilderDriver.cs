using ShiftRush.Core;
using ShiftRush.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// The garage's DRIVER tab, in the same stream look as the CAR tab: slots down the left, the pieces and their
    /// colourways under the driver on stage, and the Fit Read down the right (the look's GLOSS / GRIT lean and the
    /// big wear button). Plus the CAR / DRIVER tabs under the garage title. Called from Build Main Menu.
    /// </summary>
    public static partial class MenuBuilder
    {
        static readonly Color GlossC = new Color32(46, 242, 255, 255);
        static readonly Color GritC = new Color32(255, 138, 61, 255);
        static readonly Color Slab = new Color32(26, 22, 40, 255);

        static DriverPanel BuildDriverMode(Transform content, Transform panel, Showroom showroom, out GameObject root)
        {
            DriverCatalogBuilder.Build();
            var dr = Rect(content, "Driver Mode");
            Stretch(dr);
            root = dr.gameObject;

            // Slots down the left, one row per DriverSlot (rows a body doesn't have hide themselves).
            // Top / Bottom / Accessories, then that group's slots.
            var gButtons = new Button[3]; var gBacks = new Image[3]; var gLabels = new TMP_Text[3];
            string[] gNames = { "TOP", "BOTTOM", "ACCESSORIES" };
            float[] gW = { 80f, 104f, 134f };
            float gx = 66f;
            for (int i = 0; i < 3; i++)
            {
                var gb = Img(dr, "Group " + gNames[i], null, Slab);
                Place(gb.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(gx, -184), new Vector2(gW[i], 34));
                gx += gW[i] + 6f;
                var gl = Txt(gb.transform, "Label", gNames[i], barlowBold, 19, Dim, TextAlignmentOptions.Center);
                Stretch(gl.rectTransform);
                var b = gb.gameObject.AddComponent<Button>(); Plain(b);
                gButtons[i] = b; gBacks[i] = gb; gLabels[i] = gl;
            }
            var slotCol = Rect(dr, "Slots");
            Place(slotCol, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(66, -230), new Vector2(330, 620));
            var vg = slotCol.gameObject.AddComponent<VerticalLayoutGroup>();
            vg.spacing = 6; vg.childControlWidth = vg.childControlHeight = true; vg.childForceExpandWidth = true; vg.childForceExpandHeight = false;
            var slotValues = System.Enum.GetValues(typeof(DriverSlot));
            int n = slotValues.Length;
            var sButtons = new Button[n]; var sBacks = new Image[n]; var sNames = new TMP_Text[n]; var sVals = new TMP_Text[n];
            foreach (var slot in DriverCatalog.DisplayOrder)
            {
                int i = (int)slot;
                var bg = Img(slotCol, slot.ToString(), null, Slab);
                var le = bg.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 44;
                var nm = Txt(bg.transform, "Slot", DriverPanel.SlotTitle(slot), monoBold, 14, Dim, TextAlignmentOptions.Left);
                nm.characterSpacing = 6;
                Place(nm.rectTransform, new Vector2(0, 0), new Vector2(0.45f, 1), new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(0, 0));
                var val = Txt(bg.transform, "Value", "NONE", barlowBold, 22, Paper, TextAlignmentOptions.Right);
                val.textWrappingMode = TextWrappingModes.NoWrap; val.overflowMode = TextOverflowModes.Ellipsis;
                Place(val.rectTransform, new Vector2(0.35f, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(0, 0));
                var b = bg.gameObject.AddComponent<Button>(); Plain(b);
                sButtons[i] = b; sBacks[i] = bg; sNames[i] = nm; sVals[i] = val;
            }

            // The pieces for the picked slot, under the driver.
            const int Opts = 7;
            var optRow = Rect(dr, "Pieces");
            Place(optRow, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-40, 168), new Vector2(780, 118));
            var oh = optRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            oh.spacing = 8; oh.childAlignment = TextAnchor.MiddleCenter;
            oh.childControlWidth = oh.childControlHeight = true; oh.childForceExpandWidth = true; oh.childForceExpandHeight = true;
            var oButtons = new Button[Opts]; var oBacks = new Image[Opts]; var oIcons = new Image[Opts]; var oNames = new TMP_Text[Opts]; var oNotes = new TMP_Text[Opts];
            for (int i = 0; i < Opts; i++)
            {
                var ob = Img(optRow, "Piece " + (i + 1), null, Slab);
                var ic = Img(ob.transform, "Icon", null, Color.white); ic.preserveAspect = true; ic.raycastTarget = false;
                Place(ic.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(72, 64));
                var on = Txt(ob.transform, "Name", "NONE", barlowBold, 16, Paper, TextAlignmentOptions.Center);
                on.textWrappingMode = TextWrappingModes.NoWrap; on.overflowMode = TextOverflowModes.Ellipsis;
                Place(on.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(-10, 22));
                var nt = Txt(ob.transform, "Note", "", monoBold, 12, Gold, TextAlignmentOptions.Center);
                Place(nt.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(-10, 18));
                var bt = ob.gameObject.AddComponent<Button>(); Plain(bt);
                oButtons[i] = bt; oBacks[i] = ob; oIcons[i] = ic; oNames[i] = on; oNotes[i] = nt;
            }
            var page = Txt(dr, "Page", "", monoBold, 13, Dim, TextAlignmentOptions.Right);
            Place(page.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-40, 292), new Vector2(780, 20));

            // Colourways.
            var swRow = Rect(dr, "Colourways");
            Place(swRow, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-40, 104), new Vector2(780, 50));
            var swTitle = Txt(swRow, "Title", "COLOUR  <color=#FF2E9A>Q E</color>", monoBold, 14, Dim, TextAlignmentOptions.Left);
            swTitle.characterSpacing = 6;
            Place(swTitle.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(160, 0));
            const int Sw = 5;
            var wButtons = new Button[Sw]; var wFills = new Image[Sw]; var wRings = new Image[Sw];
            for (int i = 0; i < Sw; i++)
            {
                var ring = Img(swRow, "Ring " + (i + 1), null, Pink); ring.raycastTarget = false;
                Place(ring.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(170 + i * 58 - 5, 0), new Vector2(50, 50));
                var fill = Img(swRow, "Swatch " + (i + 1), null, Color.gray);
                Place(fill.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(170 + i * 58, 0), new Vector2(40, 40));
                var b = fill.gameObject.AddComponent<Button>(); Plain(b);
                wButtons[i] = b; wFills[i] = fill; wRings[i] = ring;
            }

            // Right column: piece name, body, Fit Read, mask note, wear button.
            const float RX = -60f, RW = 470f;
            var itemName = Txt(dr, "Piece Name", "TRACK JACKET", bsBlack, 84, Paper, TextAlignmentOptions.Right);
            itemName.textWrappingMode = TextWrappingModes.NoWrap;
            itemName.enableAutoSizing = true; itemName.fontSizeMin = 44; itemName.fontSizeMax = 84; // long names shrink to fit
            Place(itemName.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(RX, -154), new Vector2(760, 106));
            var itemLine = Txt(dr, "Piece Line", "", barlowSemi, 22, Mist, TextAlignmentOptions.TopRight);
            Place(itemLine.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(RX, -262), new Vector2(RW, 30));

            var bodyButtons = new Button[2]; var bodyBacks = new Image[2]; var bodyLabels = new TMP_Text[2];
            string[] bodyNames = { "BODY A", "BODY B" };
            for (int i = 0; i < 2; i++)
            {
                var bb = Img(dr, bodyNames[i], null, Slab);
                Place(bb.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(RX - (1 - i) * 128, -306), new Vector2(120, 38));
                var bl = Txt(bb.transform, "Label", bodyNames[i], barlowBold, 20, Dim, TextAlignmentOptions.Center);
                Stretch(bl.rectTransform);
                var b = bb.gameObject.AddComponent<Button>(); Plain(b);
                bodyButtons[i] = b; bodyBacks[i] = bb; bodyLabels[i] = bl;
            }
            var bodyHint = Txt(dr, "Body Hint", "<color=#FF2E9A>B</color>  SWAP", monoBold, 14, Dim, TextAlignmentOptions.Right);
            Place(bodyHint.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(RX - 262, -314), new Vector2(160, 22));

            var fitTitle = Txt(dr, "Fit Title", "FIT READ", monoBold, 15, Dim, TextAlignmentOptions.Left);
            fitTitle.characterSpacing = 8;
            Place(fitTitle.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(RX, -376), new Vector2(RW, 22));
            var leanBack = Img(dr, "Lean", null, GritC); leanBack.raycastTarget = false;
            Place(leanBack.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(RX, -404), new Vector2(RW, 16));
            var leanFill = Img(leanBack.transform, "Gloss", null, GlossC); leanFill.raycastTarget = false;
            var lf = leanFill.rectTransform;
            lf.anchorMin = Vector2.zero; lf.anchorMax = new Vector2(0.5f, 1f); lf.offsetMin = lf.offsetMax = Vector2.zero;
            var glossLabel = Txt(dr, "Gloss Label", "GLOSS 50%", barlowBold, 22, GlossC, TextAlignmentOptions.Left);
            Place(glossLabel.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(RX, -426), new Vector2(RW, 28));
            var gritLabel = Txt(dr, "Grit Label", "GRIT 50%", barlowBold, 22, GritC, TextAlignmentOptions.Right);
            Place(gritLabel.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(RX, -426), new Vector2(RW, 28));
            var fitNote = Txt(dr, "Fit Note", "", barlowMedium, 21, Mist, TextAlignmentOptions.TopLeft);
            Place(fitNote.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(RX, -464), new Vector2(RW, 60));

            var mask = Img(dr, "Mask Note", null, new Color32(242, 238, 227, 255)); mask.raycastTarget = false;
            Place(mask.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(RX, -540), new Vector2(RW, 112));
            mask.rectTransform.localRotation = Quaternion.Euler(0, 0, 1f);
            var mt = Txt(mask.transform, "Title", "MASK ON", monoBold, 14, new Color32(181, 18, 31, 255), TextAlignmentOptions.TopLeft);
            mt.characterSpacing = 8;
            Place(mt.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(16, -10), new Vector2(-30, 20));
            var mb = Txt(mask.transform, "Text", "Kowal can't put a face to your clips. Fans see less of you too.", barlowSemi, 21, new Color32(20, 18, 24, 255), TextAlignmentOptions.TopLeft);
            Place(mb.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(16, -34), new Vector2(-30, 70));

            var rule = Txt(dr, "Rule", "Outfits never change how the car drives. They move fans.", barlowMedium, 19, Dim, TextAlignmentOptions.TopRight);
            Place(rule.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(RX, 236), new Vector2(RW, 26));

            var goShadow = Img(dr, "Wear Shadow", null, Signal); goShadow.raycastTarget = false;
            Place(goShadow.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(RX + 8, 142), new Vector2(RW, 74));
            var go = Img(dr, "Wear", null, Pink);
            Place(go.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(RX, 150), new Vector2(RW, 74));
            var goText = Txt(go.transform, "Label", "WEAR IT", bsBlack, 40, Night, TextAlignmentOptions.Center);
            Stretch(goText.rectTransform);
            var goButton = go.gameObject.AddComponent<Button>(); Plain(goButton);

            var hint = Txt(dr, "Hint", "<color=#FF2E9A>TAB</color>  GROUP     <color=#FF2E9A>W S</color>  SLOT     <color=#FF2E9A>A D</color>  PIECE     <color=#FF2E9A>Q E</color>  COLOUR     <color=#FF2E9A>ENTER</color>  WEAR     <color=#FF2E9A>C</color>  CAR", monoMedium, 15, Dim, TextAlignmentOptions.Left);
            hint.characterSpacing = 6;
            Place(hint.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(66, 40), new Vector2(900, 26));

            var dp = panel.gameObject.AddComponent<DriverPanel>();
            var so = new SerializedObject(dp);
            so.FindProperty("visibleGroup").objectReferenceValue = panel.GetComponent<CanvasGroup>();
            so.FindProperty("showroom").objectReferenceValue = showroom;
            SetArray(so, "groupButtons", gButtons); SetArray(so, "groupBacks", gBacks); SetArray(so, "groupLabels", gLabels);
            SetArray(so, "slotButtons", sButtons); SetArray(so, "slotBacks", sBacks); SetArray(so, "slotNames", sNames); SetArray(so, "slotValues", sVals);
            SetArray(so, "optionButtons", oButtons); SetArray(so, "optionBacks", oBacks); SetArray(so, "optionIcons", oIcons); SetArray(so, "optionNames", oNames); SetArray(so, "optionNotes", oNotes);
            so.FindProperty("pageLabel").objectReferenceValue = page;
            SetArray(so, "swatchButtons", wButtons); SetArray(so, "swatchFills", wFills); SetArray(so, "swatchRings", wRings);
            SetArray(so, "bodyButtons", bodyButtons); SetArray(so, "bodyBacks", bodyBacks); SetArray(so, "bodyLabels", bodyLabels);
            so.FindProperty("itemName").objectReferenceValue = itemName;
            so.FindProperty("itemLine").objectReferenceValue = itemLine;
            so.FindProperty("leanFill").objectReferenceValue = lf;
            so.FindProperty("glossLabel").objectReferenceValue = glossLabel;
            so.FindProperty("gritLabel").objectReferenceValue = gritLabel;
            so.FindProperty("fitNote").objectReferenceValue = fitNote;
            so.FindProperty("maskNote").objectReferenceValue = mask.gameObject;
            so.FindProperty("actionButton").objectReferenceValue = goButton;
            so.FindProperty("actionLabel").objectReferenceValue = goText;
            so.FindProperty("actionFill").objectReferenceValue = go;
            so.ApplyModifiedPropertiesWithoutUndo();
            dp.enabled = false;
            root.SetActive(false);
            return dp;
        }

        static void BuildGarageTabs(Transform content, Transform panel, Showroom showroom, GameObject carRoot, GameObject driverRoot, CarSelectPanel csp, DriverPanel dp)
        {
            var tabs = Rect(content, "Garage Tabs");
            Place(tabs, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(66, -138), new Vector2(640, 34));
            var buttons = new Button[2]; var backs = new Image[2]; var labels = new TMP_Text[2];
            string[] names = { "CAR", "DRIVER" };
            for (int i = 0; i < 2; i++)
            {
                var bg = Img(tabs, names[i], null, i == 0 ? Pink : Slab);
                Place(bg.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(i * 136, 0), new Vector2(128, 0));
                var l = Txt(bg.transform, "Label", names[i], bsBlack, 26, i == 0 ? Night : Mist, TextAlignmentOptions.Center);
                Stretch(l.rectTransform);
                var b = bg.gameObject.AddComponent<Button>(); Plain(b);
                buttons[i] = b; backs[i] = bg; labels[i] = l;
            }
            var key = Txt(tabs, "Key", "<color=#FF2E9A>C</color>  SWITCH", monoBold, 14, Dim, TextAlignmentOptions.Left);
            Place(key.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(284, 0), new Vector2(130, 0));

            // Time of day for the set, next to the tabs.
            var lightBg = Img(tabs, "Light", null, Slab);
            Place(lightBg.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(424, 0), new Vector2(200, 0));
            var lightText = Txt(lightBg.transform, "Label", "<color=#FF2E9A>L</color>  LIGHT  NIGHT", monoBold, 14, Mist, TextAlignmentOptions.Center);
            Stretch(lightText.rectTransform);
            var lightButton = lightBg.gameObject.AddComponent<Button>(); Plain(lightButton);

            var gt = panel.gameObject.AddComponent<GarageTabs>();
            var so = new SerializedObject(gt);
            so.FindProperty("visibleGroup").objectReferenceValue = panel.GetComponent<CanvasGroup>();
            so.FindProperty("showroom").objectReferenceValue = showroom;
            so.FindProperty("carRoot").objectReferenceValue = carRoot;
            so.FindProperty("driverRoot").objectReferenceValue = driverRoot;
            so.FindProperty("carPanel").objectReferenceValue = csp;
            so.FindProperty("driverPanel").objectReferenceValue = dp;
            SetArray(so, "tabButtons", buttons); SetArray(so, "tabBacks", backs); SetArray(so, "tabLabels", labels);
            so.FindProperty("lightButton").objectReferenceValue = lightButton;
            so.FindProperty("lightLabel").objectReferenceValue = lightText;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Plain(Button b)
        {
            b.transition = Selectable.Transition.None;
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
        }
    }
}
