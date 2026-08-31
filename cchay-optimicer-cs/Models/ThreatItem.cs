namespace cchay_optimicer_cs.Models
{
    public class ThreatItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string RiskLevel { get; set; } = "Medio"; // Bajo, Medio, Alto, Critico
        public int ProcessId { get; set; } = 0; // 0 if not running
        public bool IsChecked { get; set; } = true;
    }
}
