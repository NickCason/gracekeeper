using System;
using System.Collections.Generic;

namespace GraceKeeper.Core;

public sealed record CleanupResult(
    int RefreshedCount,
    int FreedByBounceCount,
    int DeferredCount,
    IReadOnlyList<string> DeferredFiles,
    string? DeferredReason,
    int StillLockedCount,
    TimeSpan Duration)
{
    public static CleanupResult Empty(TimeSpan duration) =>
        new(0, 0, 0, Array.Empty<string>(), null, 0, duration);
}
