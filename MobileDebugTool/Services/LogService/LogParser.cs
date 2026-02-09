using MobileDebugTool.Models;

namespace MobileDebugTool.Services.LogService;

public sealed class LogParser : ILogParser
{
    public LogEntry Parse(string rawLine)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawLine);

        var severity = DetectSeverity(rawLine);
        return new LogEntry(DateTimeOffset.UtcNow, severity, rawLine);
    }

    private static LogSeverity DetectSeverity(string line)
    {
        if (Contains(line, "fatal") || Contains(line, "f/"))
        {
            return LogSeverity.Fatal;
        }

        if (Contains(line, "exception"))
        {
            return LogSeverity.Exception;
        }

        if (Contains(line, "error") || Contains(line, "e/"))
        {
            return LogSeverity.Error;
        }

        return LogSeverity.Info;
    }

    private static bool Contains(string source, string value)
    {
        return source.Contains(value, StringComparison.OrdinalIgnoreCase);
    }
}
