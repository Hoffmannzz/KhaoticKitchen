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
        public bool hasHit;
        public int collector;
        public int collectable;
        public int transfer;
        public int holder;
        public int disposer;
        public int interactable;

        public bool TryGetCollector(out IItemCollector component) => TryGet(collector, out component);
        public bool TryGetCollectable(out IItemCollectable component) => TryGet(collectable, out component);
        public bool TryGetTransfer(out IItemTransfer component) => TryGet(transfer, out component);
        public bool TryGetHolder(out IItemHolder component) => TryGet(holder, out component);
        public bool TryGetDisposer(out IItemDisposer component) => TryGet(disposer, out component);
        public bool TryGetInteractable(out IInteractable component) => TryGet(interactable, out component);

        internal void Write(NetWriter writer)
        {
            writer.Write(hasHit);
            writer.Write(collector);
            writer.Write(collectable);
            writer.Write(transfer);
            writer.Write(holder);
            writer.Write(disposer);
            writer.Write(interactable);
        }

        internal static InteractionTarget Read(NetReader reader) => new InteractionTarget
        {
            hasHit = reader.ReadBool(),
            collector = reader.ReadInt(),
            collectable = reader.ReadInt(),
            transfer = reader.ReadInt(),
            holder = reader.ReadInt(),
            disposer = reader.ReadInt(),
            interactable = reader.ReadInt()
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
