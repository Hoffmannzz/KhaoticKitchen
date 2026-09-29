using System;
using System.Threading.Tasks;

namespace KitchenChaos.Networking
{
    /// <summary>
    /// Decides if a connecting client may join.
    /// </summary>
    /// <param name="clientId">The connecting client.</param>
    /// <param name="payload">Data sent by the client (see <see cref="JoinOptions.Payload"/>).</param>
    /// <param name="reason">Why the client was refused.</param>
    /// <returns>Whether the client is approved.</returns>
    public delegate bool ConnectionApprover(ulong clientId, byte[] payload, out string reason);

    public readonly struct HostOptions
    {
        public bool UseRelay { get; }
        public ushort Port { get; }
        public int MaxClients { get; }
        public byte[] Payload { get; }

        public HostOptions(bool useRelay, ushort port, int maxClients, byte[] payload)
        {
            UseRelay = useRelay;
            Port = port;
            MaxClients = maxClients;
            Payload = payload;
        }
    }

    public readonly struct JoinOptions
    {
        public bool UseRelay { get; }
        public string JoinCode { get; }
        public string Address { get; }
        public ushort Port { get; }
        public byte[] Payload { get; }

        private JoinOptions(bool useRelay, string joinCode, string address, ushort port, byte[] payload)
        {
            UseRelay = useRelay;
            JoinCode = joinCode;
            Address = address;
            Port = port;
            Payload = payload;
        }

        public static JoinOptions WithRelay(string joinCode, byte[] payload) => new(true, joinCode, null, 0, payload);
        public static JoinOptions WithAddress(string address, ushort port, byte[] payload) => new(false, null, address, port, payload);
    }

    /// <summary>
    /// Connection and message delivery used by <see cref="OnlineSession"/>.
    /// Implemented by the optional KitchenChaos.Networking.Netcode assembly.
    /// </summary>
    public interface IOnlineTransport
    {
        bool IsRunning { get; }
        bool IsHost { get; }
        bool SupportsRelay { get; }
        ulong LocalClientId { get; }
        string DisconnectReason { get; }
        ConnectionApprover Approver { set; }

        /// <summary>Host: a remote client joined. Client: this machine connected to the host.</summary>
        event Action<ulong> ClientConnected;
        /// <summary>Host: a remote client left. Client: this machine lost the connection to the host.</summary>
        event Action<ulong> ClientDisconnected;
        event Action<ulong, byte[]> MessageReceived;

        /// <summary>
        /// Starts hosting.
        /// </summary>
        /// <returns>What other players need to join: a join code or an address.</returns>
        Task<string> HostAsync(HostOptions options);

        Task JoinAsync(JoinOptions options);

        void Send(ulong clientId, byte[] data, bool reliable);
        void Disconnect(ulong clientId, string reason);
        void Shutdown();
    }

    /// <summary>
    /// Holds the transport implementation registered at startup.
    /// </summary>
    public static class OnlineTransport
    {
        public static Func<IOnlineTransport> Factory { get; set; }
        public static bool IsAvailable => Factory != null;
    }
}
