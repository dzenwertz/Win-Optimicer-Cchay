namespace cchay_optimicer_cs.Models
{
    public class Tweak
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty; // performance, privacy, visual, gaming, services
        public string Risk { get; set; } = "safe"; // safe, moderate, advanced
        public bool Enabled { get; set; }
        public string ApplyScript { get; set; } = string.Empty;
        public string UnapplyScript { get; set; } = string.Empty;
    }
}
