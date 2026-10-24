using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>The operational process record accepted a complete change: its normalized content and claim keys are frozen in the intent and the name-change process continues from this fact.</summary>
[Discriminator("bdgrz.operational-process.operational-process-change-accepted", 1)]
public sealed record OperationalProcessChangeAccepted(EventStreamAddress Owner, long Revision, long Generation, ImmutableContentRef Intent,
    ImmutableContentRef? SourceProofs, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INamedRecordFact;
