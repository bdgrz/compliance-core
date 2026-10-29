using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
namespace Bdgrz.Compliance.Features.AccessReviews.Models;

/// <summary>
///     One versioned expectation. <c>Status</c> is <c>proposed</c> or <c>approved</c>; an approved
///     successor ends its predecessor at the successor's effective date. <c>Revision</c> is the logical revision of
///     the expectation line holding it: the value to send as <c>ExpectedRevision</c> on the next change to the line.
/// </summary>
public sealed record AccessExpectationView(Uuid ExpectationId, Uuid SystemInstanceId,
    Uuid? SupersedesExpectationId, string RuleKind, AccessExpectationParameters Parameters,
    string Rationale, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveUntil,
    string Status, ActorReference ProposedBy, DateTimeOffset ProposedAt,
    ActorReference? ApprovedBy, DateTimeOffset? ApprovedAt,
    Uuid? SeparationOfDutiesWaiverId, long Revision);
