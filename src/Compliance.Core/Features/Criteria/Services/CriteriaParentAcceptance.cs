using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Identifies one exact, committed parent-incarnation acceptance.</summary>
public sealed record CriteriaParentAcceptance(Uuid EditionId, string Identifier, Uuid IncarnationId,
    string Category, Uuid ProposalEventId, Uuid AcceptanceEventId);
