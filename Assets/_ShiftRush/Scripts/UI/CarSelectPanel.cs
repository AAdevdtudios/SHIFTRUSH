using System.Collections.Generic;
using ShiftRush.Core;
using ShiftRush.Vehicles;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ShiftRush.UI
{
    /// <summary>
    /// The crew's garage. Up/Down (W/S) picks a car, Left/Right (A/D) flicks through parts for the chosen slot,
    /// Tab (pad X) switches slot, Enter does whatever the big button says: buy the car, buy and fit a part, fit a
    /// part you already own, or move on to track select. Parts preview live on the turntable car and in the stats
    /// before you spend anything. Mouse works for everything too.
    /// </summary>
    public class CarSelectPanel : MonoBehaviour
    {
        [SerializeField] CanvasGroup visibleGroup;
        [SerializeField] Showroom showroom;
        [SerializeField] TMP_Text carName;
        [SerializeField] TMP_Text tagline;
        [SerializeField] TMP_Text counter;
        [Tooltip("Top speed, acceleration, handling, boost. Each bar is a row of segments.")]
        [SerializeField] StatBar[] stats = new StatBar[4];
        [SerializeField] TMP_Text handlingLabel;
        [SerializeField] Button previousButton;
        [SerializeField] Button nextButton;
        [Tooltip("The big button: buy, fit, or on to track select.")]
        [SerializeField] Button confirmButton;
        [SerializeField] TMP_Text actionLabel;
        [SerializeField] Image actionFill;
        public UnityEvent onConfirm = new UnityEvent();

        [Header("Car list")]
        [SerializeField] Image[] listBacks;
        [SerializeField] TMP_Text[] listNames;
        [SerializeField] TMP_Text[] listSubs;
        [SerializeField] Button[] listButtons;
        [Header("Drift character")]
        [SerializeField] Image[] driftPills;
        [SerializeField] TMP_Text[] driftLabels;
        [SerializeField] TMP_Text driftNote;
        [Header("Parts")]
        [Tooltip("Tyres, Spoiler.")]
        [SerializeField] Button[] slotButtons = new Button[2];
        [SerializeField] Image[] slotBacks = new Image[2];
        [SerializeField] TMP_Text[] slotValues = new TMP_Text[2];
        [SerializeField] Button[] optionButtons;
        [SerializeField] Image[] optionBacks;
        [SerializeField] TMP_Text[] optionNames;
        [SerializeField] TMP_Text[] optionPrices;
        [SerializeField] TMP_Text partLine;

        [Header("Colours")]
        [SerializeField] Color pickFill = new Color32(255, 46, 154, 255);
        [SerializeField] Color pickText = new Color32(11, 10, 18, 255);
        [SerializeField] Color idleFill = new Color32(26, 22, 40, 255);
        [SerializeField] Color idleText = new Color32(244, 241, 250, 255);
        [SerializeField] Color idleSub = new Color32(140, 134, 163, 255);
        [SerializeField] Color gold = new Color32(255, 197, 61, 255);
        [SerializeField] Color good = new Color32(124, 242, 154, 255);
        [SerializeField] Color cyan = new Color32(46, 242, 255, 255);

        int index, slot;
        readonly int[] preview = new int[2];          // option index previewed in each slot
        readonly List<PartCatalog.Part>[] options = { new List<PartCatalog.Part>(), new List<PartCatalog.Part>() };
        float shake;
        RectTransform actionRect; Vector2 actionHome;

        static PartCatalog.Slot SlotOf(int i) { return i == 0 ? PartCatalog.Slot.Tyres : PartCatalog.Slot.Spoiler; }

        void Awake()
        {
            if (previousButton) previousButton.onClick.AddListener(() => Step(-1));
            if (nextButton) nextButton.onClick.AddListener(() => Step(1));
            if (confirmButton) { confirmButton.onClick.AddListener(Action); actionRect = (RectTransform)confirmButton.transform; actionHome = actionRect.anchoredPosition; }
            if (listButtons != null)
                for (int i = 0; i < listButtons.Length; i++) { int k = i; if (listButtons[i]) listButtons[i].onClick.AddListener(() => SelectCar(k)); }
            for (int i = 0; i < slotButtons.Length; i++) { int k = i; if (slotButtons[i]) slotButtons[i].onClick.AddListener(() => SetSlot(k)); }
            if (optionButtons != null)
                for (int i = 0; i < optionButtons.Length; i++) { int k = i; if (optionButtons[i]) optionButtons[i].onClick.AddListener(() => Preview(k)); }
        }

        void OnEnable()
        {
            index = RaceSession.CarIndex;
            slot = 0;
            ResetPreview();
            Refresh(false);
        }

        void OnDisable()
        {
            // Anything previewed but not bought comes off the showroom car.
            var cat = RaceCatalog.Instance;
            if (showroom != null && cat != null && index < cat.cars.Count && showroom.ShownCar == cat.cars[index])
                CarLoadout.ApplyFitted(showroom.ShownModel, cat.cars[index], false);
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
            if (MenuInput.Up) Step(-1);
            else if (MenuInput.Down) Step(1);
            else if (MenuInput.Left) Cycle(-1);
            else if (MenuInput.Right) Cycle(1);
            else if ((kb != null && kb.tabKey.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame)) SetSlot(1 - slot);
            else if (MenuInput.Submit) Action();
        }

        RaceCatalog.Car Car { get { var cat = RaceCatalog.Instance; return cat != null && cat.cars.Count > 0 ? cat.cars[Mathf.Clamp(index, 0, cat.cars.Count - 1)] : null; } }

        void ResetPreview()
        {
            var car = Car; var parts = PartCatalog.Instance;
            for (int s = 0; s < 2; s++)
            {
                options[s] = parts != null ? parts.For(SlotOf(s), car) : new List<PartCatalog.Part>();
                string fitted = PlayerProfile.Fitted(car, SlotOf(s));
                preview[s] = Mathf.Max(0, options[s].FindIndex(p => p.id == fitted));
            }
        }

        PartCatalog.Part Previewed(int s) { return preview[s] < options[s].Count ? options[s][preview[s]] : null; }

        // ------------------------------------------------------------------ input

        void Step(int dir)
        {
            var cat = RaceCatalog.Instance;
            if (cat == null || cat.cars.Count < 2) { MenuSounds.Tick(); return; }
            index = (index + dir + cat.cars.Count) % cat.cars.Count;
            ChangeCar();
        }

        public void SelectCar(int i)
        {
            var cat = RaceCatalog.Instance;
            if (!Accepting || cat == null || i < 0 || i >= cat.cars.Count || i == index) return;
            index = i;
            ChangeCar();
        }

        void ChangeCar()
        {
            if (PlayerProfile.OwnsCar(Car)) RaceSession.CarIndex = index; // the last owned car you looked at races next
            MenuSounds.Shift();
            ResetPreview();
            Refresh(true);
        }

        void SetSlot(int s)
        {
            if (s == slot) return;
            slot = s;
            MenuSounds.Hover();
            Refresh(false);
        }

        void Cycle(int dir)
        {
            if (options[slot].Count < 2) { MenuSounds.Tick(); return; }
            preview[slot] = (preview[slot] + dir + options[slot].Count) % options[slot].Count;
            MenuSounds.Tick();
            Refresh(false);
        }

        void Preview(int option)
        {
            if (option >= options[slot].Count || option == preview[slot]) return;
            preview[slot] = option;
            MenuSounds.Tick();
            Refresh(false);
        }

        /// <summary>The big button: buy the car, buy and fit, fit, or on to track select.</summary>
        public void Action()
        {
            if (!Accepting) return;
            var car = Car;
            if (car == null) return;
            if (!PlayerProfile.OwnsCar(car))
            {
                if (PlayerProfile.BuyCar(car)) { RaceSession.CarIndex = index; MenuSounds.Confirm(); if (showroom) showroom.Kick(1f); }
                else Deny();
                Refresh(false);
                return;
            }
            int s = PendingSlot();
            if (s >= 0)
            {
                var part = Previewed(s);
                if (PlayerProfile.BuyPart(car, part)) { PlayerProfile.Fit(car, part); MenuSounds.Confirm(); if (showroom) showroom.Kick(0.8f); }
                else Deny();
                Refresh(false);
                return;
            }
            RaceSession.CarIndex = index;
            MenuSounds.Select();
            onConfirm.Invoke();
        }

        void Deny() { shake = 1f; MenuSounds.Back(); }

        /// <summary>The first slot whose previewed part isn't the one fitted (current slot first), or -1.</summary>
        int PendingSlot()
        {
            var car = Car;
            for (int k = 0; k < 2; k++)
            {
                int s = k == 0 ? slot : 1 - slot;
                var p = Previewed(s);
                if (p != null && p.id != PlayerProfile.Fitted(car, SlotOf(s))) return s;
            }
            return -1;
        }

        // ------------------------------------------------------------------ display

        void Refresh(bool showCar)
        {
            var cat = RaceCatalog.Instance;
            var car = Car;
            if (cat == null || car == null) return;
            bool owned = PlayerProfile.OwnsCar(car);

            if (carName) carName.text = car.displayName.ToUpperInvariant();
            var baseDrift = CarTypes.Drift(car.type);
            var tyre = Previewed(0); var wing = Previewed(1);
            int step = Mathf.Clamp((tyre != null ? tyre.driftStep : 0) + (wing != null ? wing.driftStep : 0), -1, 1);
            var drift = CarLoadout.Shifted(baseDrift, step);
            if (tagline)
            {
                string tag = CarTypes.Name(car.type) + "  ·  " + drift.ToString().ToUpperInvariant();
                tagline.text = "<color=#" + ColorUtility.ToHtmlStringRGB(car.accent) + "><b>" + tag + "</b></color>   " + car.tagline;
            }
            if (counter) counter.text = (index + 1).ToString("00") + " / " + cat.cars.Count.ToString("00");

            int bonus = (tyre != null ? tyre.handlingBonus : 0) + (wing != null ? wing.handlingBonus : 0);
            int[] values = { car.topSpeed, car.acceleration, Mathf.Min(10, car.handling + bonus), car.boost };
            for (int i = 0; i < stats.Length && i < values.Length; i++) if (stats[i]) stats[i].SetValue(values[i], car.accent);
            if (handlingLabel) handlingLabel.text = bonus > 0 ? "HANDLING  <color=#7CF29A>+" + bonus + "</color>" : "HANDLING";

            // Car list: owned cars show their type, the rest their price.
            if (listBacks != null)
                for (int i = 0; i < listBacks.Length && i < cat.cars.Count; i++)
                {
                    bool on = i == index;
                    var c = cat.cars[i];
                    bool own = PlayerProfile.OwnsCar(c);
                    if (listBacks[i]) listBacks[i].color = on ? pickFill : idleFill;
                    if (listNames != null && i < listNames.Length && listNames[i]) listNames[i].color = on ? pickText : (own ? idleText : new Color(idleText.r, idleText.g, idleText.b, 0.55f));
                    if (listSubs != null && i < listSubs.Length && listSubs[i])
                    {
                        listSubs[i].text = own ? CarTypes.Name(c.type) + "  ·  " + CarTypes.Drift(c.type).ToString().ToUpperInvariant() : "BUY  " + c.price.ToString("N0") + " HYPE";
                        listSubs[i].color = on ? pickText : (own ? idleSub : gold);
                    }
                }

            // Drift character pills.
            if (driftPills != null && driftPills.Length >= 3)
            {
                int lit = drift == DriftCharacter.Drifty ? 0 : drift == DriftCharacter.Grippy ? 2 : 1;
                for (int i = 0; i < 3; i++)
                {
                    if (driftPills[i]) driftPills[i].color = i == lit ? cyan : idleFill;
                    if (driftLabels != null && i < driftLabels.Length && driftLabels[i]) driftLabels[i].color = i == lit ? pickText : idleSub;
                }
                if (driftNote) driftNote.text = step != 0
                    ? (step > 0 ? "These parts loosen it one step from its " : "These parts tidy it one step from its ") + baseDrift.ToString().ToLowerInvariant() + " setup."
                    : lit == 0 ? "Steps out easily, swings wide, keeps its speed."
                    : lit == 2 ? "Needs a firm flick, stays tight, pays the biggest boost."
                    : "In between. Forgiving, and it still slides when you ask.";
            }

            // Slots and the options for the chosen one.
            for (int s = 0; s < 2; s++)
            {
                var p = Previewed(s);
                bool none = car.fit != null && ((s == 1 && !car.fit.takesSpoiler) || (s == 0 && !car.fit.takesTyres));
                if (slotValues[s]) slotValues[s].text = none ? (s == 1 ? "NONE" : "STOCK ONLY") : p != null ? p.displayName : "STOCK";
                if (slotBacks[s]) slotBacks[s].color = s == slot ? new Color32(244, 241, 250, 255) : new Color32(30, 25, 48, 230);
                if (slotValues[s]) slotValues[s].color = s == slot ? pickText : idleText;
            }
            var opts = options[slot];
            if (optionButtons != null)
                for (int i = 0; i < optionButtons.Length; i++)
                {
                    bool has = i < opts.Count;
                    if (optionButtons[i]) optionButtons[i].gameObject.SetActive(has);
                    if (!has) continue;
                    var p = opts[i];
                    bool fitted = p.id == PlayerProfile.Fitted(car, SlotOf(slot));
                    bool ownsPart = PlayerProfile.OwnsPart(car, p);
                    bool on = i == preview[slot];
                    if (optionBacks[i]) optionBacks[i].color = on ? pickFill : idleFill;
                    if (optionNames[i]) { optionNames[i].text = p.displayName; optionNames[i].color = on ? pickText : idleText; }
                    if (optionPrices[i])
                    {
                        optionPrices[i].text = fitted ? "FITTED" : ownsPart ? "OWNED" : p.price.ToString("N0") + " HYPE";
                        optionPrices[i].color = on ? pickText : fitted ? good : ownsPart ? idleSub : gold;
                    }
                }
            if (partLine)
            {
                var p = Previewed(slot);
                partLine.text = p != null && !string.IsNullOrEmpty(p.line) ? p.line : "";
            }

            // The big button says what Enter does.
            string label; bool can = true;
            if (!owned) { can = PlayerProfile.Hype >= car.price; label = can ? "BUY CAR  " + car.price.ToString("N0") : "NEED " + car.price.ToString("N0") + " HYPE"; }
            else
            {
                int s = PendingSlot();
                if (s >= 0)
                {
                    var p = Previewed(s);
                    if (PlayerProfile.OwnsPart(car, p)) label = "FIT " + p.displayName;
                    else { can = PlayerProfile.Hype >= p.price; label = can ? "BUY AND FIT  " + p.price.ToString("N0") : "NEED " + p.price.ToString("N0") + " HYPE"; }
                }
                else label = "PICK A TRACK";
            }
            if (actionLabel) actionLabel.text = label;
            if (actionFill) actionFill.color = can ? pickFill : new Color32(60, 52, 82, 255);

            if (showCar && showroom) showroom.Show(car);
            // Preview the chosen parts on the turntable car (looks only, nothing bought yet).
            if (showroom != null && showroom.ShownCar == car) CarLoadout.Apply(showroom.ShownModel, car, tyre, wing, false);
        }
    }
}
