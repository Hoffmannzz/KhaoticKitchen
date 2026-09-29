using UnityEngine;

namespace KitchenChaos.Players
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Player))]
    public sealed class PlayerInputHandler : MonoBehaviour
    {
        [SerializeField] private Player player;
        [SerializeField] private PlayerInputSettings settings;

        private IPlayerInputSource source;
        private IPlayerInputSource boundSource;

        /// <summary>
        /// The input currently driving this Player.
        /// Uses the shared <see cref="PlayerInputSettings"/> unless another source was set.
        /// </summary>
        public IPlayerInputSource Source => source ?? settings;

        private void Reset() => player = GetComponent<Player>();

        private void OnEnable()
        {
            UnBind();
            Bind();
        }

        private void OnDisable() => UnBind();

        /// <summary>
        /// Drives this Player using the given source (a local co-op seat, for example).
        /// </summary>
        /// <param name="newSource">The new input source. Null restores the shared input.</param>
        public void SetSource(IPlayerInputSource newSource)
        {
            UnBind();
            source = newSource;
            if (isActiveAndEnabled) Bind();
        }

        private void Bind()
        {
            boundSource = Source;

            boundSource.OnMove += player.Motor.Move;
            boundSource.OnCollectItem += player.Interactor.TryInteractWithItem;
            boundSource.OnInteractWithEnvironment += player.Interactor.TryInteractWithEnvironment;
        }

        private void UnBind()
        {
            if (boundSource == null) return;

            boundSource.OnMove -= player.Motor.Move;
            boundSource.OnCollectItem -= player.Interactor.TryInteractWithItem;
            boundSource.OnInteractWithEnvironment -= player.Interactor.TryInteractWithEnvironment;

            boundSource = null;
        }
    }
}
