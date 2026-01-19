using MelonLoader;
using System.Reflection;
using System;

namespace SiroccoLobby.Services
{
    public sealed class SteamReflectionBridge
    {
         private readonly MelonLogger.Instance _logger;

        public SteamReflectionBridge(MelonLogger.Instance log)
        {
            _logger = log;
        }

        public void CompareSteamIDs(ISteamLobbyService ourService)
        {
            try
            {
                _logger.Msg("--- Steam ID Comparison Test ---");

                string ourId = "Error";
                try
                {
                    var id = ourService.GetLocalSteamId();
                    ourId = id?.ToString() ?? "null";
                }
                catch (Exception ex) { ourId = $"Ex: {ex.Message}"; }
                _logger.Msg($"[My Wrapper] SteamID: {ourId}");

                string gameId = "Error";
                try
                {
                    var il2cppAssembly = Assembly.Load("Il2Cppcom.rlabrecque.steamworks.net");
                    if (il2cppAssembly != null)
                    {
                        var steamUserType = il2cppAssembly.GetType("Il2CppSteamworks.SteamUser");
                         if (steamUserType != null)
                        {
                            var method = steamUserType.GetMethod("GetSteamID");
                            if (method != null)
                            {
                                var result = method.Invoke(null, null);
                                gameId = result?.ToString() ?? "null";
                            }
                            else gameId = "Method GetSteamID not found";
                        }
                        else gameId = "Type SteamUser not found";
                    }
                    else gameId = "Assembly Il2Cppcom... not found";
                }
                catch (Exception ex) { gameId = $"Ex: {ex.Message}"; }
                _logger.Msg($"[Game Wrapper] SteamID: {gameId}");

                if (ourId == gameId && ourId != "Error")
                    _logger.Msg("SUCCESS: IDs MATCH! ");
                else
                    _logger.Warning("WARNING: IDs DO NOT MATCH! ");
            }
            catch (Exception e)
            {
                _logger.Error($"Comparison failed: {e.Message}");
            }
        }

        public bool IsGameSteamReady()
        {
            try
            {
                foreach(var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name == "Il2Cppcom.rlabrecque.steamworks.net")
                    {
                        var steamUserType = asm.GetType("Il2CppSteamworks.SteamUser");
                        if (steamUserType != null)
                        {
                            var method = steamUserType.GetMethod("GetSteamID");
                            if (method != null)
                            {
                                var result = method.Invoke(null, null);
                                return result != null;
                            }
                        }
                    }
                }
                return false;
            }
            catch 
            { 
                return false; 
            }
        }
    }
}
