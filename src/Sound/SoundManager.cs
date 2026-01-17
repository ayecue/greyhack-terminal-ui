using System.Collections.Generic;
using UnityEngine;
using BepInEx.Logging;

namespace GreyHackTerminalUI.Sound
{
    public class SoundManager : MonoBehaviour
    {
        private static SoundManager _instance;
        public static SoundManager Instance => _instance;
        
        private const int MAX_SOUNDS_PER_TERMINAL = 100;
        
        // Dictionary<TerminalPID, Dictionary<SoundName, SoundPlayer>>
        private readonly Dictionary<int, Dictionary<string, SoundPlayer>> _terminalSounds = new Dictionary<int, Dictionary<string, SoundPlayer>>();
        private readonly object _playersLock = new object();
        private ManualLogSource _logger;
        private Transform _soundRoot;
        
        public static void Initialize(ManualLogSource logger)
        {
            if (_instance != null)
            {
                logger?.LogWarning("[SoundManager] Already initialized");
                return;
            }
            
            var go = new GameObject("SoundManager");
            _instance = go.AddComponent<SoundManager>();
            _instance._logger = logger;
            DontDestroyOnLoad(go);
            
            logger?.LogInfo("[SoundManager] Initialized");
        }
        
        private void Awake()
        {
            var rootGO = new GameObject("SoundPlayers");
            rootGO.transform.SetParent(transform);
            _soundRoot = rootGO.transform;
        }
        
        public SoundPlayer CreateSound(int terminalPID, string soundName)
        {
            lock (_playersLock)
            {
                if (!_terminalSounds.ContainsKey(terminalPID))
                {
                    _terminalSounds[terminalPID] = new Dictionary<string, SoundPlayer>(System.StringComparer.OrdinalIgnoreCase);
                }
                
                var sounds = _terminalSounds[terminalPID];
                
                // If sound already exists, return existing
                if (sounds.TryGetValue(soundName, out var existing))
                {
                    _logger?.LogDebug($"[SoundManager] Sound '{soundName}' already exists for terminal {terminalPID}");
                    return existing;
                }
                
                // Check if we've reached the maximum number of sounds
                if (sounds.Count >= MAX_SOUNDS_PER_TERMINAL)
                {
                    _logger?.LogWarning($"[SoundManager] Terminal {terminalPID} has reached the maximum of {MAX_SOUNDS_PER_TERMINAL} sounds");
                    return null;
                }
                
                // Create new sound player
                var playerGO = new GameObject($"SoundPlayer_{terminalPID}_{soundName}");
                playerGO.transform.SetParent(_soundRoot);
                var player = playerGO.AddComponent<SoundPlayer>();
                player.TerminalPID = terminalPID;
                sounds[soundName] = player;
                
                _logger?.LogDebug($"[SoundManager] Created sound '{soundName}' for terminal {terminalPID}");
                return player;
            }
        }
        
        public SoundPlayer GetSound(int terminalPID, string soundName)
        {
            lock (_playersLock)
            {
                if (_terminalSounds.TryGetValue(terminalPID, out var sounds))
                {
                    if (sounds.TryGetValue(soundName, out var player))
                    {
                        return player;
                    }
                }
                return null;
            }
        }
        
        public void DestroySound(int terminalPID, string soundName)
        {
            lock (_playersLock)
            {
                if (_terminalSounds.TryGetValue(terminalPID, out var sounds))
                {
                    if (sounds.TryGetValue(soundName, out var player))
                    {
                        sounds.Remove(soundName);
                        if (player != null)
                        {
                            Destroy(player.gameObject);
                            _logger?.LogDebug($"[SoundManager] Destroyed sound '{soundName}' for terminal {terminalPID}");
                        }
                    }
                }
            }
        }
        
        public void DestroyAllSounds(int terminalPID)
        {
            lock (_playersLock)
            {
                if (_terminalSounds.TryGetValue(terminalPID, out var sounds))
                {
                    foreach (var player in sounds.Values)
                    {
                        if (player != null)
                            Destroy(player.gameObject);
                    }
                    sounds.Clear();
                    _terminalSounds.Remove(terminalPID);
                    _logger?.LogDebug($"[SoundManager] Destroyed all sounds for terminal {terminalPID}");
                }
            }
        }
        
        private void OnDestroy()
        {
            lock (_playersLock)
            {
                foreach (var sounds in _terminalSounds.Values)
                {
                    foreach (var player in sounds.Values)
                    {
                        if (player != null)
                            Destroy(player.gameObject);
                    }
                }
                _terminalSounds.Clear();
            }
        }
    }
}
