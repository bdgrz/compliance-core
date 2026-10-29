namespace Bdgrz.Compliance.Features.AccessControl.Models;

public sealed record EngagementAcceptanceSourceTimes(DateTimeOffset PartnerDirectoryRecordedAt,
    DateTimeOffset PartnerAuthorityVerifiedAt, DateTimeOffset? BoundaryChangedAt, DateTimeOffset? BoundaryApprovedAt);
