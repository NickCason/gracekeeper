using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using GraceKeeper.Core;

namespace GraceKeeper.UI.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
    private readonly LogTailer _dismisserTailer;
    private readonly LogTailer _cleanerTailer;
    private readonly ConfigStore _configStore;
    private readonly DisabledSentinel _sentinel;
    private readonly DispatcherTimer _pollTimer;
    private static readonly Regex _deletedCountRegex = new(@"(?:deleted|refreshed)=(\d+)", RegexOptions.Compiled);

    private long _popupsDismissedLifetime;
    private long _rnlFilesDeletedLifetime;
    private int _popupsToday;
    private int _filesToday;
    private string _statusText = "Healthy";
    private string _timeSinceLastCleanText = "no runs yet";
    private CleanerButtonState _cleanerState = CleanerButtonState.Idle;
    private string _lastCleanerOutcome = "";
    private string _diagnosticsStatus = "";

    public string Version =>
        System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "dev";

    public CleanerButtonState CleanerState
    {
        get => _cleanerState;
        private set => Set(ref _cleanerState, value);
    }

    public long PopupsDismissedLifetime { get => _popupsDismissedLifetime; private set => Set(ref _popupsDismissedLifetime, value); }
    public long RnlFilesDeletedLifetime { get => _rnlFilesDeletedLifetime; private set => Set(ref _rnlFilesDeletedLifetime, value); }
    public int PopupsToday { get => _popupsToday; private set => Set(ref _popupsToday, value); }
    public int FilesToday { get => _filesToday; private set => Set(ref _filesToday, value); }
    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }
    public string TimeSinceLastCleanText { get => _timeSinceLastCleanText; private set => Set(ref _timeSinceLastCleanText, value); }
    public string LastCleanerOutcome { get => _lastCleanerOutcome; private set => Set(ref _lastCleanerOutcome, value); }
    public string DiagnosticsStatus { get => _diagnosticsStatus; private set => Set(ref _diagnosticsStatus, value); }

    public ObservableCollection<LogEvent> RecentActivity { get; } = new();

    public DashboardViewModel()
    {
        _configStore = new ConfigStore(PathResolver.ConfigPath);
        _dismisserTailer = new LogTailer(PathResolver.DismisserLogPath);
        _cleanerTailer = new LogTailer(PathResolver.CleanerLogPath);
        _sentinel = new DisabledSentinel(PathResolver.DisabledSentinelPath);

        var cfg = _configStore.Load();
        _popupsDismissedLifetime = cfg.Counters.PopupsDismissedLifetime;
        _rnlFilesDeletedLifetime = cfg.Counters.RnlFilesDeletedLifetime;

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _pollTimer.Tick += (_, _) => RefreshFromLogs();
        _pollTimer.Start();
        RefreshFromLogs();
    }

    private void RefreshFromLogs()
    {
        var cfg = _configStore.Load();

        var newDismiss = _dismisserTailer.ReadNew(cfg.LogOffsets.DismisserLogOffset, out var newDismissOffset);
        var newClean = _cleanerTailer.ReadNew(cfg.LogOffsets.CleanerLogOffset, out var newCleanOffset);

        var midnight = DateTime.Today;

        foreach (var ev in newDismiss.Where(e => e.Kind == LogEventKind.Dismiss))
        {
            PopupsDismissedLifetime++;
            if (ev.Timestamp >= midnight) PopupsToday++;
            RecentActivity.Insert(0, ev);
        }
        foreach (var ev in newClean.Where(e => e.Kind == LogEventKind.Clean))
        {
            var m = _deletedCountRegex.Match(ev.Description);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var n))
            {
                RnlFilesDeletedLifetime += n;
                if (ev.Timestamp >= midnight) FilesToday += n;
            }
            RecentActivity.Insert(0, ev);
        }

        while (RecentActivity.Count > 50) RecentActivity.RemoveAt(RecentActivity.Count - 1);

        cfg.LogOffsets.DismisserLogOffset = newDismissOffset;
        cfg.LogOffsets.CleanerLogOffset = newCleanOffset;
        cfg.Counters.PopupsDismissedLifetime = PopupsDismissedLifetime;
        cfg.Counters.RnlFilesDeletedLifetime = RnlFilesDeletedLifetime;
        _configStore.Save(cfg);

        var lastClean = RecentActivity.FirstOrDefault(e => e.Kind == LogEventKind.Clean);
        TimeSinceLastCleanText = lastClean == null ? "no runs yet" : FormatElapsed(DateTime.Now - lastClean.Timestamp);

        StatusText = _sentinel.IsDisabled ? "Paused" : "Healthy";
    }

    private static string FormatElapsed(TimeSpan t)
    {
        if (t.TotalHours >= 1) return $"{(int)t.TotalHours}h ago";
        if (t.TotalMinutes >= 1) return $"{(int)t.TotalMinutes}m ago";
        return "just now";
    }

    public async Task RunCleanerNowAsync()
    {
        if (CleanerState == CleanerButtonState.Running) return;

        // Echo-activity check is UX (we don't want to silently bounce live FT
        // services). Runs in the dashboard's user context — the SYSTEM-context
        // Cleaner.exe doesn't get this confirmation, but Manual mode is
        // semantically "force" so the bounce will proceed if needed.
        try
        {
            var probe = new EchoControllerProbe(new WmiProcessTreeReader());
            var activity = probe.GetActivity();
            if (activity.Count > 0)
            {
                var families = string.Join(", ", activity.FamilyNames);
                var msg = $"Echo has {activity.Count} active controller(s) ({families}). Cleanup may fault them if a service bounce becomes necessary.";
                var confirm = MessageBox.Show(msg, "Confirm Run Cleaner", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.OK)
                {
                    return;
                }
            }
        }
        catch
        {
            // WMI probe failure: proceed without the confirmation prompt — the
            // task itself will still run safely (Manual mode is force-bounce).
        }

        CleanerState = CleanerButtonState.Running;
        LastCleanerOutcome = "Triggering SYSTEM task…";

        // Snapshot the cleaner.log size BEFORE firing so the watcher can tell
        // when the SYSTEM-context Cleaner.exe finishes writing its result.
        var logPath = PathResolver.CleanerLogPath;
        long startOffset = 0;
        try { if (File.Exists(logPath)) startOffset = new FileInfo(logPath).Length; }
        catch { /* tolerate IO race; watcher just sees any growth */ }

        try
        {
            var client = new ScheduledTaskClient("GraceKeeper - Manual Cleanup");
            if (!await client.ExistsAsync())
            {
                CleanerState = CleanerButtonState.Failed;
                LastCleanerOutcome = "Manual Cleanup task is not registered. Reinstall GraceKeeper.";
                ScheduleResetToIdle();
                return;
            }

            await client.RunNowAsync();

            var watcher = new CleanerCompletionWatcher(logPath);
            var completion = await watcher.WaitAsync(startOffset, TimeSpan.FromSeconds(90), CancellationToken.None);
            if (completion == CleanerCompletionResult.TimedOut)
            {
                CleanerState = CleanerButtonState.Failed;
                LastCleanerOutcome = "Timed out waiting for cleaner. Check Task Scheduler for 'GraceKeeper - Manual Cleanup'.";
                ScheduleResetToIdle();
                return;
            }

            // Read everything appended past the snapshot to find the latest
            // result/skipped/failed line (banner is also new, ignore it).
            var newText = ReadAppendedText(logPath, startOffset);
            var (state, summary) = ParseManualOutcome(newText);
            CleanerState = state;
            LastCleanerOutcome = summary;
        }
        catch (Exception ex)
        {
            CleanerState = CleanerButtonState.Failed;
            LastCleanerOutcome = $"Trigger failed: {ex.GetType().Name}: {ex.Message}";
        }
        ScheduleResetToIdle();
    }

    private static string ReadAppendedText(string path, long startOffset)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (startOffset > 0 && startOffset <= fs.Length) fs.Seek(startOffset, SeekOrigin.Begin);
            using var sr = new StreamReader(fs);
            return sr.ReadToEnd();
        }
        catch (Exception ex) { return $"(could not read log: {ex.Message})"; }
    }

    private static readonly Regex _refreshedRx = new(@"refreshed=(\d+)", RegexOptions.Compiled);
    private static readonly Regex _freedByBounceRx = new(@"freed-by-bounce=(\d+)", RegexOptions.Compiled);
    private static readonly Regex _stillLockedRx = new(@"still-locked=(\d+)", RegexOptions.Compiled);

    private static (CleanerButtonState, string) ParseManualOutcome(string newText)
    {
        var lines = newText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        string? resultLine = null;
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            var l = lines[i];
            if (l.IndexOf(" | refreshed=", StringComparison.Ordinal) >= 0
                || l.IndexOf(" | skipped ",  StringComparison.Ordinal) >= 0
                || l.IndexOf(" | failed: ",  StringComparison.Ordinal) >= 0)
            {
                resultLine = l;
                break;
            }
        }
        if (resultLine == null)
            return (CleanerButtonState.Failed, "Cleaner wrote the banner but no result line — see cleaner.log.");
        if (resultLine.IndexOf(" | failed: ", StringComparison.Ordinal) >= 0)
            return (CleanerButtonState.Failed, ExtractAfter(resultLine, " | failed: "));
        if (resultLine.IndexOf(" | skipped ", StringComparison.Ordinal) >= 0)
            return (CleanerButtonState.Failed, "Cleaner skipped: " + ExtractAfter(resultLine, " | skipped "));

        var refMatch = _refreshedRx.Match(resultLine);
        var freedMatch = _freedByBounceRx.Match(resultLine);
        var lockMatch = _stillLockedRx.Match(resultLine);
        int refreshed = refMatch.Success ? int.Parse(refMatch.Groups[1].Value) : 0;
        int freed = freedMatch.Success ? int.Parse(freedMatch.Groups[1].Value) : 0;
        int stillLocked = lockMatch.Success ? int.Parse(lockMatch.Groups[1].Value) : 0;

        if (stillLocked > 0)
            return (CleanerButtonState.Failed, $"Cleanup ran but {stillLocked} file(s) still locked.");
        var deleted = refreshed + freed;
        return (CleanerButtonState.Done,
            deleted == 0 ? "Nothing to clean — no stale .rnl files found." : $"Refreshed {deleted} file(s).");
    }

    private static string ExtractAfter(string line, string marker)
    {
        var i = line.IndexOf(marker, StringComparison.Ordinal);
        return i < 0 ? line : line.Substring(i + marker.Length).Trim();
    }

    private void ScheduleResetToIdle()
    {
        // Hold the success/failure visual for a few seconds before resetting,
        // so users see what happened. Failed state holds longer than Done so
        // the user has time to read the message.
        var hold = CleanerState == CleanerButtonState.Failed
            ? TimeSpan.FromSeconds(8)
            : TimeSpan.FromSeconds(3);
        var t = new DispatcherTimer { Interval = hold };
        t.Tick += (_, _) =>
        {
            t.Stop();
            if (CleanerState != CleanerButtonState.Running)
                CleanerState = CleanerButtonState.Idle;
        };
        t.Start();
    }

    public void OpenLogsFolder()
    {
        try
        {
            var dir = PathResolver.LogsDir;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
            DiagnosticsStatus = $"Opened {dir}";
        }
        catch (Exception ex)
        {
            DiagnosticsStatus = $"Open failed: {ex.Message}";
        }
    }

    public async Task CopyDiagnosticsAsync()
    {
        DiagnosticsStatus = "Collecting diagnostics…";
        try
        {
            var report = await Task.Run(BuildDiagnosticsReport);
            Clipboard.SetText(report);
            DiagnosticsStatus = $"Copied {report.Length:N0} chars to clipboard — paste into chat.";
        }
        catch (Exception ex)
        {
            DiagnosticsStatus = $"Copy failed: {ex.GetType().Name}: {ex.Message}";
        }
    }

    private static string BuildDiagnosticsReport()
    {
        string dashboardVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
        string cleanerExeVersion = "missing";
        try
        {
            if (File.Exists(PathResolver.CleanerExePath))
                cleanerExeVersion = FileVersionInfo.GetVersionInfo(PathResolver.CleanerExePath).FileVersion ?? "unknown";
        }
        catch { /* keep "missing" */ }

        bool runningAsAdmin = false;
        try
        {
            using var id = WindowsIdentity.GetCurrent();
            runningAsAdmin = new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { /* leave false */ }

        var rnlDir = PathResolver.RnlTargetDir;
        var rnlExists = Directory.Exists(rnlDir);
        var rnlFiles = new System.Collections.Generic.List<(string, long, DateTime)>();
        if (rnlExists)
        {
            try
            {
                foreach (var p in Directory.GetFiles(rnlDir, "*.rnl"))
                {
                    var fi = new FileInfo(p);
                    rnlFiles.Add((fi.Name, fi.Length, fi.LastWriteTime));
                }
            }
            catch { /* permission/IO — leave list empty */ }
        }

        string tasksReport = QueryScheduledTasksRaw(new[]
        {
            "GraceKeeper - Boot Cleanup",
            "GraceKeeper - Cleanup RNL",
            "GraceKeeper - Manual Cleanup",
            "GraceKeeper - Dashboard Logon",
        });

        var input = new DiagnosticsInput(
            LocalNow: DateTime.Now,
            DashboardVersion: dashboardVersion,
            CleanerExeVersion: cleanerExeVersion,
            RunningUser: Environment.UserDomainName + "\\" + Environment.UserName,
            RunningAsAdmin: runningAsAdmin,
            SentinelDisabled: new DisabledSentinel(PathResolver.DisabledSentinelPath).IsDisabled,
            RnlTargetDir: rnlDir,
            RnlTargetExists: rnlExists,
            RnlFiles: rnlFiles,
            ScheduledTasksReport: tasksReport,
            CleanerLogTail: DiagnosticsBundle.TailLines(PathResolver.CleanerLogPath, 50),
            DismisserLogTail: DiagnosticsBundle.TailLines(PathResolver.DismisserLogPath, 30),
            SupervisorLogTail: DiagnosticsBundle.TailLines(PathResolver.SupervisorLogPath, 30),
            OsCaption: $"{Environment.OSVersion.VersionString} ({(Environment.Is64BitOperatingSystem ? "x64" : "x86")})",
            MachineName: Environment.MachineName);

        return DiagnosticsBundle.Build(input);
    }

    private static string QueryScheduledTasksRaw(string[] taskNames)
    {
        var sb = new StringBuilder();
        foreach (var name in taskNames)
        {
            sb.AppendLine($"--- {name} ---");
            try
            {
                var psi = new ProcessStartInfo("schtasks.exe", $"/Query /TN \"{name}\" /V /FO LIST")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                using var p = Process.Start(psi)!;
                var stdout = p.StandardOutput.ReadToEnd();
                var stderr = p.StandardError.ReadToEnd();
                p.WaitForExit(5000);
                // Keep only the lines that matter for triage.
                foreach (var line in stdout.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var t = line.TrimStart();
                    if (t.StartsWith("Status:") || t.StartsWith("Last Run Time:") ||
                        t.StartsWith("Last Result:") || t.StartsWith("Next Run Time:") ||
                        t.StartsWith("Scheduled Task State:") || t.StartsWith("Run As User:"))
                    {
                        sb.AppendLine("  " + t);
                    }
                }
                if (p.ExitCode != 0)
                {
                    sb.AppendLine("  (schtasks exit " + p.ExitCode + ")");
                    if (!string.IsNullOrWhiteSpace(stderr))
                        sb.AppendLine("  stderr: " + stderr.Trim());
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  (query failed: {ex.GetType().Name}: {ex.Message})");
            }
        }
        return sb.ToString();
    }

    public void TogglePause()
    {
        if (_sentinel.IsDisabled) _sentinel.Enable();
        else _sentinel.Disable();
        StatusText = _sentinel.IsDisabled ? "Paused" : "Healthy";
    }
}

public enum CleanerButtonState
{
    Idle,
    Running,
    Done,
    Failed,
}
