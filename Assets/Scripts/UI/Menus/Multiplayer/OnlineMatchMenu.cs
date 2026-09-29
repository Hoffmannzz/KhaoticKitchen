using UnityEngine;
using KitchenChaos.Networking;
using UnityEngine.InputSystem;

namespace KitchenChaos.UI
{
    /// <summary>
    /// Overlay shown during online matches: the leave menu (online matches cannot be paused),
    /// short notices and the loading status.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OnlineMatchMenu : MenuPanel
    {
        private const float noticeDuration = 4F;

        private bool isVisible;
        private string notice;
        private float noticeEndTime;

        public void Toggle() => isVisible = !isVisible && IsInMatch();
        public void Hide() => isVisible = false;

        public void ShowNotice(string text)
        {
            notice = text;
            noticeEndTime = Time.unscaledTime + noticeDuration;
        }

        private void Update()
        {
            if (!isVisible) return;
            if (!IsInMatch())
            {
                isVisible = false;
                return;
            }

            var leave =
                (Keyboard.current != null && Keyboard.current.yKey.wasPressedThisFrame) ||
                (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame);

            if (leave) Leave();
        }

        protected override void DrawPanel()
        {
            var session = OnlineSession.Instance;
            if (session == null) return;

            if (notice != null && Time.unscaledTime < noticeEndTime)
                GUI.Label(new Rect(0F, 40F, referenceWidth, 60F), notice, TitleStyle);

            if (session.IsWaitingForPlayers())
                GUI.Label(new Rect(0F, referenceHeight - 140F, referenceWidth, 60F), "Waiting for every player to load the kitchen...", TitleStyle);

            if (!isVisible) return;

            BeginWindow(900F, 420F, "ONLINE MATCH");
            GUILayout.Label("Online matches keep running while this menu is open.", LabelStyle);
            GUILayout.Label(session.IsHost ?
                "You are the host: leaving ends the match for everyone." :
                "Leaving takes you back to the main menu.", SmallLabelStyle);

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (Button("LEAVE  (Y)")) Leave();
            GUILayout.Space(20F);
            if (Button("BACK  (P / Start)")) Hide();
            GUILayout.EndHorizontal();
            EndWindow();
        }

        private void Leave()
        {
            isVisible = false;

            var session = OnlineSession.Instance;
            if (session) session.Leave();
        }

        private static bool IsInMatch() =>
            OnlineSession.Instance != null && OnlineSession.Instance.State == SessionState.InMatch;
    }
}
