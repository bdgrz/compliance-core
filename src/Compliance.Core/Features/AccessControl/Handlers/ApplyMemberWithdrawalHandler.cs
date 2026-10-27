using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>Suspends or deprovisions the member episode a withdrawal admitted, at the revision the admission pinned.</summary>
public sealed class ApplyMemberWithdrawalHandler(AdministratorReceiverSupport support)
    : IRequestHandler<ApplyMemberWithdrawal, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ApplyMemberWithdrawal> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var verified = await support.VerifyAdmissionAsync(request.Source, request, request.MemberEpisode,
            static kind => kind is AdministratorTransitionKind.SuspendMember or AdministratorTransitionKind.DeprovisionMember,
            ct).ConfigureAwait(false);
        if (!verified.IsSuccess)
            return Result<ReceiverOutcome>.Failure(verified.Error!);
        var intent = verified.Value!.Intent;
        var tenantId = AdministratorReceiverSupport.TenantOf(request.Target);
        var stream = Member.ForOperation(tenantId, request.MemberId, request.CommandId).Stream;
        if (request.Target != stream || request.WithdrawalKind != AdministratorTransitionEffects.WireName(intent.TransitionKind))
            return AdministratorReceiverSupport.Invalid<ReceiverOutcome>("The command does not match the admitted withdrawal.");
        var provenance = AdministratorReceiverSupport.Provenance(request, nameof(ApplyMemberWithdrawal), stream, intent,
            verified.Value.FactEventId);
        var actorMemberId = AdministratorReceiverSupport.ActorMemberId(intent);
        var actorDisplay = intent.Header.OriginalActor.Display;
        var expected = request.ExpectedRevision ?? -1;
        return await support.ApplyAsync(() => Member.ForOperation(tenantId, request.MemberId, request.CommandId),
            member => member.Acceptance, AcceptedOperationBinding.Of(provenance),
            member => AdministratorSupersession.Of(member, request.MemberEpisode),
            (Member member) => intent.TransitionKind == AdministratorTransitionKind.SuspendMember
                ? member.Suspend(provenance, request.Intent, null, actorMemberId, actorDisplay, intent.Header.AcceptedAt,
                    intent.Reason ?? string.Empty, request.MemberEpisode, expected)
                : member.Deprovision(provenance, request.Intent, null, actorMemberId, actorDisplay,
                    intent.Header.AcceptedAt, intent.Reason ?? string.Empty, request.MemberEpisode, expected),
            context, ct).ConfigureAwait(false);
    }
}
