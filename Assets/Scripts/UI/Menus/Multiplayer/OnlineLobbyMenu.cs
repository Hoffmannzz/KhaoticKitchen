using System;
using System.Linq;
using UnityEngine;
using System.Net.Sockets;
using KitchenChaos.Scenes;
using KitchenChaos.Sessions;
using KitchenChaos.Networking;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Net.NetworkInformation;

namespace KitchenChaos.UI
{
    /// <summary>
    /// Menu to host or join an online match and wait in the lobby until the host starts it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OnlineLobbyMenu : MenuPanel
    {
        private const string addressKey = "KitchenChaos.LastAddress";

        private SceneSettings sceneSettings;
        private Action onClosed;
        private OnlineSession session;

        private string playerName;
        private string joinCode = string.Empty;
        private string address;
        private string port;
        private string localAddresses;
        private bool isClosing;

        public static OnlineLobbyMenu Open(SceneSettings sceneSettings, Action onClosed)
        {
            var menu = new GameObject(nameof(OnlineLobbyMenu)).AddComponent<OnlineLobbyMenu>();

            menu.sceneSettings = sceneSettings;
            menu.onClosed = onClosed;

            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);

            return menu;
        }

        private void Start()
        {
            session = OnlineTransport.IsAvailable ? OnlineSession.GetOrCreate(sceneSettings) : null;

            playerName = OnlineSession.PlayerName;
            address = PlayerPrefs.GetString(addressKey, "127.0.0.1");
            port = OnlineSession.defaultPort.ToString();
        }

        private void Update()
        {
            if (isClosing) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Back();
        }

        protected override void DrawPanel()
        {
            if (isClosing) return;

            if (session == null)
            {
                DrawUnavailable();
                return;
            }

            switch (session.State)
            {
                case SessionState.Idle: DrawMenu(); break;
                case SessionState.Connecting: DrawConnecting(); break;
                case SessionState.Lobby: DrawLobby(); break;
            }
        }

        private void DrawUnavailable()
        {
            BeginWindow(1100F, 480F, "ONLINE");
            GUILayout.Label(Application.platform == RuntimePlatform.WebGLPlayer ?
                "Online play is not available in the browser version. Download the desktop build to play with friends." :
                "Online play needs the Netcode for GameObjects package. Open the project in Unity so the Package Manager installs it.",
                LabelStyle);
            GUILayout.FlexibleSpace();
            if (Button("BACK  (Esc)")) Back();
            EndWindow();
        }

        private void DrawMenu()
        {
            BeginWindow(1400F, 920F, "ONLINE");

            playerName = TextField("Your name", playerName, 16);
            GUILayout.Space(14F);

            GUILayout.Label("HOST A KITCHEN", LabelStyle);
            GUILayout.BeginHorizontal();
            GUI.enabled = session.SupportsRelay;
            if (Button("HOST WITH JOIN CODE", 520F)) Host(useRelay: true);
            GUI.enabled = true;
            GUILayout.Space(20F);
            if (Button("HOST WITH MY IP", 420F)) Host(useRelay: false);
            GUILayout.Space(20F);
            port = TextField("Port", port, 5, 90F);
            GUILayout.EndHorizontal();

            GUILayout.Label(session.SupportsRelay ?
                "Join code: friends anywhere join with a short code (uses Unity Relay)." :
                "Join codes need Unity Relay set up for this project (see the README).",
                SmallLabelStyle);
            GUILayout.Label("My IP: friends on your network join with your IP. Over the internet, forward UDP port " + port + " on your router or use a VPN like Tailscale or ZeroTier.", SmallLabelStyle);

            GUILayout.Space(20F);
            GUILayout.Label("JOIN A KITCHEN", LabelStyle);

            GUILayout.BeginHorizontal();
            GUI.enabled = session.SupportsRelay;
            joinCode = TextField("Join code", joinCode, 12).ToUpperInvariant();
            GUILayout.Space(20F);
            if (Button("JOIN WITH CODE", 360F)) Join(useRelay: true);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            address = TextField("IP address", address, 64);
            GUILayout.Space(20F);
            if (Button("JOIN BY IP", 360F)) Join(useRelay: false);
            GUILayout.EndHorizontal();

            DrawError();

            GUILayout.FlexibleSpace();
            if (Button("BACK  (Esc)", 300F)) Back();
            EndWindow();
        }

        private void DrawConnecting()
        {
            BeginWindow(900F, 380F, "ONLINE");
            GUILayout.Label(session.IsHost ? "Opening your kitchen..." : "Connecting to the host...", LabelStyle);
            GUILayout.FlexibleSpace();
            if (Button("CANCEL  (Esc)")) session.Leave();
            EndWindow();
        }

        private void DrawLobby()
        {
            BeginWindow(1300F, 860F, "LOBBY");

            if (session.IsHost) DrawJoinInfo();

            GUILayout.Space(10F);
            var members = session.Members;
            for (int i = 0; i < GameSession.maxPlayers; i++)
            {
                if (i < members.Count)
                {
                    var host = i == 0 ? "  (host)" : string.Empty;
                    ColoredLabel($"P{i + 1}  {GetChefName(i)} chef  -  {members[i].Name}{host}", GetChefColor(i));
                }
                else ColoredLabel($"P{i + 1}  waiting for a player...", new Color(1F, 1F, 1F, 0.35F));
            }

            GUILayout.Space(10F);
            GUILayout.Label("Controls: WASD/Arrows or stick to move, E/A to pick up and drop, Q/X to chop and cook, P/Start for the match menu.", SmallLabelStyle);

            DrawError();
            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            if (session.IsHost)
            {
                if (Button("START MATCH")) session.StartMatch();
                GUILayout.Space(20F);
            }
            else GUILayout.Label("Waiting for the host to start the match...", LabelStyle);

            if (Button("LEAVE  (Esc)", 300F)) session.Leave();
            GUILayout.EndHorizontal();

            EndWindow();
        }

        private void DrawJoinInfo()
        {
            GUILayout.BeginHorizontal();

            if (session.UsesRelay)
            {
                GUILayout.Label("Join code:  " + session.JoinInfo, TitleStyle);
                if (Button("COPY", 180F)) GUIUtility.systemCopyBuffer = session.JoinInfo;
            }
            else
            {
                localAddresses ??= GetLocalAddresses();
                GUILayout.Label($"Your IP: {localAddresses}   Port: {session.JoinInfo}", LabelStyle);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawError()
        {
            if (!string.IsNullOrEmpty(session.LastError)) GUILayout.Label(session.LastError, ErrorStyle);
        }

        private void Host(bool useRelay)
        {
            OnlineSession.PlayerName = playerName;
            localAddresses = null;
            _ = session.HostAsync(useRelay, ParsePort());
        }

        private void Join(bool useRelay)
        {
            OnlineSession.PlayerName = playerName;

            if (useRelay && string.IsNullOrWhiteSpace(joinCode))
            {
                session.LastError = "Type the join code shown on the host screen.";
                return;
            }

            if (!useRelay)
            {
                if (string.IsNullOrWhiteSpace(address))
                {
                    session.LastError = "Type the host IP address.";
                    return;
                }

                PlayerPrefs.SetString(addressKey, address.Trim());
            }

            _ = session.JoinAsync(useRelay, joinCode, address, ParsePort());
        }

        private ushort ParsePort() => ushort.TryParse(port, out ushort value) && value > 0 ? value : OnlineSession.defaultPort;

        private void Back()
        {
            if (session != null && session.State != SessionState.Idle)
            {
                session.Leave();
                return;
            }

            if (session != null) session.LastError = null;

            isClosing = true;
            onClosed?.Invoke();
            Destroy(gameObject);
        }

        private static string GetLocalAddresses()
        {
            try
            {
                var addresses = NetworkInterface.GetAllNetworkInterfaces().
                    Where(networkInterface =>
                        networkInterface.OperationalStatus == OperationalStatus.Up &&
                        networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback).
                    SelectMany(networkInterface => networkInterface.GetIPProperties().UnicastAddresses).
                    Where(unicast => unicast.Address.AddressFamily == AddressFamily.InterNetwork).
                    Select(unicast => unicast.Address.ToString()).
                    Distinct().
                    ToArray();

                return addresses.Length > 0 ? string.Join(", ", addresses) : "unknown";
            }
            catch (Exception)
            {
                return "unknown";
            }
        }
    }
}
