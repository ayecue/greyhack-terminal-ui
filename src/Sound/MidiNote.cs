using System;

namespace GreyHackTerminalUI.Sound
{
    public struct MidiNote
    {
        public int Pitch { get; set; }      // MIDI note number (0-127, middle C = 60)
        public float Duration { get; set; }  // Duration in seconds
        public float Velocity { get; set; }  // Velocity/volume (0.0-1.0)
        
        public MidiNote(int pitch, float duration, float velocity = 0.7f)
        {
            Pitch = Math.Clamp(pitch, 0, 127);
            Duration = Math.Max(0.001f, duration);
            Velocity = Math.Clamp(velocity, 0f, 1f);
        }

        public float GetFrequency()
        {
            // MIDI note to frequency: f = 440 * 2^((n-69)/12)
            // where 69 is A4 (440 Hz)
            return 440f * (float)Math.Pow(2.0, (Pitch - 69) / 12.0);
        }
    }
}
