using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using GraceKeeper.Core;

namespace GraceKeeper.UI;

public sealed class CleanupScheduler : IDisposable
{
    private readonly IRnlCleaner _cleaner;
    private readonly Func<int> _intervalHoursProvider;
    private readonly TimeSpan _launchDelay;
    private readonly CleanerLogWriter _logWriter;
    private readonly DispatcherTimer _timer;
    private readonly CancellationTokenSource _cts = new();
    private bool _running;

    public CleanupScheduler(
        IRnlCleaner cleaner,
        CleanerLogWriter logWriter,
        Func<int> intervalHoursProvider,
        TimeSpan launchDelay)
    {
        _cleaner = cleaner;
        _logWriter = logWriter;
        _intervalHoursProvider = intervalHoursProvider;
        _launchDelay = launchDelay;
        _timer = new DispatcherTimer { Interval = _launchDelay };
        _timer.Tick += async (_, _) => await OnTickAsync();
    }

    public void Start() => _timer.Start();

    private async Task OnTickAsync()
    {
        if (_running) return;
        _running = true;
        try
        {
            try
            {
                var result = await Task.Run(() => _cleaner.RunAsync(CleanupMode.Runtime, _cts.Token));
                _logWriter.WriteResult(result);
            }
            catch (Exception ex)
            {
                _logWriter.WriteFailed(ex.GetType().Name, ex.Message);
            }
            _timer.Interval = TimeSpan.FromHours(Math.Max(1, _intervalHoursProvider()));
        }
        finally
        {
            _running = false;
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _cts.Cancel();
        _cts.Dispose();
    }
}
