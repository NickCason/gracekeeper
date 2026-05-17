using System.Linq;
using GraceKeeper.Core;
using Xunit;

namespace GraceKeeper.Core.Tests;

public class PhaseProgressTrackerTests
{
    [Fact]
    public void AllPhasesStartPending()
    {
        var t = new PhaseProgressTracker(new[] { "A", "B", "C" });
        Assert.All(t.Phases, p => Assert.Equal(PhaseState.Pending, p.State));
    }

    [Fact]
    public void BeginActivatesFirstPhase()
    {
        var t = new PhaseProgressTracker(new[] { "A", "B", "C" });
        t.Begin();
        Assert.Equal(PhaseState.Active, t.Phases[0].State);
        Assert.Equal(PhaseState.Pending, t.Phases[1].State);
        Assert.Equal(PhaseState.Pending, t.Phases[2].State);
    }

    [Fact]
    public void AdvanceMovesToNextPhase()
    {
        var t = new PhaseProgressTracker(new[] { "A", "B", "C" });
        t.Begin();
        t.Advance();
        Assert.Equal(PhaseState.Done, t.Phases[0].State);
        Assert.Equal(PhaseState.Active, t.Phases[1].State);
        Assert.Equal(PhaseState.Pending, t.Phases[2].State);
    }

    [Fact]
    public void CompleteMarksFinalAsDone()
    {
        var t = new PhaseProgressTracker(new[] { "A", "B", "C" });
        t.Begin();
        t.Advance();
        t.Advance();
        t.Complete();
        Assert.All(t.Phases, p => Assert.Equal(PhaseState.Done, p.State));
    }

    [Fact]
    public void AdvancePastEndIsSafeNoOp()
    {
        var t = new PhaseProgressTracker(new[] { "A" });
        t.Begin();
        t.Advance();   // completes A
        t.Advance();   // no-op
        Assert.Equal(PhaseState.Done, t.Phases[0].State);
    }

    [Fact]
    public void PhasesAreObservableForUiBinding()
    {
        var t = new PhaseProgressTracker(new[] { "A", "B" });
        Assert.NotNull(t.Phases as System.Collections.Specialized.INotifyCollectionChanged);
    }

    [Fact]
    public void PhaseRowRaisesPropertyChangedOnStateChange()
    {
        var t = new PhaseProgressTracker(new[] { "A" });
        var row = t.Phases[0];
        var changes = 0;
        row.PropertyChanged += (_, _) => changes++;
        t.Begin();
        Assert.True(changes > 0);
    }
}
