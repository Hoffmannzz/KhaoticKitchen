using System;
using UnityEngine;
using KitchenChaos.Scenes;
using KitchenChaos.Players;
using KitchenChaos.Sessions;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace KitchenChaos.UI
{
    /// <summary>
    /// Menu where up to four players join a local co-op match using their own keyboard half or gamepad.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalCoopLobby : MenuPanel
    {
        private SceneSettings sceneSettings;
        private Action onClosed;

        private bool isClosing;
        private readonly List<LocalSeat> seats = new(GameSession.MaxPlayers);

        public static LocalCoopLobby Open(SceneSettings sceneSettings, Action onClosed)
        {
            var lobby = new GameObject(nameof(LocalCoopLobby)).AddComponent<LocalCoopLobby>();

            lobby.sceneSettings = sceneSettings;
            lobby.onClosed = onClosed;

            // Stops gamepad/keyboard presses from also clicking the hidden menu buttons.
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);

            return lobby;
        }

        private void Update()
        {
            if (isClosing) return;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.eKey.wasPressedThisFrame) Join(SeatDevice.KeyboardLeft, null);
                if (keyboard.periodKey.wasPressedThisFrame || keyboard.rightCtrlKey.wasPressedThisFrame) Join(SeatDevice.KeyboardRight, null);

                if (keyboard.qKey.wasPressedThisFrame) Leave(SeatDevice.KeyboardLeft, null);
                if (keyboard.slashKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame) Leave(SeatDevice.KeyboardRight, null);

                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) TryStart();
                if (keyboard.escapeKey.wasPressedThisFrame) Close();
            }

            foreach (var gamepad in Gamepad.all)
            {
                if (gamepad.buttonSouth.wasPressedThisFrame) Join(SeatDevice.Gamepad, gamepad);
                if (gamepad.startButton.wasPressedThisFrame) TryStart();
                if (gamepad.buttonEast.wasPressedThisFrame)
                {
                    if (IsJoined(SeatDevice.Gamepad, gamepad)) Leave(SeatDevice.Gamepad, gamepad);
                    else Close();
                }
            }
        }

        private void OnDestroy() => DisposeSeats();

        protected override void DrawPanel()
        {
            BeginWindow(1180F, 820F, "LOCAL CO-OP");

            GUILayout.Label("Up to 4 players on this computer. Each player controls their own chef.", SmallLabelStyle);
            GUILayout.Space(16F);

            for (int i = 0; i < GameSession.MaxPlayers; i++)
            {
                var text = i < seats.Count ?
                    $"P{i + 1}  {GetChefName(i)} chef  -  {seats[i].GetDeviceName()}" :
                    $"P{i + 1}  waiting for a player...";
                ColoredLabel(text, i < seats.Count ? GetChefColor(i) : new Color(1F, 1F, 1F, 0.35F));
            }

            GUILayout.Space(16F);
            GUILayout.Label("Keyboard left: press E to join. WASD move, E pick up/drop, Q chop/cook.", SmallLabelStyle);
            GUILayout.Label("Keyboard right: press . or Right Ctrl to join. Arrows move, . pick up/drop, / chop/cook.", SmallLabelStyle);
            GUILayout.Label("Gamepad: press A (Cross) to join, B (Circle) to leave. Stick move, A pick up/drop, X (Square) chop/cook.", SmallLabelStyle);
            GUILayout.Label("P or Start pauses the match.", SmallLabelStyle);

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUI.enabled = seats.Count > 0;
            if (Button("START  (Enter / Start)")) TryStart();
            GUI.enabled = true;
            GUILayout.Space(20F);
            if (Button("BACK  (Esc)", 300F)) Close();
            GUILayout.EndHorizontal();

            EndWindow();
        }

        private void Join(SeatDevice device, Gamepad gamepad)
        {
            if (seats.Count >= GameSession.MaxPlayers || IsJoined(device, gamepad)) return;
            seats.Add(new LocalSeat(device, gamepad));
        }

        private void Leave(SeatDevice device, Gamepad gamepad)
        {
            var index = seats.FindIndex(seat => seat.Uses(device, gamepad));
            if (index < 0) return;

            seats[index].Input.Dispose();
            seats.RemoveAt(index);
        }

        private bool IsJoined(SeatDevice device, Gamepad gamepad) => seats.Exists(seat => seat.Uses(device, gamepad));

        private void TryStart()
        {
            if (isClosing || seats.Count == 0) return;

            isClosing = true;

            // The session now owns the seats (and their inputs).
            GameSession.StartLocalCoop(seats);
            seats.Clear();

            sceneSettings.GoToGame();
            Destroy(gameObject);
        }

        private void Close()
        {
            if (isClosing) return;

            isClosing = true;
            onClosed?.Invoke();
            Destroy(gameObject);
        }

        private void DisposeSeats()
        {
            foreach (var seat in seats)
            {
                seat.Input.Dispose();
            }
            seats.Clear();
        }
    }
}
