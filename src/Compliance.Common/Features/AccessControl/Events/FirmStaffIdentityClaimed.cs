using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>
///     The permanent claim of one canonical user by one staff record. The claiming staff member is the root
///     operation id of <see cref="Operation" />; the claim is never released.
/// </summary>
[Discriminator("bdgrz.firm-staff-identity-claim.firm-staff-identity-claimed", 1)]
public sealed record FirmStaffIdentityClaimed(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
