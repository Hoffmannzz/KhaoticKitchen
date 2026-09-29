using System;
using TMPro;
using UnityEngine;
using ActionCode.UI;
using UnityEngine.UI;
using System.Collections;
using KitchenChaos.Scenes;
using KitchenChaos.Sessions;
using KitchenChaos.Networking;

namespace KitchenChaos.UI
{
    [DisallowMultipleComponent]
    public sealed class MainMenuOptions : AbstractMenu
    {
        [SerializeField] private SceneSettings sceneSettings;

        [Header("Buttons")]
        [SerializeField] private DelayedButton playButton;
        [SerializeField] private DelayedButton tutorialButton;
        [SerializeField] private DelayedButton soundsButton;
        [SerializeField] private DelayedButton creditsButton;

        public event Action OnOpenCreditsRequested;
        public event Action OnOpenSoundsOptionsRequested;

        private DelayedButton coopButton;
        private DelayedButton onlineButton;
        private MenuPanel openPanel;

        protected override void BindButtonsEvents()
        {
            CreateMultiplayerButtons();

            playButton.onClick.AddListener(PlayGame);
            coopButton.onClick.AddListener(OpenLocalCoop);
            onlineButton.onClick.AddListener(OpenOnline);
            tutorialButton.onClick.AddListener(PlayTutorial);
            soundsButton.onClick.AddListener(RequestOpenSoundOptions);
            creditsButton.onClick.AddListener(RequestOpenCredits);

            // Coming back from an online match: go straight back to the lobby.
            // Coroutines only need an active GameObject. isActiveAndEnabled is still false during Awake.
            if (OnlineSession.ShouldShowMenu && gameObject.activeInHierarchy) StartCoroutine(OpenOnlineAfterMenuShows());
        }

        protected override void UnBindButtonsEvents()
        {
            playButton.onClick.RemoveListener(PlayGame);
            coopButton.onClick.RemoveListener(OpenLocalCoop);
            onlineButton.onClick.RemoveListener(OpenOnline);
            tutorialButton.onClick.RemoveListener(PlayTutorial);
            soundsButton.onClick.RemoveListener(RequestOpenSoundOptions);
            creditsButton.onClick.RemoveListener(RequestOpenCredits);
        }

        /// <summary>
        /// Clones the Play button so the new buttons share the menu look.
        /// </summary>
        private void CreateMultiplayerButtons()
        {
            if (coopButton != null) return;

            coopButton = CloneButton(playButton, "CoopButton", "CO-OP", 1);
            onlineButton = CloneButton(playButton, "OnlineButton", "ONLINE", 2);

            var layout = playButton.transform.parent.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (layout) layout.spacing = Mathf.Min(layout.spacing, 40F);
        }

        private static DelayedButton CloneButton(DelayedButton source, string name, string label, int siblingOffset)
        {
            var button = Instantiate(source, source.transform.parent);

            button.name = name;
            button.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + siblingOffset);

            var text = button.GetComponentInChildren<TMP_Text>(true);
            if (text) text.text = label;

            return button;
        }

        private void PlayGame()
        {
            GameSession.StartSinglePlayer();
            sceneSettings.GoToGame();
        }

        private void PlayTutorial()
        {
            GameSession.StartSinglePlayer();
            sceneSettings.GoToTutorial();
        }

        private void OpenLocalCoop()
        {
            if (openPanel) return;

            Hide();
            openPanel = LocalCoopLobby.Open(sceneSettings, HandlePanelClosed);
        }

        private void OpenOnline()
        {
            if (openPanel) return;

            Hide();
            openPanel = OnlineLobbyMenu.Open(sceneSettings, HandlePanelClosed);
        }

        private IEnumerator OpenOnlineAfterMenuShows()
        {
            yield return new WaitForSecondsRealtime(0.5F);
            if (OnlineSession.ShouldShowMenu) OpenOnline();
        }

        private void HandlePanelClosed()
        {
            openPanel = null;
            Show();
        }

        private void RequestOpenCredits() => OnOpenCreditsRequested?.Invoke();
        private void RequestOpenSoundOptions() => OnOpenSoundsOptionsRequested?.Invoke();
    }
}
