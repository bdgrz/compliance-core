using Cntryl.Portia;
using Bdgrz.Compliance.Features.Policies.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Policies.Models;

public sealed record PolicyPeriodicReviewConfirmedContent(Uuid TenantId, Uuid ProgramId,
    Uuid PolicyId, long Version, Uuid DecisionId, DateOnly ReviewedOn, string Rationale,
    ActorReference Actor, Uuid ActorMemberId, DateTimeOffset DecidedAt) : IPolicyMutationContent;
