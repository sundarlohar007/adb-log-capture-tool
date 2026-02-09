using MobileDebugTool.Models;

namespace MobileDebugTool.Services.LogService;

public interface ILogParser
{
    LogEntry Parse(string rawLine);
}
