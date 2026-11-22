using UnityEngine;
using UnityEngine.InputSystem;

namespace ShiftRush.Vehicles
{
    /// <summary>
    /// SHIFT//RUSH's keyboard layout on top of RCC Pro's input actions, applied at runtime so RCCP's own
    /// (git-ignored) asset is never edited:
    ///  - frees Left Shift (RCCP's gear-up) and Left Ctrl (gear-down) for nitro,
    ///  - moves nitro from F to Left Shift (the SHIFT key: nitro on the road, the timing key at a SHIFT ramp)
    ///    with Left Ctrl as a second nitro key, frees R (RCCP's record) for our reset,
    ///  - then layers the player's own rebinds from Settings on top (saved in PlayerPrefs).
    /// </summary>
    public static class ControlBindings
    {
        const string SaveKey = "SR_Bindings", VersionKey = "SR_BindingsVersion";
        // Q / E are Grip Pro's leap keys, so RCCP's indicators come off them.
        static readonly string[] UnbindKeyboard = { "Gear Shift Up", "Gear Shift Down", "Record", "Replay", "Feature Lab", "Indicator Left", "Indicator Right" };
        static readonly string[] UnbindGamepad = { "Gear Shift Up", "Gear Shift Down" };

        public static InputActionAsset Asset
        {
            get
            {
                var settings = RCCP_InputActions.Instance;
                return settings != null ? settings.inputActions : null;
            }
        }

        /// <summary>Defaults first, then the player's saved rebinds. Safe to call more than once.</summary>
        public static bool Apply()
        {
            var asset = Asset;
            if (asset == null)
            {
                Debug.LogWarning("[SHIFT//RUSH] RCCP input actions not found; controls not remapped.");
                return false;
            }

            asset.RemoveAllBindingOverrides();
            // RCCP keeps its own copy of rebinds ("rebinds") and loads it after us, which would undo our layout.
            // SHIFT//RUSH saves bindings itself (SR_Bindings), so RCCP's copy is dropped.
            PlayerPrefs.DeleteKey("rebinds");

            foreach (var name in UnbindKeyboard)
            {
                var action = asset.FindAction(name);
                if (action == null) continue;
                for (int i = 0; i < action.bindings.Count; i++)
                    if (action.bindings[i].path.StartsWith("<Keyboard>"))
                        action.ApplyBindingOverride(i, string.Empty);
            }
            // LB / RB are the leap on a gamepad: gear shifting comes off them (the gearbox is automatic).
            foreach (var name in UnbindGamepad)
            {
                var action = asset.FindAction(name);
                if (action == null) continue;
                for (int i = 0; i < action.bindings.Count; i++)
                    if (action.bindings[i].path.StartsWith("<Gamepad>"))
                        action.ApplyBindingOverride(i, string.Empty);
            }

            var nos = asset.FindAction("NOS");
            if (nos != null)
            {
                int keys = 0;
                for (int i = 0; i < nos.bindings.Count; i++)
                    if (nos.bindings[i].path.StartsWith("<Keyboard>"))
                        nos.ApplyBindingOverride(i, keys++ == 0 ? "<Keyboard>/leftShift" : "<Keyboard>/leftCtrl");
                if (keys < 2) AddSecondKey(nos, "<Keyboard>/leftCtrl");
                if (!nos.enabled && nos.actionMap != null && nos.actionMap.enabled) nos.Enable();
            }

            string saved = PlayerPrefs.GetString(SaveKey, "");
            if (!string.IsNullOrEmpty(saved)) asset.LoadBindingOverridesFromJson(saved, false);

            // v2: nitro moved from Left Ctrl to Shift. Saved settings from before still say Left Ctrl: move them once.
            if (PlayerPrefs.GetInt(VersionKey, 1) < 2)
            {
                if (nos != null)
                {
                    int k = FindKeyboardBinding(nos, "keyboard", "positive");
                    if (k < 0) k = FindKeyboardBinding(nos, null, null);
                    if (k >= 0 && nos.bindings[k].effectivePath == "<Keyboard>/leftCtrl") nos.ApplyBindingOverride(k, "<Keyboard>/leftShift");
                }
                PlayerPrefs.SetInt(VersionKey, 2);
                if (!string.IsNullOrEmpty(saved)) Save();
            }
            return true;
        }

        /// <summary>Left Ctrl as an extra nitro key, added once (runtime only; RCCP's asset file is untouched).</summary>
        static void AddSecondKey(InputAction action, string path)
        {
            foreach (var b in action.bindings) if (b.path == path) return;
            bool was = action.enabled;
            if (was) action.Disable();
            action.AddBinding(path);
            if (was) action.Enable();
        }

        /// <summary>Mutes or restores the nitro key (the SHIFT timing bar borrows Shift).</summary>
        public static void SetNitroInput(bool on)
        {
            var asset = Asset;
            var nos = asset != null ? asset.FindAction("NOS") : null;
            if (nos == null) return;
            if (on) { if (nos.actionMap == null || nos.actionMap.enabled) nos.Enable(); }
            else nos.Disable();
        }

        public static void Save()
        {
            var asset = Asset;
            if (asset != null) PlayerPrefs.SetString(SaveKey, asset.SaveBindingOverridesAsJson());
        }

        /// <summary>Forgets the player's rebinds and goes back to the SHIFT//RUSH defaults.</summary>
        public static void ResetToDefaults()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            Apply();
        }

        /// <summary>The key currently bound (e.g. "W", "LEFT SHIFT"), for on-screen hints; empty if unknown.</summary>
        public static string KeyName(string actionName, string composite, string part)
        {
            var asset = Asset;
            var action = asset != null ? asset.FindAction(actionName) : null;
            int i = FindKeyboardBinding(action, composite, part);
            if (i < 0) return "";
            return action.GetBindingDisplayString(i, InputBinding.DisplayStringOptions.DontIncludeInteractions).ToUpperInvariant();
        }

        /// <summary>
        /// Finds the keyboard binding for an action. For composites (WASD throttle/steering) pass the composite's
        /// name ("wasd", "keyboard") and the part ("positive", "negative"); for plain buttons pass nulls.
        /// </summary>
        public static int FindKeyboardBinding(InputAction action, string composite, string part)
        {
            if (action == null) return -1;
            var b = action.bindings;
            if (string.IsNullOrEmpty(composite))
            {
                for (int i = 0; i < b.Count; i++)
                    if (!b[i].isComposite && !b[i].isPartOfComposite && b[i].path.StartsWith("<Keyboard>")) return i;
                return -1;
            }
            for (int i = 0; i < b.Count; i++)
            {
                if (!b[i].isComposite || !string.Equals(b[i].name, composite, System.StringComparison.OrdinalIgnoreCase)) continue;
                for (int j = i + 1; j < b.Count && b[j].isPartOfComposite; j++)
                    if (string.Equals(b[j].name, part, System.StringComparison.OrdinalIgnoreCase)) return j;
            }
            return -1;
        }
    }
}
