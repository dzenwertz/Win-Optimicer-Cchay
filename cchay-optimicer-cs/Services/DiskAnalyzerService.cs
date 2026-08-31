using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace cchay_optimicer_cs.Services
{
    public class DiskItemInfo
    {
        public string Path { get; set; } = "";
        public string Name { get; set; } = "";
        public bool IsDirectory { get; set; }
        public long SizeBytes { get; set; }
        public double SizeMB => Math.Round(SizeBytes / 1024.0 / 1024.0, 2);
        public double SizeGB => Math.Round(SizeBytes / 1024.0 / 1024.0 / 1024.0, 2);
        public string FormattedSize
        {
            get
            {
                if (SizeGB >= 1.0) return $"{SizeGB:F2} GB";
                if (SizeMB >= 1.0) return $"{SizeMB:F1} MB";
                return $"{Math.Max(1, SizeBytes / 1024)} KB";
            }
        }
        public double PercentageOfTotal { get; set; }
    }

    public class DiskAnalyzerService
    {
        public static Task<List<DiskItemInfo>> ScanDirectoryAsync(string rootPath, CancellationToken ct = default, IProgress<double>? progress = null)
        {
            return Task.Run(() =>
            {
                var resultList = new List<DiskItemInfo>();
                if (!Directory.Exists(rootPath)) return resultList;

                try
                {
                    var dirInfo = new DirectoryInfo(rootPath);
                    var entries = dirInfo.GetFileSystemInfos();
                    int totalEntries = entries.Length;
                    long totalFolderBytes = 0;

                    int count = 0;
                    foreach (var entry in entries)
                    {
                        if (ct.IsCancellationRequested) break;
                        count++;
                        progress?.Report((double)count / Math.Max(1, totalEntries) * 100);

                        long size = 0;
                        bool isDir = (entry.Attributes & FileAttributes.Directory) == FileAttributes.Directory;

                        if (isDir)
                        {
                            size = CalculateDirectorySize(entry.FullName, ct);
                        }
                        else
                        {
                            try
                            {
                                size = ((FileInfo)entry).Length;
                            }
                            catch { }
                        }

                        totalFolderBytes += size;
                        resultList.Add(new DiskItemInfo
                        {
                            Path = entry.FullName,
                            Name = entry.Name,
                            IsDirectory = isDir,
                            SizeBytes = size
                        });
                    }

                    // Calculate percentage
                    if (totalFolderBytes > 0)
                    {
                        foreach (var item in resultList)
                        {
                            item.PercentageOfTotal = Math.Round((double)item.SizeBytes / totalFolderBytes * 100, 1);
                        }
                    }

                    return resultList.OrderByDescending(x => x.SizeBytes).ToList();
                }
                catch
                {
                    return resultList;
                }
            }, ct);
        }

        private static long CalculateDirectorySize(string dirPath, CancellationToken ct)
        {
            if (ct.IsCancellationRequested || !Directory.Exists(dirPath)) return 0;
            long size = 0;
            try
            {
                var dir = new DirectoryInfo(dirPath);
                foreach (var file in dir.GetFiles())
                {
                    if (ct.IsCancellationRequested) break;
                    try { size += file.Length; } catch { }
                }
                foreach (var subDir in dir.GetDirectories())
                {
                    if (ct.IsCancellationRequested) break;
                    size += CalculateDirectorySize(subDir.FullName, ct);
                }
            }
            catch { }
            return size;
        }
    }
}
