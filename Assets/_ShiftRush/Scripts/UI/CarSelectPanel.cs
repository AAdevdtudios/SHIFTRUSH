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

        int index;

        void Awake()
        {
            if (previousButton) previousButton.onClick.AddListener(() => Step(-1));
            if (nextButton) nextButton.onClick.AddListener(() => Step(1));
            if (confirmButton) confirmButton.onClick.AddListener(Confirm);
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
            if (MenuInput.Left) Step(-1);
            else if (MenuInput.Right) Step(1);
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

        void Refresh(bool showCar)
        {
            var cat = RaceCatalog.Instance;
            if (cat == null || cat.cars.Count == 0) return;
            var car = cat.cars[index];
            if (carName) carName.text = car.displayName.ToUpperInvariant();
            if (tagline) tagline.text = car.tagline;
            if (counter) counter.text = (index + 1).ToString("00") + " / " + cat.cars.Count.ToString("00");
            int[] values = { car.topSpeed, car.acceleration, car.handling, car.boost };
            for (int i = 0; i < stats.Length && i < values.Length; i++) if (stats[i]) stats[i].SetValue(values[i], car.accent);
            bool many = cat.cars.Count > 1;
            if (previousButton) previousButton.interactable = many;
            if (nextButton) nextButton.interactable = many;
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
