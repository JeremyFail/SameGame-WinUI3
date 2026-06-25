using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.System.Profile;

namespace SameGame.UI;

public static class CrashReportFormatter
{
    public static string Format(Exception exception)
    {
        var report = new StringBuilder();
        AppendSection(report, "Application");
        AppendLine(report, $"{MainPage.AppName} {GetAppVersion()}");
        AppendLine(report, $".NET {Environment.Version}");
        AppendLine(report, $"Culture: {System.Globalization.CultureInfo.CurrentUICulture.Name}");

        AppendSection(report, "System");
        AppendSystemInfo(report);

        AppendSection(report, "Exception");
        AppendException(report, exception);

        return report.ToString().TrimEnd();
    }

    private static string GetAppVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
    }

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

    private static string FormatWindowsVersion()
    {
        try
        {
            var versionInfo = AnalyticsInfo.VersionInfo;
            string family = versionInfo.DeviceFamily;
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

    private static void AppendSection(StringBuilder report, string title)
    {
        if (report.Length > 0)
        {
            report.AppendLine();
        }

        report.AppendLine($"=== {title} ===");
    }

    private static void AppendLine(StringBuilder report, string line)
    {
        report.AppendLine(line);
    }

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

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
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
    }
}
