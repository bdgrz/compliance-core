using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Evidence.Models;

public sealed record EvidenceRedactionApprovalFact(Uuid ApprovalId, long Revision,
    Uuid PreparationId, long PreparedRevision, ulong DerivedSourcePosition, DateTimeOffset DerivedAvailableAt,
    ActorReference ApprovedBy, DateTimeOffset ApprovedAt, EvidenceRedactionWaiverSnapshot? SeparationOfDutiesWaiver = null);
