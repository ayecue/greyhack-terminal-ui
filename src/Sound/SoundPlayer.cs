using System;
using System.Collections.Generic;
using UnityEngine;
using GreyHackTerminalUI.Settings;

namespace GreyHackTerminalUI.Sound
{
    public class SoundPlayer : MonoBehaviour
    {
        private List<MidiNote> _notes = new List<MidiNote>();
        private bool _isPlaying = false;
        private bool _loop = false;
        private AudioSource _audioSource;
        private AudioClip _generatedClip;
        private bool _needsRegenerate = true;
        
        private const int SAMPLE_RATE = 44100;
        private const int MAX_NOTES = 1000;
        private const float MAX_DURATION = 30f; // Max 30 seconds per sound
        
        public int TerminalPID { get; set; }
        public bool IsPlaying => _isPlaying && (_audioSource?.isPlaying ?? false);
        public bool Loop 
        { 
            get => _loop; 
            set 
            {
                _loop = value;
                if (_audioSource != null)
                {
                    _audioSource.loop = value;
                }
            } 
        }
        
        private void Awake()
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _audioSource.spatialBlend = 0f; // 2D sound
            _audioSource.volume = PluginSettings.SoundVolume?.Value ?? 1f;
            
            // Subscribe to volume changes
            PluginSettings.OnSoundVolumeChanged += OnVolumeChanged;
        }
        
        private void OnDestroy()
        {
            PluginSettings.OnSoundVolumeChanged -= OnVolumeChanged;
            Stop();
            if (_generatedClip != null)
            {
                Destroy(_generatedClip);
                _generatedClip = null;
            }
        }
        
        private void OnVolumeChanged(float volume)
        {
            if (_audioSource != null)
            {
                _audioSource.volume = volume;
            }
        }
        
        public void AddNote(int pitch, float duration, float velocity)
        {
            if (_notes.Count >= MAX_NOTES)
            {
                Debug.LogWarning($"[SoundPlayer] Terminal {TerminalPID} reached max notes limit ({MAX_NOTES})");
                return;
            }
            
            // Clamp values to reasonable ranges
            pitch = Mathf.Clamp(pitch, 0, 127);
            duration = Mathf.Clamp(duration, 0.001f, 10f);
            velocity = Mathf.Clamp01(velocity);
            
            _notes.Add(new MidiNote(pitch, duration, velocity));
            _needsRegenerate = true;
        }
        
        public void Clear()
        {
            Stop();
            _notes.Clear();
            _needsRegenerate = true;
            
            // Clean up generated clip
            if (_generatedClip != null)
            {
                Destroy(_generatedClip);
                _generatedClip = null;
            }
        }
        
        public void Play()
        {
            // Check if sound is enabled
            if (PluginSettings.SoundEnabled != null && !PluginSettings.SoundEnabled.Value)
            {
                return;
            }
            
            if (_notes.Count == 0)
            {
                Debug.LogWarning($"[SoundPlayer] Terminal {TerminalPID} has no notes to play");
                return;
            }
            
            // Stop any current playback
            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
            
            // Generate the audio clip if needed
            if (_needsRegenerate || _generatedClip == null)
            {
                GenerateAudioClip();
                _needsRegenerate = false;
            }
            
            if (_generatedClip == null)
            {
                Debug.LogError($"[SoundPlayer] Failed to generate audio clip");
                return;
            }
            
            // Ensure audio source exists
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 0f;
            }
            
            _audioSource.clip = _generatedClip;
            _audioSource.loop = _loop;
            _audioSource.volume = PluginSettings.SoundVolume?.Value ?? 1f;
            _audioSource.Play();
            _isPlaying = true;
        }
        
        public void Stop()
        {
            _isPlaying = false;
            if (_audioSource != null)
            {
                _audioSource.Stop();
            }
        }

        private void GenerateAudioClip()
        {
            if (_notes.Count == 0)
                return;
            
            // Calculate total duration
            float totalDuration = 0f;
            foreach (var note in _notes)
            {
                totalDuration += note.Duration;
            }
            
            // Clamp to max duration
            totalDuration = Mathf.Min(totalDuration, MAX_DURATION);
            
            int totalSamples = Mathf.CeilToInt(totalDuration * SAMPLE_RATE);
            if (totalSamples <= 0)
                return;
            
            // Clean up old clip
            if (_generatedClip != null)
            {
                Destroy(_generatedClip);
            }
            
            // Generate audio data
            float[] samples = new float[totalSamples];
            GenerateSamples(samples);
            
            // Create the audio clip
            _generatedClip = AudioClip.Create(
                $"Sound_{TerminalPID}_{GetHashCode()}",
                totalSamples,
                1, // Mono
                SAMPLE_RATE,
                false // Not streaming - this is key for reliability
            );
            
            _generatedClip.SetData(samples, 0);
        }

        private void GenerateSamples(float[] samples)
        {
            int sampleIndex = 0;
            float phase = 0f;
            
            foreach (var note in _notes)
            {
                float frequency = note.GetFrequency();
                float velocity = note.Velocity;
                int noteSamples = Mathf.CeilToInt(note.Duration * SAMPLE_RATE);
                
                // Attack and release envelope
                int attackSamples = Mathf.Min(noteSamples / 10, 441); // ~10ms attack
                int releaseSamples = Mathf.Min(noteSamples / 4, 4410); // ~100ms release
                
                for (int i = 0; i < noteSamples && sampleIndex < samples.Length; i++, sampleIndex++)
                {
                    // Calculate envelope
                    float envelope = 1f;
                    if (i < attackSamples)
                    {
                        envelope = (float)i / attackSamples;
                    }
                    else if (i > noteSamples - releaseSamples)
                    {
                        envelope = (float)(noteSamples - i) / releaseSamples;
                    }
                    
                    // Generate waveform (mix of sine and harmonics for richer sound)
                    float sample = 0f;
                    
                    // Fundamental sine wave
                    sample += Mathf.Sin(phase) * 0.5f;
                    
                    // Add some harmonics for richness
                    sample += Mathf.Sin(phase * 2f) * 0.2f; // 2nd harmonic
                    sample += Mathf.Sin(phase * 3f) * 0.1f; // 3rd harmonic
                    
                    // Add slight triangle wave component for brightness
                    float trianglePhase = (phase % (2f * Mathf.PI)) / Mathf.PI - 1f;
                    sample += (2f * Mathf.Abs(trianglePhase) - 1f) * 0.1f;
                    
                    // Apply envelope and velocity
                    samples[sampleIndex] = sample * envelope * velocity * 0.5f;
                    
                    // Advance phase
                    phase += 2f * Mathf.PI * frequency / SAMPLE_RATE;
                    
                    // Keep phase in reasonable range to prevent precision issues
                    if (phase > 2f * Mathf.PI * 1000f)
                    {
                        phase -= 2f * Mathf.PI * 1000f;
                    }
                }
            }
        }
        
        private void Update()
        {
            // Update playing state
            if (_isPlaying && _audioSource != null && !_audioSource.isPlaying && !_loop)
            {
                _isPlaying = false;
            }
        }
    }
}
