using System;
using System.Threading;
using GraceKeeper.Core;
using Xunit;

namespace GraceKeeper.Core.Tests;

public class SingleInstanceTests
{
    private static string UniqueName() => $"SingleInstanceTests-{Guid.NewGuid():N}";

    [Fact]
    public void FirstInstance_IsFirst()
    {
        var name = UniqueName();
        using var first = new SingleInstance(name);
        Assert.True(first.IsFirstInstance);
    }

    [Fact]
    public void SecondInstance_IsNotFirst_WhileFirstAlive()
    {
        var name = UniqueName();
        using var first = new SingleInstance(name);
        using var second = new SingleInstance(name);
        Assert.True(first.IsFirstInstance);
        Assert.False(second.IsFirstInstance);
    }

    [Fact]
    public void SignalExistingInstance_RaisesActivationRequestedOnFirst()
    {
        var name = UniqueName();
        using var first = new SingleInstance(name);
        using var signaled = new ManualResetEventSlim(false);
        first.ActivationRequested += (_, _) => signaled.Set();

        using (var second = new SingleInstance(name))
        {
            Assert.False(second.IsFirstInstance);
            second.SignalExistingInstance();
        }

        Assert.True(signaled.Wait(TimeSpan.FromSeconds(2)),
            "First instance should receive activation signal from second.");
    }

    [Fact]
    public void AfterFirstDisposed_NewInstanceIsFirst()
    {
        var name = UniqueName();
        using (var first = new SingleInstance(name))
        {
            Assert.True(first.IsFirstInstance);
        }

        using var revived = new SingleInstance(name);
        Assert.True(revived.IsFirstInstance);
    }

    [Fact]
    public void DisposingSecond_DoesNotReleaseFirstsOwnership()
    {
        var name = UniqueName();
        using var first = new SingleInstance(name);

        using (var second = new SingleInstance(name))
        {
            Assert.False(second.IsFirstInstance);
        }

        // A new third instance should still see the first as the owner.
        using var third = new SingleInstance(name);
        Assert.False(third.IsFirstInstance);
    }

    [Fact]
    public void MultipleSignals_AllReceived()
    {
        var name = UniqueName();
        using var first = new SingleInstance(name);
        var count = 0;
        using var allDone = new CountdownEvent(3);
        first.ActivationRequested += (_, _) =>
        {
            Interlocked.Increment(ref count);
            allDone.Signal();
        };

        for (var i = 0; i < 3; i++)
        {
            using var s = new SingleInstance(name);
            s.SignalExistingInstance();
            // Allow listener thread to consume the auto-reset event before next signal.
            Thread.Sleep(50);
        }

        Assert.True(allDone.Wait(TimeSpan.FromSeconds(3)),
            $"Expected 3 activations, got {count}.");
    }
}
