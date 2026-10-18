using UnityEngine;
using UnityEngine.InputSystem;

namespace KnockoutBalance
{
    /// <summary>Input adapter supporting both the Unity Input System package and legacy Input.</summary>
    public sealed class PlayerInputHandler : MonoBehaviour, IPlayerInput
    {
        [SerializeField] private bool isPlayerOne = true;
        [SerializeField, Range(0f, 0.95f)] private float controllerDeadZone = 0.2f;

        public float MoveInput { get; private set; }
        public bool IsPlayerOne => isPlayerOne;
        public bool JumpPressed { get; private set; }
        public bool JumpReleased { get; private set; }
        public bool PunchPressed { get; private set; }
        public bool BlockHeld { get; private set; }
        public bool DashPressed { get; private set; }

        public void Configure(bool playerOne) => isPlayerOne = playerOne;

        private void Update()
        {
            float keyboardMove = 0f;
            bool jumpDown = false;
            bool jumpUp = false;
            bool punchDown = false;
            bool block = false;
            bool dashDown = false;

            // 1. New Input System Keyboard
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (isPlayerOne)
                {
                    if (keyboard.dKey.isPressed) keyboardMove += 1f;
                    if (keyboard.aKey.isPressed) keyboardMove -= 1f;

                    jumpDown |= keyboard.wKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame;
                    jumpUp |= keyboard.wKey.wasReleasedThisFrame || keyboard.spaceKey.wasReleasedThisFrame;
                    punchDown |= keyboard.jKey.wasPressedThisFrame || keyboard.fKey.wasPressedThisFrame;
                    block |= keyboard.kKey.isPressed || keyboard.gKey.isPressed;
                    dashDown |= keyboard.qKey.wasPressedThisFrame || keyboard.leftShiftKey.wasPressedThisFrame;
                }
                else
                {
                    if (keyboard.rightArrowKey.isPressed) keyboardMove += 1f;
                    if (keyboard.leftArrowKey.isPressed) keyboardMove -= 1f;

                    jumpDown |= keyboard.upArrowKey.wasPressedThisFrame;
                    jumpUp |= keyboard.upArrowKey.wasReleasedThisFrame;
                    punchDown |= keyboard.numpad1Key.wasPressedThisFrame || keyboard.digit1Key.wasPressedThisFrame;
                    block |= keyboard.numpad2Key.isPressed || keyboard.digit2Key.isPressed;
                    dashDown |= keyboard.numpad0Key.wasPressedThisFrame || keyboard.digit0Key.wasPressedThisFrame;
                }
            }

            // 2. New Input System Gamepad
            float controllerMove = 0f;
            Gamepad gamepad = null;
            if (isPlayerOne && Gamepad.all.Count > 0)
                gamepad = Gamepad.all[0];
            else if (!isPlayerOne && Gamepad.all.Count > 1)
                gamepad = Gamepad.all[1];

            if (gamepad != null)
            {
                float stick = gamepad.leftStick.x.ReadValue();
                float dpad = gamepad.dpad.x.ReadValue();
                float val = Mathf.Abs(dpad) > Mathf.Abs(stick) ? dpad : stick;
                controllerMove = Mathf.Abs(val) < controllerDeadZone ? 0f : Mathf.Clamp(val, -1f, 1f);

                jumpDown |= gamepad.buttonSouth.wasPressedThisFrame || gamepad.buttonEast.wasPressedThisFrame;
                jumpUp |= gamepad.buttonSouth.wasReleasedThisFrame || gamepad.buttonEast.wasReleasedThisFrame;
                punchDown |= gamepad.buttonWest.wasPressedThisFrame;
                block |= gamepad.buttonNorth.isPressed || gamepad.rightTrigger.isPressed || gamepad.leftTrigger.isPressed;
                dashDown |= gamepad.rightShoulder.wasPressedThisFrame || gamepad.leftShoulder.wasPressedThisFrame;
            }

            // 3. Safe fallback for Legacy Input Manager (only when enabled and active)
            #if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                if (keyboardMove == 0f && controllerMove == 0f)
                {
                    KeyCode left = isPlayerOne ? KeyCode.A : KeyCode.LeftArrow;
                    KeyCode right = isPlayerOne ? KeyCode.D : KeyCode.RightArrow;
                    float legKb = (Input.GetKey(right) ? 1f : 0f) - (Input.GetKey(left) ? 1f : 0f);
                    float legCtrl = ReadLegacyControllerMove();
                    float legacyMove = Mathf.Abs(legKb) > 0f ? legKb : legCtrl;
                    if (Mathf.Abs(legacyMove) > 0f) keyboardMove = legacyMove;

                    KeyCode jumpKey = isPlayerOne ? KeyCode.W : KeyCode.UpArrow;
                    KeyCode punchKey = isPlayerOne ? KeyCode.J : KeyCode.Keypad1;
                    KeyCode blockKey = isPlayerOne ? KeyCode.K : KeyCode.Keypad2;
                    KeyCode dashKey = isPlayerOne ? KeyCode.Q : KeyCode.Keypad0;

                    jumpDown |= Input.GetKeyDown(jumpKey) || Input.GetKeyDown(LegacyControllerButton(1));
                    jumpUp |= Input.GetKeyUp(jumpKey) || Input.GetKeyUp(LegacyControllerButton(1));
                    punchDown |= Input.GetKeyDown(punchKey) || Input.GetKeyDown(LegacyControllerButton(0));
                    block |= Input.GetKey(blockKey) || Input.GetKey(LegacyControllerButton(3));
                    dashDown |= Input.GetKeyDown(dashKey) || Input.GetKeyDown(LegacyControllerButton(2));
                }
            }
            catch (System.InvalidOperationException)
            {
                // Thrown if activeInputHandler is New Input System only; safely ignored
            }
            #endif

            MoveInput = Mathf.Abs(keyboardMove) > 0f ? keyboardMove : controllerMove;
            JumpPressed = jumpDown;
            JumpReleased = jumpUp;
            PunchPressed = punchDown;
            BlockHeld = block;
            DashPressed = dashDown;
        }

        #if ENABLE_LEGACY_INPUT_MANAGER
        private KeyCode LegacyControllerButton(int button)
        {
            return (KeyCode)((isPlayerOne ? (int)KeyCode.Joystick1Button0 : (int)KeyCode.Joystick2Button0) + button);
        }

        private float ReadLegacyControllerMove()
        {
            string stick = isPlayerOne ? "P1_StickHorizontal" : "P2_StickHorizontal";
            string dpad = isPlayerOne ? "P1_DPadHorizontal" : "P2_DPadHorizontal";
            float stickValue = SafeAxis(stick);
            float dpadValue = SafeAxis(dpad);
            float value = Mathf.Abs(dpadValue) > Mathf.Abs(stickValue) ? dpadValue : stickValue;
            return Mathf.Abs(value) < controllerDeadZone ? 0f : Mathf.Clamp(value, -1f, 1f);
        }

        private static float SafeAxis(string axisName)
        {
            try { return Input.GetAxisRaw(axisName); }
            catch { return 0f; }
        }
        #endif
    }
}
