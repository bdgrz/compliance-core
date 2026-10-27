using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>Bounded current period authority; source bodies, populations and decision history remain immutable content.</summary>
public sealed record TypeIIPeriodAuthorityHeader(
    Uuid TenantId, Uuid ProgramId, Uuid PeriodId, long Revision, string Status,
    Uuid ExaminationId, Uuid TypeIIPlanId, int TypeIIPlanVersion, string TypeIIPlanContentSha256,
    DateOnly PeriodStart, DateOnly PeriodEnd, Uuid OpeningSnapshotId, string OpeningSnapshotSha256,
    int ExpectedOccurrenceCount, string ExpectedOccurrencesSha256,
    string? ValidationSha256, string? ValidationStatus, Uuid? ValidatorMemberId, DateTimeOffset? ValidatedAt,
    string? ApprovalSha256, Uuid? ApproverMemberId, string? ApproverRole, DateTimeOffset? ApprovedAt,
    int CloseSnapshotCount, Uuid? LatestCloseSnapshotId, int? LatestCloseVersion, string? LatestCloseSha256,
    Uuid? CloseActorMemberId, string? CloseActorRole, long? CloseOriginalExpectedRevision,
    Uuid ActorMemberId, DateTimeOffset UpdatedAt);
