namespace KitchenChaos.Networking
{
    /// <summary>
    /// The first byte of every network message.
    /// </summary>
    public enum MessageType : byte
    {
        // Lobby
        LobbyState = 1,
        StartMatch = 2,
        MatchReady = 3,

        // Host to clients
        GameEvent = 10,
        Timer = 11,
        Score = 12,
        Snapshot = 13,

        // Clients to host
        Interaction = 20,
        ChefPose = 21
    }

    /// <summary>
    /// Authoritative changes the host replicates to every client.
    /// </summary>
    public enum NetEventType : byte
    {
        Interaction = 1,
        PreparationCompleted = 2,
        StoveBurned = 3,
        TrashDestroyed = 4,
        PlateReturned = 5,
        OrderCreated = 6,
        OrderFailed = 7,
        OrderRemoved = 8
    }

    public enum TimerType : byte
    {
        CountDown = 0,
        TimeLimit = 1
    }

    public enum TimerEvent : byte
    {
        Started = 0,
        Updated = 1,
        Finished = 2,
        FinalSecondsStarted = 3
    }
}
