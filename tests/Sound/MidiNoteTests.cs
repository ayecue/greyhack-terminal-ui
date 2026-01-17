using Xunit;
using GreyHackTerminalUI.Sound;

namespace GreyHackTerminalUI.Tests.Sound
{
    public class MidiNoteTests
    {
        [Fact]
        public void MidiNote_CreatesNoteWithCorrectValues()
        {
            // Arrange & Act
            var note = new MidiNote(60, 0.5f, 0.7f);

            // Assert
            Assert.Equal(60, note.Pitch);
            Assert.Equal(0.5f, note.Duration);
            Assert.Equal(0.7f, note.Velocity);
        }

        [Fact]
        public void MidiNote_ClampsPitchTo0_127Range()
        {
            // Arrange & Act
            var noteLow = new MidiNote(-10, 0.5f);
            var noteHigh = new MidiNote(200, 0.5f);

            // Assert
            Assert.Equal(0, noteLow.Pitch);
            Assert.Equal(127, noteHigh.Pitch);
        }

        [Fact]
        public void MidiNote_ClampsVelocityTo0_1Range()
        {
            // Arrange & Act
            var noteLow = new MidiNote(60, 0.5f, -0.5f);
            var noteHigh = new MidiNote(60, 0.5f, 2.0f);

            // Assert
            Assert.Equal(0f, noteLow.Velocity);
            Assert.Equal(1f, noteHigh.Velocity);
        }

        [Fact]
        public void MidiNote_SetsDurationMinimumTo0_001()
        {
            // Arrange & Act
            var note = new MidiNote(60, -1.0f);

            // Assert
            Assert.Equal(0.001f, note.Duration);
        }

        [Fact]
        public void MidiNote_GetFrequency_CalculatesMiddleC()
        {
            // Arrange
            var middleC = new MidiNote(60, 0.5f);

            // Act
            float frequency = middleC.GetFrequency();

            // Assert - Middle C is approximately 261.63 Hz
            Assert.InRange(frequency, 261.0f, 262.0f);
        }

        [Fact]
        public void MidiNote_GetFrequency_CalculatesA440()
        {
            // Arrange
            var a440 = new MidiNote(69, 0.5f);

            // Act
            float frequency = a440.GetFrequency();

            // Assert - A4 should be exactly 440 Hz
            Assert.InRange(frequency, 439.9f, 440.1f);
        }

        [Fact]
        public void MidiNote_GetFrequency_CalculatesOctaveDoubling()
        {
            // Arrange
            var c4 = new MidiNote(60, 0.5f);
            var c5 = new MidiNote(72, 0.5f);

            // Act
            float freqC4 = c4.GetFrequency();
            float freqC5 = c5.GetFrequency();

            // Assert - One octave up should double the frequency
            Assert.InRange(freqC5 / freqC4, 1.99f, 2.01f);
        }

        [Theory]
        [InlineData(60, 261.63f)]  // C4
        [InlineData(62, 293.66f)]  // D4
        [InlineData(64, 329.63f)]  // E4
        [InlineData(65, 349.23f)]  // F4
        [InlineData(67, 392.00f)]  // G4
        [InlineData(69, 440.00f)]  // A4
        [InlineData(71, 493.88f)]  // B4
        public void MidiNote_GetFrequency_CalculatesCorrectFrequencies(int pitch, float expectedFreq)
        {
            // Arrange
            var note = new MidiNote(pitch, 0.5f);

            // Act
            float frequency = note.GetFrequency();

            // Assert - Allow 1% tolerance
            Assert.InRange(frequency, expectedFreq * 0.99f, expectedFreq * 1.01f);
        }
    }
}
