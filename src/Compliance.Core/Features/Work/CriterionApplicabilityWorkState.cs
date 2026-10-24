using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>
///     The projected state of one applicability line: <paramref name="LineId" /> is the identity of its owner stream
///     and <paramref name="DecisionId" /> the id of its latest proposal, which is the pending one while the state is
///     pending.
/// </summary>
public sealed record CriterionApplicabilityWorkState(Uuid TenantId, Uuid ProgramId, Uuid LineId, Uuid DecisionId,
    Uuid EditionId, string CriterionIdentifier, long Revision, int VersionNumber, Uuid ProposerMemberId,
    DateTimeOffset ProposedAt, string State);
