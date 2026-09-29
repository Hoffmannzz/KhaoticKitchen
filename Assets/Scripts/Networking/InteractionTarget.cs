using ActionCode.Physics;
using KitchenChaos.Items;

namespace KitchenChaos.Networking
{
    public enum InteractionKind : byte
    {
        /// <summary>Pick up, drop, plate or deliver items.</summary>
        Item = 0,
        /// <summary>Chop or cook.</summary>
        Environment = 1
    }

    /// <summary>
    /// What a chef's detector found when the player pressed a button.
    /// <para>
    /// The machine controlling the chef captures it, so every machine runs the
    /// interaction against the same counters and items the player saw highlighted.
    /// </para>
    /// </summary>
    public struct InteractionTarget
    {
        public bool HasHit;
        public int Collector;
        public int Collectable;
        public int Transfer;
        public int Holder;
        public int Disposer;
        public int Interactable;

        public bool TryGetCollector(out IItemCollector component) => TryGet(Collector, out component);
        public bool TryGetCollectable(out IItemCollectable component) => TryGet(Collectable, out component);
        public bool TryGetTransfer(out IItemTransfer component) => TryGet(Transfer, out component);
        public bool TryGetHolder(out IItemHolder component) => TryGet(Holder, out component);
        public bool TryGetDisposer(out IItemDisposer component) => TryGet(Disposer, out component);
        public bool TryGetInteractable(out IInteractable component) => TryGet(Interactable, out component);

        internal void Write(NetWriter writer)
        {
            writer.Write(HasHit);
            writer.Write(Collector);
            writer.Write(Collectable);
            writer.Write(Transfer);
            writer.Write(Holder);
            writer.Write(Disposer);
            writer.Write(Interactable);
        }

        internal static InteractionTarget Read(NetReader reader) => new InteractionTarget
        {
            HasHit = reader.ReadBool(),
            Collector = reader.ReadInt(),
            Collectable = reader.ReadInt(),
            Transfer = reader.ReadInt(),
            Holder = reader.ReadInt(),
            Disposer = reader.ReadInt(),
            Interactable = reader.ReadInt()
        };

        private static bool TryGet<T>(int id, out T component) where T : class, IEnable
        {
            var found =
                NetworkEntityRegistry.TryGet(id, out component) &&
                component.IsEnabled &&
                !IsHeldItem(component);

            if (!found) component = null;
            return found;
        }

        /// <summary>
        /// An item may have been picked up by another chef since the target was captured.
        /// Using it would leave two chefs holding the same item.
        /// </summary>
        private static bool IsHeldItem(object component) =>
            component is AbstractItem item &&
            item.transform.parent != null &&
            item.GetComponentInParent<ItemHandler>() != null;
    }
}
