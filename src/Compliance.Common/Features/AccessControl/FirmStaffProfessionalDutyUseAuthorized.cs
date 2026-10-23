using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     An exact currently active canonical staff record is held for recording a professional-duty designation. The
///     use does not advance the public staff content revision; <paramref name="Revision" /> is the held revision.
/// </summary>
[Discriminator("bdgrz.firm-staff-record.firm-staff-professional-duty-use-authorized", 1)]
public sealed record FirmStaffProfessionalDutyUseAuthorized(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, ISourceUseFact;
