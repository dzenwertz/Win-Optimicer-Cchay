using System;
using System.IO;
using System.Reflection;
using System.Threading;
using cchay_optimicer_cs.Services;
using Xunit;

namespace cchay_optimicer_cs_tests;

public class SafetyTests
{
    [Fact]
    public void MissingCommandReturnsError()
    {
        var result = ProcessRunner.Run("reth-nonexistent-command.exe", "");
        Assert.False(result.Success);
        Assert.Equal(-1, result.ExitCode);
        Assert.NotEmpty(result.StdErr);
    }

    [Fact]
    public void LockedFilesAreNotReportedAsFreed()
    {
        string root = Path.Combine(Path.GetTempPath(), "reth-test-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllBytes(Path.Combine(root, "free.tmp"), new byte[1024]);
            string lockedPath = Path.Combine(root, "locked.tmp");
            File.WriteAllBytes(lockedPath, new byte[2048]);
            using var locked = new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.None);
            var method = typeof(DiskService).GetMethod("CleanDirectory", BindingFlags.NonPublic | BindingFlags.Static)!;
            object[] args = { root, 0d, 0, true, "*", CancellationToken.None };
            method.Invoke(null, args);
            Assert.Equal(1, (int)args[2]);
            Assert.Equal(1024d / 1024 / 1024, (double)args[1]);
            Assert.True(File.Exists(lockedPath));
            Assert.False(File.Exists(Path.Combine(root, "free.tmp")));
        }
        finally { Directory.Delete(root, true); }
    }
}
