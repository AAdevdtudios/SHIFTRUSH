using System.Collections.Generic;
using ShiftRush.Core;
using ShiftRush.Vehicles;
using UnityEditor;
using UnityEngine;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// SHIFT RUSH → Cars → Parts Fitting. Pick a car and a tyre and spoiler to try on it. The car appears in the
    /// Scene view with the parts fitted, and every slider updates it live. Settings save to that car in the race
    /// catalog straight away (Undo works), and the garage and races use them.
    /// Per car: whether it takes a spoiler or tyre swaps at all, which parts it allows, the tyre size (height X/Y,
    /// width Z) and position, and the spoiler position, size and tilt.
    /// </summary>
    public class PartsFittingWindow : EditorWindow
    {
        const string PreviewName = "PARTS FITTING PREVIEW";
        static readonly Vector3 PreviewSpot = new Vector3(0f, 0f, 800f);

        RaceCatalog race;
        PartCatalog parts;
        int carIndex, tyreIndex, wingIndex;
        bool linkHeight = true;
        Vector2 scroll;
        GameObject preview;
        int previewFor = -1;

        [MenuItem("SHIFT RUSH/Cars/Parts Fitting")]
        static void Open() { GetWindow<PartsFittingWindow>("Parts Fitting").Show(); }

        void OnEnable()
        {
            race = AssetDatabase.LoadAssetAtPath<RaceCatalog>("Assets/_ShiftRush/Resources/RaceCatalog.asset");
            parts = AssetDatabase.LoadAssetAtPath<PartCatalog>("Assets/_ShiftRush/Resources/SR_Parts.asset");
            Undo.undoRedoPerformed += Refit;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= Refit;
            ClearPreview();
        }

        List<PartCatalog.Part> All(PartCatalog.Slot slot)
        {
            var list = new List<PartCatalog.Part>();
            if (parts != null) foreach (var p in parts.parts) if (p.slot == slot) list.Add(p);
            return list;
        }

        void OnGUI()
        {
            if (race == null || parts == null) { EditorGUILayout.HelpBox("Needs Resources/RaceCatalog and Resources/SR_Parts (SHIFT RUSH → Cars → Build Parts Catalog).", MessageType.Warning); return; }
            if (EditorApplication.isPlaying) { EditorGUILayout.HelpBox("Stop Play mode to fit parts.", MessageType.Info); return; }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            var names = new string[race.cars.Count];
            for (int i = 0; i < names.Length; i++) names[i] = race.cars[i].displayName + "  (" + CarTypes.Name(race.cars[i].type) + ")";
            int newCar = EditorGUILayout.Popup("Car", carIndex, names);
            if (newCar != carIndex) { carIndex = newCar; ClearPreview(); }
            var car = race.cars[Mathf.Clamp(carIndex, 0, race.cars.Count - 1)];
            if (car.fit == null) car.fit = new PartFit();
            var fit = car.fit;

            var tyres = All(PartCatalog.Slot.Tyres);
            var wings = All(PartCatalog.Slot.Spoiler);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Try on", EditorStyles.boldLabel);
            tyreIndex = EditorGUILayout.Popup("Tyres", Mathf.Clamp(tyreIndex, 0, tyres.Count - 1), tyres.ConvertAll(p => p.displayName).ToArray());
            wingIndex = EditorGUILayout.Popup("Spoiler", Mathf.Clamp(wingIndex, 0, wings.Count - 1), wings.ConvertAll(p => p.displayName).ToArray());

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("What this car takes", EditorStyles.boldLabel);
            bool takesTyres = EditorGUILayout.Toggle("Tyre swaps", fit.takesTyres);
            bool takesSpoiler = EditorGUILayout.Toggle("Spoiler", fit.takesSpoiler);
            var allowedTyres = AllowedList("Allowed tyres", tyres, fit.allowedTyres, takesTyres);
            var allowedWings = AllowedList("Allowed spoilers", wings, fit.allowedSpoilers, takesSpoiler);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tyre fit (all four wheels)", EditorStyles.boldLabel);
            linkHeight = EditorGUILayout.Toggle("Keep round (X = Y)", linkHeight);
            Vector3 ts = fit.tyreScale;
            float tx = EditorGUILayout.Slider("Height X", ts.x, 0.3f, 3f);
            float ty = linkHeight ? tx : EditorGUILayout.Slider("Height Y", ts.y, 0.3f, 3f);
            if (linkHeight) EditorGUILayout.LabelField("Height Y", ty.ToString("0.00"));
            float tz = EditorGUILayout.Slider("Width Z", ts.z, 0.3f, 3f);
            Vector3 to = fit.tyreOffset;
            float ox = EditorGUILayout.Slider("Forward", to.x, -0.5f, 0.5f);
            float oy = EditorGUILayout.Slider("Up", to.y, -0.5f, 0.5f);
            float oz = EditorGUILayout.Slider("Outwards", to.z, -0.4f, 0.4f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Spoiler fit", EditorStyles.boldLabel);
            Vector3 so = fit.spoilerOffset;
            float sx = EditorGUILayout.Slider("Forward", so.x, -1.5f, 1.5f);
            float sy = EditorGUILayout.Slider("Up", so.y, -0.6f, 1.2f);
            float sz = EditorGUILayout.Slider("Sideways", so.z, -0.5f, 0.5f);
            float sScale = EditorGUILayout.Slider("Size", fit.spoilerScale, 0.3f, 2f);
            float sTilt = EditorGUILayout.Slider("Tilt", fit.spoilerTilt, -30f, 30f);

            bool changed = EditorGUI.EndChangeCheck();
            if (changed)
            {
                Undo.RecordObject(race, "Parts Fitting");
                fit.takesTyres = takesTyres; fit.takesSpoiler = takesSpoiler;
                fit.allowedTyres = allowedTyres; fit.allowedSpoilers = allowedWings;
                fit.tyreScale = new Vector3(tx, ty, tz);
                fit.tyreOffset = new Vector3(ox, oy, oz);
                fit.spoilerOffset = new Vector3(sx, sy, sz);
                fit.spoilerScale = sScale; fit.spoilerTilt = sTilt;
                EditorUtility.SetDirty(race);
            }

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Frame car")) Frame();
            if (GUILayout.Button("Reset this car's fit"))
            {
                Undo.RecordObject(race, "Reset fit");
                car.fit = new PartFit();
                EditorUtility.SetDirty(race);
                changed = true;
            }
            if (GUILayout.Button("Save")) AssetDatabase.SaveAssets();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("Changes save to this car straight away. Tyres: X and Y set the height, Z the width. Untick Spoiler for cars that shouldn't wear one.", MessageType.None);
            EditorGUILayout.EndScrollView();

            EnsurePreview(car);
            if (changed || GUI.changed) Refit();
        }

        string[] AllowedList(string label, List<PartCatalog.Part> list, string[] current, bool enabled)
        {
            var result = new List<string>();
            EditorGUILayout.LabelField(label + "  (none ticked = all)");
            using (new EditorGUI.DisabledScope(!enabled))
            {
                EditorGUI.indentLevel++;
                foreach (var p in list)
                {
                    if (p.id == PartCatalog.StockId) continue;
                    bool on = current != null && System.Array.IndexOf(current, p.id) >= 0;
                    if (EditorGUILayout.ToggleLeft(p.displayName, on)) result.Add(p.id);
                }
                EditorGUI.indentLevel--;
            }
            return result.ToArray();
        }

        void EnsurePreview(RaceCatalog.Car car)
        {
            if (preview != null && previewFor == carIndex) return;
            ClearPreview();
            var source = car.showroomPrefab != null ? car.showroomPrefab : car.prefab;
            if (source == null) return;
            preview = (GameObject)PrefabUtility.InstantiatePrefab(source);
            preview.name = PreviewName;
            preview.hideFlags = HideFlags.DontSave;
            preview.transform.SetPositionAndRotation(PreviewSpot, Quaternion.identity);
            previewFor = carIndex;
            Refit();
            Frame();
        }

        void Refit()
        {
            if (preview == null || race == null || parts == null) return;
            var car = race.cars[Mathf.Clamp(carIndex, 0, race.cars.Count - 1)];
            var tyres = All(PartCatalog.Slot.Tyres);
            var wings = All(PartCatalog.Slot.Spoiler);
            var tyre = tyres.Count > 0 ? tyres[Mathf.Clamp(tyreIndex, 0, tyres.Count - 1)] : null;
            var wing = wings.Count > 0 ? wings[Mathf.Clamp(wingIndex, 0, wings.Count - 1)] : null;
            CarLoadout.Apply(preview, car, tyre, wing, false);
            SceneView.RepaintAll();
        }

        void Frame()
        {
            if (preview == null) return;
            var rs = preview.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            var sv = SceneView.lastActiveSceneView;
            if (sv != null) sv.Frame(b, false);
        }

        void ClearPreview()
        {
            if (preview != null) DestroyImmediate(preview);
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go != null && go.name == PreviewName && go.scene.IsValid()) DestroyImmediate(go);
            preview = null;
            previewFor = -1;
        }
    }
}
