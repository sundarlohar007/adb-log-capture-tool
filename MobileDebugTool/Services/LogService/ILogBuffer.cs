using MobileDebugTool.Models;

namespace MobileDebugTool.Services.LogService;

public interface ILogBuffer
{
    int Capacity { get; }
    int Count { get; }
    void Add(LogEntry entry);
    IReadOnlyList<LogEntry> Snapshot();
    void Clear();
}
