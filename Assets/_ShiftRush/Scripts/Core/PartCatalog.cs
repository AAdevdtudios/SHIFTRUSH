using System.Collections.Generic;
using UnityEngine;

namespace ShiftRush.Core
{
    /// <summary>
    /// Every part the crew can fit (Resources/SR_Parts, filled by SHIFT RUSH → Cars → Build Parts Catalog).
    /// Each part has a look (a prefab placed on the car) and what it changes: its drift step pulls the car's drift
    /// character one step looser or tidier (never more than one step from its type), downforce presses the car
    /// into the road at speed and narrows the drift, and the handling bonus shows on the garage stats.
    /// </summary>
    [CreateAssetMenu(menuName = "SHIFT RUSH/Part Catalog", fileName = "SR_Parts")]
    public class PartCatalog : ScriptableObject
    {
        public enum Slot { Tyres, Spoiler }
        public const string StockId = "stock";

        [System.Serializable]
        public class Part
        {
            public string id = StockId;
            public string displayName = "STOCK";
            public Slot slot;
            [Tooltip("What goes on the car. Empty = the car's own part.")]
            public GameObject prefab;
            public int price;
            [Tooltip("-1 tidier (towards Grippy), +1 looser (towards Drifty).")]
            [Range(-1, 1)] public int driftStep;
            [Tooltip("0 = none, 2 = a big wing. Presses the car down at speed and narrows the drift.")]
            [Range(0f, 3f)] public float downforce;
            [Tooltip("Added to the handling stat shown in the garage.")]
            [Range(0, 3)] public int handlingBonus;
            [TextArea] public string line = "";
            [Tooltip("Car types this part can't go on (e.g. no tyre swaps on heavy trucks for now).")]
            public CarType[] notFor = new CarType[0];
        }

        public List<Part> parts = new List<Part>();

        public List<Part> For(Slot slot, RaceCatalog.Car car)
        {
            var list = new List<Part>();
            foreach (var p in parts)
            {
                if (p.slot != slot) continue;
                if (car != null && System.Array.IndexOf(p.notFor, car.type) >= 0) continue;
                if (car != null && car.fit != null && p.id != StockId)
                {
                    // Per-car rules from Parts Fitting: no spoiler / no tyres at all, or only the parts it allows.
                    if (slot == Slot.Spoiler && (!car.fit.takesSpoiler || !car.fit.Allows(car.fit.allowedSpoilers, p.id))) continue;
                    if (slot == Slot.Tyres && (!car.fit.takesTyres || !car.fit.Allows(car.fit.allowedTyres, p.id))) continue;
                }
                list.Add(p);
            }
            return list;
        }

        public Part Find(Slot slot, string id)
        {
            foreach (var p in parts) if (p.slot == slot && p.id == id) return p;
            return null;
        }

        static PartCatalog instance;
        public static PartCatalog Instance { get { if (instance == null) instance = Resources.Load<PartCatalog>("SR_Parts"); return instance; } }
    }
}
