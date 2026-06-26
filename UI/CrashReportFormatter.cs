using System.Runtime.InteropServices;
using System.Text;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.System.Profile;

namespace SameGame.UI;

/// <summary>
/// Formats unhandled exceptions into a human-readable crash report string.
/// </summary>
public static class CrashReportFormatter
{
    /// <summary>
    /// Builds a full crash report containing application, system, and exception details.
    /// </summary>
    /// <param name="exception">The unhandled exception to format.</param>
    /// <returns>The formatted crash report text.</returns>
    public static string Format(Exception exception)
    {
        var report = new StringBuilder();

        // Application metadata section.
        AppendSection(report, "Application");
        AppendLine(report, $"{MainPage.AppName} {AppInfo.Version}");
        AppendLine(report, $".NET {Environment.Version}");
        AppendLine(report, $"Culture: {System.Globalization.CultureInfo.CurrentUICulture.Name}");

        // Operating system and hardware section.
        AppendSection(report, "System");
        AppendSystemInfo(report);

        // Exception details section.
        AppendSection(report, "Exception");
        AppendException(report, exception);

        return report.ToString().TrimEnd();
    }

    /// <summary>
    /// Appends operating system, runtime, and device information to the report.
    /// </summary>
    /// <param name="report">The report builder to append to.</param>
    private static void AppendSystemInfo(StringBuilder report)
    {
        AppendLine(report, FormatWindowsVersion());
        AppendLine(report, $"OS description: {RuntimeInformation.OSDescription}");
        AppendLine(report, $"OS architecture: {RuntimeInformation.OSArchitecture}");
        AppendLine(report, $"Process architecture: {RuntimeInformation.ProcessArchitecture}");
        AppendLine(report, $"Processor count: {Environment.ProcessorCount}");
        AppendLine(report, $"64-bit OS: {Environment.Is64BitOperatingSystem}");

        if (TryGetTotalPhysicalMemoryGb(out double memoryGb))
        {
            AppendLine(report, $"Installed memory: {memoryGb:0.#} GB");
        }

        try
        {
            var device = new EasClientDeviceInformation();
            if (!string.IsNullOrWhiteSpace(device.SystemManufacturer))
            {
                AppendLine(report, $"Manufacturer: {device.SystemManufacturer}");
            }

            if (!string.IsNullOrWhiteSpace(device.SystemProductName))
            {
                AppendLine(report, $"Product: {device.SystemProductName}");
            }
        }
        catch
        {
            // Best-effort device info.
        }
    }

    /// <summary>
    /// Formats the Windows version from AnalyticsInfo, falling back to Environment.OSVersion.
    /// </summary>
    /// <returns>A human-readable Windows version string.</returns>
    private static string FormatWindowsVersion()
    {
        try
        {
            var versionInfo = AnalyticsInfo.VersionInfo;
            string family = versionInfo.DeviceFamily;
            // Decode the packed DeviceFamilyVersion ulong into major.minor.build.revision.
            if (ulong.TryParse(versionInfo.DeviceFamilyVersion, out ulong encoded))
            {
                int major = (int)((encoded & 0xFFFF_0000_0000_0000) >> 48);
                int minor = (int)((encoded & 0x0000_FFFF_0000_0000) >> 32);
                int build = (int)((encoded & 0x0000_0000_FFFF_0000) >> 16);
                int revision = (int)(encoded & 0x0000_0000_0000_FFFF);
                return $"Windows ({family}) {major}.{minor}.{build}.{revision}";
            }

            return $"Windows ({family})";
        }
        catch
        {
            return $"Windows ({Environment.OSVersion.VersionString})";
        }
    }

    /// <summary>
    /// Appends exception details, flattening aggregate exceptions when present.
    /// </summary>
    /// <param name="report">The report builder to append to.</param>
    /// <param name="exception">The exception to format.</param>
    private static void AppendException(StringBuilder report, Exception exception)
    {
        if (exception is AggregateException aggregate)
        {
            int index = 1;
            foreach (Exception inner in aggregate.Flatten().InnerExceptions)
            {
                if (index > 1)
                {
                    report.AppendLine();
                }

                AppendLine(report, $"--- Exception {index} ---");
                AppendSingleException(report, inner);
                index++;
            }

            return;
        }

        AppendSingleException(report, exception);
    }

    /// <summary>
    /// Appends type, message, stack trace, and inner exception for a single exception.
    /// </summary>
    /// <param name="report">The report builder to append to.</param>
    /// <param name="exception">The exception to format.</param>
    private static void AppendSingleException(StringBuilder report, Exception exception)
    {
        AppendLine(report, $"Type: {exception.GetType().FullName}");
        AppendLine(report, $"Message: {exception.Message}");

        if (!string.IsNullOrWhiteSpace(exception.StackTrace))
        {
            report.AppendLine("Stack trace:");
            report.AppendLine(exception.StackTrace);
        }
        else
        {
            AppendLine(report, "Stack trace: (not available)");
        }

        if (exception.InnerException is not null)
        {
            report.AppendLine();
            AppendLine(report, "--- Inner exception ---");
            AppendSingleException(report, exception.InnerException);
        }
    }

    /// <summary>
    /// Appends a titled section header to the report.
    /// </summary>
    /// <param name="report">The report builder to append to.</param>
    /// <param name="title">The section title.</param>
    private static void AppendSection(StringBuilder report, string title)
    {
        if (report.Length > 0)
        {
            report.AppendLine();
        }

        report.AppendLine($"=== {title} ===");
    }

    /// <summary>
    /// Appends a single line followed by a newline to the report.
    /// </summary>
    /// <param name="report">The report builder to append to.</param>
    /// <param name="line">The line text.</param>
    private static void AppendLine(StringBuilder report, string line)
    {
        report.AppendLine(line);
    }

    /// <summary>
    /// Attempts to read total physical memory via GlobalMemoryStatusEx.
    /// </summary>
    /// <param name="memoryGb">When successful, receives the installed memory in gigabytes.</param>
    /// <returns><see langword="true"/> if memory was read successfully.</returns>
    private static bool TryGetTotalPhysicalMemoryGb(out double memoryGb)
    {
        memoryGb = 0;
        var status = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status) || status.ullTotalPhys == 0)
        {
            return false;
        }

        memoryGb = status.ullTotalPhys / (1024d * 1024d * 1024d);
        return true;
    }

    /// <summary>
    /// Retrieves extended memory status from the Windows kernel.
    /// </summary>
    /// <param name="lpBuffer">The memory status structure to fill.</param>
    /// <returns><see langword="true"/> if the call succeeded.</returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    /// <summary>
    /// Win32 MEMORYSTATUSEX structure for GlobalMemoryStatusEx.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        /// <summary>Size of the structure in bytes; must be set before the call.</summary>
        public uint dwLength;
        /// <summary>Approximate percentage of physical memory in use.</summary>
        public uint dwMemoryLoad;
        /// <summary>Total physical memory in bytes.</summary>
        public ulong ullTotalPhys;
        /// <summary>Available physical memory in bytes.</summary>
        public ulong ullAvailPhys;
        /// <summary>Total page file size in bytes.</summary>
        public ulong ullTotalPageFile;
        /// <summary>Available page file space in bytes.</summary>
        public ulong ullAvailPageFile;
        /// <summary>Total virtual address space in bytes.</summary>
        public ulong ullTotalVirtual;
        /// <summary>Available virtual address space in bytes.</summary>
        public ulong ullAvailVirtual;
        /// <summary>Available extended virtual memory in bytes.</summary>
        public ulong ullAvailExtendedVirtual;
    }
}
