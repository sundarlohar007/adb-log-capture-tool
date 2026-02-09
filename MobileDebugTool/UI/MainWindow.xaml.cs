using System.Windows;
using MobileDebugTool.Services.AndroidService;

namespace MobileDebugTool.UI;

public partial class MainWindow : Window
{
    private readonly IAndroidService _androidService;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    public MainWindow()
    {
        InitializeComponent();
        _androidService = new AndroidAdbService();
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
}
