using Cntryl.Portia;
using Bdgrz.Compliance.Features.Examinations.History;
namespace Bdgrz.Compliance.Features.Examinations.Models;

sealed record EvidenceGovernanceHoldSourceContent(DateOnly AsOf, Uuid EngagementId,
    Uuid HoldId, IReadOnlyList<EvidenceGovernanceSourceEvent> History);
