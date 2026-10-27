using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>
///     An exact currently active canonical staff record is held for recording a professional-duty designation. The
///     use does not advance the public staff content revision; <paramref name="Revision" /> is the held revision.
/// </summary>
[Discriminator("bdgrz.firm-staff-record.firm-staff-professional-duty-use-authorized", 1)]
public sealed record FirmStaffProfessionalDutyUseAuthorized(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, ISourceUseFact;
