using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

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
