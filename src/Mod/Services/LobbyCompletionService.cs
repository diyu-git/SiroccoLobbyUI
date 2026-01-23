using System;
using SiroccoLobby.Services.Core;

namespace SiroccoLobby.Services
{
    /// <summary>
    /// Handles completion of the game's *ProtoLobby* flow (self-host / hacked singleplayer path).
    /// These calls intentionally target GameAuthority.CompleteProtoLobbyServer/Client.
    /// </summary>
    public class LobbyCompletionService
    {
        private readonly GameReflectionBridge _reflection;
        public event Action? OnLobbyServerCompleted;
        public event Action? OnLobbyClientCompleted;

        public LobbyCompletionService(GameReflectionBridge reflection)
        {
            _reflection = reflection;
        }

        public void CompleteProtoLobbyServer(Action? onGameStarting = null)
        {
            if (_reflection.CompleteProtoLobbyMethod == null)
            {
                MelonLoader.MelonLogger.Error("[LobbyCompletionService] Cannot complete - GameAuthority.CompleteProtoLobbyServer method not found!");
                return;
            }
            if (_reflection.GameAuthorityInstance == null)
            {
                MelonLoader.MelonLogger.Error("[LobbyCompletionService] Cannot complete - GameAuthority instance is null!");
                return;
            }
            try
            {
                InspectGameAuthorityFields(_reflection.GameAuthorityInstance);
                MelonLoader.MelonLogger.Msg("[LobbyCompletionService] Invoking GameAuthority.CompleteProtoLobbyServer...");
                _reflection.CompleteProtoLobbyMethod.Invoke(_reflection.GameAuthorityInstance, null);
                MelonLoader.MelonLogger.Msg("[LobbyCompletionService] ProtoLobby server completion succeeded");
                onGameStarting?.Invoke();
                OnLobbyServerCompleted?.Invoke();
            }
            catch (Exception ex)
            {
                LogReflectionException(nameof(CompleteProtoLobbyServer), ex);
            }
        }

        public void CompleteProtoLobbyClient()
        {
            if (_reflection.CompleteProtoLobbyClientMethod == null)
            {
                MelonLoader.MelonLogger.Error("[LobbyCompletionService] Cannot complete - GameAuthority.CompleteProtoLobbyClient method not found!");
                return;
            }
            if (_reflection.GameAuthorityInstance == null)
            {
                MelonLoader.MelonLogger.Error("[LobbyCompletionService] Cannot complete - GameAuthority instance is null!");
                return;
            }
            try
            {
                MelonLoader.MelonLogger.Msg("[LobbyCompletionService] Invoking GameAuthority.CompleteProtoLobbyClient...");
                _reflection.CompleteProtoLobbyClientMethod.Invoke(_reflection.GameAuthorityInstance, null);
                MelonLoader.MelonLogger.Msg("[LobbyCompletionService] ProtoLobby client completion succeeded");
                OnLobbyClientCompleted?.Invoke();
            }
            catch (Exception ex)
            {
                LogReflectionException(nameof(CompleteProtoLobbyClient), ex);
            }
        }

        // Helper for diagnostics (optional, can be expanded)
        private void InspectGameAuthorityFields(object? gameAuthority)
        {
            // Optionally inspect fields for debugging, as in original logic
            // ...
        }

        private void LogReflectionException(string context, Exception ex)
        {
            var realEx = ex.InnerException ?? ex;
            MelonLoader.MelonLogger.Error($"[LobbyCompletionService:{context}] {realEx.Message}");
            MelonLoader.MelonLogger.Error(realEx.StackTrace);
        }
    }
}
