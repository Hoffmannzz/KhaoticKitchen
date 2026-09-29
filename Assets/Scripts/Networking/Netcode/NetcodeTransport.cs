using System;
using System.Net;
using System.Linq;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using System.Net.Sockets;
using System.Threading.Tasks;
using Unity.Netcode.Transports.UTP;

namespace KitchenChaos.Networking
{
    /// <summary>
    /// Online transport built on Netcode for GameObjects and Unity Transport.
    /// <para>
    /// Only the connection handling and custom messages are used: the game state is
    /// synchronized by <see cref="NetworkMatch"/>, so no NetworkObject or prefab setup is needed.
    /// </para>
    /// </summary>
    public sealed class NetcodeTransport : IOnlineTransport
    {
        private const string messageName = "KitchenChaos";

        /// <summary>
        /// Allocates a Unity Relay server and configures the transport to use it.
        /// Set by the optional KitchenChaos.Networking.Relay assembly.
        /// </summary>
        /// <returns>The join code.</returns>
        public static Func<UnityTransport, int, Task<string>> RelayHost { get; set; }

        /// <summary>
        /// Joins a Unity Relay allocation using a join code and configures the transport to use it.
        /// </summary>
        public static Func<UnityTransport, string, Task> RelayJoin { get; set; }

        public event Action<ulong> ClientConnected;
        public event Action<ulong> ClientDisconnected;
        public event Action<ulong, byte[]> MessageReceived;

        public bool IsRunning => manager != null && manager.IsListening;
        public bool IsHost => manager != null && manager.IsServer;
        public bool SupportsRelay => RelayHost != null && RelayJoin != null;
        public ulong LocalClientId => manager != null ? manager.LocalClientId : NetworkManager.ServerClientId;
        public string DisconnectReason => manager != null ? manager.DisconnectReason : null;
        public ConnectionApprover Approver { set => approver = value; }

        private NetworkManager manager;
        private UnityTransport unityTransport;
        private ConnectionApprover approver;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() => OnlineTransport.Factory = () => new NetcodeTransport();

        public async Task<string> HostAsync(HostOptions options)
        {
            await PrepareAsync(options.Payload);

            string joinInfo;
            if (options.UseRelay)
            {
                if (RelayHost == null) throw new InvalidOperationException("Unity Relay is not installed.");
                joinInfo = await RelayHost(unityTransport, options.MaxClients);
            }
            else
            {
                unityTransport.SetConnectionData("0.0.0.0", options.Port, "0.0.0.0");
                joinInfo = options.Port.ToString();
            }

            if (!manager.StartHost())
            {
                throw new InvalidOperationException(options.UseRelay ?
                    "Could not start hosting." :
                    $"Could not open port {options.Port}. Is another game using it?");
            }

            RegisterMessageHandler();
            return joinInfo;
        }

        public async Task JoinAsync(JoinOptions options)
        {
            await PrepareAsync(options.Payload);

            if (options.UseRelay)
            {
                if (RelayJoin == null) throw new InvalidOperationException("Unity Relay is not installed.");
                await RelayJoin(unityTransport, options.JoinCode);
            }
            else unityTransport.SetConnectionData(await ResolveAddressAsync(options.Address), options.Port);

            if (!manager.StartClient()) throw new InvalidOperationException("Could not start the connection.");

            RegisterMessageHandler();
        }

        public void Send(ulong clientId, byte[] data, bool reliable)
        {
            if (!IsRunning) return;

            var delivery = reliable ?
                NetworkDelivery.ReliableFragmentedSequenced :
                NetworkDelivery.UnreliableSequenced;

            try
            {
                using var writer = new FastBufferWriter(data.Length + sizeof(int), Allocator.Temp);
                writer.WriteValueSafe(data.Length);
                writer.WriteBytesSafe(data);
                manager.CustomMessagingManager.SendNamedMessage(messageName, clientId, writer, delivery);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        public void Disconnect(ulong clientId, string reason)
        {
            if (IsHost && clientId != NetworkManager.ServerClientId) manager.DisconnectClient(clientId, reason);
        }

        public void Shutdown()
        {
            if (manager == null) return;

            manager.CustomMessagingManager?.UnregisterNamedMessageHandler(messageName);
            if (manager.IsListening) manager.Shutdown();
        }

        private async Task PrepareAsync(byte[] payload)
        {
            CreateManager();

            // A previous session may still be closing.
            var timeout = Time.realtimeSinceStartup + 3F;
            while ((manager.ShutdownInProgress || manager.IsListening) && Time.realtimeSinceStartup < timeout)
            {
                await Task.Yield();
            }

            if (manager.IsListening) throw new InvalidOperationException("The previous session is still running.");

            manager.NetworkConfig.ConnectionData = payload ?? Array.Empty<byte>();
        }

        private void CreateManager()
        {
            if (manager != null) return;

            var gameObject = new GameObject(nameof(NetworkManager));
            UnityEngine.Object.DontDestroyOnLoad(gameObject);

            unityTransport = gameObject.AddComponent<UnityTransport>();
            unityTransport.ConnectTimeoutMS = 1000;
            unityTransport.MaxConnectAttempts = 15;

            manager = gameObject.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = unityTransport,
                ConnectionApproval = true,
                EnableSceneManagement = false,
                ProtocolVersion = OnlineSession.protocolVersion
            };

            manager.ConnectionApprovalCallback = HandleConnectionApproval;
            manager.OnClientConnectedCallback += HandleClientConnected;
            manager.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        private void RegisterMessageHandler() =>
            manager.CustomMessagingManager.RegisterNamedMessageHandler(messageName, HandleNamedMessage);

        private void HandleConnectionApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false;

            if (request.ClientNetworkId == NetworkManager.ServerClientId)
            {
                response.Approved = true;
                return;
            }

            string reason = null;
            response.Approved = approver == null || approver(request.ClientNetworkId, request.Payload, out reason);
            response.Reason = reason;
        }

        private void HandleClientConnected(ulong clientId) => ClientConnected?.Invoke(clientId);
        private void HandleClientDisconnected(ulong clientId) => ClientDisconnected?.Invoke(clientId);

        private void HandleNamedMessage(ulong senderId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out int length);
            if (length < 0 || length > reader.Length - reader.Position) return;

            var data = new byte[length];
            reader.ReadBytesSafe(ref data, length);

            MessageReceived?.Invoke(senderId, data);
        }

        private static async Task<string> ResolveAddressAsync(string address)
        {
            address = address.Trim();
            if (IPAddress.TryParse(address, out IPAddress _)) return address;

            var addresses = await Dns.GetHostAddressesAsync(address);
            var ipv4 = addresses.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
            if (ipv4 == null) throw new InvalidOperationException($"Could not find the address {address}.");

            return ipv4.ToString();
        }
    }
}
