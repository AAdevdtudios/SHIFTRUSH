using UnityEngine;
using UnityEngine.InputSystem;

namespace ShiftRush.Vehicles
{
    /// <summary>
    /// SHIFT//RUSH's keyboard layout on top of RCC Pro's input actions, applied at runtime so RCCP's own
    /// (git-ignored) asset is never edited:
    ///  - frees Left Shift (RCCP's gear-up) for the SHIFT selector and Left Ctrl (gear-down) for nitro,
    ///  - moves nitro from F to Left Ctrl, frees R (RCCP's record) for our reset,
    ///  - then layers the player's own rebinds from Settings on top (saved in PlayerPrefs).
    /// </summary>
    public static class ControlBindings
    {
        const string SaveKey = "SR_Bindings";
        static readonly string[] UnbindKeyboard = { "Gear Shift Up", "Gear Shift Down", "Record", "Replay", "Feature Lab" };

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

            foreach (var name in UnbindKeyboard)
            {
                var action = asset.FindAction(name);
                if (action == null) continue;
                for (int i = 0; i < action.bindings.Count; i++)
                    if (action.bindings[i].path.StartsWith("<Keyboard>"))
                        action.ApplyBindingOverride(i, string.Empty);
            }

            var nos = asset.FindAction("NOS");
            if (nos != null)
                for (int i = 0; i < nos.bindings.Count; i++)
                    if (nos.bindings[i].path.StartsWith("<Keyboard>"))
                        nos.ApplyBindingOverride(i, "<Keyboard>/leftCtrl");

            string saved = PlayerPrefs.GetString(SaveKey, "");
            if (!string.IsNullOrEmpty(saved)) asset.LoadBindingOverridesFromJson(saved, false);
            return true;
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
