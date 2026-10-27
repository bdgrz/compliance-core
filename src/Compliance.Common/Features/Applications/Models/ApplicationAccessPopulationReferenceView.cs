using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Minimal access-population history metadata for one reviewed system.</summary>
public sealed record ApplicationAccessPopulationReferenceView(Uuid TenantId,
    Uuid ApplicationId, Uuid SystemInstanceId, Uuid PopulationId,
    long SystemInstanceRevision, DateTimeOffset ObservedAt, long Revision, string Status,
    Uuid? SnapshotId, DateTimeOffset? AcceptedAt);
