using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Applications.Models;

public sealed record AccessReviewScopeDecisionView(Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, long SystemInstanceRevision, Uuid DecisionId, long Sequence,
    string Decision, string Reason, DateTimeOffset EffectiveFrom, DateTimeOffset? ReviewBy,
    ActorReference ApprovedBy, DateTimeOffset DecidedAt, Uuid? SeparationOfDutiesWaiverId);
