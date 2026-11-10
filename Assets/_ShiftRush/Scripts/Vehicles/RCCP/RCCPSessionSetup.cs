using UnityEngine;

namespace ShiftRush.Vehicles
{
    /// <summary>
    /// Puts Realistic Car Controller Pro into SHIFT//RUSH mode for this scene: applies our keyboard layout and
    /// the player's rebinds (see <see cref="ControlBindings"/>). Handling uses RCCP's "Arcade" preset, chosen
    /// per car on the prefab (Use Custom Behavior).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class RCCPSessionSetup : MonoBehaviour
    {
        void Awake()
        {
            ControlBindings.Apply();
        }
    }
}
