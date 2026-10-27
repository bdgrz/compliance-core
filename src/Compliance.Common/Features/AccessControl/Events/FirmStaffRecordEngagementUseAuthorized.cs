using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Holds the exact active registered staff record the partner evaluation relies on; the use does not advance the public revision, so <paramref name="Revision" /> is the held revision.
/// </summary>
[Discriminator("bdgrz.firm-staff-record.firm-staff-record-engagement-use-authorized", 1)]
public sealed record FirmStaffRecordEngagementUseAuthorized(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, ISourceUseFact;
