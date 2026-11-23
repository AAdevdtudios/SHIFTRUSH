using System.Collections.Generic;
using ShiftRush.Core;
using UnityEditor;
using UnityEngine;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// Fills Resources/SR_Parts with the crew's tyres and spoilers and gives each car its garage price.
    /// SHIFT RUSH → Cars → Build Parts Catalog (also run by Build Main Menu). Prices and effects are first
    /// guesses: change them in the SR_Parts asset and the RaceCatalog, and re-running keeps car prices you set.
    /// </summary>
    public static class PartsBuilder
    {
        const string PartsPath = "Assets/_ShiftRush/Resources/SR_Parts.asset";
        const string TyreDir = "Assets/Simple Drift Tyres/Prefabs/";
        const string SpoilerDir = "Assets/_ShiftRush/Prefabs/Spoilers/";

        [MenuItem("SHIFT RUSH/Cars/Build Parts Catalog")]
        public static void Build()
        {
            var cat = AssetDatabase.LoadAssetAtPath<PartCatalog>(PartsPath);
            if (cat == null) { cat = ScriptableObject.CreateInstance<PartCatalog>(); AssetDatabase.CreateAsset(cat, PartsPath); }
            // Tyres are stretched to each car's own wheels (height and width), so trucks can take them too.
            CarType[] heavy = null;
            cat.parts = new List<PartCatalog.Part>
            {
                Part("stock", "STOCK", PartCatalog.Slot.Tyres, null, 0, 0, 0f, 0, "The wheels it came with."),
                Part("street", "STREET SPORT", PartCatalog.Slot.Tyres, Tyre(2), 600, 0, 0f, 0, "Fresh rims, same feel. For the camera.", heavy),
                Part("drift", "DRIFT SLICKS", PartCatalog.Slot.Tyres, Tyre(5), 900, 1, 0f, 0, "Hard compound that lets go early. One step looser.", heavy),
                Part("grip", "GRIP COMPOUND", PartCatalog.Slot.Tyres, Tyre(7), 900, -1, 0f, 1, "Soft and sticky. One step tidier, more handling.", heavy),
                Part("attack", "TIME ATTACK", PartCatalog.Slot.Tyres, Tyre(9), 1400, -1, 0f, 2, "Semi-slicks for lap times. Tidier, sharp turn-in.", heavy),

                Part("stock", "STOCK", PartCatalog.Slot.Spoiler, null, 0, 0, 0f, 0, "The wing it came with."),
                Part("ducktail", "DUCKTAIL", PartCatalog.Slot.Spoiler, Spoiler(2), 500, 0, 0.5f, 0, "A small lip. A touch of downforce, all the style."),
                Part("lip", "LIP WING", PartCatalog.Slot.Spoiler, Spoiler(1), 900, 0, 1f, 1, "Light downforce. Steadier over crests."),
                Part("gt", "GT WING", PartCatalog.Slot.Spoiler, Spoiler(3), 1600, 0, 1.6f, 1, "Real downforce. Grips in fast corners, slides a bit narrower."),
                Part("attack", "TIME ATTACK WING", PartCatalog.Slot.Spoiler, Spoiler(4), 2200, 0, 2.2f, 2, "Maximum downforce. Planted at speed, tight drifts."),
            };
            EditorUtility.SetDirty(cat);

            // Car prices: the first car is the rookie's and free. Prices you've already set are kept.
            var race = AssetDatabase.LoadAssetAtPath<RaceCatalog>("Assets/_ShiftRush/Resources/RaceCatalog.asset");
            if (race != null)
            {
                var prices = new Dictionary<string, int> { { "zephyr", 0 }, { "tetsuo", 3000 }, { "blackout", 4000 }, { "fireburst", 6000 }, { "predator", 7000 }, { "phobos", 9000 } };
                bool anySet = false;
                foreach (var c in race.cars) if (c.price > 0) anySet = true;
                if (!anySet)
                    for (int i = 0; i < race.cars.Count; i++)
                        race.cars[i].price = i == 0 ? 0 : (prices.TryGetValue(race.cars[i].id, out var p) ? p : 5000);
                EditorUtility.SetDirty(race);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Garage] Parts catalog built: " + cat.parts.Count + " parts.");
        }

        static GameObject Tyre(int n) { return AssetDatabase.LoadAssetAtPath<GameObject>(TyreDir + "Tyre_Type_" + n + ".prefab"); }
        static GameObject Spoiler(int n) { return AssetDatabase.LoadAssetAtPath<GameObject>(SpoilerDir + "spoiler_" + n + ".obj"); }

        static PartCatalog.Part Part(string id, string name, PartCatalog.Slot slot, GameObject prefab, int price, int step, float downforce, int handling, string line, CarType[] notFor = null)
        {
            return new PartCatalog.Part
            {
                id = id, displayName = name, slot = slot, prefab = prefab, price = price, driftStep = step,
                downforce = downforce, handlingBonus = handling, line = line, notFor = notFor ?? new CarType[0],
            };
        }
    }
}
