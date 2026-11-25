using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ShiftRush.UI
{
    /// <summary>
    /// CAR and DRIVER tabs in the garage. C (pad View / Select) or a click switches. Each tab has its own controls and
    /// panel; the stage shows the car or the driver to match. The garage always opens on CAR.
    /// L (pad right stick click) or the LIGHT chip cycles the set's time of day: night, dusk, day.
    /// </summary>
    public class GarageTabs : MonoBehaviour
    {
        [SerializeField] CanvasGroup visibleGroup;
        [SerializeField] Showroom showroom;
        [SerializeField] GameObject carRoot;
        [SerializeField] GameObject driverRoot;
        [SerializeField] CarSelectPanel carPanel;
        [SerializeField] DriverPanel driverPanel;
        [SerializeField] Button[] tabButtons = new Button[2];
        [SerializeField] Image[] tabBacks = new Image[2];
        [SerializeField] TMP_Text[] tabLabels = new TMP_Text[2];
        [SerializeField] Button lightButton;
        [SerializeField] TMP_Text lightLabel;
        [SerializeField] Color onFill = new Color32(255, 46, 154, 255);
        [SerializeField] Color onText = new Color32(11, 10, 18, 255);
        [SerializeField] Color offFill = new Color32(26, 22, 40, 255);
        [SerializeField] Color offText = new Color32(189, 183, 209, 255);

        bool driver;
        bool wasVisible;

        void Awake()
        {
            for (int i = 0; i < tabButtons.Length; i++) { bool d = i == 1; if (tabButtons[i]) tabButtons[i].onClick.AddListener(() => { if (Accepting) SetDriver(d); }); }
            if (lightButton) lightButton.onClick.AddListener(() => { if (Accepting) CycleLight(); });
            SetDriver(false, true);
        }

        void OnEnable() { VFX.ShowroomTime.Changed += ShowLight; ShowLight(); }
        void OnDisable() { VFX.ShowroomTime.Changed -= ShowLight; }

        void CycleLight()
        {
            var time = VFX.ShowroomTime.Instance;
            if (time == null) { MenuSounds.Tick(); return; }
            time.Cycle();
            MenuSounds.Shift();
            if (showroom) showroom.Kick(0.5f);
        }

        void ShowLight()
        {
            var time = VFX.ShowroomTime.Instance;
            if (lightLabel) lightLabel.text = "<color=#FF2E9A>L</color>  LIGHT  " + (time != null ? time.Current : "NIGHT");
        }

        bool Accepting { get { return visibleGroup == null || (visibleGroup.alpha > 0.95f && visibleGroup.interactable); } }

        void Update()
        {
            bool visible = visibleGroup == null || visibleGroup.alpha > 0.05f;
            if (!visible && wasVisible && driver) SetDriver(false); // leaving the garage: back to the car
            wasVisible = visible;
            if (!Accepting) return;
            var kb = Keyboard.current; var pad = Gamepad.current;
            if ((kb != null && kb.cKey.wasPressedThisFrame) || (pad != null && pad.selectButton.wasPressedThisFrame)) SetDriver(!driver);
            else if ((kb != null && kb.lKey.wasPressedThisFrame) || (pad != null && pad.rightStickButton.wasPressedThisFrame)) CycleLight();
        }

        public void SetDriver(bool on, bool silent = false)
        {
            if (on == driver && !silent) return;
            driver = on;
            if (carRoot) carRoot.SetActive(!on);
            if (driverRoot) driverRoot.SetActive(on);
            if (carPanel) carPanel.enabled = !on;
            if (driverPanel) driverPanel.enabled = on;
            if (!on && showroom != null) showroom.HideDriver();
            for (int i = 0; i < 2; i++)
            {
                bool lit = (i == 1) == on;
                if (tabBacks[i]) tabBacks[i].color = lit ? onFill : offFill;
                if (tabLabels[i]) tabLabels[i].color = lit ? onText : offText;
            }
            if (!silent) MenuSounds.Shift();
        }
    }
}
