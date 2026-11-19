using Bdgrz.Compliance.Features.Criteria.Services;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

sealed class SequencedCriteriaAuthoringReadiness(params bool[] results) : ICriteriaAuthoringReadiness
{
    int _checks;

    public ValueTask<bool> IsReadyAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var index = Math.Min(_checks++, results.Length - 1);
        return ValueTask.FromResult(index >= 0 && results[index]);
    }
}
