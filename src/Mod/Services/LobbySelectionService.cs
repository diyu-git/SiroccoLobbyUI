using System;
using System.Collections.Generic;
using System.Reflection;
using SiroccoLobby.Services.Core;

namespace SiroccoLobby.Services
{
    /// <summary>
    /// Handles captain and team selection logic (UI/state).
    /// </summary>
    public class LobbySelectionService
    {
        private readonly GameReflectionBridge _reflection;
        private bool _captainsInitialized = false;
		private PropertyInfo? _captainNameProp;
		private Type? _captainNamePropType;
		private bool _hasLoggedCaptainDebug = false;
		private bool _captainNameSearchFailed = false;
		private object? _cachedCaptainsList;

        public LobbySelectionService(GameReflectionBridge reflection)
        {
            _reflection = reflection;
        }

        public void InitializeCaptainSelection()
        {
            if (_captainsInitialized) return;
            try
            {
                _reflection.InitCaptainSelectionMethod?.Invoke(_reflection.GameAuthorityInstance, null);
                _captainsInitialized = true;
                _cachedCaptainsList = null; // Invalidate cache on init
                _captainNameProp = null;
                _captainNamePropType = null;
                OnCaptainsInitialized?.Invoke();
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error($"[LobbySelectionService] Failed to initialize captain selection: {ex.Message}");
            }
        }

        public int GetCaptainCount()
        {
            var list = GetCaptainsList();
            if (list == null) return 0;
            try
            {
                var countProp = list.GetType().GetProperty("Count");
                if (countProp == null) return 0;
                return (int)(countProp.GetValue(list) ?? 0);
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error($"[LobbySelectionService] Failed to get captain count: {ex.Message}");
                return 0;
            }
        }

        public void SetSelectedCaptain(int captainIndex)
        {
            var list = GetCaptainsList();
            if (list == null) return;
            try
            {
                var countProp = list.GetType().GetProperty("Count");
                int count = (int)(countProp?.GetValue(list) ?? 0);
                if (captainIndex < 0 || captainIndex >= count) return;
                var itemProp = list.GetType().GetProperty("Item");
                var captain = itemProp?.GetValue(list, new object[] { captainIndex });
                _reflection.SelectedIndexProp?.SetValue(_reflection.GameAuthorityInstance, captainIndex);
                _reflection.SelectedCaptainProp?.SetValue(_reflection.GameAuthorityInstance, captain);
                OnSelectedCaptainChanged?.Invoke(captainIndex);
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error($"[LobbySelectionService] Failed to set selected captain: {ex.Message}");
            }
        }

        public int GetSelectedCaptainIndex()
        {
            try
            {
                return (int)(_reflection.SelectedIndexProp?.GetValue(_reflection.GameAuthorityInstance) ?? -1);
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error($"[LobbySelectionService] Failed to get selected captain index: {ex.Message}");
                return -1;
            }
        }

        public string GetCaptainName(int index)
        {
            var list = GetCaptainsList();
            if (list == null) return $"Captain {index + 1}";
            try
            {
                var countProp = list.GetType().GetProperty("Count");
                int count = (int)(countProp?.GetValue(list) ?? 0);
                if (index < 0 || index >= count) return "Unknown";
                var itemProp = list.GetType().GetProperty("Item");
                var captain = itemProp?.GetValue(list, new object[] { index });
                if (captain == null) return $"Captain {index + 1}";
                // Efficient property caching: cache by type
                if (_captainNameProp != null && _captainNamePropType == captain.GetType())
                {
                    var nameValue = _captainNameProp.GetValue(captain);
                    if (nameValue != null && !string.IsNullOrEmpty(nameValue.ToString()))
                        return nameValue?.ToString() ?? "Unknown";
                }
                if (_captainNameSearchFailed) return $"Captain {index + 1}";
                var captainType = captain.GetType();
                foreach (var propName in new[] { "name", "Name", "displayName", "DisplayName", "captainName", "CaptainName", "labelCaptainName", "Title", "title", "Header", "header", "LocalizedName", "localizedName" })
                {
                    var nameProp = captainType.GetProperty(propName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (nameProp != null)
                    {
                        var nameValue = nameProp.GetValue(captain);
                        if (nameValue != null && !string.IsNullOrEmpty(nameValue.ToString()))
                        {
                            _captainNameProp = nameProp;
                            _captainNamePropType = captainType;
                            return nameValue?.ToString() ?? "Unknown";
                        }
                    }
                }
                if (!_hasLoggedCaptainDebug)
                {
                    _hasLoggedCaptainDebug = true;
                    _captainNameSearchFailed = true;
                    MelonLoader.MelonLogger.Warning($"[LobbySelectionService] Could not find name property for type {captainType.Name}. Available properties:");
                    foreach (var p in captainType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                        MelonLoader.MelonLogger.Warning($" - {p.Name} ({p.PropertyType.Name})");
                    foreach (var f in captainType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                        MelonLoader.MelonLogger.Warning($" - [Field] {f.Name} ({f.FieldType.Name})");
                }
                return $"Captain {index + 1}";
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error($"[LobbySelectionService] Failed to get captain name: {ex.Message}");
                return $"Captain {index + 1}";
            }
        }


        public event Action<int>? OnSelectedCaptainChanged;
        public event Action<int>? OnSelectedTeamChanged;
        public event Action<string>? OnUserNameChanged;
        public event Action? OnCaptainsInitialized;

        public void SetUserName(string userName)
        {
            try
            {
                _reflection.UserNameProp?.SetValue(_reflection.GameAuthorityInstance, userName);
                OnUserNameChanged?.Invoke(userName);
            }
            catch (Exception ex)
            {
                LogError("SetUserName", ex);
            }
        }

        public void SetSelectedTeam(int teamIndex)
        {
            // UI uses 1 (A) and 2 (B). Game dropdown uses 0 (A) and 1 (B).
            const int TeamUiOffset = 1;
            try
            {
                int gameTeamIndex = teamIndex - TeamUiOffset;
                if (gameTeamIndex < 0) gameTeamIndex = 0;
                _reflection.TeamSelectionIndexProp?.SetValue(_reflection.GameAuthorityInstance, gameTeamIndex);
                OnSelectedTeamChanged?.Invoke(teamIndex);
            }
            catch (Exception ex)
            {
                LogError("SetSelectedTeam", ex);
            }
        }
        // Helper: get and cache captains list
        private object? GetCaptainsList()
        {
            if (_cachedCaptainsList == null)
            {
                _cachedCaptainsList = _reflection.CaptainsListProp?.GetValue(_reflection.GameAuthorityInstance);
                if (_cachedCaptainsList != null && _captainsInitialized)
                {
                    OnCaptainsInitialized?.Invoke();
                }
            }
            return _cachedCaptainsList;
        }

        public int GetSelectedTeamIndex()
        {
            try
            {
                return (int)(_reflection.TeamSelectionIndexProp?.GetValue(_reflection.GameAuthorityInstance) ?? -1);
            }
            catch (Exception ex)
            {
                LogError("GetSelectedTeamIndex", ex);
                return -1;
            }
        }

        private void LogError(string context, Exception ex)
        {
            var realEx = ex.InnerException ?? ex;
            MelonLoader.MelonLogger.Error($"[LobbySelectionService:{context}] {realEx.Message}");
            MelonLoader.MelonLogger.Error(realEx.StackTrace);
        }
    }
}
