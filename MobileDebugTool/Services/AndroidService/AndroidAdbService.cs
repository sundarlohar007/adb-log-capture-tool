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
}
