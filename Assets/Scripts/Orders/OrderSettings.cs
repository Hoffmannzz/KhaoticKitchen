using System;
using UnityEngine;
using KitchenChaos.Items;
using KitchenChaos.Recipes;
using System.Collections;
using KitchenChaos.Networking;
using System.Collections.Generic;

namespace KitchenChaos.Orders
{
    [CreateAssetMenu(fileName = "OrderSettings", menuName = EditorPaths.SO + "Order Settings", order = 110)]
    public sealed class OrderSettings : ScriptableObject
    {
        [SerializeField] private RecipeSettings recipeSettings;
        [SerializeField] private IngredientSettings ingredientSettings;

        [SerializeField, Min(1)] private int maxOrders = 3;
        [SerializeField, Min(0F)] private float additionalTimePerIngredient = 10F;
        [SerializeField, Min(0F)] private float timeByNewOrder = 15F;
        [SerializeField, Min(0F)] private float timeToRemoveAfterFail = 1F;
        [SerializeField, Min(0F)] private float timeToRemoveAfterDelivery = 1F;
        [SerializeField, Min(0F)] private float timeToReturnPlate = 4F;

        public event Action OnPlateReturned;
        public event Action<Order> OnOrderFailed;
        public event Action<Order> OnOrderCreated;
        public event Action<Order> OnOrderRemoved;
        public event Action<Order> OnOrderDelivered;

        public int TotalOrders => orders.Count;

        internal IReadOnlyList<Order> Orders => orders;

        private List<Order> orders;
        private Coroutine ordering;
        private OrderManager manager;
        private int nextOrderId;

        internal void Initialize(OrderManager manager)
        {
            if (orders != null) CancelAll();

            orders = new(maxOrders);
            nextOrderId = 0;
            this.manager = manager;
        }

        public bool TryDelivery(Ingredient[] ingredients, out int tip)
        {
            manager.StartCoroutine(ReturnPlateRoutine());

            foreach (var order in orders)
            {
                if (order.TryDelivery(ingredients))
                {
                    tip = order.GetTip();
                    return true;
                }
            }

            tip = 0;
            return false;
        }


        internal void StartOrdering()
        {
            // Online clients receive the orders created by the host.
            if (NetworkGame.IsClient) return;
            ordering = manager.StartCoroutine(OrderingRoutine());
        }

        internal void StopOrdering()
        {
            if (ordering != null) manager.StopCoroutine(ordering);
            CancelAll();
        }

        internal Order Create(RecipeData recipe) => Create(recipe, nextOrderId);

        internal Order FindOrder(int id) => orders.Find(order => order.Id == id);

        /// <summary>
        /// Creates the same order the host created (online clients only).
        /// </summary>
        internal void CreateFromNetwork(int id, int recipeIndex)
        {
            var recipe = recipeSettings.GetRecipe(recipeIndex);
            if (recipe != null) Create(recipe, id);
        }

        internal void RemoveFromNetwork(int id)
        {
            var order = FindOrder(id);
            if (order != null) Remove(order);
        }

        internal void ReturnPlateFromNetwork() => OnPlateReturned?.Invoke();

        private Order Create(RecipeData recipe, int id)
        {
            var waitingTime = recipe.GetWaitingTime(
                ingredientSettings,
                additionalTimePerIngredient
            );
            var order = new Order(recipe, waitingTime, id);
            nextOrderId = Mathf.Max(nextOrderId, id + 1);

            Create(order);

            return order;
        }

        private void Create(Order order)
        {
            order.OnFailed += () => Fail(order);
            order.OnDelivered += () => Delivery(order);

            order.StartCountDownWaitingTime();

            orders.Add(order);
            OnOrderCreated?.Invoke(order);
        }

        private void CreateRandom()
        {
            var recipe = recipeSettings.GetRandom();
            Order order = null;

            NetworkGame.Replicate(NetEventType.OrderCreated, null, () => order = Create(recipe), writer =>
            {
                writer.Write(order.Id);
                writer.Write(recipeSettings.IndexOf(recipe));
            });
        }

        private void Remove(Order order)
        {
            order.OnFailed -= () => Fail(order);
            order.OnDelivered -= () => Delivery(order);

            orders.Remove(order);
            OnOrderRemoved?.Invoke(order);
        }

        private void Fail(Order order)
        {
            NetworkGame.Replicate(NetEventType.OrderFailed, null, () =>
            {
                OnOrderFailed?.Invoke(order);
                manager.StartCoroutine(RemoveRoutine(order, timeToRemoveAfterFail));
            }, writer => writer.Write(order.Id));
        }

        private void Delivery(Order order)
        {
            OnOrderDelivered?.Invoke(order);
            manager.StartCoroutine(RemoveRoutine(order, timeToRemoveAfterDelivery));
        }

        private void CancelAll()
        {
            foreach (var order in orders)
            {
                order.CancelCountDownWaitingTime();
            }
        }

        private bool CanCreateNewOrders() => TotalOrders < maxOrders;

        private IEnumerator OrderingRoutine()
        {
            var waitTimeByNewOrder = new WaitForSeconds(timeByNewOrder);
            var waitUntilCanCreateNewOrders = new WaitUntil(CanCreateNewOrders);

            while (true)
            {
                CreateRandom();
                yield return waitUntilCanCreateNewOrders;
                yield return waitTimeByNewOrder;
            }
        }

        private IEnumerator RemoveRoutine(Order order, float time)
        {
            yield return new WaitForSeconds(time);

            // Online clients wait for the host to remove the order.
            if (NetworkGame.IsClient) yield break;

            NetworkGame.Replicate(NetEventType.OrderRemoved, null, () => Remove(order), writer => writer.Write(order.Id));
        }

        private IEnumerator ReturnPlateRoutine()
        {
            yield return new WaitForSeconds(timeToReturnPlate);

            // Online clients wait for the host to return the plate.
            if (NetworkGame.IsClient) yield break;

            NetworkGame.Replicate(NetEventType.PlateReturned, null, () => OnPlateReturned?.Invoke());
        }
    }
}