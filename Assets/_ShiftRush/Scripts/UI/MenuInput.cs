using UnityEngine;
using UnityEngine.InputSystem;

namespace ShiftRush.UI
{
    /// <summary>Keyboard and gamepad reads shared by the menu screens (edge-triggered, this frame only).</summary>
    public static class MenuInput
    {
        public static bool Up { get { return Pressed(Key.UpArrow, Key.W) || Pad(p => p.dpad.up.wasPressedThisFrame); } }
        public static bool Down { get { return Pressed(Key.DownArrow, Key.S) || Pad(p => p.dpad.down.wasPressedThisFrame); } }
        public static bool Left { get { return Pressed(Key.LeftArrow, Key.A) || Pad(p => p.dpad.left.wasPressedThisFrame || p.leftShoulder.wasPressedThisFrame); } }
        public static bool Right { get { return Pressed(Key.RightArrow, Key.D) || Pad(p => p.dpad.right.wasPressedThisFrame || p.rightShoulder.wasPressedThisFrame); } }
        public static bool Submit { get { return Pressed(Key.Enter, Key.Space) || Pressed(Key.NumpadEnter, Key.None) || Pad(p => p.buttonSouth.wasPressedThisFrame); } }

        /// <summary>A number or letter key, for jumping straight to a gear ("1", "2", "R").</summary>
        public static bool KeyPressed(string label)
        {
            var kb = Keyboard.current;
            if (kb == null || string.IsNullOrEmpty(label)) return false;
            char c = char.ToUpperInvariant(label[0]);
            if (c >= '0' && c <= '9')
            {
                // The Key enum runs Digit1..Digit9 then Digit0; the numpad runs Numpad0..Numpad9.
                Key row = c == '0' ? Key.Digit0 : Key.Digit1 + (c - '1');
                return kb[row].wasPressedThisFrame || kb[Key.Numpad0 + (c - '0')].wasPressedThisFrame;
            }
            if (c >= 'A' && c <= 'Z')
                return kb[Key.A + (c - 'A')].wasPressedThisFrame;
            return false;
        }

        static bool Pressed(Key a, Key b)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            return kb[a].wasPressedThisFrame || (b != Key.None && kb[b].wasPressedThisFrame);
        }

        static bool Pad(System.Func<Gamepad, bool> read)
        {
            var p = Gamepad.current;
            return p != null && read(p);
        }
    }
}
