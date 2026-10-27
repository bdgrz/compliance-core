using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work.Events;

/// <summary>Compact current accountability plus exact original native operation and immutable change/result.</summary>
[Discriminator("bdgrz.work.escalated", 2)]
public sealed record WorkItemAssignmentEscalated(Uuid TenantId, Uuid ProgramId, Uuid WorkItemId,
    long Revision, Uuid? AssigneeMemberId, bool Escalated, ImmutableContentRef Change,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, IWorkAssignmentFact;
