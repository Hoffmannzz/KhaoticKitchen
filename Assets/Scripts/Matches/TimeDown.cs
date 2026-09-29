using System;
using UnityEngine;
using System.Collections;

namespace KitchenChaos.Matches
{
    [Serializable]
    public class TimeDown
    {
        [field: SerializeField, Min(0)] public uint Time { get; private set; }

        public event Action OnStarted;
        public event Action OnFinished;
        public event Action<uint> OnUpdated;

        private readonly WaitForSeconds waitOneSecond = new WaitForSeconds(1F);

        public TimeDown(uint time) => this.Time = time;

        // Used by online clients to follow the host timers.
        internal void NotifyStarted() => OnStarted?.Invoke();
        internal void NotifyUpdated(uint time) => OnUpdated?.Invoke(time);
        internal void NotifyFinished() => OnFinished?.Invoke();

        internal IEnumerator CountDownRoutine()
        {
            var current = Time;

            OnStarted?.Invoke();

            do
            {
                OnUpdated?.Invoke(current);
                yield return waitOneSecond;
                current--;
            } while (current > 0);

            OnUpdated?.Invoke(current);
            OnFinished?.Invoke();
        }
    }
}