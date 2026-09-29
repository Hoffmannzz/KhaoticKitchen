using UnityEngine.InputSystem;

namespace KitchenChaos.Players
{
    /// <summary>
    /// A local co-op player: the device it joined with and the input created for it.
    /// </summary>
    public sealed class LocalSeat
    {
        public SeatDevice Device { get; }
        public Gamepad Gamepad { get; }
        public SeatInput Input { get; }

        public LocalSeat(SeatDevice device, Gamepad gamepad = null)
        {
            Device = device;
            Gamepad = gamepad;
            Input = new SeatInput(device, gamepad);
        }

        public string GetDeviceName() => Device switch
        {
            SeatDevice.KeyboardLeft => "Keyboard (WASD)",
            SeatDevice.KeyboardRight => "Keyboard (Arrows)",
            _ => Gamepad != null ? Gamepad.displayName : "Gamepad"
        };

        public bool Uses(SeatDevice device, Gamepad gamepad) =>
            Device == device && (device != SeatDevice.Gamepad || Gamepad == gamepad);
    }
}
