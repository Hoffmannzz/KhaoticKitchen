using UnityEngine;
using ActionCode.Physics;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace KitchenChaos.Networking
{
    /// <summary>
    /// Gives every counter, holder and item of an online match the same id on all machines.
    /// <para>
    /// Scene objects are numbered by their position in the hierarchy, which is identical on every
    /// machine. Items spawned during the match take the ids chosen by the host.
    /// </para>
    /// </summary>
    public static class NetworkEntityRegistry
    {
        public const int None = -1;

        private const int firstDynamicId = 100000;

        private static readonly Dictionary<int, GameObject> objects = new(256);
        private static readonly Dictionary<GameObject, int> ids = new(256);

        private static bool isActive;
        private static int nextDynamicId;
        private static int nextLocalId;
        private static List<int> recordedIds;
        private static Queue<int> replayedIds;

        public static bool IsActive => isActive;

        /// <summary>
        /// Numbers all the objects in the given scene.
        /// </summary>
        internal static void Begin(Scene scene)
        {
            Clear();
            isActive = true;

            var nextStaticId = 1;
            foreach (var root in scene.GetRootGameObjects())
            {
                RegisterHierarchy(root.transform, ref nextStaticId);
            }
        }

        internal static void End()
        {
            Clear();
            isActive = false;
        }

        /// <summary>
        /// Host: collects the ids of the items spawned while an authoritative change runs.
        /// </summary>
        internal static void BeginRecording(List<int> spawnedIds) => recordedIds = spawnedIds;
        internal static void EndRecording() => recordedIds = null;

        /// <summary>
        /// Client: gives the ids chosen by the host to the items spawned while replaying a change.
        /// </summary>
        internal static void BeginReplay(IEnumerable<int> spawnedIds) => replayedIds = new Queue<int>(spawnedIds);

        internal static void EndReplay()
        {
            if (replayedIds != null && replayedIds.Count > 0)
                Debug.LogWarning($"[Network] {replayedIds.Count} item(s) the host spawned were not spawned here.");

            replayedIds = null;
        }

        /// <summary>
        /// Called by items when they are spawned.
        /// </summary>
        public static void RegisterSpawned(GameObject gameObject)
        {
            if (!isActive || ids.ContainsKey(gameObject)) return;

            int id;
            if (replayedIds != null && replayedIds.Count > 0) id = replayedIds.Dequeue();
            else if (NetworkGame.IsHost)
            {
                id = nextDynamicId++;
                recordedIds?.Add(id);
            }
            else
            {
                id = nextLocalId--;
                Debug.LogWarning($"[Network] {gameObject.name} was spawned without the host. It will not be synchronized.");
            }

            Register(gameObject, id);
        }

        /// <summary>
        /// Called by items when they are destroyed.
        /// </summary>
        public static void Unregister(GameObject gameObject)
        {
            if (!ids.TryGetValue(gameObject, out int id)) return;

            ids.Remove(gameObject);
            objects.Remove(id);
        }

        public static int GetId(object component) =>
            component is Component c && c && ids.TryGetValue(c.gameObject, out int id) ? id : None;

        public static int GetId(GameObject gameObject) =>
            gameObject && ids.TryGetValue(gameObject, out int id) ? id : None;

        public static bool TryGet(int id, out GameObject gameObject)
        {
            if (id != None && objects.TryGetValue(id, out gameObject) && gameObject) return true;

            gameObject = null;
            return false;
        }

        public static bool TryGet<T>(int id, out T component) where T : class
        {
            if (TryGet(id, out GameObject gameObject) && gameObject.TryGetComponent(out component)) return true;

            component = null;
            return false;
        }

        public static IEnumerable<KeyValuePair<int, GameObject>> GetAll() => objects;

        private static void RegisterHierarchy(Transform transform, ref int nextStaticId)
        {
            // Every counter, holder, preparator and item implements IEnable.
            if (transform.TryGetComponent(out IEnable _)) Register(transform.gameObject, nextStaticId++);

            for (int i = 0; i < transform.childCount; i++)
            {
                RegisterHierarchy(transform.GetChild(i), ref nextStaticId);
            }
        }

        private static void Register(GameObject gameObject, int id)
        {
            if (objects.TryGetValue(id, out GameObject other) && other && other != gameObject)
                Debug.LogWarning($"[Network] Id {id} was already used by {other.name}.");

            objects[id] = gameObject;
            ids[gameObject] = id;
        }

        private static void Clear()
        {
            objects.Clear();
            ids.Clear();
            recordedIds = null;
            replayedIds = null;
            nextDynamicId = firstDynamicId;
            nextLocalId = None - 1;
        }
    }
}
