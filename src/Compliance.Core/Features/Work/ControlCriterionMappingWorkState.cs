using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>
///     The projected state of one mapping line: <paramref name="LineId" /> is the identity of its owner stream and
///     <paramref name="PendingMappingId" /> the id of the proposal awaiting review, if any.
/// </summary>
public sealed record ControlCriterionMappingWorkState(Uuid TenantId, Uuid ProgramId, Uuid LineId, Uuid ControlId,
    Uuid ControlVersionId, Uuid EditionId, string CriterionIdentifier, string CriterionKind, long Revision,
    int LastVersionNumber, int? ActiveVersionNumber, int? PendingVersionNumber, Uuid? PendingMappingId,
    Uuid? ProposerMemberId, DateTimeOffset? ProposedAt, string State);
