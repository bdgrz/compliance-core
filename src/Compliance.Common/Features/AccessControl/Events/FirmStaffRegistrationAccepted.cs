using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The root native acceptance of a staff registration. Registration is not complete until the canonical user is
///     claimed and <see cref="FirmStaffChangeRecorded" /> records the completion.
/// </summary>
[Discriminator("bdgrz.firm-staff-record.firm-staff-registration-accepted", 1)]
public sealed record FirmStaffRegistrationAccepted(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
