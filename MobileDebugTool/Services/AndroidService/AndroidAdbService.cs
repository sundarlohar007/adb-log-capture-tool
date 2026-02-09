using MobileDebugTool.Models;

namespace MobileDebugTool.Services.AndroidService;

public sealed class AndroidAdbService : IAndroidService
{
    private const string RelativeAdbPath = "Tools/adb/adb.exe";

    public string ResolveAdbPath()
    {
        var baseDirectory = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(baseDirectory, RelativeAdbPath));
    }

    public Task<bool> IsAdbAvailableAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var adbPath = ResolveAdbPath();
        return Task.FromResult(File.Exists(adbPath));
    }

    public async Task<IReadOnlyList<AndroidDevice>> GetConnectedDevicesAsync(CancellationToken cancellationToken)
    {
        var adbPath = ResolveAdbPath();
        if (!File.Exists(adbPath))
        {
            return Array.Empty<AndroidDevice>();
        }

        var result = await ProcessRunner.RunAsync(adbPath, "devices", cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            return Array.Empty<AndroidDevice>();
        }

        var devices = new List<AndroidDevice>();
        using var reader = new StringReader(result.StdOut);
        string? line;

        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                devices.Add(new AndroidDevice(parts[0], parts[1]));
            }
        }

        return devices;
    }

    public async Task<AndroidDeviceInfo?> GetDeviceInfoAsync(string serial, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(serial))
        {
            return null;
        }

        var adbPath = ResolveAdbPath();
        if (!File.Exists(adbPath))
        {
            return null;
        }

        var androidVersion = await QueryAsync(adbPath, serial, "shell getprop ro.build.version.release", cancellationToken).ConfigureAwait(false);
        var batteryDump = await QueryAsync(adbPath, serial, "shell dumpsys battery", cancellationToken).ConfigureAwait(false);
        var storageDump = await QueryAsync(adbPath, serial, "shell df /data", cancellationToken).ConfigureAwait(false);

        var batteryLevel = ParseBatteryLevel(batteryDump);
        var storageSummary = ParseStorageSummary(storageDump);

        return new AndroidDeviceInfo(
            serial,
            string.IsNullOrWhiteSpace(androidVersion) ? "Unknown" : androidVersion,
            batteryLevel,
            storageSummary);
    }

    private static async Task<string> QueryAsync(string adbPath, string serial, string command, CancellationToken cancellationToken)
    {
        var result = await ProcessRunner.RunAsync(adbPath, $"-s {serial} {command}", cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0 ? result.StdOut.Trim() : string.Empty;
    }

    private static string ParseBatteryLevel(string batteryDump)
    {
        if (string.IsNullOrWhiteSpace(batteryDump))
        {
            return "Unknown";
        }

        using var reader = new StringReader(batteryDump);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (!line.Contains("level", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                return $"{parts[1]}%";
            }
        }

        return "Unknown";
    }

    private static string ParseStorageSummary(string storageDump)
    {
        if (string.IsNullOrWhiteSpace(storageDump))
        {
            return "Unknown";
        }

        using var reader = new StringReader(storageDump);
        string? header = reader.ReadLine();
        string? dataLine = reader.ReadLine();

        if (string.IsNullOrWhiteSpace(dataLine))
        {
            return "Unknown";
        }

        var cols = dataLine.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (cols.Length >= 5)
        {
            return $"Used {cols[2]} / Size {cols[1]} ({cols[4]} used)";
        }

        return dataLine.Trim();
    }

    public async Task<string?> CaptureScreenshotAsync(string serial, string outputDirectory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(serial) || string.IsNullOrWhiteSpace(outputDirectory))
        {
            return null;
        }

        var adbPath = ResolveAdbPath();
        if (!File.Exists(adbPath))
        {
            return null;
        }

        Directory.CreateDirectory(outputDirectory);

        var fileName = $"android_{serial}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png";
        var localPath = Path.Combine(outputDirectory, fileName);
        var remotePath = "/sdcard/__mdt_capture.png";

        var shot = await ProcessRunner.RunAsync(adbPath, $"-s {serial} shell screencap -p {remotePath}", cancellationToken).ConfigureAwait(false);
        if (shot.ExitCode != 0)
        {
            return null;
        }

        var pull = await ProcessRunner.RunAsync(adbPath, $"-s {serial} pull {remotePath} \"{localPath}\"", cancellationToken).ConfigureAwait(false);

        _ = ProcessRunner.RunAsync(adbPath, $"-s {serial} shell rm {remotePath}", cancellationToken);

        return pull.ExitCode == 0 ? localPath : null;
    }

}
