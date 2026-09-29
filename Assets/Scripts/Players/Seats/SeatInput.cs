using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KitchenChaos.Players
{
    /// <summary>
    /// Input for a single local co-op player, restricted to one device (or one half of the keyboard).
    /// </summary>
    public sealed class SeatInput : IPlayerInputSource, IDisposable
    {
        public event Action<Vector2> OnMove;
        public event Action OnCollectItem;
        public event Action OnInteractWithEnvironment;

        private readonly InputActionMap map;

        public SeatInput(SeatDevice device, Gamepad gamepad)
        {
            map = new InputActionMap("Seat");

            var move = map.AddAction("Move", InputActionType.Value, processors: "StickDeadzone(min=0.5)", expectedControlLayout: "Vector2");
            var collect = map.AddAction("CollectItem", InputActionType.Button);
            var interact = map.AddAction("InteractWithEnvironment", InputActionType.Button);

            switch (device)
            {
                case SeatDevice.KeyboardLeft:
                    AddKeys(move, "w", "s", "a", "d");
                    collect.AddBinding("<Keyboard>/e");
                    interact.AddBinding("<Keyboard>/q");
                    break;

                case SeatDevice.KeyboardRight:
                    AddKeys(move, "upArrow", "downArrow", "leftArrow", "rightArrow");
                    collect.AddBinding("<Keyboard>/period");
                    collect.AddBinding("<Keyboard>/rightCtrl");
                    interact.AddBinding("<Keyboard>/slash");
                    interact.AddBinding("<Keyboard>/rightShift");
                    break;

                case SeatDevice.Gamepad:
                    move.AddBinding("<Gamepad>/leftStick");
                    move.AddCompositeBinding("2DVector").
                        With("up", "<Gamepad>/dpad/up").
                        With("down", "<Gamepad>/dpad/down").
                        With("left", "<Gamepad>/dpad/left").
                        With("right", "<Gamepad>/dpad/right");
                    collect.AddBinding("<Gamepad>/buttonSouth");
                    interact.AddBinding("<Gamepad>/buttonWest");
                    break;
            }

            if (gamepad != null) map.devices = new InputDevice[] { gamepad };
            else if (Keyboard.current != null) map.devices = new InputDevice[] { Keyboard.current };

            move.started += HandleMove;
            move.performed += HandleMove;
            move.canceled += HandleMove;
            collect.performed += HandleCollectItem;
            interact.performed += HandleInteractWithEnvironment;
        }

        public void Enable() => map.Enable();
        public void Disable() => map.Disable();

        public void Dispose()
        {
            map.Disable();
            map.Dispose();
        }

        private static void AddKeys(InputAction action, string up, string down, string left, string right)
        {
            action.AddCompositeBinding("2DVector").
                With("up", "<Keyboard>/" + up).
                With("down", "<Keyboard>/" + down).
                With("left", "<Keyboard>/" + left).
                With("right", "<Keyboard>/" + right);
        }

        private void HandleMove(InputAction.CallbackContext context) => OnMove?.Invoke(context.ReadValue<Vector2>());
        private void HandleCollectItem(InputAction.CallbackContext _) => OnCollectItem?.Invoke();
        private void HandleInteractWithEnvironment(InputAction.CallbackContext _) => OnInteractWithEnvironment?.Invoke();
    }
}
