using System.Diagnostics;
using MobileDebugTool.Models;
using MobileDebugTool.Services.LogService;

namespace MobileDebugTool.Services.AndroidService;

public sealed class AndroidLogcatStreamService : IAndroidLogStreamService
{
    private readonly IAndroidService _androidService;
    private readonly ILogParser _logParser;
    private readonly SemaphoreSlim _streamGate = new(1, 1);

    private Process? _process;
    private Task? _pumpTask;
    private CancellationTokenSource? _streamCts;

    public AndroidLogcatStreamService(IAndroidService androidService, ILogParser logParser)
    {
        _androidService = androidService;
        _logParser = logParser;
    }

    public bool IsRunning => _process is { HasExited: false };

    public async Task StartAsync(Action<LogEntry> onLogEntry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onLogEntry);

        await _streamGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsRunning)
            {
                return;
            }

            var adbPath = _androidService.ResolveAdbPath();
            if (!File.Exists(adbPath))
            {
                throw new FileNotFoundException("adb.exe not found in Tools/adb.", adbPath);
            }

            _streamCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = "logcat",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            if (!_process.Start())
            {
                throw new InvalidOperationException("Failed to start adb logcat process.");
            }

            _pumpTask = PumpAsync(_process, onLogEntry, _streamCts.Token);
        }
        finally
        {
            _streamGate.Release();
        }
    }

    public async Task StopAsync()
    {
        await _streamGate.WaitAsync().ConfigureAwait(false);
        try
        {
            _streamCts?.Cancel();

            if (_pumpTask is not null)
            {
                try
                {
                    await _pumpTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
        finally
        {
            _process?.Dispose();
            _process = null;

            _streamCts?.Dispose();
            _streamCts = null;

            _pumpTask = null;
            _streamGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _streamGate.Dispose();
    }

    private async Task PumpAsync(Process process, Action<LogEntry> onLogEntry, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            var entry = _logParser.Parse(line);
            onLogEntry(entry);
        }
    }
}
