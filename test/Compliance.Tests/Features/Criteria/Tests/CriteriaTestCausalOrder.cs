using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Services;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

// Test-only stand-in for an explicitly qualified causal-order provider.
sealed class CriteriaTestCausalOrder : ICriteriaParticipantCausalOrder
{
    internal static CriteriaTestCausalOrder Qualified { get; } = new();
    public ValueTask<bool> PrecedesAsync(CriteriaOrderedSourceEvent first, CriteriaOrderedSourceEvent second,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(true);
    }
}
