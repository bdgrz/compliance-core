using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Boundaries.Events;
using Bdgrz.Compliance.Features.Changes.Events;
using Bdgrz.Compliance.Features.Incidents.Events;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Versioning.Services;

namespace Bdgrz.Compliance.Features.Risks.Handlers;

/// <summary>Records a source once; a current Lead triages it into only the affected authoritative risks.</summary>
public sealed class ProgramRiskTriggerHandler(IAggregateReader reader, IAggregateExecutor executor,
    IAccessGrantRolePermissionAuthorizer roles, ISourceFactResolver sources, TimeProvider clock)
    : IRequestHandler<RaiseRiskReassessmentTrigger>, IRequestHandler<TriageRiskReassessmentTrigger>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RaiseRiskReassessmentTrigger> context, CancellationToken ct)
    {
        var request = context.Request;
        var manual = request.Kind == "manual";
        if (manual ? !await IsCurrentLeadAsync(context, request.TenantId, request.ProgramId, ct).ConfigureAwait(false)
                : !RequestActor.IsSystem(context.Actor))
            return Forbidden();
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.TriggerId == Uuid.Empty)
            return Invalid("A trigger requires its tenant, program and identity.");
        var owner = await reader.HydrateAsync(new RiskReassessmentTrigger(request.TenantId, request.TriggerId), ct).ConfigureAwait(false);
        var actor = manual ? MemberActor(context, request.TenantId) :
            ActorReference.ForSystemProcess("reactor:" + RiskReassessmentTriageReactorName, RiskReassessmentTriageReactorName);
        if (owner.Raised is null)
        {
            var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId, request.ProgramId), ct).ConfigureAwait(false);
            if (!program.IsCreated)
                return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The program was not found."));
            if (!manual)
            {
                if (request.Source is not { } proof || proof.Source.Realm != request.TenantId.ToString())
                    return Invalid("An automatic trigger requires its tenant's exact committed source.");
                try
                {
                    var resolved = await sources.ResolveAsync(proof.Source, proof.ResourcePosition, ct).ConfigureAwait(false);
                    if (resolved.Locator != proof || !MatchesSource(resolved.Event, request))
                        return Invalid("The trigger differs from its exact committed source and program.");
                }
                catch (SourceFactVerificationException error)
                {
                    return Result.Failure(new RequestError(RequestErrorKind.Conflict, error.Message, true));
                }
            }
        }
        var at = clock.GetUtcNow();
        return await executor.ExecuteAsync(new RiskReassessmentTrigger(request.TenantId, request.TriggerId),
            target => CommandFailureRequestAdapter.ToOutcome(target.Raise(request, actor, at)), context, ct).ConfigureAwait(false);
    }

    public async ValueTask<Result> HandleAsync(IRequestContext<TriageRiskReassessmentTrigger> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!await IsCurrentLeadAsync(context, request.TenantId, request.ProgramId, ct).ConfigureAwait(false))
            return Forbidden();
        var owner = await reader.HydrateAsync(new RiskReassessmentTrigger(request.TenantId, request.TriggerId), ct).ConfigureAwait(false);
        if (owner.Raised?.ProgramId != request.ProgramId)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The trigger was not found in this program."));
        if (owner.Triaged is null)
        {
            if (request.AffectedRiskIds is not { Count: <= RiskReassessmentTrigger.MaximumAffectedRisks })
                return Invalid("Triage requires a bounded risk selection.");
            foreach (var risk in request.AffectedRiskIds)
            {
                var exists = await RiskOwnerResolution.RequireRiskAsync(reader, request.TenantId, request.ProgramId, risk, ct).ConfigureAwait(false);
                if (!exists.IsSuccess)
                    return exists;
            }
        }
        var actor = MemberActor(context, request.TenantId);
        var at = clock.GetUtcNow();
        return await executor.ExecuteAsync(new RiskReassessmentTrigger(request.TenantId, request.TriggerId),
            target => CommandFailureRequestAdapter.ToOutcome(target.Triage(request, actor, at)), context, ct).ConfigureAwait(false);
    }

    public const string RiskReassessmentTriageReactorName = "RiskReassessmentTriageV1";

    async ValueTask<bool> IsCurrentLeadAsync(IRequestContext context, Uuid tenant, Uuid program, CancellationToken ct)
    {
        if (RequestActor.IsSystem(context.Actor) || !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user))
            return false;
        var memberId = RbacIds.Member(tenant, user);
        var member = await reader.HydrateAsync(Member.ForVerification(tenant, memberId), ct).ConfigureAwait(false);
        return member.UserId == user && member.Affiliation == "client_personnel" &&
            member.IsAccessEligibleOn(DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)) &&
            await roles.IsAllowedAsRoleAsync(tenant, user, memberId, program,
                BuiltInRbac.ComplianceManagementRoleId(tenant), "program.manage", ct).ConfigureAwait(false);
    }

    static bool MatchesSource(DomainEvent fact, RaiseRiskReassessmentTrigger request) => fact switch
    {
        BoundaryApproved boundary => request.Kind == "boundary_change" && boundary.TenantId == request.TenantId &&
            boundary.Header.ProgramId == request.ProgramId,
        SignificantChangeImpactAssessed change => request.Kind == "significant_change" && change.TenantId == request.TenantId &&
            change.Header.ProgramId == request.ProgramId && change.Header.HandlingClass == "significant",
        ComplianceIncidentImpactAssessed incident => request.Kind == "incident" && incident.TenantId == request.TenantId &&
            incident.Header.ProgramId == request.ProgramId && incident.Header.HandlingClass == "significant",
        _ => false,
    };

    static ActorReference MemberActor(IRequestContext context, Uuid tenant)
    {
        _ = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user);
        return ActorReference.ForMember(RbacIds.Member(tenant, user), UserIdentityClaims.BdgrzDisplay(context.Actor, user));
    }

    static Result Forbidden() => Result.Failure(new RequestError(RequestErrorKind.Forbidden, "A current Compliance Lead or exact source reactor is required."));
    static Result Invalid(string message) => Result.Failure(new RequestError(RequestErrorKind.Validation, message));
}
