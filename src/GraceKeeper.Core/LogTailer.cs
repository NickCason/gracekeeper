using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace GraceKeeper.Core;

public sealed class LogTailer
{
    private static readonly Regex _lineRegex = new(
        @"^(?<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\s\|\s(?<rest>.*)$",
        RegexOptions.Compiled);

    private readonly string _path;

    public LogTailer(string path) { _path = path; }

    public List<LogEvent> ReadNew(long fromOffset, out long newOffset)
    {
        var result = new List<LogEvent>();
        newOffset = fromOffset;

        if (!File.Exists(_path)) return result;

        using var fs = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (fromOffset > fs.Length)
        {
            // File rotated/truncated; start over
            fromOffset = 0;
        }
        fs.Seek(fromOffset, SeekOrigin.Begin);
        using var sr = new StreamReader(fs);

        string? line;
        long byteOffset = fromOffset;
        while ((line = sr.ReadLine()) != null)
        {
            byteOffset += sr.CurrentEncoding.GetByteCount(line) + 1;  // +1 for \n
            var ev = Parse(line, byteOffset);
            if (ev != null) result.Add(ev);
        }
        newOffset = byteOffset;
        return result;
    }

    private static LogEvent? Parse(string line, long offset)
    {
        var m = _lineRegex.Match(line);
        if (!m.Success) return null;

        var ts = DateTime.ParseExact(m.Groups["ts"].Value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var rest = m.Groups["rest"].Value;
        // v0.4.0 changed the cleaner log line from "deleted=N | ..." to
        // "refreshed=N | freed-by-bounce=M | ...". Keep "deleted=" recognized
        // for old log files left from earlier installs. The "started" banner
        // (v0.4.1) intentionally falls through to Other — it's diagnostic, not
        // a refresh event, so it must not increment the lifetime counter.
        var kind = rest.StartsWith("dismissed", StringComparison.OrdinalIgnoreCase) ? LogEventKind.Dismiss
                 : rest.StartsWith("deleted=", StringComparison.OrdinalIgnoreCase)   ? LogEventKind.Clean
                 : rest.StartsWith("refreshed=", StringComparison.OrdinalIgnoreCase) ? LogEventKind.Clean
                 :                                                                     LogEventKind.Other;
        return new LogEvent { Timestamp = ts, Kind = kind, Description = rest, FileOffset = offset };
    }
}
