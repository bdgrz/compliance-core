using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Persists a review outcome and comments for one exact description version.</summary>
[Discriminator("bdgrz.systemdescription.system-description-reviewed", 1)]
public sealed record SystemDescriptionReviewed(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid SystemDescriptionId,
    long AggregateRevision,
    SystemDescriptionReviewView Review,
    DateTimeOffset OccurredAt) : DomainEvent;
