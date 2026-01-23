using System;
using System.Reflection;
using MelonLoader;

namespace SiroccoLobby.Services.Helpers
{
    internal sealed class ClientAuthenticatorHelper
    {
        private readonly Type _managerType;
        private readonly NetworkManagerResolver _resolver;

        private object? _boundClientManager;
        private object? _authenticator;
        private MethodInfo? _onClientAuthenticate;

    private bool _hasLoggedAuthUnavailable;
    private bool _hasLoggedAuthAvailable;

        public ClientAuthenticatorHelper(Type managerType, NetworkManagerResolver resolver)
        {
            _managerType = managerType;
            _resolver = resolver;
        }

        public bool EnsureInitialized()
        {
            if (!_resolver.EnsureResolved(_managerType))
            {
                MelonLogger.Warning("[ProtoLobby][Auth] NetworkManager not resolved yet");
                return false;
            }

            var client = _resolver.Client;
            if (client == null)
            {
                MelonLogger.Warning("[ProtoLobby][Auth] No client NetworkManager available");
                return false;
            }

            if (ReferenceEquals(_boundClientManager, client))
                return true;

            _boundClientManager = client;
            _authenticator = null;
            _onClientAuthenticate = null;

            _hasLoggedAuthUnavailable = false;
            _hasLoggedAuthAvailable = false;

            return CacheAuthenticator(client);
        }

        public bool Authenticate()
        {
            if (!EnsureInitialized())
                return false;

            _onClientAuthenticate!.Invoke(_authenticator, null);
            return true;
        }

        private bool CacheAuthenticator(object clientManager)
        {
            _authenticator = TryGetAuthenticatorInstance(_managerType, clientManager);
            if (_authenticator == null)
            {
                // Not necessarily an error: some modes or early startup may not have created it yet.
                if (!_hasLoggedAuthUnavailable)
                {
                    _hasLoggedAuthUnavailable = true;
                    MelonLogger.Msg("[ProtoLobby][Auth] Client authenticator not available yet");
                }
                return false;
            }

            _onClientAuthenticate = _authenticator
                .GetType()
                .GetMethod("OnClientAuthenticate", BindingFlags.Public | BindingFlags.Instance);

            if (_onClientAuthenticate == null)
            {
                MelonLogger.Warning("[ProtoLobby][Auth] Authenticator found, but OnClientAuthenticate not found");
                return false;
            }

            if (!_hasLoggedAuthAvailable)
            {
                _hasLoggedAuthAvailable = true;
                _hasLoggedAuthUnavailable = false;
                MelonLogger.Msg("[ProtoLobby][Auth] Client authenticator available (cached)");
            }
            return true;
        }

        private static object? TryGetAuthenticatorInstance(Type managerType, object managerInstance)
        {
            const BindingFlags allInst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            string[] candidateNames = { "authenticator", "Authenticator", "m_Authenticator" };

            // Look for common names across the type hierarchy.
            for (Type? t = managerType; t != null; t = t.BaseType)
            {
                foreach (var name in candidateNames)
                {
                    var f = t.GetField(name, allInst);
                    if (f != null)
                    {
                        var v = f.GetValue(managerInstance);
                        if (v != null) return v;
                    }

                    var p = t.GetProperty(name, allInst);
                    if (p != null && p.GetIndexParameters().Length == 0)
                    {
                        try
                        {
                            var v = p.GetValue(managerInstance);
                            if (v != null) return v;
                        }
                        catch
                        {
                            // ignore
                        }
                    }
                }
            }

            // Conservative heuristic: any member type with OnClientAuthenticate() looks like an authenticator.
            for (Type? t = managerType; t != null; t = t.BaseType)
            {
                foreach (var f in t.GetFields(allInst))
                {
                    if (f.FieldType == null) continue;
                    if (f.FieldType.GetMethod("OnClientAuthenticate", BindingFlags.Public | BindingFlags.Instance) == null)
                        continue;
                    var v = f.GetValue(managerInstance);
                    if (v != null) return v;
                }

                foreach (var p in t.GetProperties(allInst))
                {
                    if (p.GetIndexParameters().Length != 0) continue;
                    if (p.PropertyType == null) continue;
                    if (p.PropertyType.GetMethod("OnClientAuthenticate", BindingFlags.Public | BindingFlags.Instance) == null)
                        continue;
                    try
                    {
                        var v = p.GetValue(managerInstance);
                        if (v != null) return v;
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }

            return null;
        }
    }
}
