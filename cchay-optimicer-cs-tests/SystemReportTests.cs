using System;
using Xunit;
using cchay_optimicer_cs.Services;

namespace cchay_optimicer_cs_tests
{
    public class SystemReportTests
    {
        private const string FakeUser = "usuario_ficticio_42";
        private const string FakeLocalIp = "192.0.2.15";
        private const string FakePublicIp = "198.51.100.77";

        private static SystemInfoData BuildSample() => new SystemInfoData
        {
            CpuModel = "CPU Inventada X1",
            CpuCores = 8,
            CpuThreads = 16,
            MemoryTotal = 16UL * 1024 * 1024 * 1024,
            MemoryUsed = 8UL * 1024 * 1024 * 1024,
            MemoryFree = 8UL * 1024 * 1024 * 1024,
            MemoryPercent = 50,
            OsName = "Windows Ficticio",
            OsVersion = "99H9",
            OsBuild = "12345",
            GpuModel = "GPU Inventada Z9",
            DiskTotal = 500L * 1024 * 1024 * 1024,
            DiskUsed = 250L * 1024 * 1024 * 1024,
            DiskFree = 250L * 1024 * 1024 * 1024,
            DiskPercent = 50,
            Username = FakeUser,
            IsAdmin = false,
            CpuTemperature = "55 °C",
            SmartStatus = "OK",
            LocalIp = FakeLocalIp,
            PublicIp = FakePublicIp,
            Uptime = "1d 2h",
            HealthScore = 87
        };

        [Fact]
        public void Report_DoesNotContainUsernameOrIps()
        {
            string report = SystemService.GenerateSystemReport(BuildSample());

            Assert.DoesNotContain(FakeUser, report);
            Assert.DoesNotContain(FakeLocalIp, report);
            Assert.DoesNotContain(FakePublicIp, report);
            Assert.DoesNotContain("Usuario:", report);
            Assert.DoesNotContain("IP Local", report);
            Assert.DoesNotContain("IP Pública", report);
        }

        [Fact]
        public void Report_KeepsTechnicalData()
        {
            string report = SystemService.GenerateSystemReport(BuildSample());

            Assert.Contains("CPU Inventada X1", report);
            Assert.Contains("8 Cores / 16 Threads", report);
            Assert.Contains("Windows Ficticio 99H9 (Build 12345)", report);
            Assert.Contains("GPU Inventada Z9", report);
            Assert.Contains("55 °C", report);
            Assert.Contains("OK", report);
            Assert.Contains("1d 2h", report);
            Assert.Contains("87/100", report);
        }

        [Fact]
        public void Report_StatesNoSystemChangeAndIndicativeScore()
        {
            string report = SystemService.GenerateSystemReport(BuildSample());

            Assert.Contains("no modifica el sistema", report);
            Assert.Contains("orientativo", report);
            Assert.Contains("no es un diagnóstico completo", report);
        }

        [Fact]
        public void Report_WithDefaultUnknownValues_DoesNotThrow()
        {
            var info = new SystemInfoData();

            string report = SystemService.GenerateSystemReport(info);

            Assert.False(string.IsNullOrWhiteSpace(report));
            Assert.Contains("N/A", report);
            Assert.Contains("Unknown", report);
        }

        [Fact]
        public void Report_DoesNotMutateSystemInfoData()
        {
            var info = BuildSample();

            SystemService.GenerateSystemReport(info);

            Assert.Equal(FakeUser, info.Username);
            Assert.Equal(FakeLocalIp, info.LocalIp);
            Assert.Equal(FakePublicIp, info.PublicIp);
            Assert.Equal("CPU Inventada X1", info.CpuModel);
            Assert.Equal(16, info.CpuThreads);
            Assert.Equal(16UL * 1024 * 1024 * 1024, info.MemoryTotal);
            Assert.Equal(500L * 1024 * 1024 * 1024, info.DiskTotal);
            Assert.Equal("55 °C", info.CpuTemperature);
            Assert.Equal(87, info.HealthScore);
        }
    }
}
