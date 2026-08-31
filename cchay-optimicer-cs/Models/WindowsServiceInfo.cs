namespace cchay_optimicer_cs.Models
{
    public class WindowsServiceInfo
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // En ejecución, Detenido, etc.
        public string StartupType { get; set; } = string.Empty; // Automático, Manual, Deshabilitado
        public string Description { get; set; } = string.Empty;
        public string RiskLevel { get; set; } = "normal"; // normal, critical, bloat
    }
}
