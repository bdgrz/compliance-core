using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Examinations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Examinations.Events;

/// <summary>One exact attributed native engagement transition; complete business bytes stay immutable.</summary>
[Discriminator("bdgrz.examinations.type-i-baseline-frozen", 2)]
public sealed record TypeIBaselineFrozen(Uuid TenantId, Uuid SubjectId, Uuid ProgramId, long Revision,
    string EngagementType, Uuid BoundaryId, Uuid? BaselineId, long BaselineRevision, ImmutableContentRef Change,
    DateTimeOffset OccurredAt, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult, Uuid ActorMemberId) : DomainEvent, IExaminationEngagementFact;
