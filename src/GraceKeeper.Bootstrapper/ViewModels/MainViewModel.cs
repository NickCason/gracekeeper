using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using GraceKeeper.Bootstrapper.Views;
using GraceKeeper.Core;
using WixToolset.Mba.Core;

namespace GraceKeeper.Bootstrapper.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IEngine _engine;
    private readonly IBootstrapperCommand _command;
    private readonly GraceKeeperBootstrapper _ba;

    private UserControl? _currentPage;
    private bool _acceptedLicense;
    private bool _launchOnFinish = true;
    private int _percent;
    private string _statusText = "";
    private string _timeRemaining = "";
    private string _version = "";
    private string _licenseText = "";
    private bool _removeAllData;
    private double _smoothedPercent;
    private bool _isRunning;
    private readonly PhaseProgressTracker _tracker;

    public event Action? RequestClose;
    public event PropertyChangedEventHandler? PropertyChanged;

    public UserControl? CurrentPage { get => _currentPage; set { _currentPage = value; OnPropertyChanged(); } }
    public bool AcceptedLicense { get => _acceptedLicense; set { _acceptedLicense = value; OnPropertyChanged(); ((RelayCommand)InstallCommand).RaiseCanExecuteChanged(); } }
    public bool LaunchOnFinish { get => _launchOnFinish; set { _launchOnFinish = value; OnPropertyChanged(); } }
    public int Percent
    {
        get => _percent;
        set
        {
            _percent = value;
            OnPropertyChanged();
            AnimateSmoothedPercent(value);
        }
    }
    public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }
    public string TimeRemaining { get => _timeRemaining; set { _timeRemaining = value; OnPropertyChanged(); } }
    public string Version { get => _version; set { _version = value; OnPropertyChanged(); } }
    public string LicenseText { get => _licenseText; set { _licenseText = value; OnPropertyChanged(); } }
    public bool RemoveAllData { get => _removeAllData; set { _removeAllData = value; OnPropertyChanged(); } }
    public double SmoothedPercent { get => _smoothedPercent; private set { _smoothedPercent = value; OnPropertyChanged(); } }
    public bool IsRunning { get => _isRunning; private set { _isRunning = value; OnPropertyChanged(); } }
    public System.Collections.ObjectModel.ObservableCollection<PhaseRow> Phases => _tracker.Phases;

    private void AnimateSmoothedPercent(int target)
    {
        var startValue = _smoothedPercent;        // capture once — _smoothedPercent mutates each tick
        var durationMs = 1200.0;
        var easing = new System.Windows.Media.Animation.CubicEase
        {
            EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut,
        };
        var anim = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = startValue,
            To = target,
            Duration = new System.Windows.Duration(System.TimeSpan.FromMilliseconds(durationMs)),
            EasingFunction = easing,
            FillBehavior = System.Windows.Media.Animation.FillBehavior.HoldEnd,
        };
        var clock = anim.CreateClock();
        clock.CurrentTimeInvalidated += (_, _) =>
        {
            if (clock.CurrentTime is System.TimeSpan ct)
            {
                var fraction = ct.TotalMilliseconds / durationMs;
                if (fraction > 1) fraction = 1;
                var eased = easing.Ease(fraction);
                SmoothedPercent = startValue + (target - startValue) * eased;
            }
        };
        clock.Controller?.Begin();
    }

    public ICommand InstallCommand { get; }
    public ICommand UninstallCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand FinishCommand { get; }
    public ICommand ViewLogCommand { get; }

    public MainViewModel(IEngine engine, IBootstrapperCommand command, GraceKeeperBootstrapper ba)
    {
        _engine = engine;
        _command = command;
        _ba = ba;

        InstallCommand = new RelayCommand(_ => DoInstall(), _ => AcceptedLicense);
        CancelCommand = new RelayCommand(_ => DoCancel());
        FinishCommand = new RelayCommand(_ => DoFinish());
        UninstallCommand = new RelayCommand(_ => DoUninstall());
        ViewLogCommand = new RelayCommand(_ => DoViewLog());

        // WixToolset.Mba.Core 4.0.6 event names confirmed via reflection:
        //   DetectComplete     -> DetectCompleteEventArgs     (.Status: int)
        //   PlanComplete       -> PlanCompleteEventArgs       (.Status: int)
        //   ExecuteMsiMessage  -> ExecuteMsiMessageEventArgs  (.Message: string)
        //   Progress           -> ProgressEventArgs           (.OverallPercentage: int)
        //   ApplyComplete      -> ApplyCompleteEventArgs      (.Status: int)
        _ba.DetectComplete += OnDetectComplete;
        _ba.PlanComplete += OnPlanComplete;
        _ba.ExecuteMsiMessage += OnExecuteMsiMessage;
        _ba.Progress += OnProgress;
        _ba.ApplyComplete += OnApplyComplete;

        _tracker = new PhaseProgressTracker(new[] { "Preparing", "Installing", "Finishing" });
    }

    public void Initialize()
    {
        // API adaptation: IEngine has no StringVariables indexer in 4.0.6.
        // Use GetVariableString(name) instead.
        try { Version = _engine.GetVariableString("WixBundleVersion"); }
        catch { Version = ""; }

        LicenseText = LoadEmbeddedLicense();

        if (_command.Action == LaunchAction.Uninstall)
        {
            CurrentPage = new UninstallConfirmPage { DataContext = this };
        }
        else
        {
            CurrentPage = new WelcomePage { DataContext = this };
        }

        // Burn lifecycle requires Detect to be invoked after BA startup.
        // Without this, the engine sits in an undefined state and Plan/Apply
        // will not produce package events.
        _engine.Detect();
    }

    private string LoadEmbeddedLicense()
    {
        try
        {
            var baDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            var path = Path.Combine(baDir, "LICENSE");
            return File.Exists(path) ? File.ReadAllText(path) : "License text unavailable.";
        }
        catch { return "License text unavailable."; }
    }

    private void DoUninstall()
    {
        CurrentPage = new ProgressPage { DataContext = this };
        StatusText = "Removing GraceKeeper…";
        IsRunning = true;
        _tracker.Begin();
        _engine.Plan(LaunchAction.Uninstall);
    }

    private void DoInstall()
    {
        CurrentPage = new ProgressPage { DataContext = this };
        StatusText = "Preparing…";
        IsRunning = true;
        _tracker.Begin();
        _engine.Plan(LaunchAction.Install);
    }

    private void DoCancel()
    {
        _engine.Quit(1602);  // ERROR_INSTALL_USEREXIT
        RequestClose?.Invoke();
    }

    private void DoViewLog()
    {
        try
        {
            var logVar = _engine.GetVariableString("WixBundleLog");
            if (!string.IsNullOrEmpty(logVar))
                Process.Start("explorer.exe", $"/select,\"{logVar}\"");
        }
        catch { /* best effort */ }
    }

    private void DoFinish()
    {
        if (LaunchOnFinish)
        {
            // The InstallFolder bundle variable is declared in Bundle.wxs with
            // value "[ProgramFiles64Folder]GraceKeeper\". GetVariableString
            // returns that LITERAL string; FormatString is what expands the
            // [X] reference. Call FormatString to get a real path. Fall back
            // to %ProgramFiles%\GraceKeeper if the engine throws or the
            // result still contains unexpanded brackets.
            try
            {
                string installDir;
                try
                {
                    installDir = _engine.FormatString("[InstallFolder]");
                    if (string.IsNullOrWhiteSpace(installDir) || installDir.Contains("["))
                        installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GraceKeeper");
                }
                catch
                {
                    installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GraceKeeper");
                }
                Process.Start(Path.Combine(installDir.TrimEnd('\\'), "GraceKeeper.exe"));
            }
            catch { /* best effort — if it fails, HKLM Run picks it up at next login */ }
        }
        RequestClose?.Invoke();
    }

    private void OnDetectComplete(object? sender, DetectCompleteEventArgs e)
    {
        // Detect is required before Plan/Apply. We don't gate UI on Detect's
        // status in v0.2.0 — the welcome/uninstall pages are already shown and
        // the user-driven Install/Uninstall buttons are the natural advance to
        // the Plan phase.
    }

    private void OnPlanComplete(object? sender, PlanCompleteEventArgs e)
    {
        // PlanComplete -> Apply. Burn rejects IntPtr.Zero as hwndParent
        // (0x80070057 E_INVALIDARG) — supply the main window's HWND so it can
        // parent prompts (UAC, disk-prompt, etc.) to our UI.
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (e.Status >= 0)
            {
                var hwnd = Application.Current.MainWindow != null
                    ? new WindowInteropHelper(Application.Current.MainWindow).Handle
                    : IntPtr.Zero;
                _tracker.Advance();
                _engine.Apply(hwnd);
            }
            else
            {
                StatusText = $"Planning failed with code 0x{e.Status:X8}. The Burn log directory may contain details.";
                IsRunning = false;
                CurrentPage = new ErrorPage { DataContext = this };
            }
        });
    }

    private void OnExecuteMsiMessage(object? sender, ExecuteMsiMessageEventArgs e)
    {
        var msg = e.Message ?? "";
        Application.Current.Dispatcher.Invoke(() => StatusText = msg);
    }

    private void OnProgress(object? sender, ProgressEventArgs e)
    {
        // ProgressEventArgs.OverallPercentage confirmed in 4.0.6 via reflection.
        var p = e.OverallPercentage;
        Application.Current.Dispatcher.Invoke(() =>
        {
            Percent = p;
            if (p >= 100 && _tracker.Phases[1].State == PhaseState.Active)
            {
                _tracker.Advance();  // Installing -> Done, Finishing -> Active
            }
        });
    }

    private void OnApplyComplete(object? sender, ApplyCompleteEventArgs e)
    {
        // ApplyCompleteEventArgs.Status confirmed in 4.0.6 via reflection.
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (e.Status >= 0)
            {
                _tracker.Complete();
                IsRunning = false;
                if (_command.Action == LaunchAction.Uninstall)
                {
                    if (RemoveAllData)
                    {
                        try
                        {
                            var pdRoot = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                            var dir = System.IO.Path.Combine(pdRoot, "GraceKeeper");
                            if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, recursive: true);
                        }
                        catch { /* best effort */ }
                    }
                    CurrentPage = new UninstallFinishPage { DataContext = this };
                }
                else
                {
                    CurrentPage = new FinishPage { DataContext = this };
                }
            }
            else
            {
                StatusText = $"Operation failed with code 0x{e.Status:X8}. The Burn log directory may contain details.";
                IsRunning = false;
                CurrentPage = new ErrorPage { DataContext = this };
            }
        });
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;
    public event EventHandler? CanExecuteChanged;
    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    { _execute = execute; _canExecute = canExecute; }
    public bool CanExecute(object? p) => _canExecute?.Invoke(p) ?? true;
    public void Execute(object? p) => _execute(p);
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
