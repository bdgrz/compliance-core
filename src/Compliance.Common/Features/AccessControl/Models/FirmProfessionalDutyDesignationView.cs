using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>
///     The complete designation after one change. <paramref name="ContinuityStartRevision" /> is the owner revision at
///     which the current uninterrupted active authority began; it is zero once revoked.
/// </summary>
public sealed record FirmProfessionalDutyDesignationView(Uuid DesignationId, Uuid StaffMemberId, Uuid UserId,
    string Duty, Uuid? TenantId, string SourceReference, long DirectoryStaffRevision, bool IsActive,
    long Revision, ActorReference DesignatedBy, DateTimeOffset DesignatedAt,
    ActorReference LastChangedBy, DateTimeOffset LastChangedAt, string? ChangeReason,
    string AuthorityIssuer = "", Uuid AssertionId = default, long AuthorityRevision = 0,
    DateTimeOffset AssertionIssuedAt = default, DateTimeOffset NotBefore = default,
    DateTimeOffset ExpiresAt = default, string EvidenceDigest = "", string AuthorityStatus = "",
    Uuid? SupersedesAssertionId = null, long ContinuityStartRevision = 0);
