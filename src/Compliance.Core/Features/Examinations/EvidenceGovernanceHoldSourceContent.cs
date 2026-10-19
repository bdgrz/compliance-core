using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

sealed record EvidenceGovernanceHoldSourceContent(DateOnly AsOf, Uuid EngagementId,
    Uuid HoldId, IReadOnlyList<EvidenceGovernanceSourceEvent> History);
