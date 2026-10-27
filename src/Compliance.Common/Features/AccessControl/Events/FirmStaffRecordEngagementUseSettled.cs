using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The use of the staff record ended after the irreversible personal decision or after cancellation. A settlement before any grant fences the key against late acquisition.
/// </summary>
[Discriminator("bdgrz.firm-staff-record.firm-staff-record-engagement-use-settled", 1)]
public sealed record FirmStaffRecordEngagementUseSettled(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, ISourceUseFact;
