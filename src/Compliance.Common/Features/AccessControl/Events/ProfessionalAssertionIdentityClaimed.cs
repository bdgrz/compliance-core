using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>
///     The permanent claim of one signed assertion identity by one professional-duty change. The complete verified
///     tuple is the frozen intent the fact points to; the claim is never released.
/// </summary>
[Discriminator("bdgrz.professional-assertion-identity-claim.professional-assertion-identity-claimed", 1)]
public sealed record ProfessionalAssertionIdentityClaimed(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
