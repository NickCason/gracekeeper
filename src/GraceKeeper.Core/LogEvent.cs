using System;

namespace GraceKeeper.Core;

public enum LogEventKind { Dismiss, Clean, Other }

public sealed class LogEvent
{
    public DateTime Timestamp { get; set; }
    public LogEventKind Kind { get; set; }
    public string Description { get; set; } = "";
    public long FileOffset { get; set; }  // byte offset AFTER this line was read
}
