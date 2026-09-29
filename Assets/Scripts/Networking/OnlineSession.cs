using System;
using System.Linq;
using UnityEngine;
using KitchenChaos.UI;
using KitchenChaos.Scenes;
using KitchenChaos.Matches;
using KitchenChaos.Sessions;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace KitchenChaos.Networking
{
    public enum SessionState
    {
        Idle,
        Connecting,
        Lobby,
        InMatch
    }

    /// <summary>
    /// Keeps the online connection, the lobby players and the match scene loading.
    /// It survives scene loads while hosting or connected to a host.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OnlineSession : MonoBehaviour
    {
        /// <summary>Bump it when the messages change, so different builds refuse to play together.</summary>
        public const int ProtocolVersion = 1;
        public const ushort DefaultPort = 7777;

        private const float connectTimeout = 20F;
        private const string playerNameKey = "KitchenChaos.PlayerName";

        public static OnlineSession Instance { get; private set; }

        /// <summary>Whether this machine is hosting, joining or connected to a host.</summary>
        public static bool IsActive => Instance != null && Instance.State != SessionState.Idle;

        /// <summary>Whether the online menu has something to show when coming back to the main menu.</summary>
        public static bool ShouldShowMenu => IsActive || (Instance != null && !string.IsNullOrEmpty(Instance.LastError));

        public SessionState State { get; private set; }
        public bool IsHost { get; private set; }
        public bool UsesRelay { get; private set; }

        /// <summary>What other players need to join: a join code or a port.</summary>
        public string JoinInfo { get; private set; }
        public string LastError { get; set; }
        public IReadOnlyList<OnlineSeat> Members => members;

        public bool SupportsRelay => GetTransport()?.SupportsRelay ?? false;

        public static string PlayerName
        {
            get
            {
                if (!PlayerPrefs.HasKey(playerNameKey)) PlayerName = "Chef " + UnityEngine.Random.Range(10, 100);
                return PlayerPrefs.GetString(playerNameKey);
            }
            set => PlayerPrefs.SetString(playerNameKey, CleanName(value));
        }

        private IOnlineTransport transport;
        private SceneSettings sceneSettings;
        private NetworkMatch match;
        private OnlineMatchMenu matchMenu;
        private float connectDeadline;

        private readonly List<OnlineSeat> members = new(GameSession.MaxPlayers);
        private readonly HashSet<ulong> readyClients = new();
        private readonly Dictionary<ulong, string> pendingNames = new();

        public static OnlineSession GetOrCreate(SceneSettings sceneSettings)
        {
            if (Instance == null)
            {
                var gameObject = new GameObject(nameof(OnlineSession));
                DontDestroyOnLoad(gameObject);
                Instance = gameObject.AddComponent<OnlineSession>();
            }

            if (sceneSettings) Instance.sceneSettings = sceneSettings;
            return Instance;
        }

        /// <summary>
        /// Opens or closes the in-match menu, used to leave the match.
        /// </summary>
        public static void ToggleMatchMenu()
        {
            if (Instance && Instance.matchMenu) Instance.matchMenu.Toggle();
        }

        private void Awake()
        {
            matchMenu = gameObject.AddComponent<OnlineMatchMenu>();
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            transport?.Shutdown();

            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (State == SessionState.Connecting && !IsHost && Time.unscaledTime > connectDeadline)
                EndSession("Could not reach the host. Check the code or address and try again.", returnToMenu: false);
        }

        #region Hosting and joining
        public async Task HostAsync(bool useRelay, ushort port)
        {
            if (State != SessionState.Idle || !TryBeginConnecting(isHost: true)) return;

            try
            {
                var joinInfo = await transport.HostAsync(new HostOptions(useRelay, port, GameSession.MaxPlayers - 1, CreatePayload()));

                // Cancelled while connecting.
                if (State != SessionState.Connecting)
                {
                    transport.Shutdown();
                    return;
                }

                JoinInfo = joinInfo;

                UsesRelay = useRelay;
                members.Add(new OnlineSeat(transport.LocalClientId, PlayerName));
                State = SessionState.Lobby;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EndSession(GetErrorMessage(exception, useRelay), returnToMenu: false);
            }
        }

        public async Task JoinAsync(bool useRelay, string joinCode, string address, ushort port)
        {
            if (State != SessionState.Idle || !TryBeginConnecting(isHost: false)) return;

            var options = useRelay ?
                JoinOptions.WithRelay(joinCode.Trim().ToUpperInvariant(), CreatePayload()) :
                JoinOptions.WithAddress(address.Trim(), port, CreatePayload());

            try
            {
                UsesRelay = useRelay;
                await transport.JoinAsync(options);

                // Cancelled while connecting. Otherwise we are connected once the host approves us (see HandleClientConnected).
                if (State == SessionState.Idle) transport.Shutdown();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EndSession(GetErrorMessage(exception, useRelay), returnToMenu: false);
            }
        }

        public void Leave() => Leave(null);

        public void Leave(string reason) => EndSession(reason, returnToMenu: State == SessionState.InMatch);

        /// <summary>
        /// Host only: loads the level on every machine.
        /// </summary>
        public void StartMatch()
        {
            if (!IsHost || State != SessionState.Lobby) return;

            var seats = members.Take(GameSession.MaxPlayers).ToList();

            var writer = new NetWriter(MessageType.StartMatch);
            writer.Write((byte)seats.Count);
            foreach (var seat in seats)
            {
                writer.Write(seat.ClientId);
                writer.Write(seat.Name);
            }

            SendToClients(writer.ToArray(), reliable: true);
            LoadMatch(seats);
        }

        private bool TryBeginConnecting(bool isHost)
        {
            LastError = null;

            if (GetTransport() == null)
            {
                LastError = "Online play is not available in this build.";
                return false;
            }

            State = SessionState.Connecting;
            IsHost = isHost;
            JoinInfo = null;
            members.Clear();
            readyClients.Clear();
            pendingNames.Clear();
            connectDeadline = Time.unscaledTime + connectTimeout;

            return true;
        }

        private void EndSession(string error, bool returnToMenu)
        {
            var wasActive = State != SessionState.Idle;

            State = SessionState.Idle;
            LastError = error;
            JoinInfo = null;
            members.Clear();
            readyClients.Clear();
            pendingNames.Clear();

            if (wasActive) transport?.Shutdown();
            if (matchMenu) matchMenu.Hide();

            if (GameSession.IsOnline) GameSession.StartSinglePlayer();

            if (returnToMenu && sceneSettings)
            {
                Time.timeScale = 1F;
                sceneSettings.GoToMainMenu();
            }
        }

        private static byte[] CreatePayload()
        {
            var writer = new NetWriter();
            writer.Write(ProtocolVersion);
            writer.Write(PlayerName);
            return writer.ToArray();
        }

        private static string GetErrorMessage(Exception exception, bool useRelay)
        {
            var message = exception.Message;
            if (useRelay) message = "Unity Relay: " + message + "\nCheck the join code, or see the README to set up Relay.";
            return message;
        }

        private static string CleanName(string name)
        {
            name = (name ?? string.Empty).Trim();
            if (name.Length > 16) name = name.Substring(0, 16);
            return name.Length == 0 ? "Chef" : name;
        }
        #endregion

        #region Match
        internal bool IsConnected(ulong clientId) => members.Exists(member => member.ClientId == clientId);

        internal bool IsReady(ulong clientId) => readyClients.Contains(clientId);

        internal void Disconnect(ulong clientId, string reason) => transport?.Disconnect(clientId, reason);

        internal void SendToHost(byte[] message, bool reliable)
        {
            if (!IsHost && transport != null && transport.IsRunning) transport.Send(0, message, reliable);
        }

        internal void SendToClients(byte[] message, bool reliable)
        {
            if (!IsHost || transport == null || !transport.IsRunning) return;

            foreach (var member in members)
            {
                if (member.ClientId != transport.LocalClientId) transport.Send(member.ClientId, message, reliable);
            }
        }

        internal void OnMatchEnded(NetworkMatch endedMatch)
        {
            if (match != endedMatch) return;

            match = null;
            if (State == SessionState.InMatch) State = SessionState.Lobby;
        }

        internal bool IsWaitingForPlayers() => match != null && !match.HasStarted;

        private void LoadMatch(List<OnlineSeat> seats)
        {
            var localSeat = seats.FindIndex(seat => seat.ClientId == transport.LocalClientId);
            if (localSeat < 0) return;

            GameSession.StartOnline(seats, localSeat);
            readyClients.Clear();
            State = SessionState.InMatch;

            // A client can still be on the last match's results screen. Forgets that match,
            // so unloading its level doesn't send this session back to the lobby.
            match = null;

            if (sceneSettings) sceneSettings.GoToGame();
            else Debug.LogError("[Network] Missing SceneSettings. Open the online menu from the main menu.");
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode _)
        {
            if (State != SessionState.InMatch || !HasMatchManager(scene)) return;

            match = NetworkMatch.Create(this, scene, IsHost);
        }

        private static bool HasMatchManager(Scene scene) =>
            scene.GetRootGameObjects().Any(root => root.GetComponentInChildren<MatchManager>(true) != null);
        #endregion

        #region Transport events
        private IOnlineTransport GetTransport()
        {
            if (transport != null || !OnlineTransport.IsAvailable) return transport;

            transport = OnlineTransport.Factory();
            if (transport == null) return null;

            transport.Approver = ApproveConnection;
            transport.ClientConnected += HandleClientConnected;
            transport.ClientDisconnected += HandleClientDisconnected;
            transport.MessageReceived += HandleMessageReceived;

            return transport;
        }

        private bool ApproveConnection(ulong clientId, byte[] payload, out string reason)
        {
            int version;
            string name;

            try
            {
                var reader = new NetReader(payload);
                version = reader.ReadInt();
                name = CleanName(reader.ReadString());
            }
            catch (Exception)
            {
                reason = "Invalid connection data.";
                return false;
            }

            if (version != ProtocolVersion) reason = "Your game version is different from the host version.";
            else if (State != SessionState.Lobby) reason = "A match is already being played. Try again when it is over.";
            else if (members.Count + pendingNames.Count >= GameSession.MaxPlayers) reason = "The kitchen is full (4 players).";
            else
            {
                reason = null;
                pendingNames[clientId] = name;
                return true;
            }

            return false;
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (State == SessionState.Idle) return;

            if (!IsHost)
            {
                if (State == SessionState.Connecting) State = SessionState.Lobby;
                return;
            }

            if (clientId == transport.LocalClientId) return;

            if (!pendingNames.TryGetValue(clientId, out string name)) name = "Chef";
            pendingNames.Remove(clientId);

            members.Add(new OnlineSeat(clientId, name));
            SendLobbyState();
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            // Leaving on purpose also triggers this callback, once the transport has shut down.
            if (State == SessionState.Idle) return;

            if (!IsHost || clientId == transport.LocalClientId)
            {
                var reason = IsHost ? "The network connection stopped." : transport.DisconnectReason;
                if (string.IsNullOrEmpty(reason))
                    reason = State == SessionState.Connecting ? "Could not connect to the host." : "The connection to the host was lost.";

                EndSession(reason, returnToMenu: State == SessionState.InMatch);
                return;
            }

            pendingNames.Remove(clientId);
            readyClients.Remove(clientId);

            var index = members.FindIndex(member => member.ClientId == clientId);
            if (index < 0) return;

            var name = members[index].Name;
            members.RemoveAt(index);

            if (State == SessionState.InMatch && matchMenu) matchMenu.ShowNotice($"{name} left the match.");
            SendLobbyState();
        }

        private void HandleMessageReceived(ulong senderId, byte[] data)
        {
            NetReader reader;
            MessageType type;

            try
            {
                reader = new NetReader(data);
                type = (MessageType)reader.ReadByte();
            }
            catch (Exception)
            {
                return;
            }

            try
            {
                switch (type)
                {
                    case MessageType.LobbyState when !IsHost:
                        ReadLobbyState(reader);
                        break;

                    case MessageType.StartMatch when !IsHost:
                        ReadStartMatch(reader);
                        break;

                    case MessageType.MatchReady when IsHost:
                        readyClients.Add(senderId);
                        break;

                    default:
                        if (match != null) match.HandleMessage(senderId, type, reader);
                        break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void SendLobbyState()
        {
            var writer = new NetWriter(MessageType.LobbyState);
            writer.Write((byte)members.Count);
            foreach (var member in members)
            {
                writer.Write(member.ClientId);
                writer.Write(member.Name);
            }

            SendToClients(writer.ToArray(), reliable: true);
        }

        private void ReadLobbyState(NetReader reader)
        {
            members.Clear();

            var count = reader.ReadByte();
            for (int i = 0; i < count; i++)
            {
                members.Add(new OnlineSeat(reader.ReadULong(), reader.ReadString()));
            }
        }

        private void ReadStartMatch(NetReader reader)
        {
            var seats = new List<OnlineSeat>();

            var count = reader.ReadByte();
            for (int i = 0; i < count; i++)
            {
                seats.Add(new OnlineSeat(reader.ReadULong(), reader.ReadString()));
            }

            LoadMatch(seats);
        }
        #endregion
    }
}
