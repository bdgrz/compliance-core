using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     Assigns the administrators team for the restoration or first registration a guard fact admitted, at the
///     revision and generation it pinned. It waits behind an earlier unfinished removal of the same assignment, and
///     only a proven later episode of the member supersedes it.
/// </summary>
public sealed class ApplyAdministratorAssignmentRestorationHandler(AdministratorReceiverSupport support)
    : IRequestHandler<ApplyAdministratorAssignmentRestoration, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<ApplyAdministratorAssignmentRestoration> context, CancellationToken ct)
    {
        var request = context.Request;
        var verified = await support.VerifyAdmissionAsync(request.Source, request, request.MemberEpisodeId,
            static kind => kind is AdministratorTransitionKind.RestoreAdministratorsAssignment or
                AdministratorTransitionKind.RegisterAdministratorsAssignment, ct).ConfigureAwait(false);
        if (!verified.IsSuccess)
            return Result<ReceiverOutcome>.Failure(verified.Error!);
        var intent = verified.Value!.Intent;
        var tenantId = AdministratorReceiverSupport.TenantOf(request.Target);
        var teamId = BuiltInRbac.AdministratorsTeamId(tenantId);
        var stream = new TeamMember(tenantId, teamId, request.MemberId, request.CommandId).Stream;
        if (request.Target != stream || intent.AssignmentId != request.AssignmentId ||
            intent.AssignmentGeneration is not { } generation || generation == 0 ||
            intent.ExpectedAssignmentRevision != request.ExpectedAssignmentRevision)
            return AdministratorReceiverSupport.Invalid<ReceiverOutcome>("The command does not match the admitted assignment.");
        var guard = (await support.Guards.LoadAsync(tenantId, request.CommandId, request.MemberEpisodeId, request.OperationId, ct).ConfigureAwait(false)).Guard;
        var gate = AdministratorSupersession.Of(guard.GateRestoration(request.OperationId));
        var member = await support.Reader.HydrateAsync(Member.ForVerification(tenantId, request.MemberId), ct)
            .ConfigureAwait(false);
        var memberStop = AdministratorSupersession.Of(member, request.MemberEpisodeId);
        var provenance = AdministratorReceiverSupport.Provenance(request, nameof(ApplyAdministratorAssignmentRestoration),
            stream, intent, verified.Value.FactEventId);
        return await support.ApplyAsync(() => new TeamMember(tenantId, teamId, request.MemberId, request.CommandId),
            assignment => assignment.Acceptance, AcceptedOperationBinding.Of(provenance),
            _ => gate ?? memberStop,
            (TeamMember assignment) => assignment.ApplyAssignment(provenance, request.MemberEpisodeId,
                (long)request.ExpectedAssignmentRevision, (long)generation - 1), context, ct).ConfigureAwait(false);
    }
}
