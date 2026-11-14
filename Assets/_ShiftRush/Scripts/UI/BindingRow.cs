using System.Collections.Generic;
using Michsky.UI.Heat;
using ShiftRush.Vehicles;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShiftRush.UI
{
    /// <summary>
    /// One rebindable control in Settings → Controls. Click (or Enter on) the row, press a key, done.
    /// Esc cancels. The new key is saved straight away and used in every race.
    /// </summary>
    public class BindingRow : MonoBehaviour
    {
        [SerializeField] string actionName = "Throttle";
        [Tooltip("For WASD-style composites: the composite's name (e.g. wasd, keyboard). Empty for plain buttons.")]
        [SerializeField] string composite = "";
        [Tooltip("For composites: positive or negative.")]
        [SerializeField] string part = "";
        [SerializeField] TMP_Text keyLabel;
        [Tooltip("Heat button showing the key (used instead of Key Label when set).")]
        [SerializeField] ButtonManager keyButton;
        [Tooltip("The Reset Controls row: clicking puts every key back to the defaults instead of rebinding.")]
        [SerializeField] bool resetRow;

        static readonly List<BindingRow> rows = new List<BindingRow>();

        InputActionRebindingExtensions.RebindingOperation operation;
        static int listening;
        static int lastFinishFrame = -10;

        /// <summary>True while any row waits for a key (so Esc cancels the rebind rather than leaving the screen).</summary>
        public static bool IsListening { get { return listening > 0 || Time.frameCount - lastFinishFrame <= 1; } }

        public void Configure(string action, string compositeName, string partName, ButtonManager button)
        {
            actionName = action; composite = compositeName; part = partName; keyButton = button;
        }

        public void ConfigureReset(ButtonManager button) { resetRow = true; keyButton = button; }

        // Clicks are hooked up here rather than saved in the scene: the Heat rows are prefab instances and an
        // editor-added click listener on them doesn't survive the save.
        void Awake()
        {
            if (keyButton == null)
            {
                var t = transform.Find("Binding 1");
                if (t != null) keyButton = t.GetComponent<ButtonManager>();
            }
            if (keyButton == null) return;
            keyButton.onClick.RemoveAllListeners();
            if (resetRow) keyButton.onClick.AddListener(ResetAll);
            else keyButton.onClick.AddListener(StartRebind);
        }

        void OnEnable() { if (!resetRow) { rows.Add(this); Refresh(); } }

        void OnDisable() { rows.Remove(this); Cancel(); }

        /// <summary>Settings → Controls → Reset: back to the SHIFT//RUSH layout.</summary>
        public void ResetAll()
        {
            ControlBindings.ResetToDefaults();
            MenuSounds.Back();
            foreach (var r in rows) r.Refresh();
            if (resetRow && keyButton != null) keyButton.SetText("RESET");
        }

        void Show(string text)
        {
            if (keyButton != null) keyButton.SetText(text);
            else if (keyLabel != null) keyLabel.text = text;
        }

        public void Refresh()
        {
            var action = ControlBindings.Asset != null ? ControlBindings.Asset.FindAction(actionName) : null;
            int i = ControlBindings.FindKeyboardBinding(action, composite, part);
            if (i < 0) { Show("—"); return; }
            string shown = action.GetBindingDisplayString(i, InputBinding.DisplayStringOptions.DontIncludeInteractions);
            Show(string.IsNullOrEmpty(shown) ? "UNBOUND" : shown.ToUpperInvariant());
        }

        /// <summary>Hook this to the row's click.</summary>
        public void StartRebind()
        {
            if (operation != null) return;
            var asset = ControlBindings.Asset;
            var action = asset != null ? asset.FindAction(actionName) : null;
            int i = ControlBindings.FindKeyboardBinding(action, composite, part);
            if (i < 0) return;

            bool wasEnabled = action.enabled;
            action.Disable();
            Show("PRESS A KEY");
            MenuSounds.Tick();

            listening++;
            operation = action.PerformInteractiveRebinding(i)
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithCancelingThrough("<Keyboard>/escape")
                .WithControlsExcluding("<Mouse>")
                .OnMatchWaitForAnother(0.08f)
                .OnComplete(op => Finish(action, wasEnabled, true))
                .OnCancel(op => Finish(action, wasEnabled, false))
                .Start();
        }

        void Finish(InputAction action, bool wasEnabled, bool changed)
        {
            operation.Dispose();
            operation = null;
            listening = Mathf.Max(0, listening - 1);
            lastFinishFrame = Time.frameCount;
            if (wasEnabled) action.Enable();
            if (changed) { ControlBindings.Save(); MenuSounds.Select(); }
            else MenuSounds.Back();
            Refresh();
        }

        void Cancel()
        {
            if (operation != null) operation.Cancel();
        }
    }
}
