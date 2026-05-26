using System;
using System.Text.Json.Serialization;

namespace GraceKeeper.Core;

public sealed class ConfigModel
{
    public int Version { get; set; } = 1;
    public ScheduleConfig Schedule { get; set; } = new();
    public string Theme { get; set; } = "auto";  // "auto" | "light" | "dark"
    public Counters Counters { get; set; } = new();
    public LogOffsets LogOffsets { get; set; } = new();
    public UpdateCheckConfig UpdateCheck { get; set; } = new();
    public CleanerConfig Cleaner { get; set; } = new();
}

public sealed class ScheduleConfig
{
    [JsonPropertyName("interval_minutes")] public int IntervalMinutes { get; set; } = 720;
    [JsonPropertyName("start_time")] public string StartTime { get; set; } = "03:00";
}

public sealed class Counters
{
    [JsonPropertyName("popups_dismissed_lifetime")] public long PopupsDismissedLifetime { get; set; }
    [JsonPropertyName("rnl_files_deleted_lifetime")] public long RnlFilesDeletedLifetime { get; set; }
}

public sealed class LogOffsets
{
    [JsonPropertyName("dismisser.log")] public long DismisserLogOffset { get; set; }
    [JsonPropertyName("cleaner.log")] public long CleanerLogOffset { get; set; }
}

public sealed class UpdateCheckConfig
{
    public bool Enabled { get; set; } = true;
    [JsonPropertyName("last_checked_utc")] public DateTime? LastCheckedUtc { get; set; }
    [JsonPropertyName("latest_known_version")] public string? LatestKnownVersion { get; set; }
}

public sealed class CleanerConfig
{
    [JsonPropertyName("interval_hours")] public int IntervalHours { get; set; } = 12;
}
