using Bdgrz.Compliance.Features.Criteria.Services;

namespace Bdgrz.Compliance.Features.Criteria.Contracts;

/// <summary>Qualifies committed causal order between two exact participant source facts.</summary>
/// <remarks>
/// True requires a provider-backed common ordering or a durable, verifiable
/// writer attestation binding both immutable source identities and lineages.
/// Offsets from different Fitz areas are not comparable. Event references,
/// quiet pages and projections are insufficient. No default grants this proof.
/// </remarks>
public interface ICriteriaParticipantCausalOrder
{
    /// <summary>Proves the exact first fact committed before the exact second fact.</summary>
    ValueTask<bool> PrecedesAsync(CriteriaOrderedSourceEvent first, CriteriaOrderedSourceEvent second,
        CancellationToken ct = default);
}
