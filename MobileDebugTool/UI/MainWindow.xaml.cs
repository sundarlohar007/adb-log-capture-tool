using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
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
    private readonly ObservableCollection<UiLogLine> _liveLogs = new();
    private readonly List<AndroidDevice> _lastDevices = new();
    private bool _isLogPaused;

    public MainWindow()
    {
        InitializeComponent();

        _androidService = new AndroidAdbService();
        _logBuffer = new BoundedLogBuffer();
        _logStreamService = new AndroidLogcatStreamService(_androidService, new LogParser());
        LiveLogsListBox.ItemsSource = _liveLogs;
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

            _lastDevices.Clear();
            _lastDevices.AddRange(devices);
            DevicesListBox.ItemsSource = devices.Select(d => $"{d.Serial} ({d.State})").ToList();

            if (devices.Count > 0)
            {
                await LoadDeviceInfoAsync(devices[0].Serial, cts.Token);
            }
            else
            {
                DeviceInfoTextBlock.Text = "No device info loaded";
            }

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


    private async Task LoadDeviceInfoAsync(string serial, CancellationToken cancellationToken)
    {
        var info = await _androidService.GetDeviceInfoAsync(serial, cancellationToken);
        if (info is null)
        {
            DeviceInfoTextBlock.Text = "Device info unavailable";
            return;
        }

        DeviceInfoTextBlock.Text = $"Serial: {info.Serial}\nAndroid: {info.AndroidVersion}\nBattery: {info.BatteryLevel}\nStorage: {info.StorageSummary}";
    }


    private async void OnCaptureScreenshotClick(object sender, RoutedEventArgs e)
    {
        if (_lastDevices.Count == 0)
        {
            StatusTextBlock.Text = "Status: No device available for screenshot";
            return;
        }

        var serial = _lastDevices[0].Serial;

        try
        {
            ScreenshotButton.IsEnabled = false;
            StatusTextBlock.Text = "Status: Capturing screenshot...";

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var outputDir = Path.Combine(AppContext.BaseDirectory, "captures");
            var path = await _androidService.CaptureScreenshotAsync(serial, outputDir, cts.Token);

            StatusTextBlock.Text = path is null
                ? "Status: Screenshot capture failed"
                : $"Status: Screenshot saved -> {path}";
        }
        catch (OperationCanceledException)
        {
            StatusTextBlock.Text = "Status: Screenshot capture timed out";
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Status: Screenshot error - {ex.Message}";
        }
        finally
        {
            ScreenshotButton.IsEnabled = true;
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

            _isLogPaused = false;
            PauseLogButton.Content = "Pause Logging";
            PauseLogButton.IsEnabled = true;
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
            PauseLogButton.IsEnabled = false;
            StopLogButton.IsEnabled = false;
            StatusTextBlock.Text = "Status: Stopping log stream...";

            await _logStreamService.StopAsync();

            _isLogPaused = false;
            PauseLogButton.Content = "Pause Logging";
            StartLogButton.IsEnabled = true;
            StatusTextBlock.Text = "Status: Log streaming stopped";
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Status: Failed to stop logging - {ex.Message}";
        }
    }

    private void OnPauseLoggingClick(object sender, RoutedEventArgs e)
    {
        if (!_logStreamService.IsRunning)
        {
            StatusTextBlock.Text = "Status: Logging is not running";
            return;
        }

        _isLogPaused = !_isLogPaused;
        PauseLogButton.Content = _isLogPaused ? "Resume Logging" : "Pause Logging";
        StatusTextBlock.Text = _isLogPaused
            ? "Status: Logging paused"
            : "Status: Logging resumed";
    }

    private void OnLogEntry(LogEntry entry)
    {
        if (_isLogPaused)
        {
            return;
        }

        _logBuffer.Add(entry);

        if (!ShouldIncludeInPreview(entry.Severity))
        {
            return;
        }

        Dispatcher.Invoke(() =>
        {
            if (_liveLogs.Count >= 200)
            {
                _liveLogs.RemoveAt(0);
            }

            var line = new UiLogLine($"[{entry.Severity}] {entry.Message}", GetSeverityBrush(entry.Severity));
            _liveLogs.Add(line);
            if (AutoScrollCheckBox.IsChecked == true)
            {
                LiveLogsListBox.ScrollIntoView(line);
            }

            LogStatsTextBlock.Text = $"Log buffer: {_logBuffer.Count} / {_logBuffer.Capacity}";
        });
    }

    private bool ShouldIncludeInPreview(LogSeverity severity)
    {
        return severity switch
        {
            LogSeverity.Fatal => FatalFilterCheckBox.IsChecked == true,
            LogSeverity.Error => ErrorFilterCheckBox.IsChecked == true,
            LogSeverity.Exception => ExceptionFilterCheckBox.IsChecked == true,
            _ => true
        };
    }

    private static Brush GetSeverityBrush(LogSeverity severity)
    {
        return severity switch
        {
            LogSeverity.Fatal => Brushes.IndianRed,
            LogSeverity.Error => Brushes.Orange,
            LogSeverity.Exception => Brushes.MediumOrchid,
            _ => Brushes.Gainsboro
        };
    }

    private sealed record UiLogLine(string Message, Brush Foreground);

}
