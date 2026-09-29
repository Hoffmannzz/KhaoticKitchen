using System;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Relay;
using System.Threading.Tasks;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Netcode.Transports.UTP;

namespace KitchenChaos.Networking
{
    /// <summary>
    /// Lets players join each other with a short join code, through Unity Relay servers.
    /// No port forwarding is needed. Requires the project to be linked to a Unity Cloud project.
    /// </summary>
    public static class RelayConnector
    {
        private const string connectionType = "dtls";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            NetcodeTransport.RelayHost = HostAsync;
            NetcodeTransport.RelayJoin = JoinAsync;
        }

        private static async Task<string> HostAsync(UnityTransport transport, int maxClients)
        {
            await SignInAsync();

            var allocation = await RelayService.Instance.CreateAllocationAsync(maxClients);
            var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            transport.SetRelayServerData(new RelayServerData(allocation, connectionType));
            return joinCode;
        }

        private static async Task JoinAsync(UnityTransport transport, string joinCode)
        {
            await SignInAsync();

            var allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            transport.SetRelayServerData(new RelayServerData(allocation, connectionType));
        }

        private static async Task SignInAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                // A different profile for each game instance lets several copies play on the same computer.
                var profile = "kc" + Guid.NewGuid().ToString("N").Substring(0, 8);
                await UnityServices.InitializeAsync(new InitializationOptions().SetProfile(profile));
            }

            while (UnityServices.State == ServicesInitializationState.Initializing)
            {
                await Task.Yield();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }
}
