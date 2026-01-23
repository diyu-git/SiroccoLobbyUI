using System;
using System.Diagnostics;
using MelonLoader;

namespace SiroccoLobby.Services.Helpers
{
    internal sealed class AuthDebugTracker
    {
        private readonly Stopwatch _sw = Stopwatch.StartNew();
        private bool? _lastAuthenticated;
        private bool? _lastConnected;

        // Throttling knobs (keep default conservative to avoid log spam)
        private long _lastPollLogMs;
        private int _pollLogBudget = 40;

        public void Reset(string reason)
        {
            _sw.Restart();
            _lastAuthenticated = null;
            _lastConnected = null;
            _lastPollLogMs = 0;
            _pollLogBudget = 40;
            MelonLogger.Msg($"[ProtoLobby][AuthTrace] Reset ({reason})");
        }

        public void LogConnectRequested(string address)
        {
            MelonLogger.Msg($"[ProtoLobby][AuthTrace] Connect requested; address='{address}'");
        }

        public void LogAuthenticatorTriggered(bool success, string source)
        {
            MelonLogger.Msg($"[ProtoLobby][AuthTrace] Authenticator trigger ({source}) => {(success ? "SUCCESS" : "FAIL")}");
        }

        public void TrackStates(bool? isConnected, bool? isAuthenticated, string context)
        {
            // If we don't know (reflection missing), don't spam.
            if (!isConnected.HasValue && !isAuthenticated.HasValue)
                return;

            bool changed = false;

            if (isConnected.HasValue && _lastConnected != isConnected)
            {
                changed = true;
                _lastConnected = isConnected;
            }

            if (isAuthenticated.HasValue && _lastAuthenticated != isAuthenticated)
            {
                changed = true;
                _lastAuthenticated = isAuthenticated;
            }

            var nowMs = _sw.ElapsedMilliseconds;

            // Log immediately on change; otherwise log at most every 500ms and only for a while.
            if (!changed)
            {
                if (_pollLogBudget <= 0)
                    return;

                if (nowMs - _lastPollLogMs < 500)
                    return;

                _lastPollLogMs = nowMs;
                _pollLogBudget--;
            }

            MelonLogger.Msg($"[ProtoLobby][AuthTrace] t={nowMs}ms ({context}) connected={Fmt(isConnected)} authenticated={Fmt(isAuthenticated)}");
        }

        public void LogBlockedReady(string reason, bool? isConnected, bool? isAuthenticated)
        {
            var nowMs = _sw.ElapsedMilliseconds;
            MelonLogger.Warning($"[ProtoLobby][AuthTrace] t={nowMs}ms Ready/AddPlayer blocked: {reason} connected={Fmt(isConnected)} authenticated={Fmt(isAuthenticated)}");
        }

        private static string Fmt(bool? v) => v.HasValue ? (v.Value ? "true" : "false") : "?";
    }
}
