using UnityEngine;
using ActionCode.Audio;
using ActionCode.Physics;
using KitchenChaos.Networking;

namespace KitchenChaos.Items
{
    [DisallowMultipleComponent]
    public sealed class ItemHandler : MonoBehaviour
    {
        [SerializeField] private ItemHolder holder;
        [SerializeField] private BoxCaster detector;

        [Header("Audio")]
        [SerializeField] private AudioSourceDictionary dropSource;
        [SerializeField] private AudioSourceDictionary pickupSource;

        /// <summary>
        /// When set, what the detector found on the machine controlling this chef (online matches).
        /// </summary>
        private InteractionTarget? target;

        private void Reset()
        {
            holder = GetComponentInChildren<ItemHolder>();
            detector = GetComponentInChildren<BoxCaster>();
        }

        public bool IsHoldingPlate() => holder.IsPlate(out Plate _);

        public void TryInteractWithItem()
        {
            if (NetworkGame.TryRouteInteraction(this, InteractionKind.Item)) return;
            InteractWithItem();
        }

        public void TryInteractWithEnvironment()
        {
            if (NetworkGame.TryRouteInteraction(this, InteractionKind.Environment)) return;
            InteractWithEnvironment();
        }

        /// <summary>
        /// Captures what the detector is currently finding, so the interaction can run on other machines.
        /// </summary>
        internal InteractionTarget CaptureTarget() => new InteractionTarget
        {
            HasHit = detector.HasHit,
            Collector = detector.TryGetEnabledComponent(out IItemCollector collector) ? NetworkEntityRegistry.GetId(collector) : NetworkEntityRegistry.None,
            Collectable = detector.TryGetEnabledComponent(out IItemCollectable collectable) ? NetworkEntityRegistry.GetId(collectable) : NetworkEntityRegistry.None,
            Transfer = detector.TryGetEnabledComponent(out IItemTransfer transfer) ? NetworkEntityRegistry.GetId(transfer) : NetworkEntityRegistry.None,
            Holder = detector.TryGetEnabledComponent(out IItemHolder itemHolder) ? NetworkEntityRegistry.GetId(itemHolder) : NetworkEntityRegistry.None,
            Disposer = detector.TryGetEnabledComponent(out IItemDisposer disposer) ? NetworkEntityRegistry.GetId(disposer) : NetworkEntityRegistry.None,
            Interactable = detector.TryGetEnabledComponent(out IInteractable interactable) ? NetworkEntityRegistry.GetId(interactable) : NetworkEntityRegistry.None
        };

        /// <summary>
        /// Runs an interaction against the given target instead of the detector.
        /// </summary>
        internal void Interact(InteractionKind kind, InteractionTarget interactionTarget)
        {
            target = interactionTarget;

            try
            {
                if (kind == InteractionKind.Item) InteractWithItem();
                else InteractWithEnvironment();
            }
            finally
            {
                target = null;
            }
        }

        private void InteractWithItem()
        {
            if (holder.HasItem())
            {
                if (TryInteractWithPlate()) return;

                var wasItemTransfered = TryTransferItem();
                var canReleaseItemOnTheFloor = !wasItemTransfered && !HasHit();

                if (canReleaseItemOnTheFloor)
                {
                    holder.ReleaseItem();
                    dropSource.PlayRandom();
                }
            }
            else if (TryGetCollectableItem(out IItemCollectable item))
            {
                holder.PlaceItem(item);
                pickupSource.PlayRandom();
            }
        }

        private void InteractWithEnvironment()
        {
            var hasInteractable = TryGetTarget(out IInteractable interactable);
            if (hasInteractable) interactable.Interact();
        }

        private bool TryInteractWithPlate()
        {
            var isHoldingPlate = holder.IsPlate(out Plate plate);
            return
                isHoldingPlate &&
                (
                    TryDisposeLastPlateIngredient(plate) ||
                    TryPlateIngredient(plate)
                );
        }

        private bool TryDisposeLastPlateIngredient(Plate plate)
        {
            if (!plate.HasIngredients()) return false;

            var hasDisposer = TryGetTarget(out IItemDisposer disposer);
            if (hasDisposer && disposer.IsEnabled) disposer.Dispose(plate.RemoveLast());

            return hasDisposer;
        }

        private bool TryPlateIngredient(Plate plate)
        {
            var hasHolder = TryGetTarget(out IItemHolder holder);
            return hasHolder && plate.TryTransferItem(holder);
        }

        private bool TryTransferItem()
        {
            var hasTransfer = TryGetTarget(out IItemTransfer transfer);
            return hasTransfer && transfer.TryTransferItem(this.holder);
        }

        private bool TryGetCollectableItem(out IItemCollectable item)
        {
            var hasCollector = TryGetTarget(out IItemCollector collector);
            if (hasCollector) return collector.TryCollectItem(out item);

            return TryGetTarget(out item);
        }

        private bool HasHit() => target?.HasHit ?? detector.HasHit;

        private bool TryGetTarget(out IItemCollector component) =>
            target.HasValue ? target.Value.TryGetCollector(out component) : detector.TryGetEnabledComponent(out component);

        private bool TryGetTarget(out IItemCollectable component) =>
            target.HasValue ? target.Value.TryGetCollectable(out component) : detector.TryGetEnabledComponent(out component);

        private bool TryGetTarget(out IItemTransfer component) =>
            target.HasValue ? target.Value.TryGetTransfer(out component) : detector.TryGetEnabledComponent(out component);

        private bool TryGetTarget(out IItemHolder component) =>
            target.HasValue ? target.Value.TryGetHolder(out component) : detector.TryGetEnabledComponent(out component);

        private bool TryGetTarget(out IItemDisposer component) =>
            target.HasValue ? target.Value.TryGetDisposer(out component) : detector.TryGetEnabledComponent(out component);

        private bool TryGetTarget(out IInteractable component) =>
            target.HasValue ? target.Value.TryGetInteractable(out component) : detector.TryGetEnabledComponent(out component);
    }
}
