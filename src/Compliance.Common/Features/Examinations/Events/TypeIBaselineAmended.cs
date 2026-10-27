using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Events;

/// <summary>One exact attributed native engagement transition; complete business bytes stay immutable.</summary>
[Discriminator("bdgrz.examinations.type-i-baseline-amended", 2)]
public sealed record TypeIBaselineAmended(Uuid TenantId, Uuid SubjectId, Uuid ProgramId, long Revision,
    string EngagementType, Uuid BoundaryId, Uuid? BaselineId, long BaselineRevision, ImmutableContentRef Change,
    DateTimeOffset OccurredAt, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult, Uuid ActorMemberId) : DomainEvent, IExaminationEngagementFact;
