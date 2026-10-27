using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Examinations.Contracts;

/// <summary>The exact bounded native identity shared by the four engagement transition kinds.</summary>
public interface IExaminationEngagementFact : INativeOperationFact
{
    Uuid TenantId { get; }
    Uuid SubjectId { get; }
    Uuid ProgramId { get; }
    long Revision { get; }
    string EngagementType { get; }
    Uuid BoundaryId { get; }
    Uuid? BaselineId { get; }
    long BaselineRevision { get; }
    ImmutableContentRef Change { get; }
    DateTimeOffset OccurredAt { get; }
    ImmutableContentRef? OperationResult { get; }
    Uuid ActorMemberId { get; }
}
