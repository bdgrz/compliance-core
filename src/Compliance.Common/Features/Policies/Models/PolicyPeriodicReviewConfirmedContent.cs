using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies.Models;

public sealed record PolicyPeriodicReviewConfirmedContent(Uuid TenantId, Uuid ProgramId,
    Uuid PolicyId, long Version, Uuid DecisionId, DateOnly ReviewedOn, string Rationale,
    ActorReference Actor, Uuid ActorMemberId, DateTimeOffset DecidedAt) : IPolicyMutationContent;
