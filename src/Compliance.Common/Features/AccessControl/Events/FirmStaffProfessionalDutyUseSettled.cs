using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The staff use for one professional-duty change ended, after the recorded designation or after the rejection
///     fence. A settlement before any grant fences the key against late acquisition.
/// </summary>
[Discriminator("bdgrz.firm-staff-record.firm-staff-professional-duty-use-settled", 1)]
public sealed record FirmStaffProfessionalDutyUseSettled(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, ISourceUseFact;
