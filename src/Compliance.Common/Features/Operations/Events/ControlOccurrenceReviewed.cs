using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Events;

/// <summary>
///     The independent review decision on one exact attestation version. The complete
///     <see cref="ControlOccurrenceReviewView" /> is verified immutable content.
/// </summary>
[Discriminator("bdgrz.control.occurrence.reviewed", 2)]
public sealed record ControlOccurrenceReviewed(Uuid TenantId, Uuid ProgramId, Uuid ControlId, Uuid OccurrenceId,
    long Revision, ImmutableContentRef ReviewContent, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
