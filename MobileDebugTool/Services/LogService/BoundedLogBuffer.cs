using MobileDebugTool.Models;

namespace MobileDebugTool.Services.LogService;

public sealed class BoundedLogBuffer : ILogBuffer
{
    private readonly LogEntry[] _buffer;
    private readonly object _gate = new();
    private int _start;
    private int _count;

    public BoundedLogBuffer(int capacity = 20_000)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
        }

        Capacity = capacity;
        _buffer = new LogEntry[capacity];
    }

    public int Capacity { get; }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _count;
            }
        }
    }

    public void Add(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_gate)
        {
            if (_count < Capacity)
            {
                var index = (_start + _count) % Capacity;
                _buffer[index] = entry;
                _count++;
                return;
            }

            _buffer[_start] = entry;
            _start = (_start + 1) % Capacity;
        }
    }

    public IReadOnlyList<LogEntry> Snapshot()
    {
        lock (_gate)
        {
            var snapshot = new List<LogEntry>(_count);
            for (var i = 0; i < _count; i++)
            {
                var index = (_start + i) % Capacity;
                snapshot.Add(_buffer[index]);
            }

            return snapshot;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            Array.Clear(_buffer);
            _start = 0;
            _count = 0;
        }
    }
}
