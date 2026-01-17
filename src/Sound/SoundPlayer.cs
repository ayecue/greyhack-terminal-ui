using System;
using System.Collections.Generic;
using UnityEngine;

namespace GreyHackTerminalUI.Sound
{
    public class SoundPlayer : MonoBehaviour
    {
        private List<MidiNote> _notes = new List<MidiNote>();
        private int _currentNoteIndex = 0;
        private float _currentNoteTime = 0f;
        private bool _isPlaying = false;
        private bool _loop = false;
        private AudioSource _audioSource;
        
        private const int SAMPLE_RATE = 44100;
        private const int MAX_NOTES = 1000; // Limit number of notes to prevent abuse
        
        private float _phase = 0f;
        private float _currentFrequency = 0f;
        private float _currentVolume = 0f;
        
        public int TerminalPID { get; set; }
        public bool IsPlaying => _isPlaying;
        public bool Loop 
        { 
            get => _loop; 
            set 
            {
                _loop = value; 
            } 
        }
        
        private void Awake()
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.loop = true;
            _audioSource.volume = 1f;
        }
        
        public void AddNote(int pitch, float duration, float velocity)
        {
            if (_notes.Count >= MAX_NOTES)
            {
                Debug.LogWarning($"[SoundPlayer] Terminal {TerminalPID} reached max notes limit ({MAX_NOTES})");
                return;
            }
            
            _notes.Add(new MidiNote(pitch, duration, velocity));
        }
        
        public void Clear()
        {
            Stop();
            _notes.Clear();
        }
        
        public void Play()
        {
            if (_notes.Count == 0)
            {
                Debug.LogWarning($"[SoundPlayer] Terminal {TerminalPID} has no notes to play");
                return;
            }
            
            // Preserve loop state when restarting
            bool preserveLoop = _loop;
            Stop();
            _loop = preserveLoop;
            
            _currentNoteIndex = 0;
            _currentNoteTime = 0f;
            _isPlaying = true;
            _phase = 0f;
            
            // Ensure audio source is initialized
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.loop = true;
                _audioSource.volume = 1f;
            }
            
            // Start the audio clip
            if (!_audioSource.isPlaying)
            {
                // Create a longer streaming clip (10 seconds) to accommodate longer songs
                _audioSource.clip = AudioClip.Create("GeneratedSound", SAMPLE_RATE * 10, 1, SAMPLE_RATE, true, OnAudioRead);
                _audioSource.Play();
            }
        }
        
        public void Stop()
        {
            _isPlaying = false;
            _loop = false;
            _audioSource.Stop();
            _currentVolume = 0f;
            _currentFrequency = 0f;
        }
        
        private void Update()
        {
            if (!_isPlaying || _notes.Count == 0)
                return;
            
            _currentNoteTime += Time.deltaTime;
            
            // Check if current note is finished
            if (_currentNoteIndex < _notes.Count)
            {
                var currentNote = _notes[_currentNoteIndex];
                if (_currentNoteTime >= currentNote.Duration)
                {
                    _currentNoteIndex++;
                    _currentNoteTime = 0f;

                    // If we've played all notes
                    if (_currentNoteIndex >= _notes.Count)
                    {
                        if (_loop)
                        {
                            // Restart from beginning
                            _currentNoteIndex = 0;
                            _currentNoteTime = 0f;
                        }
                        else
                        {
                            Stop();
                            return;
                        }
                    }
                }
            }
        }
        
        private void OnAudioRead(float[] data)
        {
            if (!_isPlaying)
            {
                // Fill with silence
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = 0f;
                }
                return;
            }
            
            // Handle loop restart if we've reached the end
            if (_currentNoteIndex >= _notes.Count)
            {
                if (_loop && _notes.Count > 0)
                {
                    _currentNoteIndex = 0;
                    _currentNoteTime = 0f;
                }
                else
                {
                    // Fill with silence
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = 0f;
                    }
                    return;
                }
            }
            
            var currentNote = _notes[_currentNoteIndex];
            _currentFrequency = currentNote.GetFrequency();
            _currentVolume = currentNote.Velocity;
            
            float increment = _currentFrequency * 2f * Mathf.PI / SAMPLE_RATE;
            
            for (int i = 0; i < data.Length; i++)
            {
                // Generate a simple sine wave for the tone
                data[i] = Mathf.Sin(_phase) * _currentVolume * 0.5f;
                _phase += increment;
                
                // Wrap phase to prevent float precision issues
                if (_phase > 2f * Mathf.PI)
                {
                    _phase -= 2f * Mathf.PI;
                }
            }
        }
        
        private void OnDestroy()
        {
            Stop();
            if (_audioSource != null && _audioSource.clip != null)
            {
                Destroy(_audioSource.clip);
            }
        }
    }
}
