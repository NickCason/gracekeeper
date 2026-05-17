using System;

namespace GraceKeeper.Core;

public sealed class DismisserRecord
{
    public int Pid { get; set; }
    public string ExePath { get; set; } = "";
    public string ScriptPath { get; set; } = "";
    public DateTime StartedUtc { get; set; }
}
