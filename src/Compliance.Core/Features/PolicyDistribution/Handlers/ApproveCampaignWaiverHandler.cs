using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Handlers;

/// <summary>
///     The Compliance Lead's exception approval. Until a dedicated role exists, the Compliance
///     Lead is an organization-wide program manager (as for M0-D05 scope approval). HTTP-only. The campaign admits the
///     complete exception at the expected campaign revision; the participant applies it afterwards.
/// </summary>
sealed class ApproveCampaignWaiverHandler(CampaignSource campaigns, IAggregateReader reader,
    IAccessGrantPermissionAuthorizer permissions, IImmutableContentStore content, TimeProvider clock,
    PolicyParticipantAdmission admission)
    : IRequestHandler<ApproveCampaignWaiver, OperationAcceptedResponse>
{
    public ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<ApproveCampaignWaiver> context, CancellationToken ct) =>
        PolicyCampaignFailures.ClassifyAsync(() => HandleCoreAsync(context, ct), ct);

    async ValueTask<Result<OperationAcceptedResponse>> HandleCoreAsync(
        IRequestContext<ApproveCampaignWaiver> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result<OperationAcceptedResponse>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Campaign exception approval requires personal HTTP submission."));
        var request = context.Request;
        var actor = PolicyActor.From(context, request.TenantId);
        var visibility = await permissions.GetProgramVisibilityAsync(request.TenantId,
            actor.UserId, actor.MemberId, IProgramScopedRequest.ManagementPermission, ct)
            .ConfigureAwait(false);
        if (!visibility.OrganizationWide)
            return Result<OperationAcceptedResponse>.Failure(new RequestError(
                RequestErrorKind.Forbidden,
                "Campaign exceptions require organization-wide program management."));
        var person = await reader.HydrateAsync(new Person(request.TenantId, request.PersonId), ct)
            .ConfigureAwait(false);
        if (person.CorrelatedUserId == actor.UserId)
            return Result<OperationAcceptedResponse>.Failure(new RequestError(
                RequestErrorKind.Forbidden, "A member cannot approve their own exception."));
        var source = await campaigns.ReadOwnerAsync(request.TenantId, request.ProgramId, request.CampaignId,
            request.CommandId, ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result<OperationAcceptedResponse>.Failure(source.Error);
        var now = clock.GetUtcNow();
        var plan = new ParticipantAdmissionPlan(request.CommandId, request.PersonId, request.ExpectedCampaignRevision,
            nameof(ApproveCampaignWaiver), AdmittedChildKind.CampaignWaiver,
            PolicyCampaignContent.WaiverCommandSchema, actor.Reference, null, false, now);
        return await admission.AdmitAsync(context, request.TenantId, request.ProgramId, request.CampaignId, plan, request,
            current => CampaignRules.CheckWaiverAsync(content, current, request, now, ct), ct).ConfigureAwait(false);
    }
}
