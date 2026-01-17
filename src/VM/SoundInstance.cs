namespace GreyHackTerminalUI.VM
{
    // Simple wrapper class for sound instances
    public class SoundInstance
    {
        public string Name { get; }
        public int TerminalPID { get; }

        public SoundInstance(string name, int terminalPID)
        {
            Name = name;
            TerminalPID = terminalPID;
        }

        public override string ToString() => $"SoundInstance({Name})";
    }
}
