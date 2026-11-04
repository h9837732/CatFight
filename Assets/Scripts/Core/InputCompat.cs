using UnityEngine;
using UnityEngine.InputSystem;

namespace CatFight.Core
{
    /// <summary>
    /// 输入封装。项目启用的是新版 Input System（PlayerSettings.activeInputHandler = 1），
    /// 旧的 `UnityEngine.Input` API 在此项目里会直接抛异常，因此所有输入都走这里。
    /// 后续接手柄时只扩展本类即可。
    /// </summary>
    public static class InputCompat
    {
        public static bool KeyDown(Key key)
            => Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;

        public static bool KeyHeld(Key key)
            => Keyboard.current != null && Keyboard.current[key].isPressed;

        /// <summary>滚轮（已归一化：一格 ≈ 1）。</summary>
        public static float ScrollY
        {
            get
            {
                if (Mouse.current == null) return 0f;
                return Mouse.current.scroll.ReadValue().y / 120f;
            }
        }

        /// <summary>纵向输入（W/S、上下箭头、手柄左摇杆 Y）。</summary>
        public static float VerticalAxis
        {
            get
            {
                float v = 0f;
                if (KeyHeld(Key.W) || KeyHeld(Key.UpArrow)) v += 1f;
                if (KeyHeld(Key.S) || KeyHeld(Key.DownArrow)) v -= 1f;

                var pad = Gamepad.current;
                if (pad != null && Mathf.Abs(v) < 0.01f)
                {
                    float ly = pad.leftStick.ReadValue().y;
                    if (Mathf.Abs(ly) > 0.2f) v = ly;
                }
                return v;
            }
        }

        /// <summary>手柄是否按下"确认"。</summary>
        public static bool GamepadConfirm
            => Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
    }
}
