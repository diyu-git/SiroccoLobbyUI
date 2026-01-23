using System;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace SiroccoLobby.Services.Helpers
{
    internal sealed class NetworkManagerResolver
    {
        public object? Client { get; private set; }
        public object? Server { get; private set; }
        public object? Host   { get; private set; }

        public bool HasClient => Client != null;
        public bool HasServer => Server != null;
        public bool IsHost => Host != null;

        public bool Initialize(Type managerType)
        {
            Client = null;
            Server = null;
            Host   = null;

            var il2cppType = Il2CppSystem.Type.GetType("Il2CppWartide.WartideNetworkManager");
            if (il2cppType == null)
            {
                MelonLogger.Error("[ProtoLobby][NM] Il2Cpp WartideNetworkManager type not found");
                return false;
            }

            var managers = UnityEngine.Object.FindObjectsOfType(il2cppType);
            if (managers == null || managers.Length == 0)
            {
                MelonLogger.Warning("[ProtoLobby][NM] No WartideNetworkManager instances found");
                return false;
            }

            var isClientProp = managerType.GetProperty("isClient", BindingFlags.Public | BindingFlags.Instance);
            var isServerProp = managerType.GetProperty("isServer", BindingFlags.Public | BindingFlags.Instance);

            if (isClientProp == null || isServerProp == null)
            {
                MelonLogger.Error("[ProtoLobby][NM] isClient / isServer properties not found");
                return false;
            }

            object? firstClientCapable = null;
            object? firstServerCapable = null;
            object? firstHostCapable   = null;

            foreach (var m in managers)
            {
                bool isClient = (bool)(isClientProp.GetValue(m) ?? false);
                bool isServer = (bool)(isServerProp.GetValue(m) ?? false);

                if (isClient)
                    firstClientCapable ??= m;

                if (isServer)
                    firstServerCapable ??= m;

                if (isClient && isServer)
                    firstHostCapable ??= m;
            }

            // Prefer pure roles if they exist, otherwise fall back to capability overlap
            Client = FindPure(managerType, managers, isClientProp, isServerProp, wantClient: true)
                     ?? firstClientCapable;

            Server = FindPure(managerType, managers, isClientProp, isServerProp, wantClient: false)
                     ?? firstServerCapable;

            Host = firstHostCapable;

            LogResult();
            return Client != null || Server != null;
        }

        private static object? FindPure(
            Type managerType,
            UnityEngine.Object[] managers,
            PropertyInfo isClientProp,
            PropertyInfo isServerProp,
            bool wantClient)
        {
            foreach (var m in managers)
            {
                bool isClient = (bool)(isClientProp.GetValue(m) ?? false);
                bool isServer = (bool)(isServerProp.GetValue(m) ?? false);

                if (wantClient && isClient && !isServer)
                    return m;

                if (!wantClient && isServer && !isClient)
                    return m;
            }

            return null;
        }

        public bool EnsureResolved(Type managerType)
        {
            if (Client != null || Server != null)
                return true;

            return Initialize(managerType);
        }

        private void LogResult()
        {
            MelonLogger.Msg(
                $"[ProtoLobby][NM] Resolved → " +
                $"Client={Client != null}, Server={Server != null}, Host={Host != null}"
            );
        }
    }
}
