using System;
using System.IO;
using GraceKeeper.Core;
using Xunit;

namespace GraceKeeper.Core.Tests;

public class DiagnosticsBundleTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gk-diag-" + Guid.NewGuid().ToString("N"));
    public DiagnosticsBundleTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    private static DiagnosticsInput SampleInput(
        bool sentinelDisabled = false,
        bool rnlExists = true,
        bool runningAsAdmin = false,
        string tasksReport = "--- GraceKeeper - Boot Cleanup ---\n  Status: Ready\n  Last Run Time: 2026-05-28 00:52:35\n  Last Result: 0",
        string cleanerLogTail = "2026-05-28 00:52:35 | started | mode=Boot\n2026-05-28 00:52:35 | refreshed=3")
    {
        return new DiagnosticsInput(
            LocalNow: new DateTime(2026, 5, 28, 21, 35, 12, DateTimeKind.Local),
            DashboardVersion: "0.4.1",
            CleanerExeVersion: "0.4.1.0",
            RunningUser: @"DESKTOP-X\nick",
            RunningAsAdmin: runningAsAdmin,
            SentinelDisabled: sentinelDisabled,
            RnlTargetDir: @"C:\ProgramData\Rockwell Automation\FactoryTalk Activation",
            RnlTargetExists: rnlExists,
            RnlFiles: rnlExists
                ? new[] { ("LOGIX5380.rnl", 2048L, new DateTime(2026, 5, 28, 0, 52, 30, DateTimeKind.Local)) }
                : Array.Empty<(string, long, DateTime)>(),
            ScheduledTasksReport: tasksReport,
            CleanerLogTail: cleanerLogTail,
            DismisserLogTail: "",
            SupervisorLogTail: "",
            OsCaption: "Microsoft Windows NT 10.0.26200.0 (x64)",
            MachineName: "DESKTOP-X");
    }

    [Fact]
    public void Build_ContainsAllTopLevelSections()
    {
        var md = DiagnosticsBundle.Build(SampleInput());
        Assert.Contains("# GraceKeeper Diagnostics", md);
        Assert.Contains("## Environment", md);
        Assert.Contains("## RNL Target Directory", md);
        Assert.Contains("## Scheduled Tasks", md);
        Assert.Contains("## cleaner.log", md);
        Assert.Contains("## dismisser.log", md);
        Assert.Contains("## supervisor.log", md);
    }

    [Fact]
    public void Build_SurfacesVersionAndElevation()
    {
        var md = DiagnosticsBundle.Build(SampleInput(runningAsAdmin: true));
        Assert.Contains("Dashboard version: `0.4.1`", md);
        Assert.Contains("Cleaner.exe version: `0.4.1.0`", md);
        Assert.Contains("elevated: yes", md);
    }

    [Fact]
    public void Build_FlagsMissingRnlTargetDirectory()
    {
        var md = DiagnosticsBundle.Build(SampleInput(rnlExists: false));
        Assert.Contains("Exists: no", md);
        Assert.Contains("*.rnl files: 0", md);
    }

    [Fact]
    public void Build_SurfacesSentinelPaused()
    {
        var md = DiagnosticsBundle.Build(SampleInput(sentinelDisabled: true));
        Assert.Contains("DISABLED sentinel: yes", md);
    }

    [Fact]
    public void Build_HandlesEmptyLogTails_Gracefully()
    {
        var md = DiagnosticsBundle.Build(SampleInput(cleanerLogTail: ""));
        Assert.Contains("(empty or missing)", md);
    }

    [Fact]
    public void TailLines_ReturnsEmptyString_WhenFileMissing()
    {
        var missing = Path.Combine(_dir, "no-such.log");
        Assert.Equal(string.Empty, DiagnosticsBundle.TailLines(missing, 50));
    }

    [Fact]
    public void TailLines_ReturnsAllLines_WhenFileShorterThanLimit()
    {
        var path = Path.Combine(_dir, "small.log");
        File.WriteAllLines(path, new[] { "one", "two", "three" });
        var tail = DiagnosticsBundle.TailLines(path, 50);
        Assert.Equal("one\ntwo\nthree", tail);
    }

    [Fact]
    public void TailLines_ReturnsLastN_WhenFileLargerThanLimit()
    {
        var path = Path.Combine(_dir, "large.log");
        var lines = new string[100];
        for (int i = 0; i < lines.Length; i++) lines[i] = $"line {i}";
        File.WriteAllLines(path, lines);
        var tail = DiagnosticsBundle.TailLines(path, 5);
        Assert.Equal("line 95\nline 96\nline 97\nline 98\nline 99", tail);
    }
}
