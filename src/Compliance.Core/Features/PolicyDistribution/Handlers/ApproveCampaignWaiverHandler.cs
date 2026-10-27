using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
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
