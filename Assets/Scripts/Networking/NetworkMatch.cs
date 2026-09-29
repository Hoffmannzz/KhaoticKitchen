using System;
using UnityEngine;
using KitchenChaos.Items;
using KitchenChaos.Score;
using KitchenChaos.Orders;
using KitchenChaos.Players;
using KitchenChaos.Matches;
using KitchenChaos.Physics;
using KitchenChaos.Counters;
using KitchenChaos.Sessions;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace KitchenChaos.Networking
{
    /// <summary>
    /// Runs an online match in the level scene.
    /// <para>
    /// Host: runs the game, sends every change (see <see cref="NetworkGame.Replicate"/>), the timers,
    /// the score and regular snapshots with the chef, loose item and order timer positions.
    /// </para>
    /// <para>
    /// Client: replays the host changes in order and sends its chef pose and interactions.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    internal sealed class NetworkMatch : MonoBehaviour
    {
        private const float sendInterval = 0.05F;
        private const float loadTimeout = 60F;
        private const float itemSmoothing = 15F;
        private const int maxItemsPerSnapshot = 24;

        internal bool IsHost { get; private set; }

        /// <summary>Whether the count down started (host) or was received (client).</summary>
        internal bool HasStarted { get; private set; }

        private OnlineSession session;
        private MatchManager matchManager;
        private MatchSettings matchSettings;
        private OrderSettings orderSettings;
        private ScoreSettings scoreSettings;

        private int localSeat;
        private bool isInitialized;
        private bool isFinished;
        private float nextSendTime;
        private float loadDeadline;
        private int snapshotItemOffset;

        private bool isRecording;
        private bool isReplaying;

        private readonly List<NetworkChef> chefs = new(GameSession.maxPlayers);
        private readonly List<int> recordedIds = new(8);
        private readonly List<int> recordedValues = new(4);
        private readonly Queue<int> replayedValues = new(4);
        private readonly List<byte[]> deferredMessages = new(4);
        private readonly List<int> looseItems = new(32);
        private readonly List<int> finishedItems = new(8);
        private readonly Dictionary<int, Pose> itemPoses = new(32);

        internal static NetworkMatch Create(OnlineSession session, Scene scene, bool isHost)
        {
            var gameObject = new GameObject(nameof(NetworkMatch));
            SceneManager.MoveGameObjectToScene(gameObject, scene);

            var match = gameObject.AddComponent<NetworkMatch>();
            match.session = session;
            match.IsHost = isHost;

            NetworkEntityRegistry.Begin(scene);
            NetworkGame.Match = match;

            return match;
        }

        private void Start()
        {
            matchManager = FindObjectOfType<MatchManager>();
            var playerManager = FindObjectOfType<PlayerManager>();
            var scoreManager = FindObjectOfType<ScoreManager>();

            if (matchManager == null || playerManager == null || scoreManager == null || OrderManager.Instance == null)
            {
                Debug.LogError("[Network] The level is missing a manager. Leaving the online match.");
                session.Leave("The level could not be loaded.");
                return;
            }

            matchSettings = matchManager.Settings;
            orderSettings = OrderManager.Instance.Settings;
            scoreSettings = scoreManager.Settings;
            localSeat = GameSession.LocalOnlineSeat;

            var players = playerManager.Settings.Ordered;
            for (int i = 0; i < players.Count; i++)
            {
                var chef = players[i].gameObject.AddComponent<NetworkChef>();
                chef.Initialize(players[i], isLocal: i == localSeat);
                chefs.Add(chef);
            }

            if (IsHost)
            {
                BindHostEvents();
                loadDeadline = Time.unscaledTime + loadTimeout;
            }
            else
            {
                StopSimulatingLooseItems();
                session.SendToHost(new NetWriter(MessageType.MatchReady).ToArray(), reliable: true);
            }

            isInitialized = true;
        }

        private void OnDestroy()
        {
            if (NetworkGame.Match == this) NetworkGame.Match = null;
            NetworkEntityRegistry.End();

            if (IsHost && isInitialized) UnbindHostEvents();
            if (session) session.OnMatchEnded(this);
        }

        private void Update()
        {
            if (!isInitialized) return;

            if (IsHost) TryStartMatch();
            else MoveLooseItems();

            if (Time.unscaledTime < nextSendTime) return;
            nextSendTime = Time.unscaledTime + sendInterval;

            if (IsHost) SendSnapshot();
            else SendLocalChefPose();
        }

        #region Replication
        internal void Replicate(NetEventType type, Component source, Action action, Action<NetWriter> payload)
        {
            if (!IsHost)
            {
                // Clients only run changes decided by the host.
                if (isReplaying) action();
                else Debug.LogWarning($"[Network] {type} can only happen on the host.");
                return;
            }

            // A change inside another change is sent as part of it.
            if (isRecording)
            {
                action();
                return;
            }

            isRecording = true;
            recordedIds.Clear();
            recordedValues.Clear();
            NetworkEntityRegistry.BeginRecording(recordedIds);

            try
            {
                action();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                NetworkEntityRegistry.EndRecording();
                isRecording = false;
            }

            var writer = new NetWriter(MessageType.GameEvent);
            writer.Write((byte)type);
            writer.Write(NetworkEntityRegistry.GetId(source));

            writer.Write((byte)recordedIds.Count);
            foreach (var id in recordedIds) writer.Write(id);

            writer.Write((byte)recordedValues.Count);
            foreach (var value in recordedValues) writer.Write(value);

            try
            {
                payload?.Invoke(writer);
                SendToClients(writer.ToArray());
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            foreach (var message in deferredMessages) SendToClients(message);
            deferredMessages.Clear();
        }

        internal int Sync(int value)
        {
            if (IsHost)
            {
                if (isRecording) recordedValues.Add(value);
                return value;
            }

            return isReplaying && replayedValues.Count > 0 ? replayedValues.Dequeue() : value;
        }

        private void ApplyEvent(NetReader reader)
        {
            var type = (NetEventType)reader.ReadByte();
            var sourceId = reader.ReadInt();

            var spawnedIds = new int[reader.ReadByte()];
            for (int i = 0; i < spawnedIds.Length; i++) spawnedIds[i] = reader.ReadInt();

            replayedValues.Clear();
            var valueCount = reader.ReadByte();
            for (int i = 0; i < valueCount; i++) replayedValues.Enqueue(reader.ReadInt());

            isReplaying = true;
            NetworkEntityRegistry.BeginReplay(spawnedIds);

            try
            {
                ApplyEvent(type, sourceId, reader);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                NetworkEntityRegistry.EndReplay();
                replayedValues.Clear();
                isReplaying = false;
            }
        }

        private void ApplyEvent(NetEventType type, int sourceId, NetReader reader)
        {
            switch (type)
            {
                case NetEventType.Interaction:
                    var chefIndex = reader.ReadByte();
                    var kind = (InteractionKind)reader.ReadByte();
                    var target = InteractionTarget.Read(reader);
                    if (chefIndex < chefs.Count) chefs[chefIndex].Player.Interactor.Interact(kind, target);
                    break;

                case NetEventType.PreparationCompleted:
                    if (NetworkEntityRegistry.TryGet(sourceId, out AbstractIngredientPreparator preparator))
                        preparator.CompletePreparationFromNetwork();
                    break;

                case NetEventType.StoveBurned:
                    if (NetworkEntityRegistry.TryGet(sourceId, out Stove stove)) stove.BurnFromNetwork();
                    break;

                case NetEventType.TrashDestroyed:
                    if (NetworkEntityRegistry.TryGet(sourceId, out Trash trash)) trash.DestroyItemFromNetwork();
                    break;

                case NetEventType.PlateReturned:
                    orderSettings.ReturnPlateFromNetwork();
                    break;

                case NetEventType.OrderCreated:
                    var orderId = reader.ReadInt();
                    var recipeIndex = reader.ReadInt();
                    orderSettings.CreateFromNetwork(orderId, recipeIndex);
                    break;

                case NetEventType.OrderFailed:
                    orderSettings.FindOrder(reader.ReadInt())?.FailFromNetwork();
                    break;

                case NetEventType.OrderRemoved:
                    orderSettings.RemoveFromNetwork(reader.ReadInt());
                    break;

                default:
                    Debug.LogWarning($"[Network] Unknown event {type}.");
                    break;
            }
        }
        #endregion

        #region Messages
        internal void HandleMessage(ulong senderId, MessageType type, NetReader reader)
        {
            if (!isInitialized) return;

            if (IsHost)
            {
                switch (type)
                {
                    case MessageType.Interaction: HandleInteraction(senderId, reader); break;
                    case MessageType.ChefPose: HandleChefPose(senderId, reader); break;
                }
                return;
            }

            switch (type)
            {
                case MessageType.GameEvent: ApplyEvent(reader); break;
                case MessageType.Timer: ApplyTimer(reader); break;
                case MessageType.Score: ApplyScore(reader); break;
                case MessageType.Snapshot: ApplySnapshot(reader); break;
            }
        }

        private void SendToClients(byte[] message, bool reliable = true)
        {
            // Messages created during a change are sent after it, so clients receive them in the same order.
            if (isRecording && reliable) deferredMessages.Add(message);
            else session.SendToClients(message, reliable);
        }
        #endregion

        #region Interactions
        internal bool RouteLocalInteraction(ItemHandler handler, InteractionKind kind)
        {
            var index = chefs.FindIndex(chef => chef.Player.Interactor == handler);
            if (index != localSeat || !CanInteract()) return true;

            var target = handler.CaptureTarget();

            if (IsHost)
            {
                ExecuteInteraction(index, kind, target);
                return true;
            }

            var writer = new NetWriter(MessageType.Interaction);
            writer.Write((byte)index);
            writer.Write((byte)kind);
            target.Write(writer);

            session.SendToHost(writer.ToArray(), reliable: true);
            return true;
        }

        private void HandleInteraction(ulong senderId, NetReader reader)
        {
            var index = reader.ReadByte();
            var kind = (InteractionKind)reader.ReadByte();
            var target = InteractionTarget.Read(reader);

            if (IsOwner(senderId, index) && CanInteract()) ExecuteInteraction(index, kind, target);
        }

        private void ExecuteInteraction(int chefIndex, InteractionKind kind, InteractionTarget target)
        {
            var handler = chefs[chefIndex].Player.Interactor;

            Replicate(NetEventType.Interaction, null, () => handler.Interact(kind, target), writer =>
            {
                writer.Write((byte)chefIndex);
                writer.Write((byte)kind);
                target.Write(writer);
            });
        }

        private bool CanInteract() => HasStarted && !isFinished;

        private bool IsOwner(ulong clientId, int chefIndex)
        {
            var seats = GameSession.OnlineSeats;
            return chefIndex < chefs.Count && chefIndex < seats.Count && seats[chefIndex].ClientId == clientId;
        }
        #endregion

        #region Host
        private void TryStartMatch()
        {
            if (HasStarted) return;

            var seats = GameSession.OnlineSeats;
            var isEveryoneReady = true;

            for (int i = 0; i < seats.Count; i++)
            {
                if (i == localSeat) continue;

                var clientId = seats[i].ClientId;
                var isWaiting = session.IsConnected(clientId) && !session.IsReady(clientId);
                if (!isWaiting) continue;

                if (Time.unscaledTime < loadDeadline) isEveryoneReady = false;
                else session.Disconnect(clientId, "Loading the level took too long.");
            }

            if (!isEveryoneReady) return;

            HasStarted = true;
            matchManager.BeginMatch();
        }

        private void BindHostEvents()
        {
            matchSettings.CountDown.OnStarted += HandleCountDownStarted;
            matchSettings.CountDown.OnUpdated += HandleCountDownUpdated;
            matchSettings.CountDown.OnFinished += HandleCountDownFinished;

            matchSettings.TimeLimit.OnStarted += HandleTimeLimitStarted;
            matchSettings.TimeLimit.OnUpdated += HandleTimeLimitUpdated;
            matchSettings.TimeLimit.OnFinished += HandleTimeLimitFinished;
            matchSettings.TimeLimit.OnFinalSecondsStarted += HandleFinalSecondsStarted;

            scoreSettings.OnScoreIncreased += HandleScoreChanged;
            scoreSettings.OnScoreDecreased += HandleScoreChanged;
        }

        private void UnbindHostEvents()
        {
            matchSettings.CountDown.OnStarted -= HandleCountDownStarted;
            matchSettings.CountDown.OnUpdated -= HandleCountDownUpdated;
            matchSettings.CountDown.OnFinished -= HandleCountDownFinished;

            matchSettings.TimeLimit.OnStarted -= HandleTimeLimitStarted;
            matchSettings.TimeLimit.OnUpdated -= HandleTimeLimitUpdated;
            matchSettings.TimeLimit.OnFinished -= HandleTimeLimitFinished;
            matchSettings.TimeLimit.OnFinalSecondsStarted -= HandleFinalSecondsStarted;

            scoreSettings.OnScoreIncreased -= HandleScoreChanged;
            scoreSettings.OnScoreDecreased -= HandleScoreChanged;
        }

        private void HandleCountDownStarted() => SendTimer(TimerType.CountDown, TimerEvent.Started);
        private void HandleCountDownUpdated(uint time) => SendTimer(TimerType.CountDown, TimerEvent.Updated, time);
        private void HandleCountDownFinished() => SendTimer(TimerType.CountDown, TimerEvent.Finished);
        private void HandleTimeLimitStarted() => SendTimer(TimerType.TimeLimit, TimerEvent.Started);
        private void HandleTimeLimitUpdated(uint time) => SendTimer(TimerType.TimeLimit, TimerEvent.Updated, time);
        private void HandleFinalSecondsStarted() => SendTimer(TimerType.TimeLimit, TimerEvent.FinalSecondsStarted);

        private void HandleTimeLimitFinished()
        {
            isFinished = true;
            SendTimer(TimerType.TimeLimit, TimerEvent.Finished);
        }

        private void SendTimer(TimerType timer, TimerEvent timerEvent, uint time = 0)
        {
            var writer = new NetWriter(MessageType.Timer);
            writer.Write((byte)timer);
            writer.Write((byte)timerEvent);
            writer.Write(time);
            SendToClients(writer.ToArray());
        }

        private void HandleScoreChanged(float _)
        {
            var writer = new NetWriter(MessageType.Score);
            writer.Write(scoreSettings.Tips);
            writer.Write(scoreSettings.SuccessfulDeliveries);
            writer.Write(scoreSettings.FailedDeliveries);
            writer.Write(scoreSettings.Score);
            SendToClients(writer.ToArray());
        }

        private void HandleChefPose(ulong senderId, NetReader reader)
        {
            var index = reader.ReadByte();
            var position = reader.ReadVector3();
            var yaw = reader.ReadFloat();
            var walking = reader.ReadBool();

            if (IsOwner(senderId, index)) chefs[index].SetPose(position, yaw, walking);
        }

        private void SendSnapshot()
        {
            var writer = new NetWriter(MessageType.Snapshot);

            writer.Write((byte)chefs.Count);
            foreach (var chef in chefs)
            {
                chef.GetPose(out Vector3 position, out float yaw, out bool walking);
                writer.Write(position);
                writer.Write(yaw);
                writer.Write(walking);
            }

            looseItems.Clear();
            foreach (var pair in NetworkEntityRegistry.GetAll())
            {
                var item = pair.Value;
                if (item && item.transform.parent == null && item.TryGetComponent(out AbstractItem _)) looseItems.Add(pair.Key);
            }

            // Sends the items in turns when there are too many for a single message.
            var count = Mathf.Min(looseItems.Count, maxItemsPerSnapshot);
            if (snapshotItemOffset >= looseItems.Count) snapshotItemOffset = 0;

            writer.Write((byte)count);
            for (int i = 0; i < count; i++)
            {
                var id = looseItems[(snapshotItemOffset + i) % looseItems.Count];
                NetworkEntityRegistry.TryGet(id, out GameObject item);

                writer.Write(id);
                writer.Write(item.transform.position);
                writer.Write(item.transform.rotation);
            }
            snapshotItemOffset += count;

            var orders = orderSettings.Orders;
            writer.Write((byte)orders.Count);
            foreach (var order in orders)
            {
                writer.Write(order.Id);
                writer.Write(order.WaitingTime);
            }

            SendToClients(writer.ToArray(), reliable: false);
        }
        #endregion

        #region Client
        private void ApplyTimer(NetReader reader)
        {
            var timer = (TimerType)reader.ReadByte();
            var timerEvent = (TimerEvent)reader.ReadByte();
            var time = reader.ReadUInt();

            var timeDown = timer == TimerType.CountDown ? matchSettings.CountDown : matchSettings.TimeLimit;

            switch (timerEvent)
            {
                case TimerEvent.Started:
                    HasStarted = true;
                    timeDown.NotifyStarted();
                    break;

                case TimerEvent.Updated:
                    timeDown.NotifyUpdated(time);
                    break;

                case TimerEvent.Finished:
                    if (timer == TimerType.TimeLimit) isFinished = true;
                    timeDown.NotifyFinished();
                    break;

                case TimerEvent.FinalSecondsStarted:
                    matchSettings.TimeLimit.NotifyFinalSecondsStarted();
                    break;
            }
        }

        private void ApplyScore(NetReader reader)
        {
            var tips = reader.ReadInt();
            var successfulDeliveries = reader.ReadInt();
            var failedDeliveries = reader.ReadInt();
            var score = reader.ReadFloat();

            scoreSettings.SetFromNetwork(tips, successfulDeliveries, failedDeliveries, score);
        }

        private void ApplySnapshot(NetReader reader)
        {
            var chefCount = reader.ReadByte();
            for (int i = 0; i < chefCount; i++)
            {
                var position = reader.ReadVector3();
                var yaw = reader.ReadFloat();
                var walking = reader.ReadBool();

                if (i < chefs.Count) chefs[i].SetPose(position, yaw, walking);
            }

            var itemCount = reader.ReadByte();
            for (int i = 0; i < itemCount; i++)
            {
                var id = reader.ReadInt();
                var position = reader.ReadVector3();
                var rotation = reader.ReadQuaternion();

                if (!NetworkEntityRegistry.TryGet(id, out GameObject item) || item.transform.parent != null) continue;

                if (!itemPoses.ContainsKey(id) && item.TryGetComponent(out CollectableBody body)) body.FollowNetwork();
                itemPoses[id] = new Pose(position, rotation);
            }

            var orderCount = reader.ReadByte();
            for (int i = 0; i < orderCount; i++)
            {
                var id = reader.ReadInt();
                var waitingTime = reader.ReadFloat();

                var order = orderSettings.FindOrder(id);
                if (order != null) order.WaitingTime = waitingTime;
            }
        }

        private void MoveLooseItems()
        {
            var t = 1F - Mathf.Exp(-itemSmoothing * Time.deltaTime);

            finishedItems.Clear();
            foreach (var pair in itemPoses)
            {
                var isLoose = NetworkEntityRegistry.TryGet(pair.Key, out GameObject item) && item.transform.parent == null;
                if (!isLoose)
                {
                    finishedItems.Add(pair.Key);
                    continue;
                }

                var pose = pair.Value;
                item.transform.SetPositionAndRotation(
                    Vector3.Lerp(item.transform.position, pose.position, t),
                    Quaternion.Slerp(item.transform.rotation, pose.rotation, t)
                );
            }

            foreach (var id in finishedItems) itemPoses.Remove(id);
        }

        private void StopSimulatingLooseItems()
        {
            foreach (var pair in NetworkEntityRegistry.GetAll())
            {
                var item = pair.Value;
                if (item && item.transform.parent == null && item.TryGetComponent(out CollectableBody body)) body.FollowNetwork();
            }
        }

        private void SendLocalChefPose()
        {
            if (localSeat < 0 || localSeat >= chefs.Count) return;

            chefs[localSeat].GetPose(out Vector3 position, out float yaw, out bool walking);

            var writer = new NetWriter(MessageType.ChefPose);
            writer.Write((byte)localSeat);
            writer.Write(position);
            writer.Write(yaw);
            writer.Write(walking);

            session.SendToHost(writer.ToArray(), reliable: false);
        }
        #endregion
    }
}
