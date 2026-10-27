using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>A native accountability change; full original response/history lives in verified immutable content.</summary>
public interface IWorkAssignmentFact : INativeOperationFact
{
    Uuid TenantId { get; }
    Uuid ProgramId { get; }
    Uuid WorkItemId { get; }
    long Revision { get; }
    Uuid? AssigneeMemberId { get; }
    bool Escalated { get; }
    ImmutableContentRef Change { get; }
    ImmutableContentRef? OperationResult { get; }
}
