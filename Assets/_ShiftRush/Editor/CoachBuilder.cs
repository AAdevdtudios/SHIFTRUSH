using ShiftRush.Race;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftRush.EditorTools
{
    /// <summary>Builds Resources/SR_Coach.prefab: the tutorial's coach card along the bottom of the race screen.</summary>
    public static partial class MenuBuilder
    {
        const string CoachPath = "Assets/_ShiftRush/Resources/SR_Coach.prefab";

        [MenuItem("SHIFT RUSH/Build Tutorial Coach")]
        static void BuildCoachOnly()
        {
            LoadFonts();
            MakeArt();
            MakeCoach();
            AssetDatabase.SaveAssets();
            Debug.Log("[SHIFT//RUSH] Tutorial coach built: " + CoachPath);
        }

        static void MakeCoach()
        {
            var root = new GameObject("SR_Coach", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 360; // over the SHIFT HUD's slow-motion tint (350), under pause (400)
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var coach = root.AddComponent<RaceCoach>();
            var green = new Color32(61, 255, 138, 255);

            var layer = Layer(root.transform, "Coach");
            var group = layer.gameObject.AddComponent<CanvasGroup>();

            // The card: bottom centre, between the minimap and the speedometer.
            var card = new GameObject("Card", typeof(RectTransform)).GetComponent<RectTransform>();
            card.SetParent(layer, false);
            Place(card, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(1040, 330));

            var bg = Img(card, "Bg", slant, new Color(Panel.r, Panel.g, Panel.b, 0.88f));
            bg.type = Image.Type.Sliced;
            Place(bg.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(1000, 132));
            var frame = Img(card, "Frame", slantFrame, Magenta);
            frame.type = Image.Type.Sliced;
            Place(frame.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(1000, 132));

            // Key cap on the left: the key to press (or the metres to the ramp).
            var cap = Img(card, "Key Cap", slant, new Color(1, 1, 1, 0.1f));
            cap.type = Image.Type.Sliced;
            Place(cap.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(-385, 136), new Vector2(180, 84));
            var key = Txt(cap.rectTransform, "Key", "W", fontTitle, 40, Color.white, TextAlignmentOptions.Center);
            key.enableAutoSizing = true; key.fontSizeMin = 18; key.fontSizeMax = 40;
            key.characterSpacing = 4;
            Place(key.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-36, -12));

            var tag = Txt(card, "Tag", "TUTORIAL  //  01 / 05", fontBold, 17, Magenta, TextAlignmentOptions.Left);
            tag.characterSpacing = 10;
            Place(tag.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 0.5f), new Vector2(-260, 182), new Vector2(560, 24));
            var head = Txt(card, "Headline", "HOLD W TO DRIVE", fontTitle, 40, Color.white, TextAlignmentOptions.Left);
            head.enableAutoSizing = true; head.fontSizeMin = 26; head.fontSizeMax = 40;
            Place(head.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 0.5f), new Vector2(-260, 144), new Vector2(700, 50));
            var body = Txt(card, "Detail", "", fontBold, 20, Steel, TextAlignmentOptions.Left);
            body.characterSpacing = 2;
            body.enableAutoSizing = true; body.fontSizeMin = 15; body.fontSizeMax = 20;
            Place(body.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 0.5f), new Vector2(-260, 104), new Vector2(700, 30));

            // One pip per lesson, under the card.
            var pips = new Image[5];
            for (int k = 0; k < pips.Length; k++)
            {
                pips[k] = Img(card, "Lesson " + (k + 1), slant, new Color(1, 1, 1, 0.18f));
                pips[k].type = Image.Type.Sliced; pips[k].pixelsPerUnitMultiplier = 4f;
                Place(pips[k].rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2((k - 2) * 70, 46), new Vector2(60, 10));
            }

            var so = new SerializedObject(coach);
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("card").objectReferenceValue = card;
            so.FindProperty("frame").objectReferenceValue = frame;
            so.FindProperty("keyCap").objectReferenceValue = cap;
            so.FindProperty("keyText").objectReferenceValue = key;
            so.FindProperty("tagText").objectReferenceValue = tag;
            so.FindProperty("headline").objectReferenceValue = head;
            so.FindProperty("detail").objectReferenceValue = body;
            var pp = so.FindProperty("pips");
            pp.arraySize = pips.Length;
            for (int k = 0; k < pips.Length; k++) pp.GetArrayElementAtIndex(k).objectReferenceValue = pips[k];
            so.FindProperty("accent").colorValue = Magenta;
            so.FindProperty("green").colorValue = (Color)green;
            so.ApplyModifiedPropertiesWithoutUndo();

            foreach (var g in root.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            PrefabUtility.SaveAsPrefabAsset(root, CoachPath);
            Object.DestroyImmediate(root);
        }
    }
}
