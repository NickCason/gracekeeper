using System;
using System.Threading;

namespace GraceKeeper.Core;

/// <summary>
/// Per-session single-instance guard with cross-process activation.
///
/// First-construction creates a named mutex (owned) and a named auto-reset event,
/// then starts a background listener that raises <see cref="ActivationRequested"/>
/// whenever any other instance calls <see cref="SignalExistingInstance"/>. The
/// caller is expected to check <see cref="IsFirstInstance"/> and, if false, signal
/// and exit so the running copy can foreground its window.
///
/// Why per-session ("Local\\") instead of "Global\\": GraceKeeper is a tray app
/// launched from the user's HKLM Run entry in their own logon session, so each
/// interactive user gets their own dashboard — global scoping would falsely
/// suppress launches across user sessions.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activator;
    private readonly Thread? _listenerThread;
    private readonly CancellationTokenSource _cts = new();

    public bool IsFirstInstance { get; }

    public event EventHandler? ActivationRequested;

    public SingleInstance(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("name must be non-empty.", nameof(name));

        var mutexName = $"Local\\GraceKeeper.{name}.Mutex";
        var eventName = $"Local\\GraceKeeper.{name}.Activator";

        _mutex = new Mutex(initiallyOwned: true, mutexName, out var createdNew);
        IsFirstInstance = createdNew;
        _activator = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);

        if (IsFirstInstance)
        {
            _listenerThread = new Thread(WaitForActivation)
            {
                IsBackground = true,
                Name = "SingleInstance.ActivationListener"
            };
            _listenerThread.Start();
        }
    }

    public void SignalExistingInstance() => _activator.Set();

    private void WaitForActivation()
    {
        var handles = new WaitHandle[] { _activator, _cts.Token.WaitHandle };
        while (!_cts.IsCancellationRequested)
        {
            int index;
            try { index = WaitHandle.WaitAny(handles); }
            catch (ObjectDisposedException) { break; }

            if (index == 0)
            {
                try { ActivationRequested?.Invoke(this, EventArgs.Empty); }
                catch { /* listener must survive faulty handlers */ }
            }
            else
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        _listenerThread?.Join(TimeSpan.FromSeconds(1));
        if (IsFirstInstance)
        {
            try { _mutex.ReleaseMutex(); } catch { }
        }
        _mutex.Dispose();
        _activator.Dispose();
        _cts.Dispose();
    }
}
