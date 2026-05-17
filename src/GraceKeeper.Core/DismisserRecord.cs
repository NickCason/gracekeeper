using System;
using System.Text.Json.Serialization;

namespace GraceKeeper.Core;

public sealed class DismisserRecord
{
    [JsonPropertyName("pid")] public int Pid { get; set; }
    [JsonPropertyName("exe_path")] public string ExePath { get; set; } = "";
    [JsonPropertyName("script_path")] public string ScriptPath { get; set; } = "";
    [JsonPropertyName("started_utc")] public DateTime StartedUtc { get; set; }
}
