using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// SHIFT RUSH → Cars → Enable Car Damage: gives every car (the player's and the rivals') RCC Pro's damage, the way
    /// RCC's own editor adds it: the body dents where it's hit, lights smash, loose parts (bumpers, spoilers) break,
    /// and wheels take damage but never come off (a lost wheel would end a race for good). Turns on Read/Write for
    /// the car models so the body meshes can bend. Safe to run again.
    /// </summary>
    public static class CarDamageSetup
    {
        [MenuItem("SHIFT RUSH/Cars/Enable Car Damage")]
        public static string Run()
        {
            var paths = new List<string>();
            foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_ShiftRush/Prefabs/Vehicles" })) paths.Add(AssetDatabase.GUIDToAssetPath(g));
            var field = Race.RivalField.Load();
            if (field != null && field.carPrefab != null) Add(paths, AssetDatabase.GetAssetPath(field.carPrefab));
            var cat = Core.RaceCatalog.Instance;
            if (cat != null) foreach (var c in cat.cars) if (c.prefab != null) Add(paths, AssetDatabase.GetAssetPath(c.prefab));

            int cars = 0, models = 0;
            var readable = new HashSet<string>();
            foreach (var path in paths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var car = root.GetComponentInChildren<RCCP_CarController>(true);
                    if (car == null) continue;
                    var dmg = root.GetComponentInChildren<RCCP_Damage>(true);
                    if (dmg == null)
                    {
                        var go = new GameObject("RCCP_Damage");
                        go.transform.SetParent(RCCP_ModulesUtility.GetOrCreateContainer(car).transform, false);
                        dmg = go.AddComponent<RCCP_Damage>();
                    }
                    dmg.automaticInstallation = true;
                    dmg.meshDeformation = true;
                    dmg.maximumDamage = 0.6f;       // dents, not crumpled boxes
                    dmg.deformationRadius = 0.8f;
                    dmg.lightDamage = true;
                    dmg.partDamage = true;
                    dmg.wheelDamage = true;
                    dmg.wheelDetachment = false;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    cars++;
                    foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                        if (mf.sharedMesh != null) readable.Add(AssetDatabase.GetAssetPath(mf.sharedMesh));
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            foreach (var m in readable)
            {
                var imp = AssetImporter.GetAtPath(m) as ModelImporter;
                if (imp == null || imp.isReadable) continue;
                imp.isReadable = true;
                imp.SaveAndReimport();
                models++;
            }
            AssetDatabase.SaveAssets();
            string report = "Damage on " + cars + " cars, " + models + " models made readable";
            Debug.Log("[CARS] " + report);
            return report;
        }

        static void Add(List<string> list, string p) { if (!string.IsNullOrEmpty(p) && !list.Contains(p)) list.Add(p); }
    }
}
