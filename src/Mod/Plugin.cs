using System;
using MelonLoader;
using UnityEngine;
using SteamLobbyLib;
using SiroccoLobby.Model;
using SiroccoLobby.Controller;
using SiroccoLobby.Services;
using SiroccoLobby.UI;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using Steamworks;

[assembly: MelonInfo(typeof(SiroccoLobby.Plugin), "Sirocco Lobby UI", "1.0.0", "Diyu")]
[assembly: MelonGame("LunchboxEntertainment", "Sirocco")]

namespace SiroccoLobby
{
    internal class EventForwarder : SteamLobbyLib.ILobbyEvents
    {
        public SteamLobbyLib.ILobbyEvents? Delegate { get; set; }
        public void OnLobbyListReceived(System.Collections.Generic.List<SteamLobbyLib.LobbyData> lobbies) => Delegate?.OnLobbyListReceived(lobbies);
        public void OnLobbyJoined(SteamLobbyLib.LobbyId lobbyId) => Delegate?.OnLobbyJoined(lobbyId);
        public void OnLobbyDataUpdated(SteamLobbyLib.LobbyId lobbyId) => Delegate?.OnLobbyDataUpdated(lobbyId);
        public void OnLobbyMemberChanged(SteamLobbyLib.LobbyId lobbyId, SteamLobbyLib.LobbyId memberId, Steamworks.EChatMemberStateChange change) => Delegate?.OnLobbyMemberChanged(lobbyId, memberId, change);
    }

    public class Plugin : MelonMod
    {
        private SteamLobbyLib.SteamLobbyManager? _steamManager;
        private LobbyState? _state;
        private LobbyController? _controller;
        private LobbyUIRoot? _ui;
        private ProtoLobbyIntegration? _protoLobby;
        private CaptainSelectionController? _captainController;
        private SteamReflectionBridge? _reflectionBridge;
        private SteamLobbyServiceWrapper? _serviceWrapper;
        
        private bool _showUI = false;
        private bool _canShowUI = false;
        private bool _captainInitialized = false;
        // Deferred ProtoLobby init attempts to avoid calling game reflection too early.
        private int _protoInitAttempts = 0;
        private int _lastProtoInitFrame = 0;
        private const int MAX_PROTO_INIT_ATTEMPTS = 8; // try several times across frames
        private const int PROTO_INIT_FRAME_DELAY = 8; // wait 8 frames between attempts

        private bool _isSteamInitialized = false;
            
        // CONFIG: Production Version (No Patching)
        // Set to true only if you need low-level IL2CPP tracing
        // Make this non-const to avoid compile-time unreachable-code warnings when false.
        private static readonly bool ENABLE_TRACING = false;

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("Sirocco Lobby UI initializing (Built on SLL)...");
            Debug.Log("[ProtoLobby] Sirocco Lobby UI initializing (Built on SLL)...");
            
            // Apply Harmony patches (Tracing only)
            if (ENABLE_TRACING)
            {
                HarmonyPatches.Apply(HarmonyInstance);
            }
            else
            {
                MelonLogger.Msg("Production Build: Tracing patches disabled.");
            }
            
            // Initialize services
            _reflectionBridge = new SteamReflectionBridge(LoggerInstance);
            _state = new LobbyState();
            _protoLobby = new ProtoLobbyIntegration();
            
            // Create SteamLobbyManager (from the library) with an event forwarder.
            // We forward library events into the LobbyController to avoid circular construction.
            var eventForwarder = new EventForwarder();
            _steamManager = new SteamLobbyLib.SteamLobbyManager(eventForwarder, new MelonTraceLogger(LoggerInstance));
            
            try 
            {
                _steamManager.Initialize();
                _isSteamInitialized = true;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Failed to initialize SteamLobbyManager: {ex}");
                // We keep _isSteamInitialized false so we don't crash in OnUpdate
            }

            // Create the library adapter and the thin wrapper the mod uses.
            var libAdapter = new SteamLobbyManagerAdapter(_steamManager);
            _serviceWrapper = new SteamLobbyServiceWrapper(libAdapter, LoggerInstance);

            // Create controller (controller depends on the library interface and the wrapper)
            _controller = new LobbyController(_state, libAdapter, _protoLobby, LoggerInstance, _serviceWrapper);

            // Create CaptainSelectionController but defer initialization until the game is ready
            // (Assembly-CSharp / GameAuthority may not be available at Melon init time).
            try
            {
                _captainController = new CaptainSelectionController(libAdapter, _protoLobby);
                // Do NOT call Initialize() here; wait until the game reports Steam ready and
                // ProtoLobby.Initialize() succeeds. That ensures reflection can find GameAuthority.
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"CaptainSelectionController construction failed: {ex}");
            }

            // Wire the event forwarder to the controller so it receives library callbacks
            eventForwarder.Delegate = _controller;
            
            // Create UI
            // LobbyRoomView needs ProtoLobbyIntegration
            var browserView = new LobbyBrowserView(_state, _controller, LoggerInstance);
            var roomView = new LobbyRoomView(_state, _controller, _protoLobby, LoggerInstance);
            _ui = new LobbyUIRoot(_state, browserView, roomView);
            
            MelonLogger.Msg("Sirocco Lobby UI initialized!");
        }

        public override void OnUpdate()
        {
            if (_isSteamInitialized)
            {
                _steamManager?.Tick();
            }
            _controller?.OnUpdate();
            // Let the controller process any pending lobby summary updates (debounced)
            _controller?.ProcessPendingBatch();
            
            // F5 toggle
            if (Input.GetKeyDown(KeyCode.F5))
            {
                // Simple toggle logic
                if (!_canShowUI)
                {
                   // Try to init if game is ready
                   if (_reflectionBridge?.IsGameSteamReady() == true)
                   {
                        _canShowUI = true;
                        _showUI = true;
                        if (_state != null) _state.ShowDebugUI = true;
                        
                        // Initialize ProtoLobby
                        // Attempt a deferred initialization of the ProtoLobby integration. We try a few
                        // times over multiple frames to avoid doing reflection too early (Assembly-CSharp
                        // and game singletons may not be ready during Melon init).
                        bool protoReady = false;
                        int frame = UnityEngine.Time.frameCount;
                        if (_protoInitAttempts == 0 || (frame - _lastProtoInitFrame) >= PROTO_INIT_FRAME_DELAY)
                        {
                            _lastProtoInitFrame = frame;
                            _protoInitAttempts++;
                                try
                                {
                                    protoReady = _protoLobby?.Initialize() == true;
                                }
                                catch (Exception ex)
                                {
                                    MelonLogger.Warning($"ProtoLobby.Initialize threw: {ex}");
                                    protoReady = false;
                                }
                        }

                        if (protoReady)
                        {
                            // Ensure _protoLobby is non-null for the following operations
                            if (_protoLobby != null)
                            {
                                // Initialize() already performs captain selection init.

                                // Initialize mod-side captain controller now that game reflection has succeeded
                                if (!_captainInitialized && _captainController != null)
                                {
                                    try
                                    {
                                        if (_captainController.Initialize())
                                        {
                                            _captainInitialized = true;
                                            MelonLogger.Msg("CaptainSelectionController initialized (deferred)");
                                        }
                                    }
                                    catch (System.Exception ex)
                                    {
                                        MelonLogger.Warning($"CaptainSelectionController init failed (deferred): {ex}");
                                    }
                                }

                                // Sync defaults FROM game (Source of Truth)
                                if (_state != null)
                                {
                                    int gameCap = _protoLobby.GetSelectedCaptainIndex();
                                    if (gameCap >= 0) _state.SelectedCaptainIndex = gameCap;

                                    int gameTeam = _protoLobby.GetSelectedTeamIndex();
                                    if (gameTeam >= 0) _state.SelectedTeam = gameTeam + 1;
                                }

                                if (_serviceWrapper != null)
                                {
                                    if (_state != null) _state.IsProtoLobbyReady = true;
                                }
                            }
                        }
                        else
                        {
                            // If we haven't succeeded yet and attempts remain, log sparse diagnostics
                                if (_protoInitAttempts <= 1 || _protoInitAttempts % 3 == 0)
                                    MelonLogger.Msg($"ProtoLobby initialization attempt {_protoInitAttempts} failed; will retry up to {MAX_PROTO_INIT_ATTEMPTS} attempts");
                        }
                   }
                }
                else
                {
                    _showUI = !_showUI;
                    if (_state != null) _state.ShowDebugUI = _showUI;
                    if (_showUI && _isSteamInitialized) 
                    {
                        // Refresh when opening
                        _steamManager?.RequestLobbyList(); 
                    }
                }
            }
        }

        public override void OnApplicationQuit()
        {
            MelonLogger.Msg("[Shutdown] Application Quitting - Cleaning up lobby...");
            _controller?.EndLobby(LobbyController.LobbyEndMode.ApplicationQuit);
        }

        public override void OnGUI()
        {
            if (_showUI && _state?.ShowDebugUI == true)
            {
                _ui?.Draw();
            }
        }

        // Event handling and member-refresh/batching have been moved into LobbyController.
    }

    public class MelonTraceLogger : ITraceLogger
    {
        private readonly MelonLogger.Instance _logger;
        public MelonTraceLogger(MelonLogger.Instance logger) => _logger = logger;
        public void Log(string category, string message) => _logger.Msg($"[{category}] {message}");
    }
}
