using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Policies.Models;

/// <summary>
///     An immutable approved policy version. The policy versions read retains versions with
///     <c>approved</c>, <c>superseded</c>, and <c>retired</c> status. Its half-open effective
///     interval is closed only by a successor or retirement.
/// </summary>
public sealed record PolicyVersionView(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    string Identifier, long Version, long Revision, bool Major, string Status,
    PolicyContent Content, string ContentSha256, DateOnly EffectiveFrom,
    DateOnly? EffectiveUntil, long? PredecessorVersion, Uuid ApprovalDecisionId,
    Uuid AcceptedReviewDecisionId, ActorReference ApprovedBy, DateTimeOffset ApprovedAt,
    string? ImpactDigest, Uuid? SeparationOfDutiesWaiverId);
