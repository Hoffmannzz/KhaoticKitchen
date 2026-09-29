using System;
using System.Collections;
using KitchenChaos.Items;
using KitchenChaos.Matches;
using KitchenChaos.Networking;
using UnityEngine;

namespace KitchenChaos.Counters
{
    [RequireComponent(typeof(Animation))]
    public abstract class AbstractIngredientPreparator : MonoBehaviour, IInteractable
    {
        [SerializeField] private MatchSettings matchSettings;
        [SerializeField] protected ItemHolder holder;
        [SerializeField] protected Animation animation;
        [SerializeField] protected IngredientStatus allowedStatus;
        [SerializeField] protected IngredientStatus preparatedStatus;
        [SerializeField] protected IngredientSettings ingredientSettings;
        [SerializeField, Range(1f, 2f)] protected float finalSecondsBooster = 1.25f;

        public event Action OnInteracted;
        public event Action OnIngredientRejected;
        public event Action OnPreparationStarted;
        public event Action OnPreparationCompleted;
        public event Action<float> OnPreparationUpdated;

        public event Action<IItemCollectable> OnItemPlaced
        {
            add => holder.OnItemPlaced += value;
            remove => holder.OnItemPlaced -= value;
        }

        public bool IsEnabled
        {
            get => enabled;
            set => enabled = value;
        }

        public bool IsPreparing { get; private set; }

        public bool IsPaused
        {
            get => isPaused;
            private set
            {
                isPaused = value;
                preparationTimeScale = IsPaused ? 0F : 1F;
            }
        }

        public Counter Counter { get; private set; }

        private bool isPaused;
        private float preparationBooster;
        private float preparationTimeScale;
        private Coroutine preparationRoutine;

        protected virtual void Reset()
        {
            SetupIngredientStatus();
            holder = GetComponentInChildren<ItemHolder>();
            animation = GetComponentInChildren<Animation>();
        }

        protected virtual void Awake()
        {
            Counter = GetComponentInParent<Counter>();
            PlayIdleAnimation();
            ResetPreparationBooster();
        }

        protected virtual void OnEnable()
        {
            matchSettings.TimeLimit.OnFinalSecondsStarted += HandleFinalSecondsStarted;
            matchSettings.TimeLimit.OnFinished += HandleTimeLimitFinished;
        }

        protected virtual void OnDisable()
        {
            matchSettings.TimeLimit.OnFinalSecondsStarted -= HandleFinalSecondsStarted;
            matchSettings.TimeLimit.OnFinished -= HandleTimeLimitFinished;
        }

        public virtual void Interact()
        {
            OnInteracted?.Invoke();
            TogglePreparation();
        }

        protected abstract void SetupIngredientStatus();
        protected abstract string GetIdleAnimationName();
        protected abstract string GetPreparationAnimationName();

        protected virtual void PlayIdleAnimation() => animation.Play(GetIdleAnimationName());
        protected virtual void PlayPreparationAnimation() => animation.Play(GetPreparationAnimationName());

        protected virtual void CompletePreparation()
        {
            IsPreparing = false;
            PlayIdleAnimation();
            OnPreparationCompleted?.Invoke();
        }

        private void TogglePreparation()
        {
            if (!IsPreparing) TryStartPreparation();
            else if (IsPaused) ContinuePreparation();
            else PausePreparation();
        }

        protected virtual void TryStartPreparation()
        {
            var isHoldingIngredient = holder.IsItem(out Ingredient ingredient);
            var canPrepare = isHoldingIngredient && CanPrepare(ingredient.Status);

            if (canPrepare) StartPreparation(ingredient);
            else OnIngredientRejected?.Invoke();
        }

        private void StartPreparation(Ingredient ingredient)
        {
            IsPreparing = true;
            OnPreparationStarted?.Invoke();

            ContinuePreparation();
            preparationRoutine = StartCoroutine(PreparationRoutine(ingredient));
        }

        private void PausePreparation()
        {
            IsPaused = true;
            PlayIdleAnimation();
        }

        private void ContinuePreparation()
        {
            IsPaused = false;
            PlayPreparationAnimation();
        }

        private IEnumerator PreparationRoutine(Ingredient ingredient)
        {
            var currentTime = 0F;
            var preparingTime = ingredient.PreparationTime;

            OnPreparationUpdated?.Invoke(0F);

            do
            {
                yield return null;

                currentTime += Time.deltaTime * preparationTimeScale * preparationBooster;

                var normalizedTime = currentTime / preparingTime;
                OnPreparationUpdated?.Invoke(normalizedTime);

            } while (currentTime < preparingTime);

            OnPreparationUpdated?.Invoke(1F);
            preparationRoutine = null;

            // Online clients wait for the host to say the ingredient is ready.
            if (NetworkGame.IsClient) yield break;

            var ingredientName = ingredient.Name;
            NetworkGame.Replicate(NetEventType.PreparationCompleted, this, () => FinishPreparation(ingredientName));
        }

        /// <summary>
        /// Completes the preparation when the host says so (online clients only).
        /// </summary>
        internal void CompletePreparationFromNetwork()
        {
            if (preparationRoutine != null) StopCoroutine(preparationRoutine);
            preparationRoutine = null;

            if (!holder.IsItem(out Ingredient ingredient)) return;

            OnPreparationUpdated?.Invoke(1F);
            FinishPreparation(ingredient.Name);
        }

        private void FinishPreparation(IngredientName name)
        {
            PrepareIngredient(name);
            CompletePreparation();
        }

        private void PrepareIngredient(IngredientName name)
        {
            var preparedIngredient = ingredientSettings.SpawnIngredient(name, preparatedStatus);
            holder.ReplaceItem(preparedIngredient);
        }

        private void HandleFinalSecondsStarted() => preparationBooster = finalSecondsBooster;
        private void HandleTimeLimitFinished() => ResetPreparationBooster();

        private void ResetPreparationBooster() => preparationBooster = 1f;

        private bool CanPrepare(IngredientStatus status) => status == allowedStatus;
    }
}