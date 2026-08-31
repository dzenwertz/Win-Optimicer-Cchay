namespace cchay_optimicer_cs.Models
{
    public class CleanTarget
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty; // system, browser, windows
        public bool IsChecked { get; set; } = true;
        public double SizeMB { get; set; } = -1; // -1 means not calculated
        public bool Cleaned { get; set; }
        public int ItemsCount { get; set; }
    }
}
