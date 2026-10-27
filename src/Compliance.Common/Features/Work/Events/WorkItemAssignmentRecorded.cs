using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Compact current accountability plus exact original native operation and immutable change/result.</summary>
[Discriminator("bdgrz.work.assigned", 2)]
public sealed record WorkItemAssignmentRecorded(Uuid TenantId, Uuid ProgramId, Uuid WorkItemId,
    long Revision, Uuid? AssigneeMemberId, bool Escalated, ImmutableContentRef Change,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, IWorkAssignmentFact;
