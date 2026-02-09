using MobileDebugTool.Models;

namespace MobileDebugTool.Services.AndroidService;

public interface IAndroidService
{
    string ResolveAdbPath();
    Task<bool> IsAdbAvailableAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AndroidDevice>> GetConnectedDevicesAsync(CancellationToken cancellationToken);
    Task<AndroidDeviceInfo?> GetDeviceInfoAsync(string serial, CancellationToken cancellationToken);
    Task<string?> CaptureScreenshotAsync(string serial, string outputDirectory, CancellationToken cancellationToken);
}
