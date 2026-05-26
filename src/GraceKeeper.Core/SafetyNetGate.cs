using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace GraceKeeper.Core;

public sealed class SafetyNetGate
{
    private static readonly Regex SuccessLine = new(
        @"^(?<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}) \| (?:refreshed|deleted)=\d+",
        RegexOptions.Compiled);

    private readonly string _logPath;
    private readonly IClock _clock;
    private readonly TimeSpan _window;

    public SafetyNetGate(string logPath, IClock clock, TimeSpan window)
    {
        _logPath = logPath;
        _clock = clock;
        _window = window;
    }

    public bool ShouldSkip()
    {
        if (!File.Exists(_logPath)) return false;
        var lines = File.ReadAllLines(_logPath);
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            var m = SuccessLine.Match(lines[i]);
            if (!m.Success) continue;
            if (!DateTime.TryParseExact(m.Groups["ts"].Value,
                    "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var local))
                continue;
            var ageUtc = _clock.UtcNow - local.ToUniversalTime();
            return ageUtc < _window;
        }
        return false;
    }
}
