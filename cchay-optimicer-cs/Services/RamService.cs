using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace cchay_optimicer_cs.Services
{
    public class RamService
    {
        // --- Windows API P/Invoke declarations ---

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtSetSystemInformation(int InfoClass, IntPtr Info, uint Length);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetSystemFileCacheSize(IntPtr MinimumFileCacheSize, IntPtr MaximumFileCacheSize, int Flags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        // --- Windows Token Privilege P/Invoke declarations ---

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, out LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AdjustTokenPrivileges(
            IntPtr TokenHandle,
            [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges,
            ref TOKEN_PRIVILEGES NewState,
            uint BufferLength,
            IntPtr PreviousState,
            IntPtr ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const uint TOKEN_QUERY = 0x0008;
        private const int SE_PRIVILEGE_ENABLED = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID_AND_ATTRIBUTES
        {
            public LUID Luid;
            public uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            public LUID_AND_ATTRIBUTES Privilege;
        }

        private static bool EnablePrivilege(string privilegeName)
        {
            try
            {
                IntPtr hToken;
                if (!OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out hToken))
                    return false;

                try
                {
                    LUID luid;
                    if (!LookupPrivilegeValue(null, privilegeName, out luid))
                        return false;

                    TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES();
                    tp.PrivilegeCount = 1;
                    tp.Privilege.Luid = luid;
                    tp.Privilege.Attributes = SE_PRIVILEGE_ENABLED;

                    return AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
                }
                finally
                {
                    CloseHandle(hToken);
                }
            }
            catch
            {
                return false;
            }
        }

        // --- Memory Info structure ---
        public struct MemoryInfo
        {
            public ulong TotalBytes;
            public ulong FreeBytes;
            public ulong UsedBytes;
            public int PercentUsed;
        }

        public static MemoryInfo GetMemoryUsage()
        {
            var memStatus = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(memStatus))
            {
                return new MemoryInfo
                {
                    TotalBytes = memStatus.ullTotalPhys,
                    FreeBytes = memStatus.ullAvailPhys,
                    UsedBytes = memStatus.ullTotalPhys - memStatus.ullAvailPhys,
                    PercentUsed = (int)memStatus.dwMemoryLoad
                };
            }
            return new MemoryInfo();
        }

        // --- RAM Cleaning Actions ---

        public static Task<long> CleanWorkingSet()
        {
            return Task.Run(() =>
            {
                long before = (long)GetMemoryUsage().FreeBytes;
                
                var processes = Process.GetProcesses();
                foreach (var proc in processes)
                {
                    try
                    {
                        // Skip system critical or idle processes
                        if (proc.Id == 0 || proc.ProcessName == "System" || proc.ProcessName == "Idle") 
                            continue;
                            
                        EmptyWorkingSet(proc.Handle);
                    }
                    catch
                    {
                        // Ignore processes we don't have permission to modify
                    }
                    finally
                    {
                        proc.Dispose();
                    }
                }

                long after = (long)GetMemoryUsage().FreeBytes;
                return Math.Max(0, after - before);
            });
        }

        private static Task<long> CleanSystemInformation(int command)
        {
            return Task.Run(() =>
            {
                long before = (long)GetMemoryUsage().FreeBytes;
                
                // Enable privilege for system memory list cleaning
                EnablePrivilege("SeProfileSingleProcessPrivilege");
                
                IntPtr ptr = Marshal.AllocHGlobal(4);
                Marshal.WriteInt32(ptr, command);
                try
                {
                    // InfoClass = SystemMemoryListInformation (80)
                    NtSetSystemInformation(80, ptr, 4);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"NtSetSystemInformation error (cmd {command}): {ex.Message}");
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }

                long after = (long)GetMemoryUsage().FreeBytes;
                return Math.Max(0, after - before);
            });
        }

        public static Task<long> CleanStandbyList()
        {
            // MemoryPurgeStandbyList = 4
            return CleanSystemInformation(4);
        }

        public static Task<long> CleanStandbyListLowPriority()
        {
            // MemoryPurgeLowPriorityStandbyList = 5
            return CleanSystemInformation(5);
        }

        public static Task<long> CleanSystemFileCache()
        {
            return Task.Run(() =>
            {
                long before = (long)GetMemoryUsage().FreeBytes;
                try
                {
                    // Flags: 0, set min and max cache sizes to -1 to flush cache
                    SetSystemFileCacheSize(new IntPtr(-1), new IntPtr(-1), 0);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"SetSystemFileCacheSize error: {ex.Message}");
                }
                long after = (long)GetMemoryUsage().FreeBytes;
                return Math.Max(0, after - before);
            });
        }

        public static Task<long> CleanModifiedPageList()
        {
            // MemoryFlushModifiedList = 3
            return CleanSystemInformation(3);
        }

        public static Task<long> CleanCombinedPageList()
        {
            // MemoryCombinePageList = 6
            return CleanSystemInformation(6);
        }

        public static async Task<long> CleanAll()
        {
            long total = 0;
            total += await CleanWorkingSet();
            total += await CleanStandbyList();
            total += await CleanSystemFileCache();
            total += await CleanModifiedPageList();
            total += await CleanCombinedPageList();
            return total;
        }
    }
}
