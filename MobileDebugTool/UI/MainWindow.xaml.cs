using System.Windows;
using MobileDebugTool.Models;
using MobileDebugTool.Services.AndroidService;
using MobileDebugTool.Services.LogService;

namespace MobileDebugTool.UI;

public partial class MainWindow : Window
{
    private readonly IAndroidService _androidService;
    private readonly ILogBuffer _logBuffer;
    private readonly IAndroidLogStreamService _logStreamService;

    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    public MainWindow()
    {
        InitializeComponent();

        _androidService = new AndroidAdbService();
        _logBuffer = new BoundedLogBuffer();
        _logStreamService = new AndroidLogcatStreamService(_androidService, new LogParser());
    }

    private async void OnRefreshDevicesClick(object sender, RoutedEventArgs e)
    {
        if (!await _refreshGate.WaitAsync(0))
        {
            StatusTextBlock.Text = "Status: Refresh already in progress";
            return;
        }

        RefreshDevicesButton.IsEnabled = false;

        try
        {
            StatusTextBlock.Text = "Status: Checking local adb...";

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var isAdbAvailable = await _androidService.IsAdbAvailableAsync(cts.Token);
            if (!isAdbAvailable)
            {
                DevicesListBox.ItemsSource = null;
                StatusTextBlock.Text = "Status: adb.exe not found in Tools/adb";
                return;
            }

            StatusTextBlock.Text = "Status: Querying connected devices...";
            var devices = await _androidService.GetConnectedDevicesAsync(cts.Token);

            DevicesListBox.ItemsSource = devices.Select(d => $"{d.Serial} ({d.State})").ToList();
            StatusTextBlock.Text = devices.Count == 0
                ? "Status: No Android devices detected"
                : $"Status: Detected {devices.Count} Android device(s)";
        }
        catch (OperationCanceledException)
        {
            StatusTextBlock.Text = "Status: Device query timed out";
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Status: Error - {ex.Message}";
        }
        finally
        {
            RefreshDevicesButton.IsEnabled = true;
            _refreshGate.Release();
        }
    }

    private async void OnStartLoggingClick(object sender, RoutedEventArgs e)
    {
        if (_logStreamService.IsRunning)
        {
            StatusTextBlock.Text = "Status: Logging already running";
            return;
        }

        try
        {
            StartLogButton.IsEnabled = false;
            StatusTextBlock.Text = "Status: Starting logcat stream...";

            await _logStreamService.StartAsync(OnLogEntry, CancellationToken.None);

            StopLogButton.IsEnabled = true;
            StatusTextBlock.Text = "Status: Log streaming started";
        }
        catch (Exception ex)
        {
            StartLogButton.IsEnabled = true;
            StatusTextBlock.Text = $"Status: Failed to start logging - {ex.Message}";
        }
    }

    private async void OnStopLoggingClick(object sender, RoutedEventArgs e)
    {
        try
        {
            StopLogButton.IsEnabled = false;
            StatusTextBlock.Text = "Status: Stopping log stream...";

            await _logStreamService.StopAsync();

            StartLogButton.IsEnabled = true;
            StatusTextBlock.Text = "Status: Log streaming stopped";
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Status: Failed to stop logging - {ex.Message}";
        }
    }

    private void OnLogEntry(LogEntry entry)
    {
        _logBuffer.Add(entry);

        Dispatcher.Invoke(() =>
        {
            if (LiveLogsListBox.Items.Count >= 200)
            {
                LiveLogsListBox.Items.RemoveAt(0);
            }

            LiveLogsListBox.Items.Add($"[{entry.Severity}] {entry.Message}");
            LiveLogsListBox.ScrollIntoView(LiveLogsListBox.Items[^1]);
            LogStatsTextBlock.Text = $"Log buffer: {_logBuffer.Count} / {_logBuffer.Capacity}";
        });
    }
}
