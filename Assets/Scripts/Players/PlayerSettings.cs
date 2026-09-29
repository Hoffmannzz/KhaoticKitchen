using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenChaos.Players
{
    [CreateAssetMenu(fileName = "PlayerSettings", menuName = EditorPaths.SO + "Player Settings", order = 110)]
    public sealed class PlayerSettings : ScriptableObject
    {
        [SerializeField] private PlayerType first;
        [Tooltip("Chefs spawned when a multiplayer match has more players than the chefs placed in the scene.")]
        [SerializeField] private Player[] extraPlayers;

        public event Action OnPlayerEnabled;

        public Player Current { get; private set; }

        /// <summary>
        /// All chefs in the level ordered by <see cref="PlayerType"/>.
        /// In multiplayer matches, player N controls the chef at index N.
        /// </summary>
        public IReadOnlyList<Player> Ordered => ordered;

        private bool canSwitch;
        private Dictionary<PlayerType, Player> players;
        private readonly List<Player> ordered = new(4);

        /// <summary>
        /// Switches into the next Player.
        /// <para>It checks if switch is possible.</para>
        /// </summary>
        public void Switch()
        {
            // Multiplayer matches have no current chef to switch from.
            if (canSwitch) Switch(GetNextPlayerType());
        }

        /// <summary>
        /// Switches into the given player if available.
        /// </summary>
        /// <param name="type">The Player to switch.</param>
        public void Switch(PlayerType type)
        {
            if (canSwitch) EnablePlayer(players[type]);
        }

        internal void Initialize()
        {
            canSwitch = true;
            FindPlayersInstances();
        }

        internal void EnableFirstPlayer() => EnablePlayer(players[first]);

        internal void DisableAllPlayers()
        {
            foreach (var player in players.Values)
            {
                player.SetActive(false);
            }
        }

        internal void EnablePlayerSwitch() => canSwitch = true;

        internal void DisablePlayerSwitch() => canSwitch = false;

        /// <summary>
        /// Makes sure there is one chef for each player, spawning extra chefs if needed.
        /// </summary>
        /// <param name="count">The number of players.</param>
        /// <returns>The chefs ordered by <see cref="PlayerType"/>.</returns>
        internal IReadOnlyList<Player> PrepareChefs(int count)
        {
            var sceneChefs = ordered.ToArray();

            for (int i = 0; ordered.Count < count; i++)
            {
                var chef = SpawnExtraPlayer(i, sceneChefs);
                if (chef == null) break;

                chef.Index = players.Count;
                players.Add(chef.Type, chef);
                ordered.Add(chef);
            }

            return ordered;
        }

        private void FindPlayersInstances()
        {
            var index = 0;
            var instances = FindObjectsOfType<Player>();

            players = new Dictionary<PlayerType, Player>(instances.Length);

            foreach (var instance in instances)
            {
                instance.Index = index++;
                players.Add(instance.Type, instance);
            }

            ordered.Clear();
            ordered.AddRange(players.Values.OrderBy(player => player.Type));
        }

        private void EnablePlayer(Player player)
        {
            DisableAllPlayers();

            Current = player;
            Current.SetActive(true);

            OnPlayerEnabled?.Invoke();
        }

        private Player SpawnExtraPlayer(int index, Player[] sceneChefs)
        {
            if (sceneChefs.Length == 0) return null;

            var type = GetFreePlayerType();
            if (type == PlayerType.None) return null;

            var reference = sceneChefs[index % sceneChefs.Length].transform;
            var position = reference.position;

            // Places the new chef in the free corner between two other chefs.
            if (sceneChefs.Length > 1) position.x = sceneChefs[(index + 1) % sceneChefs.Length].transform.position.x;
            else position += Vector3.right * 1.5F * (index + 1);

            var prefab = extraPlayers != null ?
                extraPlayers.FirstOrDefault(player => player != null && player.Type == type) :
                null;

            if (prefab != null) return Instantiate(prefab, position, reference.rotation);

            Debug.LogWarning($"{name} has no chef prefab for {type}. Cloning {reference.name} instead.");

            var clone = Instantiate(reference.GetComponent<Player>(), position, reference.rotation);
            clone.SetType(type);
            return clone;
        }

        private PlayerType GetFreePlayerType()
        {
            var types = (PlayerType[])Enum.GetValues(typeof(PlayerType));
            return types.FirstOrDefault(type => type != PlayerType.None && !players.ContainsKey(type));
        }

        public PlayerType GetNextPlayerType()
        {
            var index = Current.Index;
            if (++index >= players.Count) index = 0;
            return players.Keys.ElementAt(index);
        }
    }
}
