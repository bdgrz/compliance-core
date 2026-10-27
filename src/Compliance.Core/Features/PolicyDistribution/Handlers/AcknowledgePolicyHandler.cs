using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Policies.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Policies;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Handlers;

/// <summary>
///     Admits a personal acknowledgement on the campaign. A workforce person correlated to the acting member
///     acknowledges for themselves; a person without a membership may have it recorded by a program manager,
///     attributed separately as performer and recorder. HTTP-only. The campaign admits the complete result at the
///     expected campaign revision; the participant applies it afterwards.
/// </summary>
sealed class AcknowledgePolicyHandler(CampaignSource campaigns, IAccessGrantPermissionAuthorizer permissions,
    TimeProvider clock, PolicyAcknowledgementRecorderGuard recorderGuard, PolicyParticipantAdmission admission)
    : IRequestHandler<AcknowledgePolicy, OperationAcceptedResponse>
{
    public ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<AcknowledgePolicy> context, CancellationToken ct) =>
        PolicyCampaignFailures.ClassifyAsync(() => HandleCoreAsync(context, ct), ct);

    async ValueTask<Result<OperationAcceptedResponse>> HandleCoreAsync(
        IRequestContext<AcknowledgePolicy> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result<OperationAcceptedResponse>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Policy acknowledgement requires a personal HTTP invocation."));
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.PersonId == Uuid.Empty)
            return Result<OperationAcceptedResponse>.Failure(new RequestError(RequestErrorKind.Validation,
                "The command and person identities are required."));
        var source = await campaigns.ReadOwnerAsync(request.TenantId, request.ProgramId, request.CampaignId,
            request.CommandId, ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result<OperationAcceptedResponse>.Failure(source.Error);
        var actor = PolicyActor.From(context, request.TenantId);
        var person = await recorderGuard.CaptureAsync(request.TenantId, request.PersonId, ct)
            .ConfigureAwait(false);
        if (person.TenantId != request.TenantId || person.PersonId != request.PersonId)
            return NotInAudience();
        // Authorize before consulting the audience, so a member acting for someone else learns
        // nothing about who is in it or who holds a membership.
        var self = person.CorrelatedUserId == actor.UserId;
        if (!self && !await permissions.IsAllowedAsync(request.TenantId, actor.UserId, actor.MemberId,
                request.ProgramId, IProgramScopedRequest.ManagementPermission, ct).ConfigureAwait(false))
            return NotInAudience();
        if (!self && person.CorrelatedUserId is not null)
            return await RefuseActorAsync("A member must acknowledge a policy personally.").ConfigureAwait(false);
        var eligibility = await recorderGuard.EvaluateCapturedAsync(request.TenantId, actor.UserId, person, ct)
            .ConfigureAwait(false);
        if (!self && !eligibility.CanRecordProxy)
            return await RefuseActorAsync(
                "A person with actual Attest assignment history cannot record this client's management acknowledgements.")
                .ConfigureAwait(false);
        var onBehalf = !self;
        var plan = new ParticipantAdmissionPlan(request.CommandId, request.PersonId, request.ExpectedCampaignRevision,
            nameof(AcknowledgePolicy), AdmittedChildKind.PolicyAcknowledgement,
            PolicyCampaignContent.AcknowledgeCommandSchema, actor.Reference, null, onBehalf,
            clock.GetUtcNow());
        return await admission.AdmitAsync(context, request.TenantId, request.ProgramId, request.CampaignId, plan, request,
            current => ValueTask.FromResult(CampaignRules.CheckAcknowledgement(current, request)), ct)
            .ConfigureAwait(false);

        async ValueTask<Result<OperationAcceptedResponse>> RefuseActorAsync(string message)
        {
            // This request cannot execute or replay. Preserve the existing private 404/403 distinction for a new root.
            if (source.Value.Acceptance.SelectedFact is null)
            {
                var state = await campaigns.StateOfAsync(source.Value, ct).ConfigureAwait(false);
                if (!state.IsSuccess)
                    return Result<OperationAcceptedResponse>.Failure(state.Error!);
                if (state.Value.Roster.FindActive(request.PersonId) is null)
                    return NotInAudience();
            }
            return Result<OperationAcceptedResponse>.Failure(new RequestError(RequestErrorKind.Forbidden, message));
        }
    }

    static Result<OperationAcceptedResponse> NotInAudience() =>
        Result<OperationAcceptedResponse>.Failure(new RequestError(RequestErrorKind.NotFound,
            "The person is not in this campaign's current audience."));
}
