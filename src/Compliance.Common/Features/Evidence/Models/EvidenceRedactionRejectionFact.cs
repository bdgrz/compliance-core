using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;

namespace Bdgrz.Compliance.Features.Evidence.Models;

public sealed record EvidenceRedactionRejectionFact(Uuid ReviewId, long Revision, Uuid PreparationId,
    long PreparedRevision, ulong DerivedSourcePosition, DateTimeOffset DerivedAvailableAt,
    ActorReference ReviewedBy, DateTimeOffset ReviewedAt, string Comments,
    EvidenceRedactionWaiverSnapshot? SeparationOfDutiesWaiver = null);
