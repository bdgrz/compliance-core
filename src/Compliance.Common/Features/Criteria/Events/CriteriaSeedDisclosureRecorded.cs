using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

/// <summary>Records one disclosed coverage limit of one platform seed edition.</summary>
[Discriminator("bdgrz.criteria.seed-disclosure-recorded", 1)]
public sealed record CriteriaSeedDisclosureRecorded(Uuid EditionId, string Category, string Code,
    Uuid OperationId, string SeedVersion, string SeedFingerprint, Uuid EditionRequestEventId,
    string Note, ActorAttribution RecordedBy) : DomainEvent;
