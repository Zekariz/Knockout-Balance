using UnityEngine;
using UnityEngine.InputSystem;

namespace KnockoutBalance
{
    /// <summary>
    /// Keeps keyboard input on the Legacy Input Manager while reading an
    /// explicitly assigned gamepad through the New Input System.
    /// </summary>
    public sealed class PlayerInputHandler : MonoBehaviour, IPlayerInput
    {
        [SerializeField] private bool isPlayerOne = true;
        [SerializeField] private InputActionAsset controllerActions;

        private InputActionAsset runtimeActions;
        private InputAction moveAction;
        private InputAction jumpAction;
        private InputAction punchAction;
        private InputAction dashAction;
        private InputAction blockAction;
        private Gamepad assignedGamepad;

        public float MoveInput { get; private set; }
        public bool IsPlayerOne => isPlayerOne;
        public bool JumpPressed { get; private set; }
        public bool JumpReleased { get; private set; }
        public bool PunchPressed { get; private set; }
        public bool BlockHeld { get; private set; }
        public bool DashPressed { get; private set; }

        public void Configure(bool playerOne) => isPlayerOne = playerOne;

        private void Awake()
        {
            if (controllerActions == null)
                controllerActions = Resources.Load<InputActionAsset>("KnockoutBalanceControls");

            if (controllerActions == null)
            {
                Debug.LogError("KnockoutBalanceControls.inputactions is missing. Run Knockout Balance > Install Gameplay Components.", this);
                return;
            }

            runtimeActions = Instantiate(controllerActions);
            InputActionMap map = runtimeActions.FindActionMap("Gamepad", true);
            moveAction = map.FindAction("Move", true);
            jumpAction = map.FindAction("Jump", true);
            punchAction = map.FindAction("Punch", true);
            dashAction = map.FindAction("Dash", true);
            blockAction = map.FindAction("Block", true);
            RefreshAssignedGamepad();
        }

        private void OnEnable()
        {
            InputSystem.onDeviceChange += OnDeviceChange;
            RefreshAssignedGamepad();
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            if (runtimeActions != null) runtimeActions.Disable();
        }

        private void OnDestroy()
        {
            if (runtimeActions != null) Destroy(runtimeActions);
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device is Gamepad) RefreshAssignedGamepad();
        }

        private void RefreshAssignedGamepad()
        {
            if (runtimeActions == null) return;

            int gamepadIndex = isPlayerOne ? 0 : 1;
            Gamepad desired = Gamepad.all.Count > gamepadIndex ? Gamepad.all[gamepadIndex] : null;
            if (desired == assignedGamepad && (desired == null || runtimeActions.enabled)) return;

            runtimeActions.Disable();
            assignedGamepad = desired;
            if (assignedGamepad == null) return;

            // Each player owns a cloned action asset restricted to exactly one
            // device, preventing both players from responding to Gamepad 1.
            runtimeActions.devices = new InputDevice[] { assignedGamepad };
            runtimeActions.Enable();
        }

        private void Update()
        {
            // Gamepad order can change after a removal. Re-evaluate so Player 1
            // takes the first remaining pad and Player 2 takes the second.
            RefreshAssignedGamepad();

            float keyboardMove = 0f;
            bool keyboardJumpPressed = false;
            bool keyboardJumpReleased = false;
            bool keyboardPunchPressed = false;
            bool keyboardDashPressed = false;
            bool keyboardBlockHeld = false;

#if ENABLE_LEGACY_INPUT_MANAGER
            KeyCode left = isPlayerOne ? KeyCode.A : KeyCode.LeftArrow;
            KeyCode right = isPlayerOne ? KeyCode.D : KeyCode.RightArrow;
            KeyCode jump = isPlayerOne ? KeyCode.W : KeyCode.UpArrow;
            KeyCode punch = isPlayerOne ? KeyCode.J : KeyCode.Keypad1;
            KeyCode dash = isPlayerOne ? KeyCode.Q : KeyCode.Keypad0;
            KeyCode block = isPlayerOne ? KeyCode.K : KeyCode.Keypad2;

            keyboardMove = (Input.GetKey(right) ? 1f : 0f) - (Input.GetKey(left) ? 1f : 0f);
            keyboardJumpPressed = Input.GetKeyDown(jump);
            keyboardJumpReleased = Input.GetKeyUp(jump);
            keyboardPunchPressed = Input.GetKeyDown(punch);
            keyboardDashPressed = Input.GetKeyDown(dash);
            keyboardBlockHeld = Input.GetKey(block);
#endif

            bool gamepadReady = assignedGamepad != null && runtimeActions != null && runtimeActions.enabled;
            float gamepadMove = gamepadReady ? moveAction.ReadValue<float>() : 0f;

            MoveInput = Mathf.Abs(keyboardMove) > 0.01f ? keyboardMove : gamepadMove;
            JumpPressed = keyboardJumpPressed || (gamepadReady && jumpAction.WasPressedThisFrame());
            JumpReleased = keyboardJumpReleased || (gamepadReady && jumpAction.WasReleasedThisFrame());
            PunchPressed = keyboardPunchPressed || (gamepadReady && punchAction.WasPressedThisFrame());
            DashPressed = keyboardDashPressed || (gamepadReady && dashAction.WasPressedThisFrame());
            BlockHeld = keyboardBlockHeld || (gamepadReady && blockAction.IsPressed());
        }
    }
}
