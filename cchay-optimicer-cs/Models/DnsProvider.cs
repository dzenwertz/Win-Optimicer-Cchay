namespace cchay_optimicer_cs.Models
{
    public class DnsProvider
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Primary { get; set; } = string.Empty;
        public string Secondary { get; set; } = string.Empty;
        public int? Ping { get; set; }
    }
}
