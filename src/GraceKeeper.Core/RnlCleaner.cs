using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GraceKeeper.Core;

public interface IRnlCleaner
{
    Task<CleanupResult> RunAsync(CleanupMode mode, CancellationToken ct);
}

public sealed class RnlCleaner : IRnlCleaner
{
    private const int RetryAttempts = 3;

    private readonly string _targetDir;
    private readonly IEchoControllerProbe _echoProbe;
    private readonly IServiceBouncer _bouncer;
    private readonly TimeSpan _retryDelay;

    public RnlCleaner(string targetDir, IEchoControllerProbe echoProbe, IServiceBouncer bouncer, TimeSpan? retryDelay = null)
    {
        _targetDir = targetDir;
        _echoProbe = echoProbe;
        _bouncer = bouncer;
        _retryDelay = retryDelay ?? TimeSpan.FromMilliseconds(200);
    }

    public async Task<CleanupResult> RunAsync(CleanupMode mode, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        if (!Directory.Exists(_targetDir))
            return CleanupResult.Empty(sw.Elapsed);

        var files = Directory.GetFiles(_targetDir, "*.rnl");
        if (files.Length == 0)
            return CleanupResult.Empty(sw.Elapsed);

        var refreshed = 0;
        var locked = new List<string>();
        foreach (var f in files)
        {
            ct.ThrowIfCancellationRequested();
            if (await TryDeleteWithRetries(f, ct))
                refreshed++;
            else
                locked.Add(f);
        }

        if (locked.Count == 0)
            return new CleanupResult(refreshed, 0, 0, Array.Empty<string>(), null, 0, sw.Elapsed);

        var activity = _echoProbe.GetActivity();
        var bounceEligible = mode switch
        {
            CleanupMode.Boot => true,
            CleanupMode.ManualForce => true,
            CleanupMode.Manual => true,
            CleanupMode.Runtime or CleanupMode.SafetyNet => activity.Count == 0,
            _ => false
        };

        if (!bounceEligible)
        {
            var reason = "echo-busy: " + string.Join(",", activity.FamilyNames);
            return new CleanupResult(
                refreshed, 0, locked.Count,
                locked.Select(Path.GetFileName).ToList()!,
                reason, locked.Count, sw.Elapsed);
        }

        var bounceResult = await _bouncer.BounceAndRetryAsync(
            async () =>
            {
                // No cancellation check between files here: services are already
                // stopped; abandoning mid-sweep would leave them down with files
                // still locked. Caller-driven cancellation is honored via the outer
                // ct → ServiceBouncer's start phase.
                var freed = 0;
                var still = new List<string>();
                foreach (var f in locked)
                {
                    if (await TryDeleteWithRetries(f, ct))
                        freed++;
                    else
                        still.Add(Path.GetFileName(f));
                }
                return new RetryOutcome(freed, still);
            }, ct);

        return new CleanupResult(
            refreshed, bounceResult.FreedByBounceCount, 0,
            Array.Empty<string>(), null,
            bounceResult.StillLocked.Count, sw.Elapsed);
    }

    private async Task<bool> TryDeleteWithRetries(string path, CancellationToken ct)
    {
        for (int attempt = 0; attempt < RetryAttempts; attempt++)
        {
            if (attempt > 0) await Task.Delay(_retryDelay, ct);
            try { File.Delete(path); return true; }
            catch (IOException) { /* retry */ }
            catch (UnauthorizedAccessException) { /* retry */ }
        }
        return false;
    }
}
