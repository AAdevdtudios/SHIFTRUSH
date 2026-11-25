using UnityEngine;

namespace ShiftRush.Core
{
    /// <summary>
    /// The driver's look, saved in PlayerPrefs next to the garage: which body, what's worn in each slot (per body)
    /// and the pieces bought. Pieces cost Hype. The look's GLOSS / GRIT lean is what fans will react to.
    /// </summary>
    public static class DriverProfile
    {
        const string BodyKey = "GripPro.Driver.Body";
        const string WearKey = "GripPro.Driver.Wear.";   // + body + "." + slot: the worn item id ("" = nothing)
        const string OwnedKey = "GripPro.Driver.Owned";

        /// <summary>Fired when the body or anything worn changes.</summary>
        public static event System.Action Changed;

        public static DriverBody Body
        {
            get { return (DriverBody)Mathf.Clamp(PlayerPrefs.GetInt(BodyKey, 0), 0, 1); }
            set { PlayerPrefs.SetInt(BodyKey, (int)value); PlayerPrefs.Save(); Changed?.Invoke(); }
        }

        public static string Worn(DriverBody b, DriverSlot s)
        {
            var cat = DriverCatalog.Instance;
            string def = cat != null ? cat.DefaultFor(b, s) : "";
            string id = PlayerPrefs.GetString(WearKey + b + "." + s, def);
            // A saved piece that's gone from the catalog falls back to the default.
            return cat != null && !string.IsNullOrEmpty(id) && cat.Find(id) == null ? def : id;
        }

        public static void Wear(DriverBody b, DriverSlot s, DriverCatalog.Item item)
        {
            if (item == null && !DriverCatalog.Optional(s)) return;
            PlayerPrefs.SetString(WearKey + b + "." + s, item != null ? item.id : "");
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static bool Owns(DriverCatalog.Item item)
        {
            if (item == null) return true;
            if (item.price <= 0) return true;
            foreach (var s in PlayerPrefs.GetString(OwnedKey, "").Split(',')) if (s == item.id) return true;
            return false;
        }

        public static bool Buy(DriverCatalog.Item item)
        {
            if (Owns(item)) return true;
            if (!PlayerProfile.Spend(item.price)) return false;
            string cur = PlayerPrefs.GetString(OwnedKey, "");
            PlayerPrefs.SetString(OwnedKey, string.IsNullOrEmpty(cur) ? item.id : cur + "," + item.id);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>How the worn look leans: count of GLOSS and GRIT pieces (an override previews one slot).</summary>
        public static void Lean(DriverBody b, out int gloss, out int grit, DriverSlot? overrideSlot = null, DriverCatalog.Item overrideItem = null)
        {
            gloss = grit = 0;
            var cat = DriverCatalog.Instance;
            if (cat == null) return;
            foreach (DriverSlot s in System.Enum.GetValues(typeof(DriverSlot)))
            {
                var item = overrideSlot.HasValue && overrideSlot.Value == s ? overrideItem : cat.Find(Worn(b, s));
                if (item == null) continue;
                if (item.tag == DriverStyle.Gloss) gloss++;
                else if (item.tag == DriverStyle.Grit) grit++;
            }
        }
    }
}
