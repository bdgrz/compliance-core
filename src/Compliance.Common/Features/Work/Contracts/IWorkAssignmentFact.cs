using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Work.Contracts;

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
