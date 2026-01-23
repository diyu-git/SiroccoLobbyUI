using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using SiroccoLobby.Model;
using SiroccoLobby.Services;

namespace SiroccoLobby.Services
{
    /// <summary>
    /// Thin mod-side wrapper that holds mod-specific helpers and mappings only.
    /// It should not duplicate Steam API logic — the canonical Steam boundary
    /// lives in the SteamLobbyLib (SLL) and implements <see cref="ISteamLobbyService"/>.
    /// </summary>
    public class SteamLobbyServiceWrapper
    {
        private readonly ISteamLobbyService _steamLib;
        private readonly MelonLogger.Instance _log;

        public SteamLobbyServiceWrapper(ISteamLobbyService steamLib, MelonLogger.Instance log)
        {
            _steamLib = steamLib;
            _log = log;
        }

        // Map library DTOs to the mod's LobbyMember model to avoid duplicating
        // the same mapping logic in multiple places (golden rule: no replication).
        public IEnumerable<LobbyMember> GetLobbyMembersModel(object lobbyId)
        {
            var infos = _steamLib.GetLobbyMembers(lobbyId) ?? Enumerable.Empty<LobbyMemberInfo>();
            return infos.Select(info => new LobbyMember
            {
                SteamId = info.SteamId,
                Name = info.Name,
                Team = info.Team,
                CaptainIndex = info.CaptainIndex,
                IsHost = info.IsHost,
                IsReady = info.IsReady
            });
        }

        // Expose the underlying library service for callers that need raw operations.
        public ISteamLobbyService Inner => _steamLib;
    }
}
