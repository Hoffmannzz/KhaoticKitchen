namespace KitchenChaos.Players
{
    /// <summary>
    /// The device used by a local co-op player.
    /// </summary>
    public enum SeatDevice
    {
        /// <summary>WASD to move, E to pick/drop, Q to chop/cook.</summary>
        KeyboardLeft,
        /// <summary>Arrows to move, Period/Right Ctrl to pick/drop, Slash/Right Shift to chop/cook.</summary>
        KeyboardRight,
        /// <summary>Stick/D-Pad to move, South to pick/drop, West to chop/cook.</summary>
        Gamepad
    }
}
