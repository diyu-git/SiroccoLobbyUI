using MelonLoader;
using UnityEngine;
using System;
using System.Reflection;

namespace SiroccoLobby.Services
{
    public sealed class NativeLobbyWatcher
    {
        private readonly MelonLogger.Instance _logger;

        public NativeLobbyWatcher(MelonLogger.Instance log)
        {
            _logger = log;
        }

        public bool IsNativeLobbyActive()
        {
             var view = GetComponentByTypeName("UI_CustomGameView");
             return view != null && view.gameObject.activeInHierarchy;
        }

        public string? GetNativeLobbyCode()
        {
             try
             {
                 var view = GetComponentByTypeName("UI_CustomGameView");
                 if (view == null) return null;
                 
                 // _gameCode field is StyledText
                 var gameCodeComp = GetField<Component>(view, "_gameCode");
                 if (gameCodeComp != null)
                 {
                     // Access 'text' property
                     // StyledText inherits from TextMeshProUGUI
                     var prop = gameCodeComp.GetType().GetProperty("text");
                     if (prop != null)
                     {
                         return prop.GetValue(gameCodeComp, null) as string;
                     }
                 }
             }
             catch(Exception ex)
             {
                 _logger.Warning($"Error getting native lobby code: {ex.Message}");
             }
             return null;
        }
        
        // --- Helpers ---
        
        private Component? _cachedView;
        private float _lastScanTime = 0f;
        private const float SCAN_INTERVAL = 1.0f; // PERFORMANCE: Reduced from 0.25f (4x less CPU)

        private Component? GetComponentByTypeName(string typeName)
        {
            if (_cachedView != null && _cachedView.gameObject != null) // Ensure valid
            {
                 return _cachedView;
            }

            if (Time.time - _lastScanTime < SCAN_INTERVAL)
            {
                 return null; 
            }
            _lastScanTime = Time.time;

            var objects = Resources.FindObjectsOfTypeAll<Component>();
            foreach (var obj in objects)
            {
                if (obj.GetType().Name == typeName) 
                {
                    _cachedView = obj;
                    return obj;
                }
            }
            return null;
        }
        
        private T? GetField<T>(object instance, string fieldName) where T : class
        {
            if (instance == null) return null;
            var type = instance.GetType();
            var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (field != null) return field.GetValue(instance) as T;
            var prop = type.GetProperty(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if(prop != null) return prop.GetValue(instance, null) as T;
            return null;
        }
    }
}
