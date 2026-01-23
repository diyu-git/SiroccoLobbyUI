using System;
using MelonLoader;
using SiroccoLobby.Services.Core;

namespace SiroccoLobby.Services
{
    /// <summary>
    /// Facade that owns and orchestrates the lobby reflection services while preserving the historical
    /// ProtoLobbyIntegration API used by controllers/views.
    /// </summary>
    public sealed class ProtoLobbyIntegration : IDisposable
    {
        public GameReflectionBridge Reflection { get; private set; }
        public LobbySelectionService Selection { get; private set; }
        public NetworkIntegrationService Network { get; private set; }
        public LobbyCompletionService Completion { get; private set; }

        public event Action<bool>? OnReadyChanged;
        public event Action? OnClientGameStarted; // New event for client game start

        private bool _initialized;

        public ProtoLobbyIntegration()
        {
            Reflection = new GameReflectionBridge();
            Selection = new LobbySelectionService(Reflection);
            Network = new NetworkIntegrationService(Reflection);
            Completion = new LobbyCompletionService(Reflection);

            WireEvents();
        }

        public bool Initialize()
        {
            if (_initialized) return true;

            // The bridge may have been constructed too early (before Assembly-CSharp singletons exist).
            // If so, recreate it on-demand so we can re-query GameAuthority.Instance and other objects.
            if (!Reflection.IsValid)
            {
                Reflection = new GameReflectionBridge();
                if (!Reflection.IsValid)
                {
                    MelonLogger.Warning("[ProtoLobbyIntegration] Reflection bridge not ready yet.");
                    return false;
                }

                // Recreate dependent services with the refreshed bridge.
                // (Services currently capture the bridge in readonly fields.)
                Selection = new LobbySelectionService(Reflection);
                Network = new NetworkIntegrationService(Reflection);
                Completion = new LobbyCompletionService(Reflection);

                // Re-wire events against the new Completion instance.
                WireEvents();
            }

            _initialized = true;
            Selection.InitializeCaptainSelection();
            return true;
        }


        public void Shutdown()
        {
            Completion.OnLobbyServerCompleted -= OnServerCompleted;
            Completion.OnLobbyClientCompleted -= OnClientCompleted;
        }

        public void Dispose() => Shutdown();

        public bool IsReady => _initialized && Reflection.IsValid;
        public bool IsConnected => Network.IsClientConnected || Network.IsServerActive;

        // Selection
        public int GetCaptainCount() => Selection.GetCaptainCount();
        public void SetSelectedCaptain(int index) => Selection.SetSelectedCaptain(index);
        public int GetSelectedCaptainIndex() => Selection.GetSelectedCaptainIndex();
        public string GetCaptainName(int index) => Selection.GetCaptainName(index);
        public void SetUserName(string name) => Selection.SetUserName(name);
        public void SetSelectedTeam(int team) => Selection.SetSelectedTeam(team);
        public int GetSelectedTeamIndex() => Selection.GetSelectedTeamIndex();

        // Network
        public void SetReady(bool ready)
        {
            // One-time: dump proto-lobby/runtime tester object graph when the user first toggles ready.
            // This helps reverse-engineer the real readiness model even though IL2CPP wrapper bodies are opaque.
            Network.TryDumpProtoLobbyGraphOnce("SetReady");
            Network.UpdateLocalPlayerStatus(isReady: ready);
            OnReadyChanged?.Invoke(ready);
        }

        public void TriggerSinglePlayer() => Network.TriggerSinglePlayer();
        public void TriggerSinglePlayerP2P() => Network.TriggerSinglePlayerP2P();
        public void ShutdownNetwork(bool asHost) => Network.ShutdownNetwork(asHost);
        public void DisconnectNetworkClient() => Network.DisconnectClient();
        public void CallNetworkClientReady(int captainIndex, int teamIndex) => Network.CallNetworkClientReady(captainIndex, teamIndex);
        public bool ValidatePlayersReadyForGameStart() => Network.ValidatePlayersReadyForGameStart();
        public void ConnectToGameServer(string? address = null) => Network.ConnectToGameServer(address);

        // Completion
        public void CompleteProtoLobbyClient() => Completion.CompleteProtoLobbyClient();
        public void CompleteProtoLobbyServer(Action? onGameStarting = null) => Completion.CompleteProtoLobbyServer(onGameStarting);

        private void WireEvents()
        {
            // Make idempotent in case we recreate services during deferred initialization.
            Completion.OnLobbyServerCompleted -= OnServerCompleted;
            Completion.OnLobbyClientCompleted -= OnClientCompleted;
            Completion.OnLobbyServerCompleted += OnServerCompleted;
            Completion.OnLobbyClientCompleted += OnClientCompleted;
        }

        private static void OnServerCompleted() => MelonLogger.Msg("[ProtoLobbyIntegration] Lobby server completed.");
        
        private void OnClientCompleted()
        {
            MelonLogger.Msg("[ProtoLobbyIntegration] Lobby client completed - game starting.");
            OnClientGameStarted?.Invoke();
        }
    }
}
