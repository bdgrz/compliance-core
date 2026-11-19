using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Qualifies and conditionally commits an exact contested-label refusal.</summary>
/// <remarks>
/// Implementations must obtain a fresh authoritative claim head, execute
/// <see cref="Aggregates.EditionLabelClaim.RefuseContested"/> against that exact head, and durably
/// resolve the conditional append before returning success. A stale or unavailable head, an
/// unknown append, or an unqualified provider must return transient pending. A prior exact
/// committed refusal can be replayed; an unobserved refusal is never a negative decision.
/// No implementation is registered until the underlying read, CAS and unknown-commit behavior
/// have been qualified. For an existing selected refusal, the committer must replay its
/// original RequestedGeneration; it must never rebase an exact retry to the current label generation.
/// </remarks>
public interface ICriteriaLabelRefusalCommitter
{
    /// <summary>Returns success only after the exact claim-owned refusal is committed or replayed.</summary>
    ValueTask<Result> RefuseIfContestedAsync(string label, Uuid editionId, Uuid operationId,
        Uuid editionRequestEventId, ActorAttribution actor, IExecutionContext context, CancellationToken ct = default);
}
