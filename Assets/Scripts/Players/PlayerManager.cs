using UnityEngine;
using KitchenChaos.Matches;
using KitchenChaos.Sessions;
using KitchenChaos.Networking;
using ActionCode.PauseSystem;

namespace KitchenChaos.Players
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(Managers.EXECUTION_ORDER)]
    public sealed class PlayerManager : MonoBehaviour
    {
        [SerializeField] private PlayerSettings settings;
        [SerializeField] private MatchSettings matchSettings;
        [SerializeField] private PauseSettings pauseSettings;
        [SerializeField] private PlayerInputSettings inputSettings;
        [SerializeField] private bool canPause = true;

        internal PlayerSettings Settings => settings;

        private void Awake()
        {
            settings.Initialize();
            inputSettings.Initialize();
        }

        private void Start()
        {
            inputSettings.Enable();

            switch (GameSession.Mode)
            {
                case GameMode.LocalCoop:
                    EnableLocalCoopPlayers();
                    break;

                case GameMode.Online:
                    EnableOnlinePlayer();
                    break;

                default:
                    settings.EnableFirstPlayer();
                    break;
            }
        }

        private void OnEnable()
        {
            inputSettings.BindActions();

            inputSettings.OnSwitch += HandlePlayerSwitch;

            pauseSettings.OnPaused += HandlePaused;
            pauseSettings.OnResumed += HandleResumed;

            matchSettings.TimeLimit.OnFinished += HandleMatchFinished;

            if (canPause) inputSettings.OnPause += HandlePlayerPause;
        }

        private void OnDisable()
        {
            matchSettings.TimeLimit.OnFinished -= HandleMatchFinished;

            inputSettings.OnPause -= HandlePlayerPause;
            inputSettings.OnSwitch -= HandlePlayerSwitch;

            pauseSettings.OnPaused -= HandlePaused;
            pauseSettings.OnResumed -= HandleResumed;

            inputSettings.UnBindActions();

            GameSession.EnableLocalSeats(false);
        }

        /// <summary>
        /// Every local co-op player controls its own chef. Switching chefs is disabled.
        /// </summary>
        private void EnableLocalCoopPlayers()
        {
            var seats = GameSession.LocalSeats;
            var chefs = settings.PrepareChefs(seats.Count);

            settings.DisableAllPlayers();
            settings.DisablePlayerSwitch();

            var count = Mathf.Min(seats.Count, chefs.Count);
            for (int i = 0; i < count; i++)
            {
                chefs[i].Input.SetSource(seats[i].Input);
                chefs[i].SetActive(true);
            }

            GameSession.EnableLocalSeats(true);
        }

        /// <summary>
        /// This machine controls only its own chef. The other chefs are driven by the network.
        /// </summary>
        private void EnableOnlinePlayer()
        {
            var chefs = settings.PrepareChefs(GameSession.PlayerCount);
            var localSeat = GameSession.LocalOnlineSeat;

            settings.DisableAllPlayers();
            settings.DisablePlayerSwitch();

            if (localSeat >= 0 && localSeat < chefs.Count) chefs[localSeat].SetActive(true);
        }

        private void HandleMatchFinished()
        {
            settings.DisableAllPlayers();
            settings.DisablePlayerSwitch();
        }

        private void HandlePlayerSwitch()
        {
            inputSettings.ResetAxis();
            settings.Switch();
        }

        private void HandlePlayerPause()
        {
            // An online match cannot be paused, only left.
            if (GameSession.IsOnline) OnlineSession.ToggleMatchMenu();
            else pauseSettings.Pause();
        }

        private void HandlePaused()
        {
            inputSettings.Disable();
            GameSession.EnableLocalSeats(false);
        }

        private void HandleResumed()
        {
            inputSettings.Enable();
            if (GameSession.IsLocalCoop) GameSession.EnableLocalSeats(true);
        }
    }
}
