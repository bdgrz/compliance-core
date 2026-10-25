using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Persists a review outcome and comments for one exact description version.</summary>
public sealed record SystemDescriptionReviewedBody(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid SystemDescriptionId,
    long AggregateRevision,
    SystemDescriptionReviewView Review,
    DateTimeOffset OccurredAt);
