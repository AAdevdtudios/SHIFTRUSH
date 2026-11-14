using System.Collections.Generic;
using ShiftRush.Core;
using ShiftRush.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ShiftRush.EditorTools
{
    /// <summary>Builds Resources/SR_PauseMenu.prefab, the in-race pause menu, in the same style as the main menu.</summary>
    public static partial class MenuBuilder
    {
        const string PausePath = "Assets/_ShiftRush/Resources/SR_PauseMenu.prefab";

        [MenuItem("SHIFT RUSH/Build Pause Menu Only")]
        static void BuildPauseOnly()
        {
            LoadFonts();
            MakeArt();
            MakePauseMenu(LoadSounds());
            AssetDatabase.SaveAssets();
            Debug.Log("[SHIFT//RUSH] Pause menu built: " + PausePath);
        }

        static void MakePauseMenu(Sounds sounds)
        {
            var root = new GameObject("SR_PauseMenu", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400; // under the loading screen (500)
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            var rootGroup = root.AddComponent<CanvasGroup>();
            var pause = root.AddComponent<PauseMenu>();

            // Sounds (keep playing while the listener is paused).
            var src = root.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.ignoreListenerPause = true;
            src.outputAudioMixerGroup = MixerGroup("UI");
            var ms = root.AddComponent<MenuSounds>();
            var mso = new SerializedObject(ms);
            mso.FindProperty("shift").objectReferenceValue = sounds.shift;
            mso.FindProperty("scrollSoft").objectReferenceValue = sounds.scrollSoft;
            mso.FindProperty("hover").objectReferenceValue = sounds.hover;
            mso.FindProperty("select").objectReferenceValue = sounds.select;
            mso.FindProperty("back").objectReferenceValue = sounds.back;
            mso.FindProperty("confirm").objectReferenceValue = sounds.confirm;
            mso.FindProperty("tick").objectReferenceValue = sounds.tick;
            mso.ApplyModifiedPropertiesWithoutUndo();

            // Dim the frozen race, darker on the left where the menu sits.
            var dim = Img(root.transform, "Dim", null, new Color(Ink.r, Ink.g, Ink.b, 0.55f));
            Stretch(dim.rectTransform);
            var fade = Img(root.transform, "Left Fade", fadeLeft, new Color(Ink.r, Ink.g, Ink.b, 0.9f));
            fade.raycastTarget = false;
            Place(fade.rectTransform, new Vector2(0, 0), new Vector2(0.65f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            var band = Img(root.transform, "Streaks", streaks, new Color(1, 1, 1, 0.07f));
            band.type = Image.Type.Tiled; band.raycastTarget = false;
            Place(band.rectTransform, new Vector2(0, 1), new Vector2(0.6f, 1), new Vector2(0, 1), new Vector2(0, -110), new Vector2(0, 170));

            var title = Txt(root.transform, "Title", "PAUSED", fontTitle, 130, Color.white, TextAlignmentOptions.Left);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(120, -110), new Vector2(900, 150));
            var stripe = Img(root.transform, "Title Stripe", slant, Magenta);
            stripe.type = Image.Type.Sliced;
            Place(stripe.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(126, -96), new Vector2(140, 14));
            var trackName = Txt(root.transform, "Subtitle", "SHIFT//RUSH", fontBold, 22, Steel, TextAlignmentOptions.Left);
            trackName.characterSpacing = 14;
            Place(trackName.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(128, -262), new Vector2(900, 40));

            // Main list.
            var main = Group(root.transform, "Main");
            var gears = BuildGearList(main.transform, main, new List<(string, string, string, UnityAction)>
            {
                ("1", "RESUME", "Back on the throttle", pause.Resume),
                ("2", "RESTART", "From the start line", pause.Restart),
                ("3", "OPTIONS", "Speed effects and sound", pause.OpenOptions),
                ("4", "MENU", "Back to the garage", pause.BackToMenu),
            });
            PauseHint(main.transform, "<color=#FF40D9>W S</color>  SHIFT     <color=#FF40D9>ENTER</color>  ENGAGE     <color=#FF40D9>ESC</color>  RESUME");

            // Options list.
            var opts = Group(root.transform, "Options");
            var head = Txt(opts.transform, "Header", "OPTIONS", fontTitle, 56, Color.white, TextAlignmentOptions.Left);
            Place(head.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(120, 150), new Vector2(600, 70));
            var column = new GameObject("Rows", typeof(RectTransform)).GetComponent<RectTransform>();
            column.SetParent(opts.transform, false);
            Place(column, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 1), new Vector2(120, 130), new Vector2(860, 520));
            var rows = new List<OptionRow>
            {
                OptionRowUI(column, 0, "Auto Accelerate", OptionRow.Kind.Toggle, GameSettings.AutoAccelerateKey, null, 0, false, ""),
                OptionRowUI(column, 1, "Tutorial", OptionRow.Kind.Toggle, GameSettings.TutorialKey, null, 0, true, ""),
                OptionRowUI(column, 2, "Speed Effects", OptionRow.Kind.Choice, GameSettings.SpeedFxKey, GameSettings.SpeedFxLabels, 2, true, ""),
                OptionRowUI(column, 3, "Camera Shake", OptionRow.Kind.Toggle, GameSettings.CameraShakeKey, null, 0, true, ""),
                OptionRowUI(column, 4, "Reduced Motion", OptionRow.Kind.Toggle, GameSettings.ReducedMotionKey, null, 0, false, ""),
                OptionRowUI(column, 5, "Minimap", OptionRow.Kind.Choice, GameSettings.MinimapKey, GameSettings.MinimapLabels, 0, true, ""),
                OptionRowUI(column, 6, "Master Volume", OptionRow.Kind.Volume, "MasterVolume", null, 0, true, "Master"),
                OptionRowUI(column, 7, "Car & Effects", OptionRow.Kind.Volume, "SFXVolume", null, 0, true, "SFX"),
                OptionRowUI(column, 8, "Music Volume", OptionRow.Kind.Volume, "MusicVolume", null, 0, true, "Music"),
                OptionRowUI(column, 9, "Music", OptionRow.Kind.Choice, MusicLibrary.ChoiceKey, new[] { "SHUFFLE ALL" }, 0, true, ""),
                // Menu Scroll Sound lives in the main Settings only; mid-race the look matters more.
                OptionRowUI(column, 10, "Toon Look", OptionRow.Kind.Toggle, GameSettings.ToonLookKey, null, 0, true, ""),
            };
            // The Music row lists the songs in Resources/Audio/Music when it's shown, not when it's built.
            var musicRow = new SerializedObject(rows[9]);
            musicRow.FindProperty("musicChoice").boolValue = true;
            musicRow.ApplyModifiedPropertiesWithoutUndo();
            var list = opts.gameObject.AddComponent<OptionsList>();
            var lso = new SerializedObject(list);
            var lr = lso.FindProperty("rows");
            lr.arraySize = rows.Count;
            for (int i = 0; i < rows.Count; i++) lr.GetArrayElementAtIndex(i).objectReferenceValue = rows[i];
            lso.FindProperty("visibleGroup").objectReferenceValue = opts;
            lso.ApplyModifiedPropertiesWithoutUndo();
            PauseHint(opts.transform, "<color=#FF40D9>W S</color>  PICK     <color=#FF40D9>A D</color>  CHANGE     <color=#FF40D9>ESC</color>  BACK");

            var pso = new SerializedObject(pause);
            pso.FindProperty("root").objectReferenceValue = rootGroup;
            pso.FindProperty("mainGroup").objectReferenceValue = main;
            pso.FindProperty("optionsGroup").objectReferenceValue = opts;
            pso.FindProperty("gears").objectReferenceValue = gears;
            pso.ApplyModifiedPropertiesWithoutUndo();

            rootGroup.alpha = 0f;
            PrefabUtility.SaveAsPrefabAsset(root, PausePath);
            Object.DestroyImmediate(root);
        }

        static CanvasGroup Group(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            var g = go.GetComponent<CanvasGroup>();
            g.alpha = 0f;
            return g;
        }

        static void PauseHint(Transform parent, string text)
        {
            var t = Txt(parent, "Hint Bar", text, fontBold, 20, Steel, TextAlignmentOptions.Left);
            t.characterSpacing = 8;
            Place(t.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(120, 40), new Vector2(1200, 40));
        }

        /// <summary>A gear-style list like the title screen's, wired to the given actions.</summary>
        static GearMenu BuildGearList(Transform parent, CanvasGroup visible, List<(string key, string label, string caption, UnityAction call)> items)
        {
            var list = new GameObject("Gears", typeof(RectTransform)).GetComponent<RectTransform>();
            list.SetParent(parent, false);
            Place(list, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 1), new Vector2(120, 80), new Vector2(760, 420));
            var selector = Img(list, "Selector", slant, new Color(Magenta.r, Magenta.g, Magenta.b, 0.92f));
            selector.type = Image.Type.Sliced; selector.raycastTarget = false;
            Place(selector.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(-24, -48), new Vector2(560, 84));

            var menu = list.gameObject.AddComponent<GearMenu>();
            var so = new SerializedObject(menu);
            var arr = so.FindProperty("gears");
            arr.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++)
            {
                var g = items[i];
                var row = new GameObject("Gear " + g.key, typeof(RectTransform)).GetComponent<RectTransform>();
                row.SetParent(list, false);
                Place(row, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(0, -48 - i * 100), new Vector2(700, 84));
                var hit = Img(row, "Hit", null, new Color(0, 0, 0, 0));
                Stretch(hit.rectTransform);
                var keyBox = Img(row, "Key Box", slantFrame, new Color(1, 1, 1, 0.35f));
                keyBox.type = Image.Type.Sliced; keyBox.raycastTarget = false;
                Place(keyBox.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(84, 62));
                var key = Txt(row, "Key", g.key, fontTitle, 40, Color.white, TextAlignmentOptions.Center);
                Place(key.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(84, 62));
                var label = Txt(row, "Label", g.label, fontTitle, 72, Color.white, TextAlignmentOptions.Left);
                Place(label.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(112, 2), new Vector2(420, 84));
                var cap = Txt(row, "Caption", g.caption, fontMedium, 22, Color.white, TextAlignmentOptions.Left);
                cap.characterSpacing = 6; cap.alpha = 0f;
                Place(cap.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(560, -4), new Vector2(420, 40));
                row.gameObject.AddComponent<GearMenuRow>();

                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("key").stringValue = g.key;
                e.FindPropertyRelative("label").stringValue = g.label;
                e.FindPropertyRelative("caption").stringValue = g.caption;
                e.FindPropertyRelative("row").objectReferenceValue = row;
                e.FindPropertyRelative("keyText").objectReferenceValue = key;
                e.FindPropertyRelative("labelText").objectReferenceValue = label;
                e.FindPropertyRelative("captionText").objectReferenceValue = cap;
            }
            so.FindProperty("selector").objectReferenceValue = selector.rectTransform;
            so.FindProperty("visibleGroup").objectReferenceValue = visible;
            so.ApplyModifiedPropertiesWithoutUndo();
            for (int i = 0; i < items.Count; i++) UnityEventTools.AddVoidPersistentListener(gearMenu_Event(menu, i), items[i].call);
            return menu;
        }

        static OptionRow OptionRowUI(RectTransform column, int i, string title, OptionRow.Kind kind, string key, string[] choices, int defaultIndex, bool defaultOn, string channel)
        {
            var row = new GameObject(title, typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(column, false);
            Place(row, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, -i * 51), new Vector2(0, 46)); // 11 rows above the key hints
            var bg = Img(row, "Background", slant, new Color(1, 1, 1, 0.05f));
            bg.type = Image.Type.Sliced;
            Stretch(bg.rectTransform);
            var hl = Img(row, "Highlight", slant, new Color(Magenta.r, Magenta.g, Magenta.b, 0f));
            hl.type = Image.Type.Sliced; hl.raycastTarget = false;
            Stretch(hl.rectTransform);
            var label = Txt(row, "Label", title.ToUpperInvariant(), fontTitle, 34, Color.white, TextAlignmentOptions.Left);
            Place(label.rectTransform, new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(0, 0.5f), new Vector2(40, 0), Vector2.zero);
            var value = Txt(row, "Value", "", fontBold, 26, Color.white, TextAlignmentOptions.Right);
            value.characterSpacing = 6;
            Place(value.rectTransform, new Vector2(0.5f, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-40, 0), Vector2.zero);

            var segs = new Image[0];
            if (kind == OptionRow.Kind.Volume)
            {
                segs = new Image[10];
                for (int k = 0; k < 10; k++)
                {
                    segs[k] = Img(row, "Seg " + k, slant, new Color(1, 1, 1, 0.12f));
                    segs[k].type = Image.Type.Sliced; segs[k].pixelsPerUnitMultiplier = 4f; segs[k].raycastTarget = false;
                    Place(segs[k].rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-150 - (9 - k) * 30, 0), new Vector2(26, 22));
                }
            }

            var opt = row.gameObject.AddComponent<OptionRow>();
            var so = new SerializedObject(opt);
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("key").stringValue = key;
            so.FindProperty("defaultIndex").intValue = defaultIndex;
            so.FindProperty("defaultOn").boolValue = defaultOn;
            so.FindProperty("channel").stringValue = channel;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("value").objectReferenceValue = value;
            so.FindProperty("highlight").objectReferenceValue = hl;
            var ch = so.FindProperty("choices");
            ch.arraySize = choices != null ? choices.Length : 0;
            for (int k = 0; choices != null && k < choices.Length; k++) ch.GetArrayElementAtIndex(k).stringValue = choices[k];
            var sp = so.FindProperty("segments");
            sp.arraySize = segs.Length;
            for (int k = 0; k < segs.Length; k++) sp.GetArrayElementAtIndex(k).objectReferenceValue = segs[k];
            so.ApplyModifiedPropertiesWithoutUndo();
            return opt;
        }
    }
}
