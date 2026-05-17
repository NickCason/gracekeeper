using System;
using System.IO;

namespace GraceKeeper.Core;

public static class PathResolver
{
    public static string ProgramDataRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "GraceKeeper");

    public static string ConfigPath => Path.Combine(ProgramDataRoot, "config.json");
    public static string LogsDir => Path.Combine(ProgramDataRoot, "logs");
    public static string DismisserLogPath => Path.Combine(LogsDir, "dismisser.log");
    public static string CleanerLogPath => Path.Combine(LogsDir, "cleaner.log");
    public static string DisabledSentinelPath => Path.Combine(ProgramDataRoot, "DISABLED");
    public static string SupervisorLogPath => Path.Combine(LogsDir, "supervisor.log");
    public static string DismisserPidFilePath => Path.Combine(ProgramDataRoot, "dismisser-pid.json");
}
