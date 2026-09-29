using UnityEngine;
using KitchenChaos.Networking;

namespace KitchenChaos.Physics
{
    /// <summary>
    /// Component representing a Rigidbody able to be collected.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CollectableBody : MonoBehaviour, ICollectable
    {
        [SerializeField] private Rigidbody body;

        private void Reset() => body = GetComponent<Rigidbody>();

        public void PickUp(Transform holder)
        {
            EnableRigidbody(false);

            transform.SetParent(holder);
            transform.SetPositionAndRotation(
                holder.position,
                holder.rotation
            );
        }

        public void Drop()
        {
            transform.SetParent(null);

            // Online clients do not simulate loose items: the host sends their positions.
            if (NetworkGame.IsClient) FollowNetwork();
            else EnableRigidbody(true);
        }

        /// <summary>
        /// Stops simulating this body so its position can be set by the network.
        /// </summary>
        internal void FollowNetwork()
        {
            body.isKinematic = true;
            body.detectCollisions = true;
        }

        private void EnableRigidbody(bool enabled)
        {
            body.isKinematic = !enabled;
            body.detectCollisions = enabled;
        }
    }
}