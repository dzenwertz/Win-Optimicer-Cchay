using System;

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

    public class DnsProvider
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Primary { get; set; } = string.Empty;
        public string Secondary { get; set; } = string.Empty;
        public int? Ping { get; set; }
    }

    public class StartupItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty; // HKCU Run, HKLM Run, User Startup, Common Startup
        public bool Enabled { get; set; }
    }

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

    public class RestorePoint
    {
        public uint SequenceNumber { get; set; }
        public string Description { get; set; } = string.Empty;
        public string CreationTime { get; set; } = string.Empty;
    }

    public class SoftwarePackage
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool IsChecked { get; set; }
        public string Status { get; set; } = "Pendiente";
    }

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
