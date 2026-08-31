namespace cchay_optimicer_cs.Models
{
    public class StartupItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty; // HKCU Run, HKLM Run, User Startup, Common Startup
        public bool Enabled { get; set; }
    }
}
