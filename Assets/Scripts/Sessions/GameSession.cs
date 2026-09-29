using UnityEngine;
using System.Linq;
using KitchenChaos.Players;
using System.Collections.Generic;

namespace KitchenChaos.Sessions
{
    public enum GameMode
    {
        SinglePlayer,
        LocalCoop,
        Online
    }

    /// <summary>
    /// A player taking part in an online match.
    /// </summary>
    public readonly struct OnlineSeat
    {
        public ulong ClientId { get; }
        public string Name { get; }

        public OnlineSeat(ulong clientId, string name)
        {
            ClientId = clientId;
            Name = name;
        }
    }

    /// <summary>
    /// How the next match will be played. It survives scene loads.
    /// </summary>
    public static class GameSession
    {
        public const int maxPlayers = 4;

        public static GameMode Mode { get; private set; } = GameMode.SinglePlayer;

        public static bool IsLocalCoop => Mode == GameMode.LocalCoop;
        public static bool IsOnline => Mode == GameMode.Online;

        public static IReadOnlyList<LocalSeat> LocalSeats => localSeats;
        public static IReadOnlyList<OnlineSeat> OnlineSeats => onlineSeats;

        /// <summary>
        /// The seat (and chef index) controlled by this machine in an online match.
        /// </summary>
        public static int LocalOnlineSeat { get; private set; } = -1;

        public static int PlayerCount => Mode switch
        {
            GameMode.LocalCoop => localSeats.Count,
            GameMode.Online => onlineSeats.Count,
            _ => 1
        };

        private static readonly List<LocalSeat> localSeats = new(maxPlayers);
        private static readonly List<OnlineSeat> onlineSeats = new(maxPlayers);

        public static void StartSinglePlayer()
        {
            Clear();
            Mode = GameMode.SinglePlayer;
        }

        public static void StartLocalCoop(IEnumerable<LocalSeat> seats)
        {
            Clear();
            localSeats.AddRange(seats.Take(maxPlayers));
            Mode = GameMode.LocalCoop;
        }

        public static void StartOnline(IEnumerable<OnlineSeat> seats, int localSeat)
        {
            Clear();
            onlineSeats.AddRange(seats.Take(maxPlayers));
            LocalOnlineSeat = localSeat;
            Mode = GameMode.Online;
        }

        public static void EnableLocalSeats(bool enabled)
        {
            foreach (var seat in localSeats)
            {
                if (enabled) seat.Input.Enable();
                else seat.Input.Disable();
            }
        }

        private static void Clear()
        {
            foreach (var seat in localSeats)
            {
                seat.Input.Dispose();
            }

            localSeats.Clear();
            onlineSeats.Clear();
            LocalOnlineSeat = -1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            localSeats.Clear();
            onlineSeats.Clear();
            LocalOnlineSeat = -1;
            Mode = GameMode.SinglePlayer;
        }
    }
}
