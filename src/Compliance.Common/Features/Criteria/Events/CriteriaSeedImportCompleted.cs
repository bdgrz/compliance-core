using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

/// <summary>Records completion of the individually verified fixed seed operations.</summary>
[Discriminator("bdgrz.criteria.seed-import-completed", 1)]
public sealed record CriteriaSeedImportCompleted(Uuid EditionId, Uuid OperationId, Uuid EditionRequestEventId,
    string SeedVersion, string SeedFingerprint, Uuid PublicationEventId, ActorAttribution RecordedBy) : DomainEvent;
