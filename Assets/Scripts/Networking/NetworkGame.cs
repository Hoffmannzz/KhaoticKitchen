using System;
using UnityEngine;
using KitchenChaos.Items;

namespace KitchenChaos.Networking
{
    /// <summary>
    /// Entry point used by the game code to stay in sync during online matches.
    /// <para>
    /// The host runs the real game. Every change it decides (an interaction, an ingredient
    /// getting ready, a new order...) is wrapped with <see cref="Replicate"/> and replayed by
    /// the clients in the same order, so they reach the same state running the same code.
    /// </para>
    /// Outside online matches everything runs locally, as before.
    /// </summary>
    public static class NetworkGame
    {
        internal static NetworkMatch Match { get; set; }

        /// <summary>Whether an online match is running on this machine.</summary>
        public static bool IsOnline => Match != null;

        public static bool IsHost => Match != null && Match.IsHost;

        /// <summary>Whether this machine only mirrors the match run by the host.</summary>
        public static bool IsClient => Match != null && !Match.IsHost;

        /// <summary>Whether this machine decides what happens: offline, local co-op or the online host.</summary>
        public static bool HasAuthority => !IsClient;

        /// <summary>
        /// Runs a game change and sends it to the clients.
        /// <para>Offline: runs the action.</para>
        /// <para>Host: runs the action and sends it, with the ids of the items it spawned.</para>
        /// <para>Client: runs the action only while replaying the same change sent by the host.</para>
        /// </summary>
        /// <param name="type">The change type, handled by the clients.</param>
        /// <param name="source">The object where the change happens.</param>
        /// <param name="action">The change.</param>
        /// <param name="payload">Extra data the clients need, written after the action runs.</param>
        public static void Replicate(NetEventType type, Component source, Action action, Action<NetWriter> payload = null)
        {
            if (Match == null) action();
            else Match.Replicate(type, source, action, payload);
        }

        /// <summary>
        /// Makes a value computed during a replicated change identical on every machine.
        /// </summary>
        /// <param name="value">The value computed by this machine.</param>
        /// <returns>The host value.</returns>
        public static int Sync(int value) => Match != null ? Match.Sync(value) : value;

        public static bool Sync(bool value) => Sync(value ? 1 : 0) != 0;

        /// <summary>
        /// Sends a chef interaction to the host instead of running it locally.
        /// </summary>
        /// <returns>Whether the interaction was handled by the network.</returns>
        public static bool TryRouteInteraction(ItemHandler handler, InteractionKind kind) =>
            Match != null && Match.RouteLocalInteraction(handler, kind);
    }
}
