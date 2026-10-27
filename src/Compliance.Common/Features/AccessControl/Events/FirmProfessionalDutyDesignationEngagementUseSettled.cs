using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Exports.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>
///     The use of the duty designation ended after the irreversible personal decision or after cancellation. A settlement before any grant fences the key against late acquisition.
/// </summary>
[Discriminator("bdgrz.firm-professional-duty-designation.firm-professional-duty-designation-engagement-use-settled", 1)]
public sealed record FirmProfessionalDutyDesignationEngagementUseSettled(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, ISourceUseFact;
