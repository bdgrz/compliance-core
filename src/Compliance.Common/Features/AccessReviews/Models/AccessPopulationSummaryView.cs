using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Models;

/// <summary>A population's identity and status, without its facts.</summary>
public sealed record AccessPopulationSummaryView(Uuid TenantId, Uuid PopulationId,
    Uuid ApplicationId, Uuid SystemInstanceId, long SystemInstanceRevision,
    DateTimeOffset ObservedAt, long Revision, string Status, Uuid? SnapshotId,
    string? ContentSha256, DateTimeOffset? AcceptedAt);
