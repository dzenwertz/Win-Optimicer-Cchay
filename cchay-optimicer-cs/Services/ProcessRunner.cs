using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace cchay_optimicer_cs.Services
{
    /// <summary>
    /// Centralized utility for running external processes with proper timeout,
    /// error capture, and logging. Replaces duplicated RunCmd/RunPowerShell
    /// methods across services.
    /// </summary>
    public static class ProcessRunner
    {
        /// <summary>
        /// Result of a process execution.
        /// </summary>
        public class ProcessResult
        {
            public bool Success { get; set; }
            public int ExitCode { get; set; }
            public string StdOut { get; set; } = string.Empty;
            public string StdErr { get; set; } = string.Empty;
            public bool TimedOut { get; set; }
        }

        /// <summary>
        /// Runs a command synchronously with a configurable timeout.
        /// </summary>
        public static ProcessResult Run(string fileName, string arguments, int timeoutMs = 15000)
        {
            var result = new ProcessResult();
            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                var stdOutBuilder = new StringBuilder();
                var stdErrBuilder = new StringBuilder();

                process.OutputDataReceived += (_, e) => { if (e.Data != null) stdOutBuilder.AppendLine(e.Data); };
                process.ErrorDataReceived += (_, e) => { if (e.Data != null) stdErrBuilder.AppendLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(timeoutMs))
                {
                    try { process.Kill(true); } catch { }
                    result.TimedOut = true;
                    Debug.WriteLine($"[ProcessRunner] Command timed out after {timeoutMs}ms: {fileName} {arguments}");
                    return result;
                }

                // Ensure async output buffers are flushed
                process.WaitForExit();

                result.ExitCode = process.ExitCode;
                result.StdOut = stdOutBuilder.ToString().TrimEnd();
                result.StdErr = stdErrBuilder.ToString().TrimEnd();
                result.Success = process.ExitCode == 0;

                if (!result.Success && !string.IsNullOrWhiteSpace(result.StdErr))
                {
                    Debug.WriteLine($"[ProcessRunner] Command failed (exit {result.ExitCode}): {fileName} {arguments} | stderr: {result.StdErr}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessRunner] Error executing: {fileName} {arguments} | {ex.Message}");
                result.Success = false;
            }
            return result;
        }

        /// <summary>
        /// Runs a command asynchronously with a configurable timeout and optional cancellation.
        /// </summary>
        public static async Task<ProcessResult> RunAsync(string fileName, string arguments, int timeoutMs = 30000, CancellationToken ct = default)
        {
            return await Task.Run(() => Run(fileName, arguments, timeoutMs), ct);
        }

        /// <summary>
        /// Runs a PowerShell script with proper encoding and isolation.
        /// </summary>
        public static ProcessResult RunPowerShell(string script, int timeoutMs = 30000)
        {
            var base64Script = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            return Run("powershell.exe",
                $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {base64Script}",
                timeoutMs);
        }

        /// <summary>
        /// Runs a PowerShell script asynchronously.
        /// </summary>
        public static async Task<ProcessResult> RunPowerShellAsync(string script, int timeoutMs = 30000, CancellationToken ct = default)
        {
            return await Task.Run(() => RunPowerShell(script, timeoutMs), ct);
        }
    }
}
