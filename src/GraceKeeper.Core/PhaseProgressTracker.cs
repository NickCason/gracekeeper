using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GraceKeeper.Core;

public enum PhaseState
{
    Pending,
    Active,
    Done,
}

public sealed class PhaseRow : INotifyPropertyChanged
{
    private string _title;
    private PhaseState _state;

    public PhaseRow(string title)
    {
        _title = title;
    }

    public string Title
    {
        get => _title;
        set { if (_title == value) return; _title = value; OnPropertyChanged(); }
    }

    public PhaseState State
    {
        get => _state;
        set { if (_state == value) return; _state = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class PhaseProgressTracker
{
    private int _activeIndex = -1;

    public PhaseProgressTracker(IEnumerable<string> titles)
    {
        Phases = new ObservableCollection<PhaseRow>();
        foreach (var t in titles) Phases.Add(new PhaseRow(t));
    }

    public ObservableCollection<PhaseRow> Phases { get; }

    public void Begin()
    {
        if (Phases.Count == 0) return;
        _activeIndex = 0;
        Phases[0].State = PhaseState.Active;
    }

    public void Advance()
    {
        if (_activeIndex < 0 || _activeIndex >= Phases.Count) return;
        Phases[_activeIndex].State = PhaseState.Done;
        _activeIndex++;
        if (_activeIndex < Phases.Count)
            Phases[_activeIndex].State = PhaseState.Active;
    }

    public void Complete()
    {
        foreach (var p in Phases) p.State = PhaseState.Done;
        _activeIndex = Phases.Count;
    }
}
