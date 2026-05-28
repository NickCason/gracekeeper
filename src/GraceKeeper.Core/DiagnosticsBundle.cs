using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GraceKeeper.Core;

public sealed record DiagnosticsInput(
    DateTime LocalNow,
    string DashboardVersion,
    string CleanerExeVersion,
    string RunningUser,
    bool RunningAsAdmin,
    bool SentinelDisabled,
    string RnlTargetDir,
    bool RnlTargetExists,
    IReadOnlyList<(string Name, long LengthBytes, DateTime LastWriteLocal)> RnlFiles,
    string ScheduledTasksReport, // raw `schtasks /Query /V /FO LIST` output for the 3 GK tasks
    string CleanerLogTail,       // already-tailed text (last N lines), or "" if missing
    string DismisserLogTail,
    string SupervisorLogTail,
    string OsCaption,            // e.g. "Windows 11 Pro 10.0.26200"
    string MachineName);

public static class DiagnosticsBundle
{
    // Build a markdown report suitable for pasting into a chat message. Order
    // matters for triage: top of the report has the smoking guns (versions,
    // permissions, task registration status). Logs are last so they don't
    // bury the symptoms.
    public static string Build(DiagnosticsInput d)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# GraceKeeper Diagnostics — {d.LocalNow:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        sb.AppendLine("## Environment");
        sb.AppendLine($"- Machine: `{d.MachineName}`");
        sb.AppendLine($"- OS: {d.OsCaption}");
        sb.AppendLine($"- User: `{d.RunningUser}` (elevated: {YesNo(d.RunningAsAdmin)})");
        sb.AppendLine($"- Dashboard version: `{d.DashboardVersion}`");
        sb.AppendLine($"- Cleaner.exe version: `{d.CleanerExeVersion}`");
        sb.AppendLine($"- DISABLED sentinel: {YesNo(d.SentinelDisabled)}");
        sb.AppendLine();

        sb.AppendLine("## RNL Target Directory");
        sb.AppendLine($"- Path: `{d.RnlTargetDir}`");
        sb.AppendLine($"- Exists: {YesNo(d.RnlTargetExists)}");
        sb.AppendLine($"- *.rnl files: {d.RnlFiles.Count}");
        foreach (var f in d.RnlFiles)
        {
            sb.AppendLine($"  - `{f.Name}` ({f.LengthBytes} bytes, modified {f.LastWriteLocal:yyyy-MM-dd HH:mm:ss})");
        }
        sb.AppendLine();

        sb.AppendLine("## Scheduled Tasks");
        AppendFence(sb, d.ScheduledTasksReport);
        sb.AppendLine();

        sb.AppendLine("## cleaner.log");
        AppendFence(sb, d.CleanerLogTail);
        sb.AppendLine();
        sb.AppendLine("## dismisser.log");
        AppendFence(sb, d.DismisserLogTail);
        sb.AppendLine();
        sb.AppendLine("## supervisor.log");
        AppendFence(sb, d.SupervisorLogTail);
        sb.AppendLine();

        return sb.ToString();
    }

    private static void AppendFence(StringBuilder sb, string body)
    {
        sb.AppendLine("```");
        sb.AppendLine(string.IsNullOrEmpty(body) ? "(empty or missing)" : body.TrimEnd());
        sb.AppendLine("```");
    }

    private static string YesNo(bool b) => b ? "yes" : "no";

    // Read the last N lines of a file, or empty string if missing/unreadable.
    // Used by UI layer to populate the *LogTail inputs.
    public static string TailLines(string path, int lineCount)
    {
        try
        {
            if (!File.Exists(path)) return string.Empty;
            var all = File.ReadAllLines(path);
            if (all.Length <= lineCount) return string.Join("\n", all);
            var slice = new string[lineCount];
            Array.Copy(all, all.Length - lineCount, slice, 0, lineCount);
            return string.Join("\n", slice);
        }
        catch (Exception ex)
        {
            return $"(read failed: {ex.GetType().Name}: {ex.Message})";
        }
    }
}
