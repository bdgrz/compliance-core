using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Reinstates the member episode a restoration admitted. It waits behind any earlier unfinished withdrawal of
///     the same episode and reason, and a later withdrawal supersedes it.
/// </summary>
public sealed class ApplyMemberRestorationHandler(AdministratorReceiverSupport support)
    : IRequestHandler<ApplyMemberRestoration, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ApplyMemberRestoration> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var verified = await support.VerifyAdmissionAsync(request.Source, request, request.MemberEpisode,
            static kind => kind == AdministratorTransitionKind.ReinstateMember, ct).ConfigureAwait(false);
        if (!verified.IsSuccess)
            return Result<ReceiverOutcome>.Failure(verified.Error!);
        var intent = verified.Value!.Intent;
        var tenantId = AdministratorReceiverSupport.TenantOf(request.Target);
        var stream = Member.ForOperation(tenantId, request.MemberId, request.CommandId).Stream;
        if (request.Target != stream)
            return AdministratorReceiverSupport.Invalid<ReceiverOutcome>("The command does not match the admitted restoration.");
        var guard = (await support.Guards.LoadAsync(tenantId, request.CommandId, request.MemberEpisode, request.OperationId, ct).ConfigureAwait(false)).Guard;
        var gate = guard.GateRestoration(request.OperationId);
        var provenance = AdministratorReceiverSupport.Provenance(request, nameof(ApplyMemberRestoration), stream, intent,
            verified.Value.FactEventId);
        var actorMemberId = AdministratorReceiverSupport.ActorMemberId(intent);
        var actorDisplay = intent.Header.OriginalActor.Display;
        return await support.ApplyAsync(() => Member.ForOperation(tenantId, request.MemberId, request.CommandId),
            member => member.Acceptance, AcceptedOperationBinding.Of(provenance),
            member => AdministratorSupersession.Of(gate) ?? AdministratorSupersession.Of(member, request.MemberEpisode),
            (Member member) => member.Reinstate(provenance, request.Intent, null, actorMemberId, actorDisplay,
                intent.Header.AcceptedAt, request.MemberEpisode, -1), context, ct).ConfigureAwait(false);
    }
}
