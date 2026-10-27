using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.Operations.Events;

/// <summary>Records the ManagementReviewActionCompleted domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.operations.management-review-action-completed", 1)]
public sealed record ManagementReviewActionCompleted(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
