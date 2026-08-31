namespace cchay_optimicer_cs.Models
{
    public class SoftwarePackage
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool IsChecked { get; set; }
        public string Status { get; set; } = "Pendiente";
    }
}
