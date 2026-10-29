using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Events;

/// <summary>The name claim is held by the operational process record and change generation named by this fact.</summary>
[Discriminator("bdgrz.operational-process-name-claim.operational-process-name-claimed", 1)]
public sealed record OperationalProcessNameClaimed(EventStreamAddress Owner, long Revision, long Generation, ImmutableContentRef Intent,
    ImmutableContentRef? SourceProofs, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INamedRecordFact;
