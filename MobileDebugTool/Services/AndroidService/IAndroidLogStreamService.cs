using MobileDebugTool.Models;

namespace MobileDebugTool.Services.AndroidService;

public interface IAndroidLogStreamService : IAsyncDisposable
{
    bool IsRunning { get; }
    Task StartAsync(Action<LogEntry> onLogEntry, CancellationToken cancellationToken);
    Task StopAsync();
}
