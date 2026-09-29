using UnityEngine;
using ActionCode.Audio;
using KitchenChaos.Items;
using System.Collections;
using KitchenChaos.Networking;
using ActionCode.VisualEffects;

namespace KitchenChaos.Counters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animation))]
    [RequireComponent(typeof(HighlighterContainer))]
    [RequireComponent(typeof(AudioSourceDictionary))]
    public sealed class Trash : MonoBehaviour, IItemDisposer, IItemTransfer
    {
        [SerializeField] private ItemHolder holder;
        [SerializeField] private Animation animation;
        [SerializeField] private AudioSourceDictionary sourceDictionary;

        public bool IsEnabled
        {
            get => enabled;
            set => enabled = value;
        }

        private void Reset()
        {
            animation = GetComponent<Animation>();
            holder = GetComponentInChildren<ItemHolder>();
            sourceDictionary = GetComponent<AudioSourceDictionary>();
        }

        public bool TryTransferItem(IItemHolder fromHolder)
        {
            if (fromHolder.CurrentItem is Ingredient ingredient)
            {
                fromHolder.ReleaseItem();
                Dispose(ingredient);
                return true;
            }

            return false;
        }

        public void Dispose(IItemCollectable item)
        {
            holder.PlaceItem(item);
            StartCoroutine(PlayAnimationAndDestroyItem());
        }

        private IEnumerator PlayAnimationAndDestroyItem()
        {
            sourceDictionary.PlayRandom();

            animation.Play();
            yield return new WaitWhile(() => animation.isPlaying);
            animation.Stop();

            // Online clients wait for the host to destroy the item.
            if (NetworkGame.IsClient) yield break;

            NetworkGame.Replicate(NetEventType.TrashDestroyed, this, holder.DestroyItem);
        }

        /// <summary>
        /// Destroys the disposed item when the host says so (online clients only).
        /// </summary>
        internal void DestroyItemFromNetwork() => holder.DestroyItem();
    }
}