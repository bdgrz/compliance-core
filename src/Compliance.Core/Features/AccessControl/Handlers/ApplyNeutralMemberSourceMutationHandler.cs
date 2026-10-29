using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;


/// <summary>
///     Applies the original suspend, deprovision or reinstate of a member episode the guard admitted as neutral, with
///     the authored revision. It never registers or releases availability.
/// </summary>
public sealed class ApplyNeutralMemberSourceMutationHandler(AdministratorReceiverSupport support)
    : IRequestHandler<ApplyNeutralMemberSourceMutation, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<ApplyNeutralMemberSourceMutation> context, CancellationToken ct)
    {
        var request = context.Request;
        var verified = await support.VerifyAdmissionAsync(request.Source, request, request.MemberEpisode,
            static kind => kind == AdministratorTransitionKind.NeutralMemberMutation, ct).ConfigureAwait(false);
        if (!verified.IsSuccess)
            return Result<ReceiverOutcome>.Failure(verified.Error!);
        var intent = verified.Value!.Intent;
        var admitted = (TenantManagerSourceMutationAdmitted)verified.Value.Fact;
        var tenantId = AdministratorReceiverSupport.TenantOf(request.Target);
        var stream = Member.ForOperation(tenantId, request.MemberId, request.CommandId).Stream;
        var expectedKind = admitted.Operation.CommandKind switch
        {
            AdministratorCommandKinds.Suspend => "suspend",
            AdministratorCommandKinds.Deprovision => "deprovision",
            AdministratorCommandKinds.Reinstate => "reinstate",
            _ => string.Empty,
        };
        if (request.Target != stream || request.MutationKind != expectedKind)
            return AdministratorReceiverSupport.Invalid<ReceiverOutcome>("The command does not match the admitted mutation.");
        var provenance = AdministratorReceiverSupport.Provenance(request, nameof(ApplyNeutralMemberSourceMutation),
            stream, intent, verified.Value.FactEventId);
        var actorMemberId = AdministratorReceiverSupport.ActorMemberId(intent);
        var actorDisplay = intent.Header.OriginalActor.Display;
        var expected = request.ExpectedRevision ?? -1;
        var result = await AdministratorTransitionEffects.UnitResultAsync(support.Content, request.Target.Realm,
            request.OperationId, ct).ConfigureAwait(false);
        return await support.ApplyAsync(() => Member.ForOperation(tenantId, request.MemberId, request.CommandId),
            member => member.Acceptance, AcceptedOperationBinding.Of(provenance),
            member => AdministratorSupersession.Of(member, request.MemberEpisode),
            (Member member) => request.MutationKind switch
            {
                "suspend" => member.Suspend(provenance, request.Intent, result, actorMemberId, actorDisplay,
                    intent.Header.AcceptedAt, intent.Reason ?? string.Empty, request.MemberEpisode, expected),
                "deprovision" => member.Deprovision(provenance, request.Intent, result, actorMemberId, actorDisplay,
                    intent.Header.AcceptedAt, intent.Reason ?? string.Empty, request.MemberEpisode, expected),
                _ => member.Reinstate(provenance, request.Intent, result, actorMemberId, actorDisplay,
                    intent.Header.AcceptedAt, request.MemberEpisode, expected),
            }, context, ct).ConfigureAwait(false);
    }
}
