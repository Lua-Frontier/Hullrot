using Content.Client._NF.Latejoin;
using Content.Client.Audio;
using Content.Client.GameTicking.Managers;
using Content.Client.LateJoin;
using Content.Client.Lobby.UI;
using Content.Client.UserInterface.Systems.Chat;
using Content.Client.Voting;
using Robust.Client;
using Robust.Client.Console;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.Lobby
{
    public sealed class LobbyState : Robust.Client.State.State
    {
        [Dependency] private readonly IBaseClient _baseClient = default!;
        [Dependency] private readonly IClientConsoleHost _consoleHost = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly IUserInterfaceManager _userInterfaceManager = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly IVoteManager _voteManager = default!;

        private ClientGameTicker _gameTicker = default!;
        private ContentAudioSystem _contentAudioSystem = default!;

        protected override Type? LinkedScreenType { get; } = typeof(LobbyGui);
        public LobbyGui? Lobby;

        protected override void Startup()
        {
            if (_userInterfaceManager.ActiveScreen == null)
                return;

            Lobby = (LobbyGui) _userInterfaceManager.ActiveScreen;

            var chatController = _userInterfaceManager.GetUIController<ChatUIController>();
            _gameTicker = _entityManager.System<ClientGameTicker>();
            _contentAudioSystem = _entityManager.System<ContentAudioSystem>();

            chatController.SetMainChat(true);

            _voteManager.SetPopupContainer(Lobby.VoteContainer);
            LayoutContainer.SetAnchorPreset(Lobby, LayoutContainer.LayoutPreset.Wide);
            Lobby.ServerName.Text = _baseClient.GameInfo?.ServerName;

            UpdateLobbyUi();

            Lobby.CharacterPreview.CharacterSetupButton.OnPressed += OnSetupPressed;
            Lobby.ManifestButton.OnPressed += OnManifestPressed;
            Lobby.ReadyButton.OnPressed += OnReadyPressed;
            Lobby.ReadyButton.OnToggled += OnReadyToggled;
            Lobby.MinimizeButton.OnToggled += OnMinimizeToggled;

            Lobby.PreviousTrackButton.OnPressed += _ =>
            {
                _contentAudioSystem.PlayPreviousTrack();
                UpdateMusicButtonState();
            };
            Lobby.StopTrackButton.OnToggled += OnMusicToggled;
            Lobby.NextTrackButton.OnPressed += _ =>
            {
                _contentAudioSystem.PlayNextTrack();
                UpdateMusicButtonState();
            };

            Lobby.PreviousBackgroundButton.OnPressed += _ => CycleBackground(-1);
            Lobby.NextBackgroundButton.OnPressed += _ => CycleBackground(1);

            UpdateMusicButtonState();

            _gameTicker.InfoBlobUpdated += UpdateLobbyUi;
            _gameTicker.LobbyStatusUpdated += LobbyStatusUpdated;
            _gameTicker.LobbyLateJoinStatusUpdated += LobbyLateJoinStatusUpdated;
        }

        protected override void Shutdown()
        {
            var chatController = _userInterfaceManager.GetUIController<ChatUIController>();
            chatController.SetMainChat(false);
            _gameTicker.InfoBlobUpdated -= UpdateLobbyUi;
            _gameTicker.LobbyStatusUpdated -= LobbyStatusUpdated;
            _gameTicker.LobbyLateJoinStatusUpdated -= LobbyLateJoinStatusUpdated;

            _voteManager.ClearPopupContainer();

            Lobby!.CharacterPreview.CharacterSetupButton.OnPressed -= OnSetupPressed;
            Lobby!.ManifestButton.OnPressed -= OnManifestPressed;
            Lobby!.ReadyButton.OnPressed -= OnReadyPressed;
            Lobby!.ReadyButton.OnToggled -= OnReadyToggled;
            Lobby!.MinimizeButton.OnToggled -= OnMinimizeToggled;

            Lobby = null;
        }

        public void SwitchState(LobbyGui.LobbyGuiState state)
        {
            Lobby?.SwitchState(state);
        }

        private void OnMinimizeToggled(BaseButton.ButtonToggledEventArgs args)
        {
            if (args.Pressed)
                Lobby?.SwitchState(LobbyGui.LobbyGuiState.Minimize);
            else
            {
                Lobby?.SwitchState(LobbyGui.LobbyGuiState.Default);
                UpdateLobbyUi();
            }
        }

        private void OnMusicToggled(BaseButton.ButtonToggledEventArgs args)
        {
            _contentAudioSystem.ToggleMusicPlayback();
            UpdateMusicButtonState();
        }

        private void UpdateMusicButtonState()
        {
            if (Lobby == null) return;
            var isPlaying = _contentAudioSystem.IsMusicPlaying();
            Lobby.StopTrackButton.Text = isPlaying
                ? Loc.GetString("ui-lobby-stop-track")
                : Loc.GetString("ui-lobby-resume-track");
            Lobby.StopTrackButton.Pressed = !isPlaying;
        }

        private void CycleBackground(int direction)
        {
            if (Lobby == null) return;
            if (direction > 0)
                Lobby.Background.NextBackground();
            else
                Lobby.Background.PreviousBackground();
        }

        private void OnSetupPressed(BaseButton.ButtonEventArgs args)
        {
            SetReady(false);
            Lobby?.SwitchState(LobbyGui.LobbyGuiState.CharacterSetup);
        }

        private void OnReadyPressed(BaseButton.ButtonEventArgs args)
        {
            if (!_gameTicker.IsGameStarted)
                return;

            new NFLateJoinGui().OpenCentered();
        }

        private void OnReadyToggled(BaseButton.ButtonToggledEventArgs args)
        {
            SetReady(args.Pressed);
        }

        private void OnManifestPressed(BaseButton.ButtonEventArgs args)
        {
        }

        public override void FrameUpdate(FrameEventArgs e)
        {
            if (_gameTicker.IsGameStarted)
            {
                Lobby!.StartTime.Text = string.Empty;
                var roundTime = _gameTiming.CurTime.Subtract(_gameTicker.RoundStartTimeSpan);
                Lobby!.StationTime.Text = Loc.GetString("lobby-state-player-status-round-time", ("hours", roundTime.Hours), ("minutes", roundTime.Minutes));
                return;
            }

            Lobby!.StationTime.Text = Loc.GetString("lobby-state-player-status-round-not-started");
            string text;

            if (_gameTicker.Paused)
                text = Loc.GetString("lobby-state-paused");
            else if (_gameTicker.StartTime < _gameTiming.CurTime)
            {
                Lobby!.StartTime.Text = Loc.GetString("lobby-state-soon");
                return;
            }
            else
            {
                var difference = _gameTicker.StartTime - _gameTiming.CurTime;
                var seconds = difference.TotalSeconds;
                if (seconds < 0)
                    text = Loc.GetString(seconds < -5
                        ? "lobby-state-right-now-question"
                        : "lobby-state-right-now-confirmation");
                else
                    text = $"{difference.Minutes}:{difference.Seconds:D2}";
            }

            Lobby!.StartTime.Text = Loc.GetString("lobby-state-round-start-countdown-text", ("timeLeft", text));
        }

        private void LobbyStatusUpdated()
        {
            UpdateLobbyBackground();
            UpdateLobbyUi();
        }

        private void LobbyLateJoinStatusUpdated()
        {
            Lobby!.ReadyButton.Disabled = _gameTicker.DisallowedLateJoin;
        }

        private void UpdateLobbyUi()
        {
            if (_gameTicker.IsGameStarted)
            {
                Lobby!.ReadyButton.Text = Loc.GetString("lobby-state-ready-button-join-state");
                Lobby!.ReadyButton.ToggleMode = false;
                Lobby!.ReadyButton.Pressed = false;
                Lobby!.ObserveButton.Disabled = false;
                Lobby!.ManifestButton.Disabled = true;
            }
            else
            {
                Lobby!.StartTime.Text = string.Empty;
                Lobby!.ReadyButton.Text = Loc.GetString(Lobby!.ReadyButton.Pressed ? "lobby-state-player-status-ready" : "lobby-state-player-status-not-ready");
                Lobby!.ReadyButton.ToggleMode = true;
                Lobby!.ReadyButton.Disabled = false;
                Lobby!.ReadyButton.Pressed = _gameTicker.AreWeReady;
                Lobby!.ManifestButton.Disabled = false;
                Lobby!.ObserveButton.Disabled = true;
            }
        }

        private void UpdateLobbyBackground()
        {
        }

        private void SetReady(bool newReady)
        {
            if (_gameTicker.IsGameStarted)
                return;

            _consoleHost.ExecuteCommand($"toggleready {newReady}");
        }
    }
}
