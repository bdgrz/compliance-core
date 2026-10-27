using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Events;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Events;

/// <summary>The operational process record accepted a complete change: its normalized content and claim keys are frozen in the intent and the name-change process continues from this fact.</summary>
[Discriminator("bdgrz.operational-process.operational-process-change-accepted", 1)]
public sealed record OperationalProcessChangeAccepted(EventStreamAddress Owner, long Revision, long Generation, ImmutableContentRef Intent,
    ImmutableContentRef? SourceProofs, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INamedRecordFact;
