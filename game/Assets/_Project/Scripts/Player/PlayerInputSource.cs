using UnityEngine;
using UnityEngine.InputSystem;

namespace Tycoon.Player
{
    /// <summary>
    /// Merges the on-screen joystick with a keyboard fallback.
    ///
    /// The keyboard path exists so the game can be driven and tested in a desktop browser
    /// during development; on the phone only the joystick ever writes here.
    /// </summary>
    public static class PlayerInputSource
    {
        /// <summary>Written every frame by <see cref="Tycoon.UI.VirtualJoystick"/>.</summary>
        public static Vector2 Joystick;

        public static Vector2 Read()
        {
            Vector2 value = Joystick;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                Vector2 keys = Vector2.zero;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) keys.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) keys.y -= 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) keys.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) keys.x += 1f;
                if (keys.sqrMagnitude > 0.01f) value = keys;
            }

            return Vector2.ClampMagnitude(value, 1f);
        }
    }
}
