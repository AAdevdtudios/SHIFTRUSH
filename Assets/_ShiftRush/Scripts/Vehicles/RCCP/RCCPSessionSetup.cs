using UnityEngine;
using UnityEngine.InputSystem;

namespace ShiftRush.Vehicles
{
    /// <summary>
    /// Puts Realistic Car Controller Pro into SHIFT//RUSH mode for this scene, at runtime only, so RCCP's own
    /// (git-ignored) assets never need editing:
    ///  - frees Left Shift (RCCP's gear-up) for the SHIFT selector, and Left Ctrl (gear-down) for nitro,
    ///  - moves RCCP's NOS from F to Left Ctrl, frees R (RCCP's record) for our reset.
    /// Handling uses RCCP's "Arcade" preset, chosen per car on the prefab (Use Custom Behavior).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class RCCPSessionSetup : MonoBehaviour
    {
        static readonly string[] UnbindKeyboard = { "Gear Shift Up", "Gear Shift Down", "Record", "Replay", "Feature Lab" };

        void Awake()
        {
            var settings = RCCP_InputActions.Instance;
            if (settings == null || settings.inputActions == null)
            {
                Debug.LogWarning("[SHIFT//RUSH] RCCP input actions not found; controls not remapped.");
                return;
            }

            var asset = settings.inputActions;
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
        }

    }
}
