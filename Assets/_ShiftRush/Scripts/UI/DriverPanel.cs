using System.Collections.Generic;
using ShiftRush.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ShiftRush.UI
{
    /// <summary>
    /// The garage's Driver tab. Tab (pad X) switches between Top, Bottom and Accessories, Up/Down (W/S) picks a slot, Left/Right (A/D) flicks through the pieces for it, Q/E
    /// (pad triggers) change the colourway, B (pad Y) swaps the body, Enter wears it (buying it first if needed).
    /// Everything previews live on the driver on stage before you spend. The Fit Read shows how the look leans: GLOSS or GRIT.
    /// Outfits never change the car, only how the stream sees you.
    /// </summary>
    public class DriverPanel : MonoBehaviour
    {
        [SerializeField] CanvasGroup visibleGroup;
        [SerializeField] Showroom showroom;

        [Header("Groups: Top, Bottom, Accessories")]
        [SerializeField] Button[] groupButtons = new Button[3];
        [SerializeField] Image[] groupBacks = new Image[3];
        [SerializeField] TMP_Text[] groupLabels = new TMP_Text[3];

        [Header("Slots (one row per DriverSlot, in enum order)")]
        [SerializeField] Button[] slotButtons;
        [SerializeField] Image[] slotBacks;
        [SerializeField] TMP_Text[] slotNames;
        [SerializeField] TMP_Text[] slotValues;

        [Header("Pieces for the picked slot")]
        [SerializeField] Button[] optionButtons;
        [SerializeField] Image[] optionBacks;
        [SerializeField] Image[] optionIcons;
        [SerializeField] TMP_Text[] optionNames;
        [SerializeField] TMP_Text[] optionNotes;
        [SerializeField] TMP_Text pageLabel;

        [Header("Colourways")]
        [SerializeField] Button[] swatchButtons;
        [SerializeField] Image[] swatchFills;
        [SerializeField] Image[] swatchRings;

        [Header("Body")]
        [SerializeField] Button[] bodyButtons = new Button[2];
        [SerializeField] Image[] bodyBacks = new Image[2];
        [SerializeField] TMP_Text[] bodyLabels = new TMP_Text[2];

        [Header("Right column")]
        [SerializeField] TMP_Text itemName;
        [SerializeField] TMP_Text itemLine;
        [SerializeField] RectTransform leanFill;
        [SerializeField] TMP_Text glossLabel;
        [SerializeField] TMP_Text gritLabel;
        [SerializeField] TMP_Text fitNote;
        [SerializeField] GameObject maskNote;
        [SerializeField] Button actionButton;
        [SerializeField] TMP_Text actionLabel;
        [SerializeField] Image actionFill;

        [Header("Colours")]
        [SerializeField] Color pickFill = new Color32(255, 46, 154, 255);
        [SerializeField] Color pickText = new Color32(11, 10, 18, 255);
        [SerializeField] Color idleFill = new Color32(26, 22, 40, 255);
        [SerializeField] Color idleText = new Color32(244, 241, 250, 255);
        [SerializeField] Color idleSub = new Color32(140, 134, 163, 255);
        [SerializeField] Color gold = new Color32(255, 197, 61, 255);
        [SerializeField] Color good = new Color32(124, 242, 154, 255);
        [SerializeField] Color glossColour = new Color32(46, 242, 255, 255);
        [SerializeField] Color gritColour = new Color32(255, 138, 61, 255);

        readonly List<DriverSlot> slots = new List<DriverSlot>();
        List<List<DriverCatalog.Item>> styles = new List<List<DriverCatalog.Item>>(); // null entry = NONE
        int slotIndex, style, colour;
        DriverGroup group;
        DriverBody body;
        float shake;
        RectTransform actionRect; Vector2 actionHome;

        /// <summary>
        /// Slots added after the menu was built (Gloves) get a row copied from an existing one, so the tab works
        /// without rebuilding the whole menu.
        /// </summary>
        void AddMissingSlotRows()
        {
            int n = System.Enum.GetValues(typeof(DriverSlot)).Length;
            if (slotButtons == null || slotButtons.Length == 0 || slotButtons.Length >= n) return;
            Button template = null;
            int t = -1;
            for (int i = 0; i < slotButtons.Length; i++) if (slotButtons[i]) { template = slotButtons[i]; t = i; }
            if (template == null) return;
            int old = slotButtons.Length;
            System.Array.Resize(ref slotButtons, n); System.Array.Resize(ref slotBacks, n);
            System.Array.Resize(ref slotNames, n); System.Array.Resize(ref slotValues, n);
            for (int i = old; i < n; i++)
            {
                var row = Instantiate(template.gameObject, template.transform.parent);
                var s = (DriverSlot)i;
                row.name = s.ToString();
                slotButtons[i] = row.GetComponent<Button>();
                slotButtons[i].onClick.RemoveAllListeners();
                slotBacks[i] = row.GetComponent<Image>();
                var name = row.transform.Find("Slot"); var val = row.transform.Find("Value");
                slotNames[i] = name ? name.GetComponent<TMP_Text>() : null;
                slotValues[i] = val ? val.GetComponent<TMP_Text>() : null;
                if (slotNames[i]) slotNames[i].text = SlotTitle(s);
            }
        }

        DriverSlot Slot { get { return slots.Count > 0 ? slots[Mathf.Clamp(slotIndex, 0, slots.Count - 1)] : DriverSlot.Torso; } }

        void Awake()
        {
            AddMissingSlotRows();
            for (int i = 0; slotButtons != null && i < slotButtons.Length; i++) { int k = i; if (slotButtons[i]) slotButtons[i].onClick.AddListener(() => PickSlotRow(k)); }
            for (int i = 0; optionButtons != null && i < optionButtons.Length; i++) { int k = i; if (optionButtons[i]) optionButtons[i].onClick.AddListener(() => PickOption(k)); }
            for (int i = 0; swatchButtons != null && i < swatchButtons.Length; i++) { int k = i; if (swatchButtons[i]) swatchButtons[i].onClick.AddListener(() => PickColour(k)); }
            for (int i = 0; groupButtons != null && i < groupButtons.Length; i++) { int k = i; if (groupButtons[i]) groupButtons[i].onClick.AddListener(() => SetGroup((DriverGroup)k)); }
            for (int i = 0; i < bodyButtons.Length; i++) { int k = i; if (bodyButtons[i]) bodyButtons[i].onClick.AddListener(() => SetBody((DriverBody)k)); }
            if (actionButton) { actionButton.onClick.AddListener(Action); actionRect = (RectTransform)actionButton.transform; actionHome = actionRect.anchoredPosition; }
        }

        void OnEnable()
        {
            body = DriverProfile.Body;
            group = DriverGroup.Top;
            slotIndex = 0;
            BuildSlots();
            Stage();
            LoadSlot();
            Refresh();
        }

        bool Accepting { get { return visibleGroup == null || (visibleGroup.alpha > 0.95f && visibleGroup.interactable); } }

        void Update()
        {
            if (actionRect != null)
            {
                shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 3f);
                actionRect.anchoredPosition = actionHome + new Vector2(Mathf.Sin(Time.unscaledTime * 70f) * 10f * shake * shake, 0f);
            }
            if (!Accepting) return;
            var kb = Keyboard.current; var pad = Gamepad.current;
            if (MenuInput.Up) StepSlot(-1);
            else if (MenuInput.Down) StepSlot(1);
            else if (MenuInput.Left) StepStyle(-1);
            else if (MenuInput.Right) StepStyle(1);
            else if ((kb != null && kb.qKey.wasPressedThisFrame) || (pad != null && pad.leftTrigger.wasPressedThisFrame)) StepColour(-1);
            else if ((kb != null && kb.eKey.wasPressedThisFrame) || (pad != null && pad.rightTrigger.wasPressedThisFrame)) StepColour(1);
            else if ((kb != null && kb.tabKey.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame)) SetGroup((DriverGroup)(((int)group + 1) % 3));
            else if ((kb != null && kb.bKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame)) SetBody(body == DriverBody.BodyA ? DriverBody.BodyB : DriverBody.BodyA);
            else if (MenuInput.Submit) Action();
        }

        // ------------------------------------------------------------------ state

        void BuildSlots()
        {
            slots.Clear();
            var cat = DriverCatalog.Instance;
            foreach (var s in DriverCatalog.DisplayOrder)
                if (cat != null && DriverCatalog.GroupOf(s) == group && cat.Has(body, s)) slots.Add(s);
        }

        /// <summary>Top, Bottom or Accessories: the slot list shows that group's slots.</summary>
        void SetGroup(DriverGroup g)
        {
            if (!Accepting || g == group) return;
            group = g;
            MenuSounds.Shift();
            slotIndex = 0;
            BuildSlots();
            LoadSlot();
            Refresh();
        }

        /// <summary>The body on stage, dressed in the saved look.</summary>
        void Stage()
        {
            var cat = DriverCatalog.Instance;
            if (showroom == null || cat == null) return;
            var model = showroom.ShowDriver(cat.Body(body));
            DriverLook.Apply(model, body);
        }

        /// <summary>Points the picker at what's worn in the current slot.</summary>
        void LoadSlot()
        {
            var cat = DriverCatalog.Instance;
            styles = cat != null ? cat.Styles(body, Slot) : new List<List<DriverCatalog.Item>>();
            if (DriverCatalog.Optional(Slot)) styles.Insert(0, null);
            style = 0; colour = 0;
            var worn = cat != null ? cat.Find(DriverProfile.Worn(body, Slot)) : null;
            for (int i = 0; i < styles.Count; i++)
            {
                if (styles[i] == null) continue;
                int c = styles[i].IndexOf(worn);
                if (c >= 0) { style = i; colour = c; break; }
            }
        }

        DriverCatalog.Item Previewed
        {
            get
            {
                if (style >= styles.Count || styles[style] == null) return null;
                var g = styles[style];
                return g[Mathf.Clamp(colour, 0, g.Count - 1)];
            }
        }

        // ------------------------------------------------------------------ input

        void StepSlot(int dir)
        {
            if (slots.Count == 0) return;
            slotIndex = (slotIndex + dir + slots.Count) % slots.Count;
            MenuSounds.Hover();
            LoadSlot();
            Refresh();
        }

        void PickSlotRow(int row)
        {
            if (!Accepting || row >= System.Enum.GetValues(typeof(DriverSlot)).Length) return;
            int k = slots.IndexOf((DriverSlot)row);
            if (k < 0 || k == slotIndex) return;
            slotIndex = k;
            MenuSounds.Hover();
            LoadSlot();
            Refresh();
        }

        void StepStyle(int dir)
        {
            if (styles.Count < 2) { MenuSounds.Tick(); return; }
            style = (style + dir + styles.Count) % styles.Count;
            colour = 0;
            MenuSounds.Tick();
            Refresh();
        }

        void PickOption(int i)
        {
            int k = PageStart() + i;
            if (!Accepting || k >= styles.Count || k == style) return;
            style = k; colour = 0;
            MenuSounds.Tick();
            Refresh();
        }

        void StepColour(int dir)
        {
            if (style >= styles.Count || styles[style] == null || styles[style].Count < 2) { MenuSounds.Tick(); return; }
            colour = (colour + dir + styles[style].Count) % styles[style].Count;
            MenuSounds.Tick();
            Refresh();
        }

        void PickColour(int i)
        {
            if (!Accepting || style >= styles.Count || styles[style] == null || i >= styles[style].Count || i == colour) return;
            colour = i;
            MenuSounds.Tick();
            Refresh();
        }

        void SetBody(DriverBody b)
        {
            if (!Accepting || b == body) return;
            body = b;
            DriverProfile.Body = b;
            MenuSounds.Shift();
            var keep = Slot;
            BuildSlots();
            slotIndex = Mathf.Max(0, slots.IndexOf(keep));
            Stage();
            LoadSlot();
            Refresh();
        }

        /// <summary>Wear the previewed piece, buying it first if it isn't yours.</summary>
        void Action()
        {
            if (!Accepting) return;
            var item = Previewed;
            string worn = DriverProfile.Worn(body, Slot);
            if ((item == null && string.IsNullOrEmpty(worn)) || (item != null && item.id == worn)) { MenuSounds.Tick(); return; }
            if (item != null && !DriverProfile.Owns(item))
            {
                if (!DriverProfile.Buy(item)) { shake = 1f; MenuSounds.Back(); Refresh(); return; }
            }
            DriverProfile.Wear(body, Slot, item);
            MenuSounds.Confirm();
            if (showroom) showroom.Kick(0.7f);
            Refresh();
        }

        // ------------------------------------------------------------------ display

        int PageSize { get { return optionButtons != null ? optionButtons.Length : 0; } }
        int PageStart() { return PageSize == 0 ? 0 : (style / PageSize) * PageSize; }

        void Refresh()
        {
            var cat = DriverCatalog.Instance;
            if (cat == null) return;
            var item = Previewed;
            string wornId = DriverProfile.Worn(body, Slot);

            // Slot rows: hidden when this body has no such slot.
            for (int r = 0; slotBacks != null && r < slotBacks.Length; r++)
            {
                var s = (DriverSlot)r;
                bool has = slots.Contains(s);
                if (slotButtons != null && r < slotButtons.Length && slotButtons[r]) slotButtons[r].gameObject.SetActive(has);
                if (!has) continue;
                bool on = s == Slot;
                var w = cat.Find(DriverProfile.Worn(body, s));
                if (slotBacks[r]) slotBacks[r].color = on ? pickFill : idleFill;
                if (slotNames[r]) slotNames[r].color = on ? pickText : idleSub;
                if (slotValues[r]) { slotValues[r].text = w != null ? w.displayName : "NONE"; slotValues[r].color = on ? pickText : (w != null ? idleText : idleSub); }
            }

            // The pieces for this slot, a page at a time.
            int start = PageStart();
            for (int i = 0; optionButtons != null && i < optionButtons.Length; i++)
            {
                int k = start + i;
                bool has = k < styles.Count;
                if (optionButtons[i]) optionButtons[i].gameObject.SetActive(has);
                if (!has) continue;
                var g = styles[k];
                bool on = k == style;
                var shown = g == null ? null : (on ? g[Mathf.Clamp(colour, 0, g.Count - 1)] : First(g, wornId));
                if (optionBacks[i]) optionBacks[i].color = on ? pickFill : idleFill;
                if (optionIcons[i]) { optionIcons[i].sprite = shown != null ? shown.icon : null; optionIcons[i].enabled = shown != null && shown.icon != null; }
                if (optionNames[i]) { optionNames[i].text = g == null ? "NONE" : g[0].displayName; optionNames[i].color = on ? pickText : idleText; }
                if (optionNotes[i])
                {
                    bool wornHere = g == null ? string.IsNullOrEmpty(wornId) : g.Exists(x => x.id == wornId);
                    bool owned = g == null || g.Exists(DriverProfile.Owns);
                    optionNotes[i].text = wornHere ? "WORN" : g == null ? "" : owned ? TagText(g[0].tag) : g[0].price.ToString("N0");
                    optionNotes[i].color = on ? pickText : wornHere ? good : g == null ? idleSub : owned ? TagColour(g[0].tag) : gold;
                }
            }
            if (pageLabel)
            {
                int pages = PageSize == 0 ? 1 : Mathf.Max(1, (styles.Count + PageSize - 1) / PageSize);
                pageLabel.text = pages > 1 ? "PAGE " + (start / PageSize + 1) + " / " + pages : "";
            }

            // Colourways of the picked piece.
            var group = style < styles.Count ? styles[style] : null;
            for (int i = 0; swatchButtons != null && i < swatchButtons.Length; i++)
            {
                bool has = group != null && i < group.Count;
                if (swatchButtons[i]) swatchButtons[i].gameObject.SetActive(has);
                if (!has) { if (swatchRings[i]) swatchRings[i].enabled = false; continue; }
                if (swatchFills[i]) swatchFills[i].color = group[i].swatch;
                if (swatchRings[i]) swatchRings[i].enabled = i == colour;
            }

            for (int i = 0; groupBacks != null && i < groupBacks.Length; i++)
            {
                bool on = (int)this.group == i;
                if (groupBacks[i]) groupBacks[i].color = on ? idleText : idleFill;
                if (groupLabels != null && i < groupLabels.Length && groupLabels[i]) groupLabels[i].color = on ? pickText : idleSub;
            }

            for (int i = 0; i < bodyBacks.Length; i++)
            {
                bool on = (int)body == i;
                if (bodyBacks[i]) bodyBacks[i].color = on ? glossColour : idleFill;
                if (bodyLabels[i]) bodyLabels[i].color = on ? pickText : idleSub;
            }

            // Right column: the piece, the look's lean and what it means for fans.
            if (itemName) itemName.text = item != null ? item.displayName : "NO " + SlotTitle(Slot);
            if (itemLine)
            {
                itemLine.text = item == null ? "Leave it bare."
                    : "<color=#" + ColorUtility.ToHtmlStringRGB(TagColour(item.tag)) + "><b>" + TagText(item.tag) + "</b></color>   " + SlotTitle(Slot) + "  ·  COLOUR " + (colour + 1) + " OF " + group.Count;
            }
            DriverProfile.Lean(body, out int gloss, out int grit, Slot, item);
            int total = gloss + grit;
            float g01 = total == 0 ? 0.5f : gloss / (float)total;
            if (leanFill) leanFill.anchorMax = new Vector2(g01, 1f);
            if (glossLabel) glossLabel.text = "GLOSS " + Mathf.RoundToInt(g01 * 100f) + "%";
            if (gritLabel) gritLabel.text = "GRIT " + (100 - Mathf.RoundToInt(g01 * 100f)) + "%";
            if (fitNote)
            {
                fitNote.text = total == 0 ? "A neutral look. Pick GLOSS or GRIT pieces to give the fans a side to cheer."
                    : g01 >= 0.65f ? "Reads GLOSS: clean lines, sharp kit. Race clean and drift long to match it."
                    : g01 <= 0.35f ? "Reads GRIT: worn and heavy. Knocks and wrecks match it."
                    : "Mixed signals. Fans like a look that matches how you race.";
            }
            bool masked = Slot == DriverSlot.Mask ? item != null : !string.IsNullOrEmpty(DriverProfile.Worn(body, DriverSlot.Mask));
            if (maskNote) maskNote.SetActive(masked);

            // The big button.
            string label; bool can = true;
            bool same = item == null ? string.IsNullOrEmpty(wornId) : item.id == wornId;
            if (same) label = item == null ? "NOTHING WORN" : "WORN";
            else if (item == null) label = "TAKE IT OFF";
            else if (DriverProfile.Owns(item)) label = "WEAR IT";
            else { can = PlayerProfile.Hype >= item.price; label = can ? "BUY AND WEAR  " + item.price.ToString("N0") : "NEED " + item.price.ToString("N0") + " HYPE"; }
            if (actionLabel) actionLabel.text = label;
            if (actionFill) actionFill.color = same ? new Color32(42, 37, 64, 255) : can ? pickFill : new Color32(60, 52, 82, 255);

            // Live preview on the driver.
            if (showroom != null && showroom.ShownDriver != null) DriverLook.Apply(showroom.ShownDriver, body, Slot, item);
        }

        static DriverCatalog.Item First(List<DriverCatalog.Item> g, string wornId)
        {
            foreach (var x in g) if (x.id == wornId) return x;
            return g[0];
        }

        string TagText(DriverStyle t) { return t == DriverStyle.Gloss ? "GLOSS" : t == DriverStyle.Grit ? "GRIT" : "NEUTRAL"; }
        Color TagColour(DriverStyle t) { return t == DriverStyle.Gloss ? glossColour : t == DriverStyle.Grit ? gritColour : idleSub; }

        public static string SlotTitle(DriverSlot s)
        {
            switch (s)
            {
                case DriverSlot.Brows: return "BROWS";
                case DriverSlot.Headphones: return "HEADPHONES";
                case DriverSlot.Torso: return "TOP";
                default: return s.ToString().ToUpperInvariant();
            }
        }
    }
}
