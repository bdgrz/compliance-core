using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription.Models;

/// <summary>Bounded current description authority; narrative arrays and history are verified immutable content.</summary>
public sealed record SystemDescriptionAuthorityHeader(Uuid TenantId, Uuid ProgramId, Uuid SystemDescriptionId,
    long Revision, long CurrentVersion, Uuid CurrentVersionId, Uuid? ParentVersionId, string Status, string Title,
    string ContentSha256, SystemDescriptionScope Scope, Uuid AuthorMemberId, DateTimeOffset VersionUpdatedAt,
    Uuid? LatestReviewId, Uuid? LatestReviewerMemberId, string? LatestReviewOutcome,
    long ReviewCount, long ApprovalCount, long NoChangeDecisionCount, string NoChangeDecisionsSha256,
    long BindingCount, string BindingsSha256, Uuid? LatestApprovedVersionId,
    string VersionsSha256, string ReviewsSha256, string ApprovalsSha256);
