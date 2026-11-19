using Bdgrz.Compliance.Features.Criteria.Services;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

// Test-only stand-in; no production provider grants readiness by registration alone.
sealed class TestCriteriaAuthoringReadiness(bool ready) : ICriteriaAuthoringReadiness
{
    internal static TestCriteriaAuthoringReadiness Ready { get; } = new(true);

    public ValueTask<bool> IsReadyAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ready);
    }
}
