using System.Collections.Generic;
using UnityEngine;

namespace ShiftRush.Core
{
    /// <summary>
    /// The player's garage and wallet, saved in PlayerPrefs: Hype, the cars they own, the parts they've bought for
    /// each car and the parts fitted to it. A new profile starts with the first catalog car and a little Hype
    /// fronted by the crew, so there's something to spend on day one.
    /// </summary>
    public static class PlayerProfile
    {
        public const string HypeKey = "GripPro.Hype";
        const string StartedKey = "GripPro.Started";
        const string CarsKey = "GripPro.Cars";
        const string PartsKey = "GripPro.Parts.";   // + car id: comma list of part ids bought for that car
        const string FitKey = "GripPro.Fit.";       // + car id + "." + slot: the fitted part id
        public const int StarterHype = 1500;

        /// <summary>Fired whenever Hype goes up or down, so every Hype label can refresh.</summary>
        public static event System.Action Changed;

        static void EnsureStarted()
        {
            if (PlayerPrefs.GetInt(StartedKey, 0) == 1) return;
            PlayerPrefs.SetInt(StartedKey, 1);
            PlayerPrefs.SetInt(HypeKey, PlayerPrefs.GetInt(HypeKey, 0) + StarterHype);
            var cat = RaceCatalog.Instance;
            if (cat != null && cat.cars.Count > 0) AddToList(CarsKey, cat.cars[0].id);
            PlayerPrefs.Save();
        }

        public static int Hype { get { EnsureStarted(); return PlayerPrefs.GetInt(HypeKey, 0); } }

        public static void AddHype(int amount)
        {
            EnsureStarted();
            PlayerPrefs.SetInt(HypeKey, Mathf.Max(0, PlayerPrefs.GetInt(HypeKey, 0) + amount));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>Takes the Hype if there's enough. False (and nothing spent) if not.</summary>
        public static bool Spend(int amount)
        {
            if (amount <= 0) return true;
            if (Hype < amount) return false;
            AddHype(-amount);
            return true;
        }

        // ------------------------------------------------------------------ cars

        public static bool OwnsCar(RaceCatalog.Car car)
        {
            if (car == null) return false;
            EnsureStarted();
            return car.price <= 0 || InList(CarsKey, car.id);
        }

        public static bool BuyCar(RaceCatalog.Car car)
        {
            if (car == null || OwnsCar(car)) return true;
            if (!Spend(car.price)) return false;
            AddToList(CarsKey, car.id);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>The index of the first owned car, for when the last pick isn't owned.</summary>
        public static int FirstOwnedIndex()
        {
            var cat = RaceCatalog.Instance;
            if (cat == null) return 0;
            for (int i = 0; i < cat.cars.Count; i++) if (OwnsCar(cat.cars[i])) return i;
            return 0;
        }

        // ------------------------------------------------------------------ parts

        public static bool OwnsPart(RaceCatalog.Car car, PartCatalog.Part part)
        {
            if (car == null || part == null) return false;
            return part.price <= 0 || InList(PartsKey + car.id, part.id);
        }

        public static bool BuyPart(RaceCatalog.Car car, PartCatalog.Part part)
        {
            if (OwnsPart(car, part)) return true;
            if (!Spend(part.price)) return false;
            AddToList(PartsKey + car.id, part.id);
            PlayerPrefs.Save();
            return true;
        }

        public static string Fitted(RaceCatalog.Car car, PartCatalog.Slot slot)
        {
            if (car == null) return PartCatalog.StockId;
            return PlayerPrefs.GetString(FitKey + car.id + "." + slot, PartCatalog.StockId);
        }

        public static void Fit(RaceCatalog.Car car, PartCatalog.Part part)
        {
            if (car == null || part == null) return;
            PlayerPrefs.SetString(FitKey + car.id + "." + part.slot, part.id);
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------ helpers

        static bool InList(string key, string id)
        {
            foreach (var s in PlayerPrefs.GetString(key, "").Split(',')) if (s == id) return true;
            return false;
        }

        static void AddToList(string key, string id)
        {
            if (InList(key, id)) return;
            string cur = PlayerPrefs.GetString(key, "");
            PlayerPrefs.SetString(key, string.IsNullOrEmpty(cur) ? id : cur + "," + id);
        }
    }
}
