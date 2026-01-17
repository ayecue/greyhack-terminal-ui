using Xunit;
using GreyHackTerminalUI.VM;

namespace GreyHackTerminalUI.Tests.VM
{
    public class SoundInstanceTests
    {
        [Fact]
        public void SoundInstance_CreatesWithNameAndPID()
        {
            var instance = new SoundInstance("test", 123);
            
            Assert.Equal("test", instance.Name);
            Assert.Equal(123, instance.TerminalPID);
        }

        [Fact]
        public void SoundInstance_ToStringReturnsFormattedName()
        {
            var instance = new SoundInstance("welcome", 456);
            
            Assert.Equal("SoundInstance(welcome)", instance.ToString());
        }

        [Fact]
        public void SoundInstance_SupportsEmptyName()
        {
            var instance = new SoundInstance("", 789);
            
            Assert.Equal("", instance.Name);
            Assert.Equal("SoundInstance()", instance.ToString());
        }
    }
}
