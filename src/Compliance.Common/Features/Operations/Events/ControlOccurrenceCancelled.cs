using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Events;

/// <summary>An open occurrence was cancelled; prior proof and attribution remain immutable.</summary>
[Discriminator("bdgrz.control.occurrence.cancelled", 1)]
public sealed record ControlOccurrenceCancelled(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid OccurrenceId, long Revision, string Reason, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
