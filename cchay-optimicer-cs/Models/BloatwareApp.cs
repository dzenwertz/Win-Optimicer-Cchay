using System.Collections.Generic;

namespace cchay_optimicer_cs.Models
{
    public class BloatwareApp
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Packages { get; set; } = new List<string>();
        public bool Installed { get; set; }
    }
}
