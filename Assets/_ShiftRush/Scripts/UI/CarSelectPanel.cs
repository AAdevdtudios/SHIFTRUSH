using ShiftRush.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ShiftRush.UI
{
    /// <summary>
    /// Garage: flick through the catalog's cars (Left/Right, A/D, bumpers, or the arrow buttons) with the car
    /// shown live on the showroom plinth, its name, tagline and four stat bars. Enter confirms.
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
        [SerializeField] Button previousButton;
        [SerializeField] Button nextButton;
        [SerializeField] Button confirmButton;
        public UnityEvent onConfirm = new UnityEvent();

        [Header("Car list (optional)")]
        [Tooltip("One row per catalog car, in catalog order. The selected row lights up.")]
        [SerializeField] Image[] listBacks;
        [SerializeField] TMP_Text[] listNames;
        [SerializeField] TMP_Text[] listSubs;
        [SerializeField] Button[] listButtons;
        [Header("Drift character (optional)")]
        [Tooltip("Drifty, Balanced, Grippy: the car's own one is lit.")]
        [SerializeField] Image[] driftPills;
        [SerializeField] TMP_Text[] driftLabels;
        [SerializeField] TMP_Text driftNote;
        [SerializeField] Color pickFill = new Color32(255, 46, 154, 255);
        [SerializeField] Color pickText = new Color32(11, 10, 18, 255);
        [SerializeField] Color idleFill = new Color32(26, 22, 40, 255);
        [SerializeField] Color idleText = new Color32(244, 241, 250, 255);
        [SerializeField] Color idleSub = new Color32(140, 134, 163, 255);

        int index;

        void Awake()
        {
            if (previousButton) previousButton.onClick.AddListener(() => Step(-1));
            if (nextButton) nextButton.onClick.AddListener(() => Step(1));
            if (confirmButton) confirmButton.onClick.AddListener(Confirm);
            if (listButtons != null)
                for (int i = 0; i < listButtons.Length; i++)
                {
                    int k = i;
                    if (listButtons[i]) listButtons[i].onClick.AddListener(() => SelectCar(k));
                }
        }

        void OnEnable()
        {
            index = RaceSession.CarIndex;
            Refresh(false);
        }

        bool Accepting { get { return visibleGroup == null || (visibleGroup.alpha > 0.95f && visibleGroup.interactable); } }

        void Update()
        {
            if (!Accepting) return;
            if (MenuInput.Left || MenuInput.Up) Step(-1);
            else if (MenuInput.Right || MenuInput.Down) Step(1);
            else if (MenuInput.Submit) Confirm();
        }

        void Step(int dir)
        {
            var cat = RaceCatalog.Instance;
            if (cat == null || cat.cars.Count == 0) return;
            if (cat.cars.Count == 1) { MenuSounds.Tick(); if (showroom) showroom.Kick(0.5f); return; }
            index = (index + dir + cat.cars.Count) % cat.cars.Count;
            RaceSession.CarIndex = index;
            MenuSounds.Shift();
            Refresh(true);
        }

        /// <summary>Picks a car straight from the list (mouse).</summary>
        public void SelectCar(int i)
        {
            var cat = RaceCatalog.Instance;
            if (!Accepting || cat == null || i < 0 || i >= cat.cars.Count || i == index) return;
            index = i;
            RaceSession.CarIndex = index;
            MenuSounds.Shift();
            Refresh(true);
        }

        void Refresh(bool showCar)
        {
            var cat = RaceCatalog.Instance;
            if (cat == null || cat.cars.Count == 0) return;
            var car = cat.cars[index];
            if (carName) carName.text = car.displayName.ToUpperInvariant();
            if (tagline)
            {
                // "MUSCLE · DRIFTY" in the car's colour, then its line. Drift is what the car actually has.
                var dm = car.prefab != null ? car.prefab.GetComponent<Vehicles.DriftMoves>() : null;
                var drift = dm != null ? dm.Character : Vehicles.CarTypes.Drift(car.type);
                string tag = Vehicles.CarTypes.Name(car.type) + "  ·  " + drift.ToString().ToUpperInvariant();
                tagline.text = "<color=#" + ColorUtility.ToHtmlStringRGB(car.accent) + "><b>" + tag + "</b></color>   " + car.tagline;
            }
            if (counter) counter.text = (index + 1).ToString("00") + " / " + cat.cars.Count.ToString("00");
            int[] values = { car.topSpeed, car.acceleration, car.handling, car.boost };
            for (int i = 0; i < stats.Length && i < values.Length; i++) if (stats[i]) stats[i].SetValue(values[i], car.accent);
            bool many = cat.cars.Count > 1;
            if (previousButton) previousButton.interactable = many;
            if (nextButton) nextButton.interactable = many;
            if (listBacks != null)
                for (int i = 0; i < listBacks.Length; i++)
                {
                    bool on = i == index;
                    if (listBacks[i]) listBacks[i].color = on ? pickFill : idleFill;
                    if (listNames != null && i < listNames.Length && listNames[i]) listNames[i].color = on ? pickText : idleText;
                    if (listSubs != null && i < listSubs.Length && listSubs[i]) listSubs[i].color = on ? pickText : idleSub;
                }
            if (driftPills != null && driftPills.Length >= 3)
            {
                var dm2 = car.prefab != null ? car.prefab.GetComponent<Vehicles.DriftMoves>() : null;
                var d = dm2 != null ? dm2.Character : Vehicles.CarTypes.Drift(car.type);
                int lit = d == Vehicles.DriftCharacter.Drifty ? 0 : d == Vehicles.DriftCharacter.Grippy ? 2 : 1;
                for (int i = 0; i < 3; i++)
                {
                    if (driftPills[i]) driftPills[i].color = i == lit ? new Color32(46, 242, 255, 255) : idleFill;
                    if (driftLabels != null && i < driftLabels.Length && driftLabels[i]) driftLabels[i].color = i == lit ? pickText : idleSub;
                }
                if (driftNote) driftNote.text = lit == 0 ? "Steps out easily, swings wide, keeps its speed."
                                              : lit == 2 ? "Needs a firm flick, stays tight, pays the biggest boost."
                                              : "In between. Forgiving, and it still slides when you ask.";
            }
            if (showCar && showroom) showroom.Show(car);
        }

        public void Confirm()
        {
            if (!Accepting) return;
            RaceSession.CarIndex = index;
            MenuSounds.Select();
            onConfirm.Invoke();
        }
    }
}
