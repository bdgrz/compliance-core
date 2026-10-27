using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Exports.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>
///     The root native acceptance of a professional-duty change. The designation changes only when the process claims
///     the signed assertion and <see cref="FirmProfessionalDutyDesignationRecorded" /> or
///     <see cref="FirmProfessionalDutyDesignationRevoked" /> follows.
/// </summary>
[Discriminator("bdgrz.firm-professional-duty-designation.professional-duty-change-accepted", 1)]
public sealed record ProfessionalDutyChangeAccepted(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
