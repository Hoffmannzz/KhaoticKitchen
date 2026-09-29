using UnityEngine;

namespace KitchenChaos.Score
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(Managers.EXECUTION_ORDER)]
    public sealed class ScoreManager : MonoBehaviour
    {
        [SerializeField] private ScoreSettings settings;

        internal ScoreSettings Settings => settings;

        private void Awake() => settings.Initialize();
    }
}