using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ShiftRush.Core
{
    /// <summary>
    /// Dresses a modular body: every clothing part inside it is switched off, then the worn ones on (both sides
    /// for paired parts like watches and earrings). Loose pack pieces (helmets, gloves and the rest the Wardrobe
    /// Wizard adds) are rigged meshes that ride the body's bones. Head and bare hands always stay; the pack's full naked body is
    /// hidden because the clothes carry their own skin. Also swaps the pack's
    /// built-in materials for the catalog's URP ones.
    /// </summary>
    public static class DriverLook
    {
        static readonly Regex PartName = new Regex(@"^([MF])_C3CC1_([A-Z]+)_(\d+)(?:_(\d+))?(?:_(LEFT|RIGHT))?$");

        /// <summary>Splits a part's object name into its slot code (TORSO), style number, colour and side-less key.</summary>
        public static bool Parse(string name, out string code, out string number, out int colour, out string key)
        {
            code = number = key = null; colour = 0;
            var m = PartName.Match(name);
            if (!m.Success) return false;
            code = m.Groups[2].Value;
            number = m.Groups[3].Value;
            colour = m.Groups[4].Success ? int.Parse(m.Groups[4].Value) : 0;
            key = m.Groups[5].Success ? name.Substring(0, name.Length - m.Groups[5].Value.Length - 1) : name;
            return true;
        }

        public static bool SlotOf(string code, out DriverSlot slot)
        {
            switch (code)
            {
                case "HAIR": slot = DriverSlot.Hair; return true;
                case "EYEBROWS": slot = DriverSlot.Brows; return true;
                case "BEARD": slot = DriverSlot.Beard; return true;
                case "EARRING": slot = DriverSlot.Earrings; return true;
                case "GLASSES": slot = DriverSlot.Glasses; return true;
                case "MASK": slot = DriverSlot.Mask; return true;
                case "HAT": slot = DriverSlot.Hat; return true;
                case "HEADPHONES": slot = DriverSlot.Headphones; return true;
                case "TORSO": slot = DriverSlot.Torso; return true;
                case "PANTS": slot = DriverSlot.Pants; return true;
                case "SHOES": slot = DriverSlot.Shoes; return true;
                case "WATCHES": slot = DriverSlot.Watch; return true;
            }
            slot = DriverSlot.Hair;
            return false;
        }

        /// <summary>
        /// Dresses the body in the saved look, with one slot optionally swapped for a preview. A comic actor can pass
        /// its own outfit as <paramref name="wornOf"/> (slot to item id) in place of the save.
        /// </summary>
        public static void Apply(GameObject root, DriverBody body, DriverSlot? previewSlot = null, DriverCatalog.Item previewItem = null, System.Func<DriverSlot, string> wornOf = null)
        {
            var cat = DriverCatalog.Instance;
            if (root == null || cat == null) return;
            var worn = new HashSet<string>();
            var loose = new List<DriverCatalog.Item>();
            DriverCatalog.Item helmet = null;
            foreach (DriverSlot s in System.Enum.GetValues(typeof(DriverSlot)))
            {
                var item = previewSlot.HasValue && previewSlot.Value == s ? previewItem : cat.Find(wornOf != null ? wornOf(s) : DriverProfile.Worn(body, s));
                if (item == null) continue;
                if (s == DriverSlot.Helmet) helmet = item;
                if (item.mesh != null) loose.Add(item);
                else if (s != DriverSlot.Helmet) worn.Add(item.part);
            }
            bool gloves = false;
            foreach (var l in loose) if (l.slot == DriverSlot.Gloves) gloves = true;
            // Under a helmet the hair, hats and glasses go, loose ones too.
            if (helmet != null) loose.RemoveAll(l => Covered(l.slot));

            var helmetHolder = HelmetHolder(root, false);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (helmetHolder != null && r.transform.IsChildOf(helmetHolder)) continue; // our helmet, not a pack part
                if (r.transform.parent != null && r.transform.parent.name == LooseHolderName) continue; // rigged loose pieces
                var go = r.gameObject;
                // The pack's whole naked body sits under the clothes and pokes through them. Every top, bottom and
                // pair of shoes carries its own skin (arms, legs, feet), and head and hands are separate, so it goes.
                if (go.name.EndsWith("_FULL_BODY")) { go.SetActive(false); SwapMaterials(r, cat); continue; }
                if (Parse(go.name, out string code, out _, out _, out string key))
                {
                    // Under a helmet: no hair, hats, cans, glasses or masks poking through.
                    if (SlotOf(code, out DriverSlot slot)) go.SetActive(worn.Contains(key) && !(helmet != null && Covered(slot)));
                    else if (code == "HAND") go.SetActive(!gloves && go.name.Contains("_HAND_01_")); // bare hands, unless gloves are on
                }
                SwapMaterials(r, cat);
            }
            FitHelmet(root, helmet);
            DressLoose(root, loose, cat);
        }

        const string LooseHolderName = "Driver Loose Pieces";

        /// <summary>
        /// Loose pack pieces: each item's mesh was rigged to the body's skeleton by the Wardrobe Wizard, so it gets
        /// a skinned renderer driven by the same bones as the body (it bends with the arms and turns with the head).
        /// Pieces already on stay, so previewing doesn't rebuild everything.
        /// </summary>
        static void DressLoose(GameObject root, List<DriverCatalog.Item> loose, DriverCatalog cat)
        {
            SkinnedMeshRenderer skin = null;
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (r.name.EndsWith("_FULL_BODY")) { skin = r; break; }
            if (skin == null) return;
            var parent = skin.transform.parent;
            var holder = parent.Find(LooseHolderName);
            if (holder == null)
            {
                if (loose.Count == 0) return;
                holder = new GameObject(LooseHolderName).transform;
                holder.SetParent(parent, false);
                // Same place as the body's skin, so the rigged meshes' bind poses line up.
                holder.localPosition = skin.transform.localPosition; holder.localRotation = skin.transform.localRotation; holder.localScale = skin.transform.localScale;
            }
            var want = new Dictionary<string, Mesh>();
            foreach (var l in loose) want[l.id] = l.mesh;
            var have = new HashSet<string>();
            for (int i = holder.childCount - 1; i >= 0; i--)
            {
                var c = holder.GetChild(i).gameObject;
                var on = c.GetComponent<SkinnedMeshRenderer>();
                if (want.TryGetValue(c.name, out var m) && on != null && on.sharedMesh == m) { have.Add(c.name); continue; }
                if (Application.isPlaying) Object.Destroy(c); else Object.DestroyImmediate(c);
            }
            foreach (var item in loose)
            {
                if (have.Contains(item.id)) continue;
                var g = new GameObject(item.id);
                g.layer = skin.gameObject.layer;
                g.transform.SetParent(holder, false);
                var r = g.AddComponent<SkinnedMeshRenderer>();
                r.sharedMesh = item.mesh;
                r.bones = skin.bones;
                r.rootBone = skin.rootBone;
                r.localBounds = skin.localBounds;
                r.updateWhenOffscreen = false;
                r.shadowCastingMode = skin.shadowCastingMode;
                var mats = new Material[item.mesh.subMeshCount];
                for (int m = 0; m < mats.Length; m++) mats[m] = (item.clearMask & (1 << m)) != 0 && cat.seeThrough != null ? cat.seeThrough : cat.solid;
                r.sharedMaterials = mats;
            }
        }

        public static bool Covered(DriverSlot s)
        {
            return s == DriverSlot.Hair || s == DriverSlot.Hat || s == DriverSlot.Headphones || s == DriverSlot.Glasses || s == DriverSlot.Mask || s == DriverSlot.Earrings;
        }

        /// <summary>The object under the head bone that holds the fitted helmet (made on demand).</summary>
        static Transform HelmetHolder(GameObject root, bool create)
        {
            Transform head = null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == "Head") { head = t; break; }
            if (head == null) return null;
            var holder = head.Find("Driver Helmet");
            if (holder == null && create)
            {
                holder = new GameObject("Driver Helmet").transform;
                holder.SetParent(head, false);
            }
            return holder;
        }

        /// <summary>Puts the helmet on the head, sized and centred on the head mesh, tinted to the piece's colour.</summary>
        static void FitHelmet(GameObject root, DriverCatalog.Item item)
        {
            var holder = HelmetHolder(root, item != null && item.prefab != null);
            if (holder == null) return;
            for (int i = holder.childCount - 1; i >= 0; i--)
            {
                var c = holder.GetChild(i).gameObject;
                if (Application.isPlaying) Object.Destroy(c); else Object.DestroyImmediate(c);
            }
            if (item == null || item.prefab == null) return;

            // Centre on the head mesh: its bounds are in the body's space, so this holds however the body is turned.
            Renderer headMesh = null;
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (r.name.Contains("_HEAD_")) { headMesh = r; break; }
            var h = Object.Instantiate(item.prefab, holder);
            h.name = item.prefab.name;
            if (headMesh != null)
            {
                var b = headMesh.bounds;
                h.transform.position = b.center + root.transform.up * (b.size.y * 0.12f);
                h.transform.rotation = root.transform.rotation;
                float size = Mathf.Max(b.size.x, b.size.z) / 0.36f;
                h.transform.localScale = Vector3.one * Mathf.Clamp(size, 0.7f, 1.4f) / Mathf.Max(0.0001f, holder.lossyScale.x);
            }
            var block = new MaterialPropertyBlock();
            foreach (var r in h.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.name.StartsWith("Shell")) continue;
                block.SetColor("_BaseColor", item.tint);
                r.SetPropertyBlock(block);
            }
        }

        static void SwapMaterials(Renderer r, DriverCatalog cat)
        {
            if (cat.solid == null) return;
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null || mats[i] == cat.solid || mats[i] == cat.seeThrough) continue;
                bool clear = mats[i].name.Contains("Transparent");
                mats[i] = clear && cat.seeThrough != null ? cat.seeThrough : cat.solid;
                changed = true;
            }
            if (changed) r.sharedMaterials = mats;
        }
    }
}
