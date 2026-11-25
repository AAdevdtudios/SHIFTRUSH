using System.Collections.Generic;
using UnityEngine;

namespace ShiftRush.Core
{
    public enum DriverBody { BodyA, BodyB }
    public enum DriverSlot { Hair, Brows, Beard, Earrings, Glasses, Mask, Hat, Headphones, Torso, Pants, Shoes, Watch, Helmet, Gloves }
    /// <summary>The Driver tab's three groups.</summary>
    public enum DriverGroup { Top, Bottom, Accessories }
    /// <summary>Which fan faction an item speaks to. Matching it to how you race is what fans reward.</summary>
    public enum DriverStyle { Neutral, Gloss, Grit }

    /// <summary>
    /// Everything the driver can wear (Resources/SR_Driver, filled by SHIFT RUSH → Driver → Build Driver Catalog
    /// from the modular character pack). Each item is one part inside the body prefab, switched on by name, or a loose pack piece rigged to the body
    /// (SHIFT RUSH → Driver → Wardrobe Wizard). Items
    /// that share a style are the same piece in different colourways. Outfits never touch the car's handling:
    /// they only show on the stream and move fans.
    /// </summary>
    [CreateAssetMenu(menuName = "SHIFT RUSH/Driver Catalog", fileName = "SR_Driver")]
    public class DriverCatalog : ScriptableObject
    {
        [System.Serializable]
        public class Item
        {
            public string id;
            public string displayName;
            public DriverBody body;
            public DriverSlot slot;
            [Tooltip("Items with the same style are one piece in different colours.")]
            public string style;
            public int colour;
            [Tooltip("The part's object name inside the body prefab, without _LEFT / _RIGHT.")]
            public string part;
            public Sprite icon;
            [Tooltip("For pieces that aren't inside the body prefab (helmets): what gets fitted to the head.")]
            public GameObject prefab;
            [Tooltip("Loose pack pieces (added by the Wardrobe Wizard): the piece's mesh, rigged to the body's bones.")]
            public Mesh mesh;
            [Tooltip("Bit per submesh of the rigged mesh that uses the see-through material (lenses, visors).")]
            public int clearMask;
            [Tooltip("Colour for prefab pieces (the helmet shell).")]
            public Color tint = Color.white;
            public Color swatch = Color.white;
            public DriverStyle tag;
            public int price;
        }

        [Tooltip("The two modular bodies, in DriverBody order.")]
        public GameObject[] bodies = new GameObject[2];
        [Tooltip("URP materials for the pack's colour atlas (solid, then see-through for lenses).")]
        public Material solid, seeThrough;
        [Tooltip("Showroom idles for each body, in DriverBody order. The first clip is the resting idle the driver comes back to between the others.")]
        public AnimationClip[] maleIdles = new AnimationClip[0], femaleIdles = new AnimationClip[0];
        public List<Item> items = new List<Item>();

        /// <summary>Slots that can be left empty.</summary>
        public static bool Optional(DriverSlot s)
        {
            return s == DriverSlot.Helmet || s == DriverSlot.Beard || s == DriverSlot.Earrings || s == DriverSlot.Glasses || s == DriverSlot.Mask
                || s == DriverSlot.Hat || s == DriverSlot.Headphones || s == DriverSlot.Watch || s == DriverSlot.Gloves;
        }

        /// <summary>Top = what the stream sees first (helmet, hair, top), Bottom = pants and shoes, the rest Accessories.</summary>
        public static DriverGroup GroupOf(DriverSlot s)
        {
            if (s == DriverSlot.Helmet || s == DriverSlot.Hair || s == DriverSlot.Torso) return DriverGroup.Top;
            if (s == DriverSlot.Pants || s == DriverSlot.Shoes) return DriverGroup.Bottom;
            return DriverGroup.Accessories;
        }

        /// <summary>Slots in the order the Driver tab lists them.</summary>
        public static readonly DriverSlot[] DisplayOrder =
        {
            DriverSlot.Helmet, DriverSlot.Hair, DriverSlot.Torso,
            DriverSlot.Pants, DriverSlot.Shoes,
            DriverSlot.Glasses, DriverSlot.Mask, DriverSlot.Hat, DriverSlot.Headphones, DriverSlot.Watch, DriverSlot.Gloves, DriverSlot.Beard, DriverSlot.Earrings, DriverSlot.Brows,
        };

        public bool Has(DriverBody b, DriverSlot s) { foreach (var i in items) if (i.body == b && i.slot == s) return true; return false; }

        /// <summary>The styles for a slot, in catalog order (each a list of colourways).</summary>
        public List<List<Item>> Styles(DriverBody b, DriverSlot s)
        {
            var list = new List<List<Item>>();
            var index = new Dictionary<string, List<Item>>();
            foreach (var i in items)
            {
                if (i.body != b || i.slot != s) continue;
                if (!index.TryGetValue(i.style, out var group)) { group = new List<Item>(); index[i.style] = group; list.Add(group); }
                group.Add(i);
            }
            return list;
        }

        public Item Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var i in items) if (i.id == id) return i;
            return null;
        }

        /// <summary>What a new driver wears: the first piece in every slot that can't be empty, nothing in the rest.</summary>
        public string DefaultFor(DriverBody b, DriverSlot s)
        {
            if (Optional(s)) return "";
            foreach (var i in items) if (i.body == b && i.slot == s) return i.id;
            return "";
        }

        public GameObject Body(DriverBody b) { int k = (int)b; return bodies != null && k < bodies.Length ? bodies[k] : null; }

        static DriverCatalog instance;
        public static DriverCatalog Instance { get { if (instance == null) instance = Resources.Load<DriverCatalog>("SR_Driver"); return instance; } }
    }
}
