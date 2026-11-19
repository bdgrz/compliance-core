using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

/// <summary>One platform seed edition's immutable initial source and coverage metadata.</summary>
[Discriminator("bdgrz.criteria.seed-metadata-recorded", 1)]
public sealed record CriteriaSeedMetadataRecorded(Uuid EditionId, Uuid OperationId, string SeedVersion,
    string SeedFingerprint, Uuid EditionRequestEventId, string Framework, DateTimeOffset PublishedAt,
    string CoverageNote, string SourceUrl, string ContentRights, bool NumberedCriteriaComplete,
    ActorAttribution RecordedBy) : DomainEvent;
