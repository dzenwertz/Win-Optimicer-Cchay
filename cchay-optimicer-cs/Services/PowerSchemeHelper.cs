using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace cchay_optimicer_cs.Services
{
    public static class PowerSchemeHelper
    {
        public const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
        public const string HighPerformanceGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
        public const string UltimatePerformanceTemplateGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";

        private static readonly Regex GuidRegex = new(
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
            RegexOptions.Compiled);

        /// <summary>
        /// Retrieves the currently active power scheme GUID from Windows.
        /// </summary>
        public static string GetActiveSchemeGuid()
        {
            try
            {
                var res = ProcessRunner.Run("powercfg", "/getactivescheme");
                if (res.Success && !string.IsNullOrWhiteSpace(res.StdOut))
                {
                    var match = GuidRegex.Match(res.StdOut);
                    if (match.Success)
                    {
                        return match.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PowerSchemeHelper] Error getting active scheme: {ex.Message}");
            }

            return BalancedGuid;
        }

        /// <summary>
        /// Retrieves the friendly name of the currently active power plan.
        /// </summary>
        public static string GetActiveSchemeName()
        {
            try
            {
                var res = ProcessRunner.Run("powercfg", "/getactivescheme");
                if (res.Success && !string.IsNullOrWhiteSpace(res.StdOut))
                {
                    // e.g. "GUID del esquema de energía: 381b4222-...  (Equilibrado)"
                    int openParen = res.StdOut.IndexOf('(');
                    int closeParen = res.StdOut.IndexOf(')');
                    if (openParen >= 0 && closeParen > openParen)
                    {
                        return res.StdOut.Substring(openParen + 1, closeParen - openParen - 1).Trim();
                    }
                }
            }
            catch { }

            return "Equilibrado";
        }

        /// <summary>
        /// Sets a power scheme active by its GUID.
        /// </summary>
        public static bool SetActiveScheme(string schemeGuid)
        {
            if (string.IsNullOrWhiteSpace(schemeGuid) || !GuidRegex.IsMatch(schemeGuid))
            {
                schemeGuid = BalancedGuid;
            }

            try
            {
                var res = ProcessRunner.Run("powercfg", $"-setactive {schemeGuid}");
                return res.Success;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PowerSchemeHelper] Failed to set active scheme {schemeGuid}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Activates the Ultimate Performance power plan, or falls back to High Performance.
        /// Avoids repeatedly duplicating schemes if an Ultimate scheme is already installed.
        /// </summary>
        public static bool EnableUltimateOrHighPerformance()
        {
            try
            {
                // 1. Check existing schemes to avoid polluting the system with duplicates
                var listRes = ProcessRunner.Run("powercfg", "/list");
                if (listRes.Success && !string.IsNullOrWhiteSpace(listRes.StdOut))
                {
                    var lines = listRes.StdOut.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (line.Contains("Rendimiento máximo", StringComparison.OrdinalIgnoreCase) ||
                            line.Contains("Ultimate Performance", StringComparison.OrdinalIgnoreCase))
                        {
                            var match = GuidRegex.Match(line);
                            if (match.Success)
                            {
                                return SetActiveScheme(match.Value);
                            }
                        }
                    }
                }

                // 2. Not found, try duplicating the template scheme
                var dupRes = ProcessRunner.Run("powercfg", $"-duplicatescheme {UltimatePerformanceTemplateGuid}");
                if (dupRes.Success && !string.IsNullOrWhiteSpace(dupRes.StdOut))
                {
                    var match = GuidRegex.Match(dupRes.StdOut);
                    if (match.Success)
                    {
                        return SetActiveScheme(match.Value);
                    }
                }

                // 3. Fallback to standard High Performance (present on all Windows versions)
                return SetActiveScheme(HighPerformanceGuid);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PowerSchemeHelper] Error enabling ultimate performance: {ex.Message}");
                return SetActiveScheme(HighPerformanceGuid);
            }
        }
    }
}
