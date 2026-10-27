using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Models;

public sealed record ApplicationImportRetirementRow(Uuid SourceClaimId, string SourceRecordId,
    Uuid ApplicationId, Uuid LastObservedBatchId, long ExpectedApplicationRevision,
    DateTimeOffset ObservationCommittedAt);
