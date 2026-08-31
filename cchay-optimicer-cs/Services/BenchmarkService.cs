using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace cchay_optimicer_cs.Services
{
    public class BenchmarkResult
    {
        public double DiskSpeedMBs { get; set; }
        public double RamLatencyMs { get; set; }
        public double CpuResponseMs { get; set; }
        public int TotalScore { get; set; }
        public string Grade { get; set; } = "C";
        public DateTime TestedDate { get; set; } = DateTime.Now;
    }

    public class BenchmarkService
    {
        private static readonly string BaselineFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RethOptimicer",
            "benchmarkBaseline.json"
        );

        public static Task<BenchmarkResult> RunBenchmarkAsync(CancellationToken ct = default, IProgress<double>? progress = null)
        {
            return Task.Run(() =>
            {
                var result = new BenchmarkResult();

                // Step 1: Disk I/O Test (33%)
                progress?.Report(10);
                double diskSpeed = MeasureDiskSpeed(ct);
                result.DiskSpeedMBs = Math.Round(diskSpeed, 2);

                if (ct.IsCancellationRequested) return result;

                // Step 2: RAM Latency & Memory Throughput Test (66%)
                progress?.Report(50);
                double ramLatency = MeasureRamLatency(ct);
                result.RamLatencyMs = Math.Round(ramLatency, 2);

                if (ct.IsCancellationRequested) return result;

                // Step 3: CPU Multi-Thread Response Test (100%)
                progress?.Report(85);
                double cpuResponse = MeasureCpuResponse(ct);
                result.CpuResponseMs = Math.Round(cpuResponse, 2);

                progress?.Report(100);

                // Calculate Overall Score (Max ~ 1000)
                int diskScore = (int)Math.Min(350, diskSpeed * 0.7);
                int ramScore = (int)Math.Max(50, Math.Min(350, (100.0 / Math.Max(0.1, ramLatency)) * 50));
                int cpuScore = (int)Math.Max(50, Math.Min(300, (500.0 / Math.Max(1.0, cpuResponse)) * 100));

                result.TotalScore = Math.Clamp(diskScore + ramScore + cpuScore, 50, 1000);
                result.Grade = CalculateGrade(result.TotalScore);
                result.TestedDate = DateTime.Now;

                return result;
            }, ct);
        }

        private static double MeasureDiskSpeed(CancellationToken ct)
        {
            try
            {
                string tempFile = Path.Combine(Path.GetTempPath(), $"cchay_bench_{Guid.NewGuid():N}.tmp");
                byte[] data = new byte[10 * 1024 * 1024]; // 10MB test file
                new Random().NextBytes(data);

                var sw = Stopwatch.StartNew();
                File.WriteAllBytes(tempFile, data);
                byte[] readBack = File.ReadAllBytes(tempFile);
                sw.Stop();

                try { File.Delete(tempFile); } catch { }

                double totalMB = 20.0; // 10MB write + 10MB read
                double elapsedSec = sw.Elapsed.TotalSeconds;
                return elapsedSec > 0 ? (totalMB / elapsedSec) : 100.0;
            }
            catch
            {
                return 80.0; // Default fallback estimate
            }
        }

        private static double MeasureRamLatency(CancellationToken ct)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                int size = 5 * 1024 * 1024; // 5M integers
                int[] arr = new int[size];
                for (int i = 0; i < size; i++)
                {
                    if (ct.IsCancellationRequested) break;
                    arr[i] = i * 3 + 7;
                }
                sw.Stop();
                return sw.Elapsed.TotalMilliseconds;
            }
            catch
            {
                return 15.0;
            }
        }

        private static double MeasureCpuResponse(CancellationToken ct)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                Parallel.For(0, 50, i =>
                {
                    double val = 1.0;
                    for (int j = 0; j < 50000; j++)
                    {
                        val = Math.Sin(j) * Math.Cos(j);
                    }
                });
                sw.Stop();
                return sw.Elapsed.TotalMilliseconds;
            }
            catch
            {
                return 25.0;
            }
        }

        private static string CalculateGrade(int score)
        {
            if (score >= 850) return "S";
            if (score >= 700) return "A";
            if (score >= 550) return "B";
            if (score >= 400) return "C";
            return "D";
        }

        public static BenchmarkResult? GetBaseline()
        {
            try
            {
                if (File.Exists(BaselineFilePath))
                {
                    string json = File.ReadAllText(BaselineFilePath);
                    return JsonSerializer.Deserialize<BenchmarkResult>(json);
                }
            }
            catch { }
            return null;
        }

        public static void SaveBaseline(BenchmarkResult result)
        {
            try
            {
                string? dir = Path.GetDirectoryName(BaselineFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(BaselineFilePath, json);
            }
            catch { }
        }
    }
}
