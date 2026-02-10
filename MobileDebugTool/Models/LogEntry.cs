namespace MobileDebugTool.Models;

public sealed record LogEntry(DateTimeOffset Timestamp, LogSeverity Severity, string Message);
