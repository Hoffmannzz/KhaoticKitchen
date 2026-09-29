using UnityEngine;
using KitchenChaos.Players;

namespace KitchenChaos.Networking
{
    /// <summary>
    /// Synchronizes a chef position during online matches.
    /// <para>
    /// The machine controlling the chef moves it with physics and sends its pose.
    /// Every other machine smoothly moves it to the last received pose.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class NetworkChef : MonoBehaviour
    {
        private const float smoothing = 15F;

        internal Player Player { get; private set; }
        internal bool IsLocal { get; private set; }

        private bool hasPose;
        private bool isWalking;
        private float targetYaw;
        private Vector3 targetPosition;

        internal void Initialize(Player player, bool isLocal)
        {
            Player = player;
            IsLocal = isLocal;

            if (isLocal) return;

            player.Motor.enabled = false;

            if (TryGetComponent(out Rigidbody body))
            {
                body.isKinematic = true;
                body.interpolation = RigidbodyInterpolation.None;
            }
        }

        internal void SetPose(Vector3 position, float yaw, bool walking)
        {
            if (IsLocal) return;

            targetPosition = position;
            targetYaw = yaw;

            if (!hasPose)
            {
                hasPose = true;
                transform.SetPositionAndRotation(position, Quaternion.Euler(0F, yaw, 0F));
            }

            if (walking != isWalking)
            {
                isWalking = walking;
                Player.Motor.SetWalkingAnimation(walking);
            }
        }

        internal void GetPose(out Vector3 position, out float yaw, out bool walking)
        {
            if (!IsLocal && hasPose)
            {
                position = targetPosition;
                yaw = targetYaw;
                walking = isWalking;
                return;
            }

            position = transform.position;
            yaw = transform.eulerAngles.y;
            walking = IsLocal && Player.Motor.IsMoveInputting;
        }

        private void Update()
        {
            if (IsLocal || !hasPose) return;

            var t = 1F - Mathf.Exp(-smoothing * Time.deltaTime);
            var position = Vector3.Lerp(transform.position, targetPosition, t);
            var rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0F, targetYaw, 0F), t);

            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
