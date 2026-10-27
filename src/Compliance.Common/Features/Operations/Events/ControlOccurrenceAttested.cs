using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Operations.Events;

/// <summary>
///     One attestation version of an occurrence. The complete <see cref="ControlAttestationView" /> is verified
///     immutable content, so a large evidence list is never an event-size rejection.
/// </summary>
[Discriminator("bdgrz.control.occurrence.attested", 2)]
public sealed record ControlOccurrenceAttested(Uuid TenantId, Uuid ProgramId, Uuid ControlId, Uuid OccurrenceId,
    long Revision, ImmutableContentRef AttestationContent, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
