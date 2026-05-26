using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GraceKeeper.Core;

public sealed record RetryOutcome(int FreedCount, IReadOnlyList<string> StillLocked);

public sealed record BounceResult(int FreedByBounceCount, IReadOnlyList<string> StillLocked);

public interface IServiceBouncer
{
    Task<BounceResult> BounceAndRetryAsync(
        Func<Task<RetryOutcome>> retryDelete, CancellationToken ct);
}

public sealed class ServiceBouncer : IServiceBouncer
{
    private readonly IServiceController _svc;
    private readonly IReadOnlyList<string> _orderedServices;  // stop order; start = reverse
    private readonly TimeSpan _perServiceTimeout;

    public ServiceBouncer(IServiceController svc, IReadOnlyList<string> orderedServices, TimeSpan perServiceTimeout)
    {
        _svc = svc;
        _orderedServices = orderedServices;
        _perServiceTimeout = perServiceTimeout;
    }

    public async Task<BounceResult> BounceAndRetryAsync(Func<Task<RetryOutcome>> retryDelete, CancellationToken ct)
    {
        foreach (var name in _orderedServices)
        {
            ct.ThrowIfCancellationRequested();
            if (!_svc.Exists(name)) continue;
            await _svc.StopAsync(name, _perServiceTimeout, ct);
        }

        var outcome = await retryDelete();

        for (int i = _orderedServices.Count - 1; i >= 0; i--)
        {
            ct.ThrowIfCancellationRequested();
            var name = _orderedServices[i];
            if (!_svc.Exists(name)) continue;
            await _svc.StartAsync(name, _perServiceTimeout, ct);
        }

        return new BounceResult(outcome.FreedCount, outcome.StillLocked);
    }
}
