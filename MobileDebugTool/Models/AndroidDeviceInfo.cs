namespace MobileDebugTool.Models;

public sealed record AndroidDeviceInfo(
    string Serial,
    string AndroidVersion,
    string BatteryLevel,
    string StorageSummary);
